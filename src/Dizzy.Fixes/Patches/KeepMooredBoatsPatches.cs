using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Dizzy.Fixes
{
    // BoatHorizon puts boats kinematic beyond ~1000 m. On return it wakes
    // them: PickupableBoatMooringRope rewrites the dock spring to the drifted
    // distance (so the line no longer holds), Unity can drop connectedBody,
    // and Anchor.ExtraFixedUpdate pops the hook. NPC and player boats then
    // float away.
    // Pause, load, and sleep also set nearby boats kinematic — including the
    // one you are sailing. Only snap a dock pose after a distance sleep, and
    // only while the boat is still tied up.
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

    internal static class MooringJointFix
    {
        private const float SnapIfDriftedMeters = 2f;

        private class JointSave
        {
            internal float MaxDistance;
            internal float LengthSquared;
            internal Vector3 ConnectedAnchor;
            internal float Spring;
            internal float Damper;
        }

        private class PoseSave
        {
            internal Transform Dock;
            internal Vector3 LocalPos;
            internal Quaternion LocalRot;
        }

        private static readonly Dictionary<int, JointSave> Joints = new Dictionary<int, JointSave>();
        private static readonly Dictionary<int, PoseSave> Poses = new Dictionary<int, PoseSave>();

        internal static void RememberFromMoor(PickupableBoatMooringRope rope)
        {
            if (!TryReadRope(rope, out SpringJoint spring, out Rigidbody boat, out Vector3 anchor, out float lengthSquared))
                return;
            if (spring == null || boat == null)
                return;

            spring.autoConfigureConnectedAnchor = false;
            Joints[spring.GetInstanceID()] = new JointSave
            {
                MaxDistance = spring.maxDistance,
                LengthSquared = lengthSquared,
                ConnectedAnchor = anchor,
                Spring = spring.spring,
                Damper = spring.damper
            };
        }

        internal static void Forget(PickupableBoatMooringRope rope)
        {
            if (rope == null)
                return;

            Traverse t = Traverse.Create(rope);
            SpringJoint spring = t.Field("mooredToSpring").GetValue<SpringJoint>();
            if (spring != null)
                Joints.Remove(spring.GetInstanceID());

            Rigidbody boat = t.Field("boatRigidbody").GetValue<Rigidbody>();
            if (boat != null && !HasMooredRope(boat, rope))
                Poses.Remove(boat.GetInstanceID());
        }

        internal static void SnapshotBoatIfMoored(Rigidbody boat)
        {
            PickupableBoatMooringRope[] ropes = RopesOn(boat);
            if (ropes == null)
                return;

            Transform dock = null;
            for (int i = 0; i < ropes.Length; i++)
            {
                if (ropes[i] == null || !ropes[i].IsMoored())
                    continue;
                RememberFromMoor(ropes[i]);
                SpringJoint spring = Traverse.Create(ropes[i]).Field("mooredToSpring").GetValue<SpringJoint>();
                if (spring != null)
                    dock = spring.transform;
            }

            if (dock == null)
                return;

            Poses[boat.GetInstanceID()] = new PoseSave
            {
                Dock = dock,
                LocalPos = dock.InverseTransformPoint(boat.position),
                LocalRot = Quaternion.Inverse(dock.rotation) * boat.rotation
            };
        }

        internal static void RestoreBoat(Rigidbody boat)
        {
            if (boat == null)
                return;

            int boatId = boat.GetInstanceID();
            if (!HasMooredRope(boat))
            {
                Poses.Remove(boatId);
                return;
            }

            if (Poses.TryGetValue(boatId, out PoseSave pose) && pose.Dock != null)
            {
                Vector3 world = pose.Dock.TransformPoint(pose.LocalPos);
                if ((world - boat.position).sqrMagnitude > SnapIfDriftedMeters * SnapIfDriftedMeters)
                {
                    boat.position = world;
                    boat.rotation = pose.Dock.rotation * pose.LocalRot;
                    boat.velocity = Vector3.zero;
                    boat.angularVelocity = Vector3.zero;
                }
            }

            PickupableBoatMooringRope[] ropes = RopesOn(boat);
            if (ropes == null)
                return;
            for (int i = 0; i < ropes.Length; i++)
                Ensure(ropes[i]);
        }

        internal static void Ensure(PickupableBoatMooringRope rope)
        {
            if (rope == null || !FixesConfig.KeepMooredBoats.Value)
                return;
            if (!TryReadRope(rope, out SpringJoint spring, out Rigidbody boat, out Vector3 anchor, out float lengthSquared))
                return;
            if (spring == null || boat == null)
                return;

            if (!Joints.TryGetValue(spring.GetInstanceID(), out JointSave saved))
            {
                saved = new JointSave
                {
                    MaxDistance = spring.maxDistance > 0.01f ? spring.maxDistance : Mathf.Sqrt(Mathf.Max(lengthSquared, 0f)),
                    LengthSquared = lengthSquared,
                    ConnectedAnchor = anchor,
                    Spring = boat.mass * 6f,
                    Damper = boat.mass * 12f
                };
            }

            spring.autoConfigureConnectedAnchor = false;
            spring.connectedBody = boat;
            spring.connectedAnchor = saved.ConnectedAnchor;
            spring.spring = saved.Spring > 0f ? saved.Spring : boat.mass * 6f;
            spring.damper = saved.Damper > 0f ? saved.Damper : spring.spring * 2f;
            spring.minDistance = 0f;
            spring.maxDistance = saved.MaxDistance;
            rope.currentRopeLengthSquared = saved.LengthSquared;

            Collider col = spring.GetComponent<Collider>();
            if (col != null)
                col.enabled = false;
        }

        private static bool TryReadRope(
            PickupableBoatMooringRope rope,
            out SpringJoint spring,
            out Rigidbody boat,
            out Vector3 anchor,
            out float lengthSquared)
        {
            Traverse t = Traverse.Create(rope);
            spring = t.Field("mooredToSpring").GetValue<SpringJoint>();
            boat = t.Field("boatRigidbody").GetValue<Rigidbody>();
            anchor = t.Field("springAnchor").GetValue<Vector3>();
            lengthSquared = rope.currentRopeLengthSquared;
            return true;
        }

        private static bool HasMooredRope(Rigidbody boat, PickupableBoatMooringRope except = null)
        {
            PickupableBoatMooringRope[] ropes = RopesOn(boat);
            if (ropes == null)
                return false;

            for (int i = 0; i < ropes.Length; i++)
            {
                if (ropes[i] == null || ropes[i] == except)
                    continue;
                if (ropes[i].IsMoored())
                    return true;
            }

            return false;
        }

        private static PickupableBoatMooringRope[] RopesOn(Rigidbody boat)
        {
            if (boat == null)
                return null;
            BoatMooringRopes mooring = boat.GetComponent<BoatMooringRopes>();
            if (mooring == null)
                mooring = boat.GetComponentInChildren<BoatMooringRopes>();
            if (mooring == null)
                return null;
            return mooring.ropes;
        }
    }

    [HarmonyPatch(typeof(BoatHorizon), "UpdateKinematic")]
    internal static class BoatHorizonWakePatch
    {
        private static readonly HashSet<int> NearbySleep = new HashSet<int>();

        private static void Prefix(Rigidbody ___rigidbody, out bool __state)
        {
            __state = ___rigidbody != null && ___rigidbody.isKinematic;
        }

        private static void Postfix(Rigidbody ___rigidbody, bool ___closeToPlayer, bool __state)
        {
            if (!FixesConfig.KeepMooredBoats.Value || ___rigidbody == null)
                return;

            int id = ___rigidbody.GetInstanceID();
            bool kinematic = ___rigidbody.isKinematic;
            if (!__state && kinematic)
            {
                if (___closeToPlayer)
                {
                    NearbySleep.Add(id);
                    return;
                }

                NearbySleep.Remove(id);
                MooringJointFix.SnapshotBoatIfMoored(___rigidbody);
                return;
            }

            if (__state && !kinematic)
            {
                BoatWakeGuard.MarkAwake(___rigidbody);
                if (NearbySleep.Remove(id))
                    return;
                MooringJointFix.RestoreBoat(___rigidbody);
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
            MooringJointFix.RememberFromMoor(__instance);
        }
    }

    [HarmonyPatch(typeof(PickupableBoatMooringRope), nameof(PickupableBoatMooringRope.ChangeRopeLength))]
    internal static class MooringLengthAdjustPatch
    {
        private static void Postfix(PickupableBoatMooringRope __instance, bool __result)
        {
            if (!FixesConfig.KeepMooredBoats.Value || !__result)
                return;
            MooringJointFix.RememberFromMoor(__instance);
        }
    }

    [HarmonyPatch(typeof(PickupableBoatMooringRope), nameof(PickupableBoatMooringRope.Unmoor))]
    internal static class MooringUnmoorPatch
    {
        private static void Prefix(PickupableBoatMooringRope __instance)
        {
            if (!FixesConfig.KeepMooredBoats.Value)
                return;
            MooringJointFix.Forget(__instance);
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

            MooringJointFix.Ensure(__instance);
            ___wasKinematic = false;
        }
    }

    [HarmonyPatch(typeof(Anchor), "ReleaseAnchor")]
    internal static class AnchorWakeReleasePatch
    {
        private static bool Prefix(ConfigurableJoint ___joint)
        {
            if (!FixesConfig.KeepMooredBoats.Value)
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
