using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Dizzy.Fixes
{
    // A two-handed item is held at pointer.TransformPoint(decolLocalPos). Each frame
    // GoPointer.LateUpdate adds PickupableItemCollisionChecker.GetDecollision() to the held
    // position and range-checks the result with < and > only. Every such comparison is false
    // for NaN, so one NaN push-out passes the checks, decolLocalPos becomes NaN and stays NaN
    // (a lerp from NaN is NaN), and the item sits at an invalid position until it is picked up
    // again, which a vanished item can't be.
    //
    // GetDecollision calls Physics.ComputePenetration for each collider the item touches and
    // ignores the return value. For a non-convex MeshCollider, such as the large dhow's hull
    // structure (Cube_023), it returns false and leaves direction undefined: zero, or garbage
    // such as 1.7e15 or NaN. Garbage times the distance of zero is NaN. This version skips a
    // collider the call fails for or that is gone, and drops anything not finite.
    [HarmonyPatch(typeof(PickupableItemCollisionChecker), nameof(PickupableItemCollisionChecker.GetDecollision))]
    internal static class HeldItemDecollisionPatch
    {
        private static readonly AccessTools.FieldRef<PickupableItemCollisionChecker, List<Collider>> TouchingColliders =
            GameMembers.Field<PickupableItemCollisionChecker, List<Collider>>("collidedCols");

        private static bool Prefix(PickupableItemCollisionChecker __instance, ref Vector3 __result)
        {
            if (!FixesConfig.PreventHeldItemVanishing.Value || TouchingColliders == null)
                return true;

            if (!Settings.enableDecol)
            {
                __result = Vector3.zero;
                return false;
            }

            Vector3 total = Vector3.zero;
            Collider self = __instance.GetComponent<Collider>();
            List<Collider> touching = TouchingColliders(__instance);
            for (int i = 0; touching != null && self != null && i < touching.Count; i++)
            {
                Collider other = touching[i];
                if (other == null)
                    continue;

                Vector3 direction;
                float distance;
                if (!Physics.ComputePenetration(
                        self,
                        __instance.transform.position,
                        __instance.transform.rotation,
                        other,
                        other.transform.position,
                        other.transform.rotation,
                        out direction,
                        out distance))
                    continue;

                Vector3 push = direction * distance * 1.8f;
                if (FiniteCheck.IsFinite(push))
                    total += push;
            }

            if (!FiniteCheck.IsFinite(total))
                total = Vector3.zero;

            __instance.decollisionVector = total;
            __result = __instance.transform.InverseTransformVector(total);
            if (!FiniteCheck.IsFinite(__result))
                __result = Vector3.zero;
            return false;
        }

    }

    internal static class FiniteCheck
    {
        internal static bool IsFinite(Vector3 v)
        {
            return IsFinite(v.x) && IsFinite(v.y) && IsFinite(v.z);
        }

        internal static bool IsFinite(float f)
        {
            return !float.IsNaN(f) && !float.IsInfinity(f);
        }
    }
}
