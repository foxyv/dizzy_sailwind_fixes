using HarmonyLib;
using UnityEngine;

namespace Dizzy.Fixes
{
    // Vanilla right-click on a held, untied mooring line does nothing.
    // Left-click attach needs a 1.8 m first-hit look on the tiny cleat,
    // which dock mesh and DockPushCol steal (Gold Rock especially).
    // Towable-boat bollards are the same GPButtonDockMooring type on the
    // hull; snapping to those springs the boat to itself.
    // The throw only considers free cleats inside a forward arc, and
    // picks the one closest to where the player is facing.
    internal static class DockMooringSnap
    {
        // The highlight looks for the target every frame while a line is
        // held, so the scene search is cached. The throw uses the same list,
        // so it always lands on the cleat that was highlighted.
        private const float CleatRefreshSeconds = 1f;

        private static GPButtonDockMooring[] _cleats;
        private static float _cleatsFoundAt;

        internal static bool HighlightEnabled()
        {
            return FixesConfig.HighlightNearestDockMooring != null && FixesConfig.HighlightNearestDockMooring.Value
                && FixesConfig.RightClickNearestDockMooring != null && FixesConfig.RightClickNearestDockMooring.Value;
        }

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

        internal static float ArcDegrees
        {
            get
            {
                float degrees = FixesConfig.RightClickNearestDockMooringArcDegrees != null
                    ? FixesConfig.RightClickNearestDockMooringArcDegrees.Value
                    : 10f;
                if (degrees < 0.1f)
                    degrees = 0.1f;
                return degrees;
            }
        }

        internal static GPButtonDockMooring AimedFree(PickupableBoatMooringRope rope, GoPointer pointer)
        {
            if (rope == null)
                return null;

            Transform aim = pointer != null ? pointer.transform : null;
            if (aim == null && rope.held != null)
                aim = rope.held.transform;
            if (aim == null)
                return null;

            Vector3 forward = aim.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
                return null;
            forward.Normalize();

            Vector3 from = rope.transform.position;
            Rigidbody boat = rope.GetBoatRigidbody();
            GPButtonDockMooring[] cleats = Cleats();
            if (cleats == null || cleats.Length == 0)
                return null;

            float range = Range;
            float maxSqr = range * range;
            float halfArc = ArcDegrees * 0.5f;
            GPButtonDockMooring best = null;
            float bestAngle = halfArc;
            float bestSqr = maxSqr;
            for (int i = 0; i < cleats.Length; i++)
            {
                GPButtonDockMooring cleat = cleats[i];
                if (!IsFree(cleat) || IsOnSameBoat(cleat, boat))
                    continue;

                Vector3 toRope = cleat.transform.position - from;
                float sqr = toRope.sqrMagnitude;
                if (sqr > maxSqr || sqr < 0.0001f)
                    continue;

                Vector3 toPlayer = cleat.transform.position - aim.position;
                toPlayer.y = 0f;
                if (toPlayer.sqrMagnitude < 0.0001f)
                    continue;

                float angle = Vector3.Angle(forward, toPlayer);
                if (angle > halfArc)
                    continue;
                if (angle > bestAngle)
                    continue;
                if (angle == bestAngle && sqr >= bestSqr)
                    continue;

                bestAngle = angle;
                bestSqr = sqr;
                best = cleat;
            }

            return best;
        }

        // Cleats come and go with island scenes; a destroyed one in the cached
        // list compares equal to null, and IsFree skips it.
        private static GPButtonDockMooring[] Cleats()
        {
            float now = Time.unscaledTime;
            if (_cleats == null || now - _cleatsFoundAt >= CleatRefreshSeconds || now < _cleatsFoundAt)
            {
                _cleats = Object.FindObjectsOfType<GPButtonDockMooring>();
                _cleatsFoundAt = now;
            }

            return _cleats;
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

            GoPointer pointer = activatingPointer != null ? activatingPointer : __instance.held;
            GPButtonDockMooring cleat = DockMooringSnap.AimedFree(__instance, pointer);
            if (cleat == null)
                return;

            cleat.ThrowRope(__instance);
            __instance.OnDrop();
            if (pointer != null)
                pointer.DropItem();
        }
    }

    // While a boat's line is held, outline the cleat a right-click would
    // throw it onto, with the game's own flash outline. Calling it every
    // frame keeps it lit; it fades a few frames after the cleat stops being
    // the target.
    [HarmonyPatch(typeof(PickupableBoatMooringRope), nameof(PickupableBoatMooringRope.ExtraLateUpdate))]
    internal static class DockMooringTargetHighlightPatch
    {
        private const float OutlineSeconds = 0.1f;

        private static void Postfix(PickupableBoatMooringRope __instance)
        {
            if (!DockMooringSnap.HighlightEnabled())
                return;
            if (__instance.held == null || __instance.IsMoored() || GameState.inCursorMenu)
                return;

            GPButtonDockMooring cleat = DockMooringSnap.AimedFree(__instance, __instance.held);
            if (cleat != null)
                cleat.FlashOutline(OutlineSeconds);
        }
    }
}
