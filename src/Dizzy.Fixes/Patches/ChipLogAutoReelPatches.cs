using HarmonyLib;

namespace Dizzy.Fixes
{
    // ShipItemChipLog.Update reels in whenever the chip is airborne.
    // Crest waves flicker SimpleFloatingObject.InWater, so the line
    // winds itself back in chop. InstantUnthrow (right-click) sets
    // currentTargetLength directly and does not use ChangeLineLength.
    internal static class ChipLogDeployed
    {
        private static bool _loggedMissing;

        internal static bool ShouldSkipAutoReturn(ShipItemChipLog log, float value)
        {
            if (!FixesConfig.KeepChipLogDeployed.Value)
                return false;
            if (value >= 0f)
                return false;

            Traverse thrown = Traverse.Create(log).Field("thrown");
            if (!thrown.FieldExists())
            {
                if (!_loggedMissing)
                {
                    Plugin.Log.LogWarning("KeepChipLogDeployed: ShipItemChipLog.thrown is missing; leaving vanilla auto-reel.");
                    _loggedMissing = true;
                }

                return false;
            }

            return thrown.GetValue<bool>();
        }
    }

    [HarmonyPatch(typeof(ShipItemChipLog), "ChangeLineLength")]
    internal static class ChipLogAutoReelPatch
    {
        private static bool Prefix(ShipItemChipLog __instance, float value)
        {
            return !ChipLogDeployed.ShouldSkipAutoReturn(__instance, value);
        }
    }
}
