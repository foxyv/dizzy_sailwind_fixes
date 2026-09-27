using HarmonyLib;
using UnityEngine;

namespace Dizzy.Fixes
{
    // The look ray stops on the first collider. Food already on a drying
    // rack or shelf is in front of that surface, and PreferSittingItemLook
    // will also grab a nearby apple. Either way the rack never stays
    // targeted, so a held apple highlights the one on the shelf instead of
    // placing. While holding food, skip other food and keep the next
    // surface the held item is allowed to click.
    internal static class HeldFoodLook
    {
        private static readonly RaycastHit[] Hits = new RaycastHit[32];

        internal static bool Enabled()
        {
            return FixesConfig.SkipOtherFoodWhileHolding != null
                && FixesConfig.SkipOtherFoodWhileHolding.Value;
        }

        // A destroyed hold still passes `is ShipItemFood`. Unity's == is
        // what treats it as empty. Eating an apple leaves that corpse in
        // the pointer until another item replaces it.
        internal static bool HoldingFood(PickupableItem held)
        {
            return held != null && held is ShipItemFood;
        }

        internal static GoPointerButton SurfaceBehindFood(
            PickupableItem held,
            bool debugEditorPointer,
            Ray raycastRay)
        {
            if (!HoldingFood(held))
                return null;

            Ray ray = SittingItemLook.MakeRay(debugEditorPointer, raycastRay);
            int count = Physics.RaycastNonAlloc(
                ray,
                Hits,
                SittingItemLook.MaxDistance,
                SittingItemLook.LayerMask);
            GoPointerButton best = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                float distance = Hits[i].distance;
                if (distance >= bestDistance)
                    continue;

                Collider collider = Hits[i].collider;
                if (collider != null && collider.CompareTag("ItemSubcollider") && collider.transform.parent != null)
                    collider = collider.transform.parent.GetComponent<Collider>();
                if (collider == null)
                    continue;

                GoPointerButton button = collider.GetComponent<GoPointerButton>();
                if (!HeldCanLook(held, button))
                    continue;

                bestDistance = distance;
                best = button;
            }

            return best;
        }

        private static bool HeldCanLook(PickupableItem held, GoPointerButton button)
        {
            if (button == null || button.unclickable || held == null)
                return false;

            ShipItem ship = button.GetComponent<ShipItem>();
            if (ship == held || ship is ShipItemFood)
                return false;
            if (held.AllowOnItemClick(button))
                return true;
            return ship == null && button.GetComponent<GPButtonBed>() == null;
        }
    }

    [HarmonyPatch(typeof(GoPointer), "DoRaycast")]
    [HarmonyAfter("Dizzy.Fixes.SittingItemLookPatch")]
    internal static class SkipOtherFoodWhileHoldingPatch
    {
        private static void Postfix(
            GoPointer __instance,
            PickupableItem ___heldItem,
            bool ___debugEditorPointer,
            Ray ___raycastRay,
            ref GoPointerButton ___pointedAtButton,
            ref float ___currentLookDistance)
        {
            if (!HeldFoodLook.Enabled() || !HeldFoodLook.HoldingFood(___heldItem))
                return;
            if (___pointedAtButton != null && !(___pointedAtButton is ShipItemFood))
                return;

            GoPointerButton surface = HeldFoodLook.SurfaceBehindFood(
                ___heldItem,
                ___debugEditorPointer,
                ___raycastRay);
            if (surface == null || surface == ___pointedAtButton)
            {
                if (___pointedAtButton is ShipItemFood)
                {
                    ___pointedAtButton.ForceUnlook();
                    ___pointedAtButton = null;
                    ___currentLookDistance = 0f;
                }

                return;
            }

            if (___pointedAtButton != null)
                ___pointedAtButton.ForceUnlook();

            ___pointedAtButton = surface;
            surface.Look(__instance);
            ___currentLookDistance = Vector3.Distance(__instance.transform.position, surface.transform.position);
        }
    }

    // Destroy() does not clear GoPointer.heldItem, and DropItem bails on a
    // destroyed object, so the corpse stays until another pickup replaces
    // it. The stall also keeps recentlyBoughtItem, which blocks the buy
    // prompt on that same object. Releasing both is what picking an
    // inventory item up and putting it back was doing by accident.
    [HarmonyPatch(typeof(ShipItem), nameof(ShipItem.DestroyItem))]
    internal static class ReleaseDestroyedHoldPatch
    {
        private static void Postfix(ShipItem __instance, bool __runOriginal)
        {
            if (!__runOriginal)
                return;
            if (FixesConfig.ReleaseDestroyedHeldItem == null || !FixesConfig.ReleaseDestroyedHeldItem.Value)
                return;
            if (__instance == null)
                return;

            GoPointer pointer = __instance.held;
            if (pointer != null && pointer.GetHeldItem() == __instance)
            {
                Traverse.Create(pointer).Field("heldItem").SetValue(null);
                __instance.held = null;
            }

            BuyItemUI ui = BuyItemUI.instance;
            if (ui != null)
            {
                if (ui.activeItem == __instance)
                    ui.DeactivateUI();
                else if (ui.recentlyBoughtItem == __instance)
                    ui.recentlyBoughtItem = null;
            }

            GPButtonInventorySlot[] slots = GPButtonInventorySlot.inventorySlots;
            if (slots == null)
                return;

            for (int i = 0; i < slots.Length; i++)
            {
                GPButtonInventorySlot slot = slots[i];
                if (slot != null && slot.currentItem == __instance)
                    slot.currentItem = null;
            }
        }
    }
}
