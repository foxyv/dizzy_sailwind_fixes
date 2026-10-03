using HarmonyLib;
using UnityEngine;

namespace Dizzy.Fixes
{
    // Vanilla OnAltHeld adds Time.deltaTime and nails at 2s. Swing is
    // 550 deg/s between -35 and -95. Scale both by 2 / configured seconds.
    internal static class HammerNail
    {
        private const float VanillaSeconds = 2f;
        internal const float VanillaSwing = 550f;
        private const float MinSeconds = 0.05f;
        private const float MaxSeconds = 10f;

        internal static float Seconds
        {
            get
            {
                float value = FixesConfig.HammerNailSeconds != null
                    ? FixesConfig.HammerNailSeconds.Value
                    : VanillaSeconds;
                if (value < MinSeconds)
                    value = MinSeconds;
                if (value > MaxSeconds)
                    value = MaxSeconds;
                return value;
            }
        }

        internal static float Mult
        {
            get { return VanillaSeconds / Seconds; }
        }

        internal static bool Faster()
        {
            return FixesConfig.HammerNailSeconds != null
                && Mathf.Abs(Mult - 1f) > 0.001f;
        }
    }

    [HarmonyPatch(typeof(ShipItemHammer), nameof(ShipItemHammer.OnAltHeld))]
    internal static class HammerNailSpeedPatch
    {
        private static void Prefix(
            ShipItemHammer __instance,
            ShipItem ___currentlyNailedItem,
            ref float ___nailTimer)
        {
            if (!HammerNail.Faster())
                return;
            if (__instance == null || !__instance.sold || ___currentlyNailedItem == null)
                return;
            if (__instance.held == null)
                return;
            if (__instance.held.GetPointedAtItem() != ___currentlyNailedItem)
                return;

            ___nailTimer += Time.deltaTime * (HammerNail.Mult - 1f);
        }

        private static void Postfix(
            ShipItemHammer __instance,
            float ___nailTimer,
            bool ___swingingBack)
        {
            if (!HammerNail.Faster())
                return;
            if (__instance == null || ___nailTimer <= 0f)
                return;

            float extra = Time.deltaTime * HammerNail.VanillaSwing * (HammerNail.Mult - 1f);
            if (___swingingBack)
                __instance.heldRotationOffset += extra;
            else
                __instance.heldRotationOffset -= extra;
        }
    }
}
