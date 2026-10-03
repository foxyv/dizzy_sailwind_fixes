using System;
using HarmonyLib;
using UnityEngine;

namespace Dizzy.Fixes
{
    internal static class InventorySipGuard
    {
        private const float GraceSeconds = 1f;

        private static readonly AccessTools.FieldRef<ShipItemBottle, bool> BottleIsDrinking = GameMembers.Field<ShipItemBottle, bool>("drinking");
        private static readonly AccessTools.FieldRef<ShipItemSoup, bool> SoupIsDrinking = GameMembers.Field<ShipItemSoup, bool>("drinking");
        private static readonly Func<GoPointer, bool> AltButtonHeld = GameMembers.Method<Func<GoPointer, bool>>(typeof(GoPointer), "AltButtonHeld");

        private static ShipItem _item;
        private static float _until;

        internal static void Remember(ShipItem item)
        {
            if (item == null)
                return;

            _item = item;
            _until = Time.realtimeSinceStartup + GraceSeconds;
        }

        internal static bool ShouldBlockAccidentalSip(Component component)
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

            if (component != _item && component.GetComponent<ShipItem>() != _item)
                return false;

            // Holding drink (Activate) is intentional — do not eat that first sip.
            // Skipping MouthCol.BottleEnter caused OnTriggerEnter to be consumed
            // during grace, so the player had to leave and re-enter the mouth.
            return !IsDrinkHeld(component);
        }

        private static bool IsDrinkHeld(Component component)
        {
            ShipItemBottle bottle = component.GetComponent<ShipItemBottle>();
            if (bottle != null)
            {
                if (bottle.IsDrinking())
                    return true;
                if (BottleIsDrinking != null && BottleIsDrinking(bottle))
                    return true;
                return AltHeld(bottle.held);
            }

            ShipItemSoup soup = component.GetComponent<ShipItemSoup>();
            if (soup != null)
            {
                if (SoupIsDrinking != null && SoupIsDrinking(soup))
                    return true;
                return AltHeld(soup.held);
            }

            return false;
        }

        private static bool AltHeld(GoPointer pointer)
        {
            return pointer != null && AltButtonHeld != null && AltButtonHeld(pointer);
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

    [HarmonyPatch(typeof(BottleDrinking), nameof(BottleDrinking.TryDrink))]
    internal static class BottleDrinkingTryDrinkPatch
    {
        private static bool Prefix(BottleDrinking __instance)
        {
            return !InventorySipGuard.ShouldBlockAccidentalSip(__instance);
        }
    }
}
