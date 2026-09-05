using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Dizzy.Fixes
{
    // BoatHorizon puts boats kinematic beyond ~1000 m. On return it wakes
    // them: PickupableBoatMooringRope rewrites the dock spring to the drifted
    // distance (so the line no longer holds), and Anchor.ExtraFixedUpdate
    // sees a joint impulse (or a short limit) and calls ReleaseAnchor. NPC
    // and player boats at the quay then float away.
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

    internal static class MooringLengthMemory
    {
        private static readonly Dictionary<int, float> MaxDistance = new Dictionary<int, float>();
        private static readonly Dictionary<int, float> LengthSquared = new Dictionary<int, float>();

        internal static void Remember(SpringJoint spring, float lengthSquared)
        {
            if (spring == null)
                return;
            int id = spring.GetInstanceID();
            MaxDistance[id] = spring.maxDistance;
            LengthSquared[id] = lengthSquared;
        }

        internal static bool TryRestore(SpringJoint spring, out float lengthSquared)
        {
            lengthSquared = 0f;
            if (spring == null)
                return false;

            int id = spring.GetInstanceID();
            if (!MaxDistance.TryGetValue(id, out float maxDistance))
                return false;

            spring.maxDistance = maxDistance;
            if (LengthSquared.TryGetValue(id, out lengthSquared))
                return true;

            lengthSquared = maxDistance * maxDistance;
            return true;
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
            if (!FixesConfig.KeepMooredBoats.Value)
                return;
            if (__state && ___rigidbody != null && !___rigidbody.isKinematic)
                BoatWakeGuard.MarkAwake(___rigidbody);
        }
    }

    [HarmonyPatch(typeof(PickupableBoatMooringRope), "Update")]
    internal static class MooringWakeLengthPatch
    {
        private static void Prefix(
            ref bool ___wasKinematic,
            SpringJoint ___mooredToSpring,
            Rigidbody ___boatRigidbody,
            Vector3 ___springAnchor,
            ref float ___currentRopeLengthSquared)
        {
            if (!FixesConfig.KeepMooredBoats.Value)
                return;
            if (___mooredToSpring == null || ___boatRigidbody == null)
                return;

            if (___boatRigidbody.isKinematic)
            {
                MooringLengthMemory.Remember(___mooredToSpring, ___currentRopeLengthSquared);
                return;
            }

            if (!___wasKinematic)
                return;

            if (___mooredToSpring.connectedBody == null)
            {
                ___mooredToSpring.connectedBody = ___boatRigidbody;
                ___mooredToSpring.connectedAnchor = ___springAnchor;
            }

            if (MooringLengthMemory.TryRestore(___mooredToSpring, out float lengthSquared))
                ___currentRopeLengthSquared = lengthSquared;

            // Keep the length from MoorTo; vanilla overwrites it with the
            // (possibly drifted) distance after kinematic sleep.
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
