using HarmonyLib;
using UnityEngine;

namespace Dizzy.Fixes
{
    internal static class InventorySipGuard
    {
        private const float GraceSeconds = 1f;

        private static ShipItem _item;
        private static float _until;

        internal static void Remember(ShipItem item)
        {
            if (item == null)
                return;

            _item = item;
            _until = Time.realtimeSinceStartup + GraceSeconds;
        }

        internal static bool ShouldBlock(Component component)
        {
            if (!FixesConfig.PreventInventoryWithdrawSip.Value)
                return false;
            if (_item == null || component == null)
                return false;
            if (Time.realtimeSinceStartup >= _until)
            {
                _item = null;
                return false;
            }

            return component == _item || component.GetComponent<ShipItem>() == _item;
        }
    }

    [HarmonyPatch(typeof(GPButtonInventorySlot), nameof(GPButtonInventorySlot.WithdrawItem))]
    internal static class WithdrawItemSipPatch
    {
        private static void Prefix(GPButtonInventorySlot __instance)
        {
            InventorySipGuard.Remember(__instance.currentItem);
        }
    }

    // Inventory slots sit on the needs UI. With the UI closed (scale 0) that
    // origin is at the camera / mouth, so enabling the bottle collider on
    // withdraw immediately overlaps MouthCol and sips one unit.
    [HarmonyPatch(typeof(MouthCol), nameof(MouthCol.BottleEnter))]
    internal static class MouthColBottleEnterPatch
    {
        private static bool Prefix(BottleDrinking bottle)
        {
            return !InventorySipGuard.ShouldBlock(bottle);
        }
    }

    [HarmonyPatch(typeof(BottleDrinking), nameof(BottleDrinking.TryDrink))]
    internal static class BottleDrinkingTryDrinkPatch
    {
        private static bool Prefix(BottleDrinking __instance)
        {
            return !InventorySipGuard.ShouldBlock(__instance);
        }
    }
}
