using BepInEx.Bootstrap;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace Dizzy.Fixes
{
    // The look ray stops on the first collider, and a highlighted object
    // blocks the drop. Food on a drying rack or shelf sits in front of that
    // surface, so a held apple highlights the one on the shelf instead of
    // placing; a held piece of firewood highlights the next piece and left
    // click does nothing. While holding either, skip items of the same kind
    // and keep the next surface the held item is allowed to click.
    internal static class HeldItemLook
    {
        internal enum Kind
        {
            None,
            Food,
            Firewood
        }

        private const string FirewoodBundleGuid = "com.dizzy.sailwind.firewoodbundle";

        private static readonly RaycastHit[] Hits = new RaycastHit[32];
        private static bool _bundleChecked;
        private static ConfigEntryBase _bundleStackFirewood;

        // The kind this fix acts on for the held item, or None when the item
        // is neither or its toggle is off.
        internal static Kind ActiveKind(PickupableItem held)
        {
            if (HoldingFood(held))
                return FoodEnabled() ? Kind.Food : Kind.None;
            if (IsFirewood(held))
                return FirewoodEnabled() ? Kind.Firewood : Kind.None;
            return Kind.None;
        }

        internal static bool SameKind(Kind kind, Component item)
        {
            if (kind == Kind.Food)
                return item is ShipItemFood;
            if (kind == Kind.Firewood)
                return IsFirewood(item);
            return false;
        }

        private static bool FoodEnabled()
        {
            return FixesConfig.SkipOtherFoodWhileHolding != null
                && FixesConfig.SkipOtherFoodWhileHolding.Value;
        }

        private static bool FirewoodEnabled()
        {
            return FixesConfig.SkipOtherFirewoodWhileHolding != null
                && FixesConfig.SkipOtherFirewoodWhileHolding.Value
                && !FirewoodBundleHandlesIt();
        }

        // Dizzy.FirewoodBundle aims past other wood while a piece is held, and
        // runs after this patch. Step aside while its "Stack Firewood" is on.
        private static bool FirewoodBundleHandlesIt()
        {
            if (!_bundleChecked)
            {
                _bundleChecked = true;
                BepInEx.PluginInfo info;
                if (Chainloader.PluginInfos.TryGetValue(FirewoodBundleGuid, out info) && info.Instance != null)
                {
                    ConfigDefinition key = new ConfigDefinition("General", "Stack Firewood");
                    if (info.Instance.Config.ContainsKey(key))
                        _bundleStackFirewood = info.Instance.Config[key];
                    else
                        Plugin.Log.LogWarning("SkipOtherFirewoodWhileHolding: Dizzy.FirewoodBundle has no [General] Stack Firewood setting; keeping this fix on.");
                }
            }

            return _bundleStackFirewood != null
                && _bundleStackFirewood.BoxedValue is bool on
                && on;
        }

        // A destroyed hold still passes `is ShipItemFood`. Unity's == is
        // what treats it as empty. Eating an apple leaves that corpse in
        // the pointer until another item replaces it.
        private static bool HoldingFood(PickupableItem held)
        {
            return held != null && held is ShipItemFood;
        }

        // ShipItem.name is the item label ("firewood"), not the crate
        // ("108 crate of firewood") and not the clone's object name.
        private static bool IsFirewood(Component item)
        {
            ShipItem ship = item as ShipItem;
            return ship != null && ship.name == "firewood";
        }

        internal static GoPointerButton SurfaceBehind(
            Kind kind,
            PickupableItem held,
            bool debugEditorPointer,
            Ray raycastRay)
        {
            if (kind == Kind.None || held == null)
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
                if (!HeldCanLook(kind, held, button))
                    continue;

                bestDistance = distance;
                best = button;
            }

            return best;
        }

        private static bool HeldCanLook(Kind kind, PickupableItem held, GoPointerButton button)
        {
            if (button == null || button.unclickable)
                return false;

            ShipItem ship = button.GetComponent<ShipItem>();
            if (ship == held || SameKind(kind, button))
                return false;
            if (held.AllowOnItemClick(button))
                return true;
            return ship == null && button.GetComponent<GPButtonBed>() == null;
        }
    }

    [HarmonyPatch(typeof(GoPointer), "DoRaycast")]
    // Just below SittingItemLookPatch (Priority.Low) so it runs after it.
    [HarmonyPriority(Priority.Low - 10)]
    internal static class SkipOtherHeldKindPatch
    {
        private static void Postfix(
            GoPointer __instance,
            PickupableItem ___heldItem,
            bool ___debugEditorPointer,
            Ray ___raycastRay,
            ref GoPointerButton ___pointedAtButton,
            ref float ___currentLookDistance)
        {
            HeldItemLook.Kind kind = HeldItemLook.ActiveKind(___heldItem);
            if (kind == HeldItemLook.Kind.None)
                return;
            if (___pointedAtButton != null && !HeldItemLook.SameKind(kind, ___pointedAtButton))
                return;

            GoPointerButton surface = HeldItemLook.SurfaceBehind(
                kind,
                ___heldItem,
                ___debugEditorPointer,
                ___raycastRay);
            if (surface == null || surface == ___pointedAtButton)
            {
                if (HeldItemLook.SameKind(kind, ___pointedAtButton))
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
