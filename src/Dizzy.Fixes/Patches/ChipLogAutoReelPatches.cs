using System.Collections.Generic;
using Crest;
using HarmonyLib;
using UnityEngine;

namespace Dizzy.Fixes
{
    // ShipItemChipLog.Update reels in whenever the chip is airborne.
    // ChangeLineLength is a one-line adder and Mono may inline it, so
    // patching that method does not stop the wind-in. After Update has
    // subtracted autoReturnSpeed, add twice that back so chop pays the
    // line out instead. InstantUnthrow (right-click) still sets
    // currentTargetLength to minLength directly.
    //
    // Wait until the throw animation finishes (or the chip is in the
    // water) so the toss can still reel in the air.
    internal static class ChipLogDeployed
    {
        private static readonly Dictionary<int, bool> Deployed = new Dictionary<int, bool>();

        internal static bool Enabled()
        {
            return FixesConfig.KeepChipLogDeployed != null
                && FixesConfig.KeepChipLogDeployed.Value;
        }

        internal static void NoteDeployed(
            ShipItemChipLog log,
            bool thrown,
            bool throwing,
            SimpleFloatingObject floater)
        {
            if (!Enabled() || log == null)
                return;

            int id = log.GetInstanceID();
            if (!thrown)
            {
                Deployed.Remove(id);
                return;
            }

            bool inWater = floater != null && floater.InWater;
            if (!throwing || inWater)
                Deployed[id] = true;
        }

        internal static bool IsDeployed(ShipItemChipLog log)
        {
            if (!Enabled() || log == null)
                return false;
            bool deployed;
            return Deployed.TryGetValue(log.GetInstanceID(), out deployed) && deployed;
        }

        internal static void Forget(ShipItemChipLog log)
        {
            if (log != null)
                Deployed.Remove(log.GetInstanceID());
        }

        internal static void ReverseAirborneReturn(
            ShipItemChipLog log,
            SimpleFloatingObject floater,
            Rigidbody body,
            float autoReturnSpeed,
            ref float currentTargetLength)
        {
            if (!IsDeployed(log))
                return;
            if (floater == null || body == null)
                return;
            if (floater.InWater || body.isKinematic)
                return;

            currentTargetLength += 2f * autoReturnSpeed * Time.deltaTime;
        }
    }

    [HarmonyPatch(typeof(ShipItemChipLog), "Update")]
    internal static class ChipLogKeepDeployedPatch
    {
        private static void Prefix(
            ShipItemChipLog __instance,
            bool ___thrown,
            bool ___throwing,
            SimpleFloatingObject ___bobberFloater)
        {
            ChipLogDeployed.NoteDeployed(__instance, ___thrown, ___throwing, ___bobberFloater);
        }

        private static void Postfix(
            ShipItemChipLog __instance,
            SimpleFloatingObject ___bobberFloater,
            Rigidbody ___bobberBody,
            float ___autoReturnSpeed,
            ref float ___currentTargetLength)
        {
            ChipLogDeployed.ReverseAirborneReturn(
                __instance,
                ___bobberFloater,
                ___bobberBody,
                ___autoReturnSpeed,
                ref ___currentTargetLength);
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
