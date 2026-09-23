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
    // Only after the bobber has been in the water. A freshly bought
    // log can report airborne the whole throw (bobber still at the
    // stall, or the floater not sampling yet) and would otherwise pay
    // the line out to the stop with no speed reading. The toss can
    // still reel in the air. Right-click still sets min length.
    //
    // OnLoad parents the bobber to the shifting world and leaves it
    // at the stall. OnBuy does not move it, and Sell picks the reel
    // up before the held pose is applied, so a one-shot snap on buy
    // freezes the chip at the stall. Until the log is thrown, snap
    // the bobber onto the reel each ExtraLateUpdate so it follows
    // the item into the hand. ThrowRod then releases it.
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

            if (floater != null && floater.InWater)
                Deployed[id] = true;
        }

        internal static bool IsDeployed(ShipItemChipLog log)
        {
            if (!Enabled() || log == null)
                return false;
            bool deployed;
            return Deployed.TryGetValue(log.GetInstanceID(), out deployed) && deployed;
        }

        internal static void HoldOnReel(
            ShipItemChipLog log,
            bool thrown,
            bool throwing,
            ConfigurableJoint bobberJoint,
            ref Rigidbody bobberBody,
            Vector3 initialBobberPos,
            ref float currentTargetLength,
            float minLength)
        {
            if (!Enabled() || log == null || !log.sold || thrown || throwing || bobberJoint == null)
                return;

            if (bobberBody == null)
                bobberBody = bobberJoint.GetComponent<Rigidbody>();
            if (bobberBody == null)
                return;

            if (initialBobberPos == Vector3.zero)
                initialBobberPos = bobberJoint.connectedAnchor;

            bobberBody.isKinematic = true;
            bobberJoint.transform.position = log.transform.TransformPoint(initialBobberPos);
            bobberJoint.transform.rotation = log.transform.rotation;
            currentTargetLength = minLength;
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
            SimpleFloatingObject ___bobberFloater)
        {
            ChipLogDeployed.NoteDeployed(__instance, ___thrown, ___bobberFloater);
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

    [HarmonyPatch(typeof(ShipItemChipLog), "ExtraLateUpdate")]
    internal static class ChipLogHoldOnReelPatch
    {
        private static void Postfix(
            ShipItemChipLog __instance,
            bool ___thrown,
            bool ___throwing,
            ConfigurableJoint ___bobberJoint,
            ref Rigidbody ___bobberBody,
            Vector3 ___initialBobberPos,
            ref float ___currentTargetLength,
            float ___minLength)
        {
            ChipLogDeployed.HoldOnReel(
                __instance,
                ___thrown,
                ___throwing,
                ___bobberJoint,
                ref ___bobberBody,
                ___initialBobberPos,
                ref ___currentTargetLength,
                ___minLength);
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
