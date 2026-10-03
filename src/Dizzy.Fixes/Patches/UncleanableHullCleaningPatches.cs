using System;
using HarmonyLib;
using UnityEngine;

namespace Dizzy.Fixes
{
    // ConfirmOrder charges gold, applies parts, then calls GetCleanable()
    // without a null-check on SaveableObject. Boats that cannot get dirty
    // (Jong, missing SaveableObject, empty saveCleanable) NRE there, skip
    // ResetOrder, and DischargeShip → CancelOrder reverts the paid work.
    internal static class UncleanableBoat
    {
        internal static bool CanClean(GameObject ship)
        {
            if (ship == null)
                return false;

            SaveableObject saveable = ship.GetComponent<SaveableObject>();
            if (saveable == null)
                return false;

            return saveable.GetCleanable() != null;
        }
    }

    [HarmonyPatch(typeof(Shipyard), nameof(Shipyard.CleanHull))]
    internal static class ShipyardCleanHullPatch
    {
        private static bool Prefix(Shipyard __instance, GameObject ___currentShip, ref bool ___currentOrderIncludesCleaning)
        {
            if (!FixesConfig.SkipUncleanableHullCleaning.Value)
                return true;
            if (UncleanableBoat.CanClean(___currentShip))
                return true;

            ___currentOrderIncludesCleaning = false;
            NotificationUi.instance.ShowNotification("This ship cannot be cleaned.");
            __instance.UpdateOrder();
            return false;
        }
    }

    [HarmonyPatch(typeof(Shipyard), nameof(Shipyard.UpdateOrder), new[] { typeof(bool) })]
    internal static class ShipyardUpdateOrderCleaningPatch
    {
        private static void Prefix(GameObject ___currentShip, ref bool ___currentOrderIncludesCleaning)
        {
            if (!FixesConfig.SkipUncleanableHullCleaning.Value)
                return;
            if (___currentOrderIncludesCleaning && !UncleanableBoat.CanClean(___currentShip))
                ___currentOrderIncludesCleaning = false;
        }
    }

    [HarmonyPatch(typeof(Shipyard), nameof(Shipyard.ConfirmOrder))]
    internal static class ShipyardConfirmOrderCleaningPatch
    {
        private static void Prefix(Shipyard __instance, GameObject ___currentShip, ref bool ___currentOrderIncludesCleaning, out int __state)
        {
            __state = PlayerGold.currency[__instance.region];
            if (!FixesConfig.SkipUncleanableHullCleaning.Value)
                return;
            if (!___currentOrderIncludesCleaning || UncleanableBoat.CanClean(___currentShip))
                return;

            ___currentOrderIncludesCleaning = false;
            __instance.UpdateOrder();
        }

        // Only recover once vanilla has charged the gold: past that point a
        // throw would let DischargeShip → CancelOrder revert the paid work. An
        // exception before the charge is left to propagate so the order is not
        // committed for free.
        private static Exception Finalizer(Shipyard __instance, Exception __exception, int __state)
        {
            if (!FixesConfig.SkipUncleanableHullCleaning.Value || __exception == null)
                return __exception;
            if (PlayerGold.currency[__instance.region] >= __state)
                return __exception;

            Plugin.Log.LogError("Shipyard confirm failed after charging; keeping applied modifications. " + __exception);
            try
            {
                Traverse.Create(__instance).Method("ResetOrder").GetValue();
                __instance.UpdateOrder();
            }
            catch (Exception resetError)
            {
                Plugin.Log.LogError("Could not snapshot shipyard order after confirm failure: " + resetError);
            }

            return null;
        }
    }
}
