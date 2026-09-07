using HarmonyLib;
using UnityEngine;

namespace Dizzy.Fixes
{
    // Vanilla right-click on a held, untied mooring line does nothing.
    // Left-click attach needs a 1.8 m first-hit look on the tiny cleat,
    // which dock mesh and DockPushCol steal (Gold Rock especially).
    // Towable-boat bollards are the same GPButtonDockMooring type on the
    // hull; snapping to those springs the boat to itself.
    internal static class DockMooringSnap
    {
        internal static float Range
        {
            get
            {
                float feet = FixesConfig.RightClickNearestDockMooringFeet != null
                    ? FixesConfig.RightClickNearestDockMooringFeet.Value
                    : 15f;
                if (feet < 0.1f)
                    feet = 0.1f;
                return feet;
            }
        }

        internal static GPButtonDockMooring NearestFree(PickupableBoatMooringRope rope)
        {
            if (rope == null)
                return null;

            Vector3 from = rope.transform.position;
            Rigidbody boat = rope.GetBoatRigidbody();
            GPButtonDockMooring[] cleats = Object.FindObjectsOfType<GPButtonDockMooring>();
            if (cleats == null || cleats.Length == 0)
                return null;

            float range = Range;
            float maxSqr = range * range;
            GPButtonDockMooring best = null;
            float bestSqr = maxSqr;
            for (int i = 0; i < cleats.Length; i++)
            {
                GPButtonDockMooring cleat = cleats[i];
                if (!IsFree(cleat) || IsOnSameBoat(cleat, boat))
                    continue;

                float sqr = (cleat.transform.position - from).sqrMagnitude;
                if (sqr > bestSqr)
                    continue;

                bestSqr = sqr;
                best = cleat;
            }

            return best;
        }

        private static bool IsOnSameBoat(GPButtonDockMooring cleat, Rigidbody boat)
        {
            if (cleat == null || boat == null)
                return false;

            Transform root = boat.transform;
            Transform t = cleat.transform;
            return t == root || t.IsChildOf(root);
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

            GPButtonDockMooring cleat = DockMooringSnap.NearestFree(__instance);
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
