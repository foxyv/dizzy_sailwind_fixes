using System;
using HarmonyLib;

namespace Dizzy.Fixes
{
    // The boat-end coil is MooringRopeLengthAdjuster. Vanilla left-click
    // picks it up to pay the line in/out. Vanilla right-click does the
    // same PickUpItem. Cast-off is walking to the dock knot and picking
    // it up (Unmoor on PickupableBoatMooringRope.OnPickup).
    internal static class BoatMooringCastOff
    {
        internal static bool Enabled()
        {
            return FixesConfig.RightClickBoatMooringCastOff != null
                && FixesConfig.RightClickBoatMooringCastOff.Value;
        }

        internal static bool TryCastOff(MooringRopeLengthAdjuster coil)
        {
            if (!Enabled() || coil == null)
                return false;
            if (coil.held != null)
                return false;

            PickupableBoatMooringRope rope = coil.mooringRope;
            if (rope == null || !rope.IsMoored())
                return false;

            try
            {
                rope.Unmoor();
                rope.OnDrop();
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("RightClickBoatMooringCastOff: Unmoor failed: " + e.Message);
                return false;
            }

            // Vanilla unmoor is PickUpItem on the dock knot, which plays
            // itemPickup. This path never picks the rope up, so play it here.
            if (UISoundPlayer.instance != null)
                UISoundPlayer.instance.PlayUISound(UISounds.itemPickup, 0.45f, 1.5f);

            return true;
        }
    }

    [HarmonyPatch(typeof(MooringRopeLengthAdjuster), nameof(MooringRopeLengthAdjuster.OnAltActivate), typeof(GoPointer))]
    internal static class BoatMooringCastOffPatch
    {
        private static bool Prefix(MooringRopeLengthAdjuster __instance)
        {
            return !BoatMooringCastOff.TryCastOff(__instance);
        }
    }
}
