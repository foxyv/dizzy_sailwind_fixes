using HarmonyLib;
using UnityEngine;

namespace Dizzy.Fixes
{
    // The look ray hits the lamp-hook collider before the hanging lantern.
    // Occupied hooks (especially hammer-locked / nailed) keep focus, so click
    // never reaches the hangable. Prefer the connected hanging item when
    // hands are empty.
    internal static class OccupiedHookLook
    {
        private const float MaxDistance = 1.8f;
        private const int LayerMask = -604165;
        private static readonly RaycastHit[] Hits = new RaycastHit[32];

        internal static GoPointerButton HangingItemOnLookRay(
            GoPointerButton vanilla,
            bool debugEditorPointer,
            Ray raycastRay)
        {
            Ray ray = debugEditorPointer && Camera.main != null
                ? Camera.main.ScreenPointToRay(Input.mousePosition)
                : raycastRay;

            ShipItemLampHook hook = vanilla != null
                ? vanilla.GetComponent<ShipItemLampHook>()
                : null;
            if (hook == null)
                hook = ClosestHook(ray);
            if (hook == null)
                return null;

            return HangingFrom(hook);
        }

        private static ShipItemLampHook ClosestHook(Ray ray)
        {
            int count = Physics.RaycastNonAlloc(ray, Hits, MaxDistance, LayerMask);
            ShipItemLampHook best = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                Collider collider = Hits[i].collider;
                if (collider == null)
                    continue;
                if (collider.CompareTag("ItemSubcollider") && collider.transform.parent != null)
                    collider = collider.transform.parent.GetComponent<Collider>();
                if (collider == null)
                    continue;

                ShipItemLampHook hook = collider.GetComponent<ShipItemLampHook>();
                if (hook == null)
                    continue;
                if (Hits[i].distance >= bestDistance)
                    continue;

                bestDistance = Hits[i].distance;
                best = hook;
            }

            return best;
        }

        private static GoPointerButton HangingFrom(ShipItemLampHook hook)
        {
            ItemRigidbody twin = hook.GetItemRigidbody();
            if (twin == null)
                return null;

            ConfigurableJoint joint = twin.GetComponent<ConfigurableJoint>();
            if (joint == null || joint.connectedBody == null)
                return null;

            ItemRigidbody hangingTwin = joint.connectedBody.GetComponent<ItemRigidbody>();
            if (hangingTwin == null)
                return null;

            ShipItem hanging = hangingTwin.GetShipItem();
            if (hanging == null)
                return null;

            HangableItem hangable = hanging.GetComponent<HangableItem>();
            if (hangable == null || !hangable.IsHanging())
                return null;
            if (hanging.unclickable)
                return null;

            return hanging;
        }
    }

    [HarmonyPatch(typeof(GoPointer), "DoRaycast")]
    [HarmonyPriority(Priority.Normal - 10)]
    internal static class OccupiedHookHoverPatch
    {
        private static void Postfix(
            GoPointer __instance,
            PickupableItem ___heldItem,
            bool ___debugEditorPointer,
            Ray ___raycastRay,
            ref GoPointerButton ___pointedAtButton,
            ref float ___currentLookDistance)
        {
            if (!FixesConfig.PreferHangingItemOnHook.Value)
                return;
            if (___heldItem != null)
                return;

            GoPointerButton hanging = OccupiedHookLook.HangingItemOnLookRay(
                ___pointedAtButton,
                ___debugEditorPointer,
                ___raycastRay);
            if (hanging == null || hanging == ___pointedAtButton)
                return;

            if (___pointedAtButton != null)
                ___pointedAtButton.ForceUnlook();

            ___pointedAtButton = hanging;
            hanging.Look(__instance);
            ___currentLookDistance = Vector3.Distance(__instance.transform.position, hanging.transform.position);
        }
    }
}
