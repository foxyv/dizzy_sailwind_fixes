using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Dizzy.Fixes
{
    // ShipItemChipLog.Update reels in whenever the chip is airborne.
    // Crest waves flicker SimpleFloatingObject.InWater, so the line
    // winds itself back in chop after it is in the water.
    // InstantUnthrow (right-click) sets currentTargetLength directly
    // and does not use ChangeLineLength.
    //
    // Skipping every negative ChangeLineLength while thrown also
    // blocked the air-toss reel. The chip is still flying, useVelocity
    // / useDeltaPos keep paying out, and the line stretches to maxLength
    // with no reading. Only skip auto-reel after this throw has been
    // in the water once.
    internal static class ChipLogDeployed
    {
        private static readonly Dictionary<int, bool> HitWater = new Dictionary<int, bool>();

        private static bool _loggedThrown;
        private static bool _loggedFloater;

        internal static bool Enabled()
        {
            return FixesConfig.KeepChipLogDeployed != null
                && FixesConfig.KeepChipLogDeployed.Value;
        }

        internal static void NoteInWater(ShipItemChipLog log)
        {
            if (!Enabled() || log == null)
                return;

            int id = log.GetInstanceID();
            Traverse thrown = Traverse.Create(log).Field("thrown");
            if (!thrown.FieldExists())
            {
                if (!_loggedThrown)
                {
                    Plugin.Log.LogWarning("KeepChipLogDeployed: ShipItemChipLog.thrown is missing; leaving vanilla auto-reel.");
                    _loggedThrown = true;
                }

                return;
            }

            if (!thrown.GetValue<bool>())
            {
                HitWater.Remove(id);
                return;
            }

            if (BobberInWater(log))
                HitWater[id] = true;
        }

        internal static void Forget(ShipItemChipLog log)
        {
            if (log != null)
                HitWater.Remove(log.GetInstanceID());
        }

        internal static bool ShouldSkipAutoReturn(ShipItemChipLog log, float value)
        {
            if (!Enabled())
                return false;
            if (log == null || value >= 0f)
                return false;

            int id = log.GetInstanceID();
            bool hit;
            return HitWater.TryGetValue(id, out hit) && hit;
        }

        private static bool BobberInWater(ShipItemChipLog log)
        {
            Traverse floaterField = Traverse.Create(log).Field("bobberFloater");
            if (!floaterField.FieldExists())
            {
                if (!_loggedFloater)
                {
                    Plugin.Log.LogWarning("KeepChipLogDeployed: ShipItemChipLog.bobberFloater is missing; leaving vanilla auto-reel.");
                    _loggedFloater = true;
                }

                return false;
            }

            object floater = floaterField.GetValue();
            if (floater == null || floater.Equals(null))
                return false;

            Traverse inWater = Traverse.Create(floater).Property("InWater");
            if (inWater.PropertyExists())
                return inWater.GetValue<bool>();

            inWater = Traverse.Create(floater).Field("InWater");
            if (inWater.FieldExists())
                return inWater.GetValue<bool>();

            return false;
        }
    }

    [HarmonyPatch(typeof(ShipItemChipLog), "Update")]
    internal static class ChipLogNoteInWaterPatch
    {
        private static void Postfix(ShipItemChipLog __instance)
        {
            ChipLogDeployed.NoteInWater(__instance);
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

    [HarmonyPatch(typeof(ShipItemChipLog), "OnDestroy")]
    internal static class ChipLogDestroyPatch
    {
        private static void Prefix(ShipItemChipLog __instance)
        {
            ChipLogDeployed.Forget(__instance);
        }
    }
}
