using HarmonyLib;
using UnityEngine;

namespace Dizzy.Fixes
{
    // Vanilla look is a single Physics.Raycast. A mug or pipe sitting on
    // a desk, crate, or walk mesh is often behind that first hit (kinematic
    // settle, flush place-align, one-sided mesh), so it is only clickable
    // from the exposed side. Prefer a nearby pickupable along the ray.
    internal static class SittingItemLook
    {
        internal const float MaxDistance = 1.8f;
        internal const int LayerMask = -604165;
        internal const float Slack = 0.22f;
        internal const float OverlapRadius = 0.2f;

        private static readonly RaycastHit[] Hits = new RaycastHit[32];
        private static readonly Collider[] Overlaps = new Collider[24];

        internal static Ray MakeRay(bool debugEditorPointer, Ray raycastRay)
        {
            Ray ray = debugEditorPointer && Camera.main != null
                ? Camera.main.ScreenPointToRay(Input.mousePosition)
                : raycastRay;
            return new Ray(ray.origin, ray.direction.normalized);
        }

        internal static bool ShouldKeep(GoPointerButton vanilla)
        {
            if (vanilla == null)
                return false;
            if (vanilla.GetComponent<Anchor>() != null)
                return true;
            if (vanilla.GetComponent<ShipItemLampHook>() != null)
                return true;
            if (vanilla.GetComponent<PickupableBoatMooringRope>() != null)
                return true;
            if (vanilla.GetComponent<GPButtonRopeWinch>() != null)
                return true;
            if (vanilla.GetComponent<ShipItemStove>() != null)
                return true;
            return false;
        }

        internal static PickupableItem Find(
            GoPointerButton vanilla,
            PickupableItem held,
            bool debugEditorPointer,
            Ray raycastRay,
            RaycastHit hit)
        {
            if (ShouldKeep(vanilla))
                return null;

            Ray ray = MakeRay(debugEditorPointer, raycastRay);
            PickupableItem best = null;
            float bestDistance = float.MaxValue;

            ConsiderRay(ray, held, ref best, ref bestDistance);
            if (hit.collider != null)
                ConsiderOverlap(hit.point, ray, held, ref best, ref bestDistance);

            if (best == null)
                return null;

            PickupableItem vanillaItem = vanilla != null
                ? vanilla.GetComponent<PickupableItem>()
                : null;
            if (best == vanillaItem)
                return null;

            float surface = hit.collider != null ? hit.distance : MaxDistance;
            if (bestDistance > surface + Slack)
                return null;

            return best;
        }

        private static void ConsiderRay(
            Ray ray,
            PickupableItem held,
            ref PickupableItem best,
            ref float bestDistance)
        {
            int count = Physics.RaycastNonAlloc(ray, Hits, MaxDistance, LayerMask);
            for (int i = 0; i < count; i++)
                ConsiderCollider(Hits[i].collider, Hits[i].distance, ray, held, ref best, ref bestDistance);
        }

        private static void ConsiderOverlap(
            Vector3 point,
            Ray ray,
            PickupableItem held,
            ref PickupableItem best,
            ref float bestDistance)
        {
            int count = Physics.OverlapSphereNonAlloc(
                point,
                OverlapRadius,
                Overlaps,
                LayerMask,
                QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
            {
                Collider collider = Overlaps[i];
                if (collider == null)
                    continue;
                float distance = Vector3.Dot(collider.bounds.center - ray.origin, ray.direction);
                if (distance < 0f || distance > MaxDistance)
                    continue;
                ConsiderCollider(collider, distance, ray, held, ref best, ref bestDistance);
            }
        }

        private static void ConsiderCollider(
            Collider collider,
            float distance,
            Ray ray,
            PickupableItem held,
            ref PickupableItem best,
            ref float bestDistance)
        {
            if (collider == null)
                return;
            if (collider.CompareTag("ItemSubcollider") && collider.transform.parent != null)
                collider = collider.transform.parent.GetComponent<Collider>();
            if (collider == null)
                return;

            PickupableItem item = collider.GetComponent<PickupableItem>();
            if (item == null)
                item = collider.GetComponentInParent<PickupableItem>();
            if (item == null || item == held)
                return;
            if (item.unclickable)
                return;
            if (item.held != null)
                return;
            if (HeldFoodLook.Enabled() && held is ShipItemFood && item is ShipItemFood)
                return;

            ShipItem shipItem = item as ShipItem;
            if (shipItem != null)
            {
                if (!shipItem.sold)
                    return;
                if (shipItem.nailed
                    && !(shipItem is ShipItemCrate)
                    && !(shipItem is ShipItemBottle)
                    && !(shipItem is ShipItemBed))
                    return;
            }

            if (distance >= bestDistance)
                return;

            Vector3 closest = collider.ClosestPoint(ray.origin + ray.direction * Mathf.Max(distance, 0f));
            Vector3 to = closest - ray.origin;
            float along = Vector3.Dot(to, ray.direction);
            if (along < 0f || along > MaxDistance)
                return;
            float lateral = (to - ray.direction * along).magnitude;
            if (lateral > OverlapRadius)
                return;

            bestDistance = distance;
            best = item;
        }
    }

    [HarmonyPatch(typeof(GoPointer), "DoRaycast")]
    [HarmonyAfter(
        "Dizzy.Fixes.StoveHoverFlickerPatch",
        "Dizzy.Fixes.OccupiedHookHoverPatch",
        "Dizzy.Fixes.DroppedAnchorLookPatch",
        "Dizzy.Fixes.DockMooringLookPatch")]
    internal static class SittingItemLookPatch
    {
        private static void Postfix(
            GoPointer __instance,
            PickupableItem ___heldItem,
            bool ___debugEditorPointer,
            Ray ___raycastRay,
            RaycastHit ___hit,
            ref GoPointerButton ___pointedAtButton,
            ref float ___currentLookDistance)
        {
            if (FixesConfig.PreferSittingItemLook == null || !FixesConfig.PreferSittingItemLook.Value)
                return;

            PickupableItem item = SittingItemLook.Find(
                ___pointedAtButton,
                ___heldItem,
                ___debugEditorPointer,
                ___raycastRay,
                ___hit);
            if (item == null || item == ___pointedAtButton)
                return;

            if (___pointedAtButton != null)
                ___pointedAtButton.ForceUnlook();

            ___pointedAtButton = item;
            item.Look(__instance);
            ___currentLookDistance = Vector3.Distance(__instance.transform.position, item.transform.position);
        }
    }
}
