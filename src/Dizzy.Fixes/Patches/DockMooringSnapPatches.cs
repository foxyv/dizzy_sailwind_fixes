using HarmonyLib;
using UnityEngine;

namespace Dizzy.Fixes
{
    // Vanilla right-click on a held, untied mooring line does nothing.
    // Left-click attach needs a 1.8 m first-hit look on the tiny cleat,
    // which dock mesh and DockPushCol steal (Gold Rock especially).
    internal static class DockMooringSnap
    {
        private const float FeetToMeters = 0.3048f;

        internal static float RangeMeters
        {
            get
            {
                float feet = FixesConfig.RightClickNearestDockMooringFeet != null
                    ? FixesConfig.RightClickNearestDockMooringFeet.Value
                    : 15f;
                if (feet < 0.1f)
                    feet = 0.1f;
                return feet * FeetToMeters;
            }
        }

        internal static GPButtonDockMooring NearestFree(Vector3 from)
        {
            GPButtonDockMooring[] cleats = Object.FindObjectsOfType<GPButtonDockMooring>();
            if (cleats == null || cleats.Length == 0)
                return null;

            float range = RangeMeters;
            float maxSqr = range * range;
            GPButtonDockMooring best = null;
            float bestSqr = maxSqr;
            for (int i = 0; i < cleats.Length; i++)
            {
                GPButtonDockMooring cleat = cleats[i];
                if (!IsFree(cleat))
                    continue;

                float sqr = (cleat.transform.position - from).sqrMagnitude;
                if (sqr > bestSqr)
                    continue;

                bestSqr = sqr;
                best = cleat;
            }

            return best;
        }

        private static bool IsFree(GPButtonDockMooring cleat)
        {
            if (cleat == null || cleat.spring == null)
                return false;
            if (cleat.spring.connectedBody != null)
                return false;

            Collider col = cleat.GetComponent<Collider>();
            if (col != null && !col.enabled)
                return false;

            return true;
        }
    }

    [HarmonyPatch(typeof(PickupableBoatMooringRope), nameof(PickupableBoatMooringRope.OnAltActivate), typeof(GoPointer))]
    internal static class DockMooringSnapPatch
    {
        private static void Prefix(PickupableBoatMooringRope __instance, GoPointer activatingPointer)
        {
            if (!FixesConfig.RightClickNearestDockMooring.Value)
                return;
            if (__instance == null || __instance.held == null)
                return;
            if (__instance.IsMoored())
                return;

            GPButtonDockMooring cleat = DockMooringSnap.NearestFree(__instance.transform.position);
            if (cleat == null)
                return;

            GoPointer pointer = activatingPointer != null ? activatingPointer : __instance.held;
            cleat.ThrowRope(__instance);
            __instance.OnDrop();
            if (pointer != null)
                pointer.DropItem();
        }
    }
}
