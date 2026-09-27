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

        internal static GoPointerButton SurfaceBehindFood(
            PickupableItem held,
            bool debugEditorPointer,
            Ray raycastRay)
        {
            if (!(held is ShipItemFood))
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
            if (!HeldFoodLook.Enabled() || !(___heldItem is ShipItemFood))
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
}
