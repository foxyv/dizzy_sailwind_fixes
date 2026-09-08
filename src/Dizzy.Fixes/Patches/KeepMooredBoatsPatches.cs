using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace Dizzy.Fixes
{
    // Vanilla sleeps boats past ~1 km, origin-shifts them off the quay, then
    // on wake rewrites spring maxDistance to the drifted gap. Load also calls
    // UnmoorAllRopes. Persist the tie in GameState.modData, keep kinematic
    // hulls stuck to the bollard, and remoor after load.
    internal static class BoatWakeGuard
    {
        private const float GraceSeconds = 3f;
        private static readonly Dictionary<int, float> Until = new Dictionary<int, float>();

        internal static void MarkAwake(Rigidbody body)
        {
            if (body == null)
                return;
            Prune();
            Until[body.GetInstanceID()] = Time.realtimeSinceStartup + GraceSeconds;
        }

        internal static bool RecentlyAwoke(Rigidbody body)
        {
            if (body == null)
                return false;

            int id = body.GetInstanceID();
            if (!Until.TryGetValue(id, out float until))
                return false;
            if (Time.realtimeSinceStartup >= until)
            {
                Until.Remove(id);
                return false;
            }

            return true;
        }

        private static void Prune()
        {
            if (Until.Count == 0)
                return;

            float now = Time.realtimeSinceStartup;
            List<int> expired = null;
            foreach (KeyValuePair<int, float> pair in Until)
            {
                if (now < pair.Value)
                    continue;
                if (expired == null)
                    expired = new List<int>();
                expired.Add(pair.Key);
            }

            if (expired == null)
                return;
            for (int i = 0; i < expired.Count; i++)
                Until.Remove(expired[i]);
        }
    }

    internal static class MooringStore
    {
        internal const string Key = "Dizzy.Fixes.Mooring.v1";
        internal const float DockMatchMeters = 12f;

        internal static bool IgnoreUnmoorForget;

        internal class RopeRec
        {
            internal int RopeIndex;
            internal float MaxDistance;
            internal float LengthSquared;
            internal Vector3 ConnectedAnchor;
            internal float Spring;
            internal float Damper;
            internal string DockPath;
            internal Vector3 DockRealPos;
        }

        internal class BoatRec
        {
            internal int SceneIndex;
            internal Vector3 LocalPos;
            internal Quaternion LocalRot;
            internal List<RopeRec> Ropes = new List<RopeRec>();
        }

        internal static readonly Dictionary<int, BoatRec> ByScene = new Dictionary<int, BoatRec>();

        internal static void Remember(PickupableBoatMooringRope rope)
        {
            if (rope == null || !rope.IsMoored())
                return;

            Rigidbody boat = rope.GetBoatRigidbody();
            SpringJoint spring = Traverse.Create(rope).Field("mooredToSpring").GetValue<SpringJoint>();
            Vector3 anchor = Traverse.Create(rope).Field("springAnchor").GetValue<Vector3>();
            if (boat == null || spring == null)
                return;

            int scene = SceneIndex(boat);
            if (scene < 0)
                return;

            int ropeIndex = RopeIndex(boat, rope);
            if (ropeIndex < 0)
                return;

            if (!ByScene.TryGetValue(scene, out BoatRec rec))
            {
                rec = new BoatRec { SceneIndex = scene };
                ByScene[scene] = rec;
            }

            Transform dock = spring.transform;
            rec.LocalPos = dock.InverseTransformPoint(boat.position);
            rec.LocalRot = Quaternion.Inverse(dock.rotation) * boat.rotation;

            RopeRec line = FindRope(rec, ropeIndex);
            if (line == null)
            {
                line = new RopeRec { RopeIndex = ropeIndex };
                rec.Ropes.Add(line);
            }

            line.MaxDistance = spring.maxDistance;
            line.LengthSquared = rope.currentRopeLengthSquared;
            line.ConnectedAnchor = anchor;
            line.Spring = spring.spring > 0f ? spring.spring : boat.mass * 6f;
            line.Damper = spring.damper > 0f ? spring.damper : line.Spring * 2f;
            line.DockPath = PathOf(dock);
            line.DockRealPos = ToReal(dock.position);

            spring.autoConfigureConnectedAnchor = false;
            WriteModData();
        }

        internal static void Forget(PickupableBoatMooringRope rope)
        {
            if (IgnoreUnmoorForget || rope == null)
                return;

            Rigidbody boat = rope.GetBoatRigidbody();
            if (boat == null)
                return;

            int scene = SceneIndex(boat);
            if (scene < 0 || !ByScene.TryGetValue(scene, out BoatRec rec))
                return;

            int ropeIndex = RopeIndex(boat, rope);
            rec.Ropes.RemoveAll(r => r.RopeIndex == ropeIndex);
            if (rec.Ropes.Count == 0)
                ByScene.Remove(scene);

            WriteModData();
        }

        internal static void StickIfSleeping(Rigidbody boat)
        {
            if (boat == null || !boat.isKinematic)
                return;
            if (IsCurrentBoat(boat))
                return;

            BoatRec rec = RecFor(boat);
            if (rec == null || rec.Ropes.Count == 0)
                return;

            Transform dock = LiveDock(rec, boat);
            if (dock == null)
                return;

            Vector3 world = dock.TransformPoint(rec.LocalPos);
            if ((world - boat.position).sqrMagnitude < 0.0001f)
                return;

            boat.position = world;
            boat.rotation = dock.rotation * rec.LocalRot;
            boat.velocity = Vector3.zero;
            boat.angularVelocity = Vector3.zero;
        }

        internal static void Restore(Rigidbody boat)
        {
            if (boat == null)
                return;

            BoatRec rec = RecFor(boat);
            if (rec == null || rec.Ropes.Count == 0)
                return;

            Transform dock = LiveDock(rec, boat);
            if (dock != null)
            {
                boat.position = dock.TransformPoint(rec.LocalPos);
                boat.rotation = dock.rotation * rec.LocalRot;
                boat.velocity = Vector3.zero;
                boat.angularVelocity = Vector3.zero;
            }

            PickupableBoatMooringRope[] ropes = RopesOn(boat);
            if (ropes == null)
                return;

            for (int i = 0; i < rec.Ropes.Count; i++)
            {
                RopeRec line = rec.Ropes[i];
                if (line.RopeIndex < 0 || line.RopeIndex >= ropes.Length)
                    continue;
                PickupableBoatMooringRope rope = ropes[line.RopeIndex];
                GPButtonDockMooring cleat = FindCleat(line, boat);
                if (rope == null || cleat == null)
                    continue;

                if (!rope.IsMoored())
                    rope.MoorTo(cleat);

                ApplyLine(rope, line, boat);
            }
        }

        internal static void RemoorAll()
        {
            ReadModData();
            List<int> scenes = new List<int>(ByScene.Keys);
            for (int i = 0; i < scenes.Count; i++)
            {
                Rigidbody boat = BoatByScene(scenes[i]);
                if (boat != null)
                    Restore(boat);
            }
        }

        internal static void Ensure(PickupableBoatMooringRope rope)
        {
            if (rope == null || !rope.IsMoored())
                return;

            Rigidbody boat = rope.GetBoatRigidbody();
            SpringJoint spring = Traverse.Create(rope).Field("mooredToSpring").GetValue<SpringJoint>();
            if (boat == null || spring == null)
                return;

            BoatRec rec = RecFor(boat);
            RopeRec line = rec != null ? FindRope(rec, RopeIndex(boat, rope)) : null;
            if (line != null)
            {
                ApplyLine(rope, line, boat);
                return;
            }

            spring.autoConfigureConnectedAnchor = false;
            spring.connectedBody = boat;
            Collider col = spring.GetComponent<Collider>();
            if (col != null)
                col.enabled = false;
        }

        internal static void ReadModData()
        {
            ByScene.Clear();
            if (GameState.modData == null)
                return;
            if (!GameState.modData.TryGetValue(Key, out string raw) || string.IsNullOrEmpty(raw))
                return;

            string[] boats = raw.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < boats.Length; i++)
            {
                BoatRec rec = ParseBoat(boats[i]);
                if (rec != null)
                    ByScene[rec.SceneIndex] = rec;
            }
        }

        internal static void WriteModData()
        {
            if (GameState.modData == null)
                GameState.modData = new Dictionary<string, string>();

            if (ByScene.Count == 0)
            {
                GameState.modData.Remove(Key);
                return;
            }

            StringBuilder sb = new StringBuilder();
            foreach (BoatRec rec in ByScene.Values)
            {
                if (sb.Length > 0)
                    sb.Append('\n');
                sb.Append(FormatBoat(rec));
            }

            GameState.modData[Key] = sb.ToString();
        }

        private static void ApplyLine(PickupableBoatMooringRope rope, RopeRec line, Rigidbody boat)
        {
            SpringJoint spring = Traverse.Create(rope).Field("mooredToSpring").GetValue<SpringJoint>();
            if (spring == null)
                return;

            spring.autoConfigureConnectedAnchor = false;
            spring.connectedBody = boat;
            spring.connectedAnchor = line.ConnectedAnchor;
            spring.spring = line.Spring > 0f ? line.Spring : boat.mass * 6f;
            spring.damper = line.Damper > 0f ? line.Damper : spring.spring * 2f;
            spring.minDistance = 0f;
            spring.maxDistance = line.MaxDistance;
            rope.currentRopeLengthSquared = line.LengthSquared;

            Collider col = spring.GetComponent<Collider>();
            if (col != null)
                col.enabled = false;
        }

        private static BoatRec RecFor(Rigidbody boat)
        {
            int scene = SceneIndex(boat);
            if (scene < 0)
                return null;
            BoatRec rec;
            return ByScene.TryGetValue(scene, out rec) ? rec : null;
        }

        private static RopeRec FindRope(BoatRec rec, int ropeIndex)
        {
            for (int i = 0; i < rec.Ropes.Count; i++)
            {
                if (rec.Ropes[i].RopeIndex == ropeIndex)
                    return rec.Ropes[i];
            }

            return null;
        }

        private static Transform LiveDock(BoatRec rec, Rigidbody boat)
        {
            for (int i = 0; i < rec.Ropes.Count; i++)
            {
                GPButtonDockMooring cleat = FindCleat(rec.Ropes[i], boat);
                if (cleat != null)
                    return cleat.transform;
            }

            return null;
        }

        private static GPButtonDockMooring FindCleat(RopeRec line, Rigidbody boat)
        {
            if (!string.IsNullOrEmpty(line.DockPath))
            {
                GameObject found = GameObject.Find(line.DockPath);
                if (found != null)
                {
                    GPButtonDockMooring byPath = found.GetComponent<GPButtonDockMooring>();
                    if (byPath != null && !IsOnBoat(byPath, boat))
                        return byPath;
                }
            }

            Vector3 want = ToShifting(line.DockRealPos);
            GPButtonDockMooring[] all = UnityEngine.Object.FindObjectsOfType<GPButtonDockMooring>();
            GPButtonDockMooring best = null;
            float bestSqr = DockMatchMeters * DockMatchMeters;
            for (int i = 0; i < all.Length; i++)
            {
                GPButtonDockMooring cleat = all[i];
                if (cleat == null || IsOnBoat(cleat, boat))
                    continue;
                float sqr = (cleat.transform.position - want).sqrMagnitude;
                if (sqr >= bestSqr)
                    continue;
                bestSqr = sqr;
                best = cleat;
            }

            return best;
        }

        private static bool IsOnBoat(GPButtonDockMooring cleat, Rigidbody boat)
        {
            if (cleat == null || boat == null)
                return false;
            Transform root = boat.transform;
            Transform t = cleat.transform;
            return t == root || t.IsChildOf(root);
        }

        private static bool IsCurrentBoat(Rigidbody boat)
        {
            if (boat == null || GameState.currentBoat == null)
                return false;
            Transform cur = GameState.currentBoat;
            return cur == boat.transform || cur.parent == boat.transform || cur.IsChildOf(boat.transform);
        }

        internal static int SceneIndex(Rigidbody boat)
        {
            if (boat == null)
                return -1;
            SaveableObject save = boat.GetComponent<SaveableObject>();
            if (save == null)
                save = boat.GetComponentInParent<SaveableObject>();
            return save != null ? save.sceneIndex : -1;
        }

        private static Rigidbody BoatByScene(int scene)
        {
            if (SaveLoadManager.instance == null)
                return null;
            SaveableObject save = SaveLoadManager.instance.GetObject(scene);
            if (save == null)
                return null;
            Rigidbody body = save.GetComponent<Rigidbody>();
            if (body == null)
                body = save.GetComponentInChildren<Rigidbody>();
            return body;
        }

        private static PickupableBoatMooringRope[] RopesOn(Rigidbody boat)
        {
            if (boat == null)
                return null;
            BoatMooringRopes mooring = boat.GetComponent<BoatMooringRopes>();
            if (mooring == null)
                mooring = boat.GetComponentInChildren<BoatMooringRopes>();
            return mooring != null ? mooring.ropes : null;
        }

        private static int RopeIndex(Rigidbody boat, PickupableBoatMooringRope rope)
        {
            PickupableBoatMooringRope[] ropes = RopesOn(boat);
            if (ropes == null)
                return -1;
            for (int i = 0; i < ropes.Length; i++)
            {
                if (ropes[i] == rope)
                    return i;
            }

            return -1;
        }

        private static Vector3 ToReal(Vector3 shifting)
        {
            if (FloatingOriginManager.instance == null)
                return shifting;
            return FloatingOriginManager.instance.ShiftingPosToRealPos(shifting);
        }

        private static Vector3 ToShifting(Vector3 real)
        {
            if (FloatingOriginManager.instance == null)
                return real;
            return FloatingOriginManager.instance.RealPosToShiftingPos(real);
        }

        private static string PathOf(Transform t)
        {
            if (t == null)
                return "";
            List<string> parts = new List<string>();
            while (t != null)
            {
                parts.Add(t.name);
                t = t.parent;
            }

            parts.Reverse();
            return string.Join("/", parts.ToArray());
        }

        private static string FormatBoat(BoatRec rec)
        {
            CultureInfo c = CultureInfo.InvariantCulture;
            StringBuilder sb = new StringBuilder();
            sb.Append(rec.SceneIndex.ToString(c));
            sb.Append(';');
            sb.Append(F(rec.LocalPos.x, c)).Append(',').Append(F(rec.LocalPos.y, c)).Append(',').Append(F(rec.LocalPos.z, c));
            sb.Append(';');
            sb.Append(F(rec.LocalRot.x, c)).Append(',').Append(F(rec.LocalRot.y, c)).Append(',').Append(F(rec.LocalRot.z, c)).Append(',').Append(F(rec.LocalRot.w, c));
            for (int i = 0; i < rec.Ropes.Count; i++)
            {
                RopeRec r = rec.Ropes[i];
                sb.Append(';');
                sb.Append(r.RopeIndex.ToString(c)).Append(',');
                sb.Append(F(r.MaxDistance, c)).Append(',');
                sb.Append(F(r.LengthSquared, c)).Append(',');
                sb.Append(F(r.ConnectedAnchor.x, c)).Append(',').Append(F(r.ConnectedAnchor.y, c)).Append(',').Append(F(r.ConnectedAnchor.z, c)).Append(',');
                sb.Append(F(r.Spring, c)).Append(',');
                sb.Append(F(r.Damper, c)).Append(',');
                sb.Append(EscapePath(r.DockPath)).Append(',');
                sb.Append(F(r.DockRealPos.x, c)).Append(',').Append(F(r.DockRealPos.y, c)).Append(',').Append(F(r.DockRealPos.z, c));
            }

            return sb.ToString();
        }

        private static BoatRec ParseBoat(string raw)
        {
            try
            {
                string[] parts = raw.Split(';');
                if (parts.Length < 4)
                    return null;

                CultureInfo c = CultureInfo.InvariantCulture;
                BoatRec rec = new BoatRec
                {
                    SceneIndex = int.Parse(parts[0], c),
                    LocalPos = ParseVec3(parts[1], c),
                    LocalRot = ParseQuat(parts[2], c)
                };

                for (int i = 3; i < parts.Length; i++)
                {
                    string[] f = SplitRope(parts[i]);
                    if (f == null || f.Length < 12)
                        continue;
                    rec.Ropes.Add(new RopeRec
                    {
                        RopeIndex = int.Parse(f[0], c),
                        MaxDistance = float.Parse(f[1], c),
                        LengthSquared = float.Parse(f[2], c),
                        ConnectedAnchor = new Vector3(float.Parse(f[3], c), float.Parse(f[4], c), float.Parse(f[5], c)),
                        Spring = float.Parse(f[6], c),
                        Damper = float.Parse(f[7], c),
                        DockPath = UnescapePath(f[8]),
                        DockRealPos = new Vector3(float.Parse(f[9], c), float.Parse(f[10], c), float.Parse(f[11], c))
                    });
                }

                return rec.Ropes.Count > 0 ? rec : null;
            }
            catch
            {
                return null;
            }
        }

        private static string[] SplitRope(string raw)
        {
            List<string> fields = new List<string>();
            StringBuilder cur = new StringBuilder();
            bool esc = false;
            for (int i = 0; i < raw.Length; i++)
            {
                char ch = raw[i];
                if (esc)
                {
                    cur.Append(ch);
                    esc = false;
                    continue;
                }

                if (ch == '\\')
                {
                    esc = true;
                    continue;
                }

                if (ch == ',')
                {
                    fields.Add(cur.ToString());
                    cur.Length = 0;
                    continue;
                }

                cur.Append(ch);
            }

            fields.Add(cur.ToString());
            return fields.ToArray();
        }

        private static string EscapePath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return "";
            return path.Replace("\\", "\\\\").Replace(",", "\\,").Replace(";", "\\;");
        }

        private static string UnescapePath(string path)
        {
            return path ?? "";
        }

        private static string F(float v, CultureInfo c)
        {
            return v.ToString("G9", c);
        }

        private static Vector3 ParseVec3(string raw, CultureInfo c)
        {
            string[] p = raw.Split(',');
            return new Vector3(float.Parse(p[0], c), float.Parse(p[1], c), float.Parse(p[2], c));
        }

        private static Quaternion ParseQuat(string raw, CultureInfo c)
        {
            string[] p = raw.Split(',');
            return new Quaternion(float.Parse(p[0], c), float.Parse(p[1], c), float.Parse(p[2], c), float.Parse(p[3], c));
        }
    }

    [HarmonyPatch(typeof(BoatHorizon), "Update")]
    internal static class BoatHorizonStickPatch
    {
        private static void Postfix(Rigidbody ___rigidbody)
        {
            if (!FixesConfig.KeepMooredBoats.Value || ___rigidbody == null)
                return;
            MooringStore.StickIfSleeping(___rigidbody);
        }
    }

    [HarmonyPatch(typeof(BoatHorizon), "UpdateKinematic")]
    internal static class BoatHorizonWakePatch
    {
        private static void Prefix(Rigidbody ___rigidbody, out bool __state)
        {
            __state = ___rigidbody != null && ___rigidbody.isKinematic;
        }

        private static void Postfix(Rigidbody ___rigidbody, bool __state)
        {
            if (!FixesConfig.KeepMooredBoats.Value || ___rigidbody == null)
                return;

            bool kinematic = ___rigidbody.isKinematic;
            if (!__state && kinematic)
            {
                PickupableBoatMooringRope[] ropes = null;
                BoatMooringRopes mooring = ___rigidbody.GetComponent<BoatMooringRopes>();
                if (mooring == null)
                    mooring = ___rigidbody.GetComponentInChildren<BoatMooringRopes>();
                if (mooring != null)
                    ropes = mooring.ropes;
                if (ropes != null)
                {
                    for (int i = 0; i < ropes.Length; i++)
                    {
                        if (ropes[i] != null && ropes[i].IsMoored())
                            MooringStore.Remember(ropes[i]);
                    }
                }

                return;
            }

            if (__state && !kinematic)
            {
                BoatWakeGuard.MarkAwake(___rigidbody);
                MooringStore.Restore(___rigidbody);
            }
        }
    }

    [HarmonyPatch(typeof(PickupableBoatMooringRope), nameof(PickupableBoatMooringRope.MoorTo))]
    internal static class MooringMoorToPatch
    {
        private static void Postfix(PickupableBoatMooringRope __instance)
        {
            if (!FixesConfig.KeepMooredBoats.Value)
                return;
            MooringStore.Remember(__instance);
        }
    }

    [HarmonyPatch(typeof(PickupableBoatMooringRope), nameof(PickupableBoatMooringRope.ChangeRopeLength))]
    internal static class MooringLengthAdjustPatch
    {
        private static void Postfix(PickupableBoatMooringRope __instance, bool __result)
        {
            if (!FixesConfig.KeepMooredBoats.Value || !__result)
                return;
            MooringStore.Remember(__instance);
        }
    }

    [HarmonyPatch(typeof(PickupableBoatMooringRope), nameof(PickupableBoatMooringRope.Unmoor))]
    internal static class MooringUnmoorPatch
    {
        private static void Prefix(PickupableBoatMooringRope __instance)
        {
            if (!FixesConfig.KeepMooredBoats.Value)
                return;
            MooringStore.Forget(__instance);
        }
    }

    [HarmonyPatch(typeof(BoatMooringRopes), nameof(BoatMooringRopes.UnmoorAllRopes))]
    internal static class MooringUnmoorAllPatch
    {
        private static void Prefix()
        {
            MooringStore.IgnoreUnmoorForget = true;
        }

        private static void Postfix()
        {
            MooringStore.IgnoreUnmoorForget = false;
        }
    }

    [HarmonyPatch(typeof(SaveLoadManager), nameof(SaveLoadManager.LoadGame))]
    internal static class MooringLoadPatch
    {
        private static void Postfix()
        {
            if (!FixesConfig.KeepMooredBoats.Value)
                return;
            try
            {
                MooringStore.RemoorAll();
            }
            catch (Exception e)
            {
                if (Plugin.Log != null)
                    Plugin.Log.LogWarning("KeepMooredBoats remoor after load failed: " + e.Message);
            }
        }
    }

    [HarmonyPatch(typeof(PickupableBoatMooringRope), "Update")]
    internal static class MooringWakeLengthPatch
    {
        private static void Prefix(
            PickupableBoatMooringRope __instance,
            ref bool ___wasKinematic,
            SpringJoint ___mooredToSpring,
            Rigidbody ___boatRigidbody)
        {
            if (!FixesConfig.KeepMooredBoats.Value)
                return;
            if (___mooredToSpring == null || ___boatRigidbody == null)
                return;

            if (___boatRigidbody.isKinematic)
            {
                ___wasKinematic = true;
                return;
            }

            MooringStore.Ensure(__instance);
            ___wasKinematic = false;
        }
    }

    [HarmonyPatch(typeof(Anchor), "ReleaseAnchor")]
    internal static class AnchorWakeReleasePatch
    {
        private static bool Prefix(Anchor __instance, ConfigurableJoint ___joint)
        {
            if (!FixesConfig.KeepMooredBoats.Value)
                return true;
            if (__instance.held != null)
                return true;
            if (___joint == null || ___joint.connectedBody == null)
                return true;

            Rigidbody boat = ___joint.connectedBody;
            if (boat.isKinematic)
                return false;
            return !BoatWakeGuard.RecentlyAwoke(boat);
        }
    }

    [HarmonyPatch(typeof(Anchor), "Awake")]
    internal static class AnchorShiftingWorldPatch
    {
        private static Transform _shiftingWorld;

        private static void Postfix(Anchor __instance)
        {
            if (!FixesConfig.KeepMooredBoats.Value)
                return;

            Transform world = ResolveShiftingWorld();
            if (world == null)
                return;

            if (__instance.transform.parent != world)
                __instance.transform.parent = world;
        }

        private static Transform ResolveShiftingWorld()
        {
            if (_shiftingWorld != null)
                return _shiftingWorld;
            if (Refs.shiftingWorld != null)
                return _shiftingWorld = Refs.shiftingWorld;

            GameObject found = GameObject.Find("_shifting world");
            if (found == null)
                return null;
            return _shiftingWorld = found.transform;
        }
    }
}
