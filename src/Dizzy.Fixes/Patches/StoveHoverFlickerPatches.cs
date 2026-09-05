using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Dizzy.Fixes
{
    // GoPointer keeps the first collider on the look ray. Food sits inside
    // the stove, so the outline used to flip stove/item. Stick to a miss
    // only while the ray is still on that stove; pick the cook slot whose
    // position is closest to the ray so neighboring items stay selectable.
    internal static class StoveHoverAim
    {
        internal const float MaxDistance = 1.8f;
        internal const int LayerMask = -604165;
        internal const float StickySeconds = 0.12f;
        internal const float AimRadius = 0.2f;

        private static readonly RaycastHit[] Hits = new RaycastHit[32];
        private static readonly List<ShipItemStove> Stoves = new List<ShipItemStove>(4);

        internal static GoPointerButton _sticky;
        internal static float _stickyUntil;

        internal static void ClearSticky()
        {
            _sticky = null;
            _stickyUntil = 0f;
        }

        internal static bool StickyValid()
        {
            if (_sticky == null || Time.realtimeSinceStartup >= _stickyUntil)
                return false;
            return IsStoveItem(_sticky);
        }

        internal static bool IsOtherInteractable(GoPointerButton button)
        {
            if (button == null)
                return false;
            if (button.GetComponent<ShipItemStove>() != null)
                return false;
            return !IsStoveItem(button);
        }

        internal static Ray MakeRay(bool debugEditorPointer, Ray raycastRay)
        {
            Ray ray = debugEditorPointer && Camera.main != null
                ? Camera.main.ScreenPointToRay(Input.mousePosition)
                : raycastRay;
            Vector3 origin = ray.origin;
            Vector3 direction = ray.direction.normalized;
            return new Ray(origin, direction);
        }

        internal static void CollectStovesAlongRay(Ray ray)
        {
            Stoves.Clear();
            int count = Physics.RaycastNonAlloc(ray, Hits, MaxDistance, LayerMask);
            for (int i = 0; i < count; i++)
            {
                Collider collider = ResolveCollider(Hits[i].collider);
                if (collider == null)
                    continue;
                ShipItemStove stove = StoveFrom(collider);
                if (stove != null && !Stoves.Contains(stove))
                    Stoves.Add(stove);
            }
        }

        internal static int StoveCount
        {
            get { return Stoves.Count; }
        }

        internal static GoPointerButton ClosestAimedItem(Ray ray, out float along)
        {
            along = MaxDistance;
            GoPointerButton best = null;
            float bestLateral = AimRadius;
            Vector3 origin = ray.origin;
            Vector3 direction = ray.direction;

            for (int s = 0; s < Stoves.Count; s++)
            {
                ShipItemStove stove = Stoves[s];
                if (stove == null || stove.slots == null)
                    continue;

                for (int i = 0; i < stove.slots.Length; i++)
                {
                    StoveCookTrigger slot = stove.slots[i];
                    if (slot == null || slot.currentFood == null)
                        continue;

                    GoPointerButton button = slot.currentFood.GetComponent<GoPointerButton>();
                    if (button == null || button.unclickable)
                        continue;

                    if (!IsCloserAim(origin, direction, slot.transform.position, ref bestLateral, ref along))
                        continue;
                    best = button;
                }
            }

            int hitCount = Physics.RaycastNonAlloc(ray, Hits, MaxDistance, LayerMask);
            for (int i = 0; i < hitCount; i++)
            {
                Collider collider = ResolveCollider(Hits[i].collider);
                if (collider == null)
                    continue;

                StoveFuel fuel = collider.GetComponent<StoveFuel>();
                if (fuel == null || !fuel.inserted)
                    continue;

                GoPointerButton button = collider.GetComponent<GoPointerButton>();
                if (button == null || button.unclickable)
                    continue;

                if (!IsCloserAim(origin, direction, collider.bounds.center, ref bestLateral, ref along))
                    continue;
                best = button;
            }

            return best;
        }

        private static bool IsCloserAim(Vector3 origin, Vector3 direction, Vector3 point, ref float bestLateral, ref float along)
        {
            Vector3 to = point - origin;
            float projected = Vector3.Dot(to, direction);
            if (projected < 0f || projected > MaxDistance)
                return false;

            float lateral = (to - direction * projected).magnitude;
            if (lateral >= bestLateral)
                return false;

            bestLateral = lateral;
            along = projected;
            return true;
        }

        private static ShipItemStove StoveFrom(Component component)
        {
            ShipItemStove stove = component.GetComponent<ShipItemStove>();
            if (stove != null)
                return stove;

            StoveCookTrigger slot = component.GetComponent<StoveCookTrigger>();
            if (slot != null)
                return slot.stove;

            CookableFood food = component.GetComponent<CookableFood>();
            if (food != null && food.isInTrigger())
                return food.GetCurrentCookTrigger().stove;

            stove = component.GetComponentInParent<ShipItemStove>();
            if (stove != null)
                return stove;

            return component.transform.parent != null
                ? component.transform.parent.GetComponent<ShipItemStove>()
                : null;
        }

        private static Collider ResolveCollider(Collider collider)
        {
            if (collider == null)
                return null;
            if (collider.CompareTag("ItemSubcollider") && collider.transform.parent != null)
                return collider.transform.parent.GetComponent<Collider>();
            return collider;
        }

        internal static bool IsStoveItem(Component component)
        {
            CookableFood food = component.GetComponent<CookableFood>();
            if (food != null && food.isInTrigger())
                return true;

            StoveFuel fuel = component.GetComponent<StoveFuel>();
            if (fuel != null && fuel.inserted)
                return true;

            ShipItem item = component.GetComponent<ShipItem>();
            if (item == null)
                return false;
            ItemRigidbody body = item.GetItemRigidbody();
            return body != null && body.inStove;
        }
    }

    [HarmonyPatch(typeof(GoPointer), "DoRaycast")]
    internal static class StoveHoverFlickerPatch
    {
        private static void Postfix(
            GoPointer __instance,
            PickupableItem ___heldItem,
            bool ___debugEditorPointer,
            Ray ___raycastRay,
            ref GoPointerButton ___pointedAtButton,
            ref float ___currentLookDistance)
        {
            if (!FixesConfig.StabilizeStoveItemHover.Value)
                return;

            if (___heldItem != null)
            {
                StoveHoverAim.ClearSticky();
                return;
            }

            GoPointerButton vanilla = ___pointedAtButton;
            if (StoveHoverAim.IsOtherInteractable(vanilla))
            {
                StoveHoverAim.ClearSticky();
                return;
            }

            Ray ray = StoveHoverAim.MakeRay(___debugEditorPointer, ___raycastRay);
            StoveHoverAim.CollectStovesAlongRay(ray);
            GoPointerButton aimed = StoveHoverAim.ClosestAimedItem(ray, out float along);
            GoPointerButton target = aimed;
            if (target == null && StoveHoverAim.StoveCount > 0 && StoveHoverAim.StickyValid())
                target = StoveHoverAim._sticky;

            if (target == null)
            {
                StoveHoverAim.ClearSticky();
                return;
            }

            StoveHoverAim._sticky = target;
            StoveHoverAim._stickyUntil = Time.realtimeSinceStartup + StoveHoverAim.StickySeconds;

            if (vanilla != null && vanilla != target)
                vanilla.ForceUnlook();

            ___pointedAtButton = target;
            target.Look(__instance);
            if (aimed == target)
                ___currentLookDistance = along;
        }
    }
}
