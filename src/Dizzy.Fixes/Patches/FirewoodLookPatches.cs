using HarmonyLib;
using UnityEngine;

namespace Dizzy.Fixes
{
    // A highlighted object blocks the drop. Another piece of firewood is
    // not something the held piece can be placed on, but the look ray still
    // stops on it, so left click does nothing.
    internal static class HeldFirewoodLook
    {
        private static readonly RaycastHit[] Hits = new RaycastHit[32];

        internal static bool Enabled()
        {
            return FixesConfig.SkipOtherFirewoodWhileHolding != null
                && FixesConfig.SkipOtherFirewoodWhileHolding.Value;
        }

        // ShipItem.name is the item label ("firewood"), not the crate
        // ("108 crate of firewood") and not the clone's object name.
        internal static bool IsFirewood(Component item)
        {
            ShipItem ship = item as ShipItem;
            return ship != null && ship.name == "firewood";
        }

        internal static GoPointerButton SurfaceBehind(
            PickupableItem held,
            bool debugEditorPointer,
            Ray raycastRay)
        {
            if (!IsFirewood(held))
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
            if (IsFirewood(button) || button.GetComponent<ShipItem>() == held)
                return false;
            if (held.AllowOnItemClick(button))
                return true;
            return button.GetComponent<ShipItem>() == null && button.GetComponent<GPButtonBed>() == null;
        }
    }

    [HarmonyPatch(typeof(GoPointer), "DoRaycast")]
    [HarmonyAfter(
        "Dizzy.Fixes.SittingItemLookPatch",
        "Dizzy.Fixes.SkipOtherFoodWhileHoldingPatch")]
    internal static class SkipOtherFirewoodWhileHoldingPatch
    {
        private static void Postfix(
            GoPointer __instance,
            PickupableItem ___heldItem,
            bool ___debugEditorPointer,
            Ray ___raycastRay,
            ref GoPointerButton ___pointedAtButton,
            ref float ___currentLookDistance)
        {
            if (!HeldFirewoodLook.Enabled() || !HeldFirewoodLook.IsFirewood(___heldItem))
                return;
            if (___pointedAtButton != null && !HeldFirewoodLook.IsFirewood(___pointedAtButton))
                return;

            GoPointerButton surface = HeldFirewoodLook.SurfaceBehind(
                ___heldItem,
                ___debugEditorPointer,
                ___raycastRay);
            if (surface == null || surface == ___pointedAtButton)
            {
                if (HeldFirewoodLook.IsFirewood(___pointedAtButton))
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
}
