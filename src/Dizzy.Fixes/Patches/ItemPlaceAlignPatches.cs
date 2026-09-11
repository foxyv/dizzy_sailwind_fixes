using HarmonyLib;
using UnityEngine;

namespace Dizzy.Fixes
{
    // Vanilla place-preview copies pointer rotation and lifts along
    // world Vector3.up. That leaves pipes and quadrants at an angle on
    // a walk mesh that is not world-aligned, and sits a quadrant on
    // edge (thin on local X). Align those two to the raycast hit.
    // Other items keep vanilla pose so they do not glue to crate sides.
    // The look ray runs in FixedUpdate, so world hit.point is stale by
    // LateUpdate while the boat interpolates. Store the hit in the
    // collider's local space and transform it back each preview tick.
    // ShipItemPipe.ExtraLateUpdate zeros world X/Z while held, which
    // fights this pose. Re-apply after that, without smoking pitch.
    // Do not keep a stale table hit — that made the ghost sticky.
    // Held pipe/quadrant children stay on the ignore-raycast layer so
    // a tilted mesh does not eat the look ray.
    internal static class ItemPlaceAlign
    {
        private const float MinFwdSqr = 0.0001f;
        internal const int IgnoreRaycastLayer = 2;
        internal const int DefaultLayer = 0;

        private static Transform _surface;
        private static Vector3 _localPoint;
        private static Vector3 _localNormal;
        private static bool _hasSurface;
        private static Vector3 _lastFwd;
        private static int _lastHeldId;
        private static int _pipePlaceId;
        private static float _pipeYaw;
        private static bool _pipeFlipped;

        internal static bool Enabled()
        {
            return FixesConfig.AlignPlacedItemToSurface != null
                && FixesConfig.AlignPlacedItemToSurface.Value;
        }

        internal static bool UsesSurfaceAlign(PickupableItem heldItem)
        {
            return heldItem is ShipItemPipe || heldItem is ShipItemQuadrant;
        }

        internal static void Capture(RaycastHit hit, GoPointerButton pointedAtButton, PickupableItem heldItem)
        {
            if (!Enabled()
                || !UsesSurfaceAlign(heldItem)
                || pointedAtButton == null
                || !pointedAtButton.allowPlacingItems
                || hit.collider == null)
            {
                _hasSurface = false;
                _surface = null;
                return;
            }

            _surface = hit.collider.transform;
            _localPoint = _surface.InverseTransformPoint(hit.point);
            _localNormal = _surface.InverseTransformDirection(hit.normal);
            _hasSurface = true;
        }

        internal static void SetHeldLayers(PickupableItem item, int layer)
        {
            if (item == null)
                return;

            Transform[] transforms = item.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i] != null)
                    transforms[i].gameObject.layer = layer;
            }
        }

        internal static void Apply(
            PickupableItem heldItem,
            GoPointerButton pointedAtButton,
            RaycastHit hit)
        {
            if (!Enabled() || !UsesSurfaceAlign(heldItem) || heldItem.big)
                return;
            if (pointedAtButton == null || !pointedAtButton.allowPlacingItems)
                return;
            if (hit.collider == null)
                return;

            ShipItem shipItem = heldItem as ShipItem;
            if (shipItem != null && shipItem.wallAttachment)
                return;

            Vector3 point;
            Vector3 up;
            if (_hasSurface && _surface != null && hit.collider.transform == _surface)
            {
                point = _surface.TransformPoint(_localPoint);
                up = _surface.TransformDirection(_localNormal);
            }
            else
            {
                point = hit.point;
                up = hit.normal;
            }

            if (up.sqrMagnitude < MinFwdSqr)
                return;

            up.Normalize();
            Transform pointer = heldItem.held != null ? heldItem.held.transform : null;
            if (pointer != null && Vector3.Dot(up, pointer.position - point) < 0f)
                up = -up;

            heldItem.transform.position = point + up * heldItem.furniturePlaceHeight;

            Vector3 yawRef = pointer != null ? pointer.up : Vector3.forward;
            Vector3 tangent = Vector3.ProjectOnPlane(yawRef, up);
            if (tangent.sqrMagnitude < MinFwdSqr)
                tangent = Vector3.ProjectOnPlane(Vector3.forward, up);
            if (tangent.sqrMagnitude < MinFwdSqr)
                return;
            tangent.Normalize();

            if (heldItem is ShipItemQuadrant)
            {
                Vector3 fwd = Vector3.Cross(up, tangent);
                if (fwd.sqrMagnitude < MinFwdSqr)
                    return;
                heldItem.transform.rotation = Quaternion.LookRotation(fwd.normalized, tangent);
            }
            else
            {
                Vector3 fwd = Vector3.zero;
                if (pointer != null)
                    fwd = Vector3.Cross(pointer.right, up);
                if (fwd.sqrMagnitude < MinFwdSqr)
                    fwd = Vector3.ProjectOnPlane(pointer != null ? pointer.forward : Vector3.forward, up);
                if (fwd.sqrMagnitude < MinFwdSqr)
                    fwd = Vector3.ProjectOnPlane(Vector3.forward, up);
                if (fwd.sqrMagnitude < MinFwdSqr)
                    return;

                fwd.Normalize();
                int heldId = heldItem.GetInstanceID();
                if (heldId == _lastHeldId && _lastFwd.sqrMagnitude > MinFwdSqr && Vector3.Dot(fwd, _lastFwd) < 0f)
                    fwd = -fwd;
                _lastHeldId = heldId;
                _lastFwd = fwd;

                heldItem.transform.rotation = Quaternion.LookRotation(fwd, up);

                EnsurePipePlace(heldItem);
                heldItem.transform.Rotate(up, _pipeYaw, Space.World);
                if (_pipeFlipped)
                    heldItem.transform.Rotate(Vector3.forward, 180f, Space.Self);
            }

            heldItem.forceDisableRedOutline = true;
        }

        internal static bool IsPlacing(PickupableItem item)
        {
            if (item == null || item.held == null)
                return false;

            Traverse pointed = Traverse.Create(item.held).Field("pointedAtButton");
            if (!pointed.FieldExists())
                return false;

            GoPointerButton button = pointed.GetValue<GoPointerButton>();
            return button != null && button.allowPlacingItems;
        }

        internal static void ResetPipePlace(PickupableItem item)
        {
            _pipePlaceId = item != null ? item.GetInstanceID() : 0;
            _pipeYaw = 0f;
            _pipeFlipped = false;
        }

        internal static void AddPipeYaw(PickupableItem item, float input)
        {
            if (item == null)
                return;
            EnsurePipePlace(item);
            _pipeYaw += input * 3f;
        }

        internal static void PollPipeFlip(PickupableItem item)
        {
            if (!(item is ShipItemPipe) || !IsPlacing(item))
                return;
            if (!Input.GetKeyDown(KeyCode.Q))
                return;

            EnsurePipePlace(item);
            _pipeFlipped = !_pipeFlipped;
        }

        private static void EnsurePipePlace(PickupableItem item)
        {
            int id = item.GetInstanceID();
            if (id == _pipePlaceId)
                return;
            _pipePlaceId = id;
            _pipeYaw = 0f;
            _pipeFlipped = false;
        }

        internal static void ApplyFromPointer(GoPointer pointer)
        {
            if (!Enabled() || pointer == null)
                return;

            PickupableItem held = pointer.GetHeldItem();
            if (held == null)
                return;

            Traverse t = Traverse.Create(pointer);
            Traverse pointed = t.Field("pointedAtButton");
            Traverse hitField = t.Field("hit");
            if (!pointed.FieldExists() || !hitField.FieldExists())
                return;

            Apply(held, pointed.GetValue<GoPointerButton>(), hitField.GetValue<RaycastHit>());
        }
    }

    [HarmonyPatch(typeof(GoPointer), "FixedUpdate")]
    internal static class ItemPlaceAlignCapturePatch
    {
        private static void Postfix(
            PickupableItem ___heldItem,
            GoPointerButton ___pointedAtButton,
            RaycastHit ___hit)
        {
            ItemPlaceAlign.Capture(___hit, ___pointedAtButton, ___heldItem);
        }
    }

    [HarmonyPatch(typeof(GoPointer), "LateUpdate")]
    internal static class ItemPlaceAlignPreviewPatch
    {
        private static void Postfix(
            PickupableItem ___heldItem,
            GoPointerButton ___pointedAtButton,
            RaycastHit ___hit)
        {
            ItemPlaceAlign.PollPipeFlip(___heldItem);
            ItemPlaceAlign.Apply(___heldItem, ___pointedAtButton, ___hit);
        }
    }

    [HarmonyPatch(typeof(ShipItem), nameof(ShipItem.OnDrop))]
    internal static class ItemPlaceAlignDropPatch
    {
        private static void Prefix(ShipItem __instance)
        {
            if (__instance == null)
                return;
            ItemPlaceAlign.ApplyFromPointer(__instance.held);
        }
    }

    [HarmonyPatch(typeof(ShipItemPipe), nameof(ShipItemPipe.ExtraLateUpdate))]
    internal static class ItemPlaceAlignPipePatch
    {
        private static void Postfix(ShipItemPipe __instance)
        {
            if (__instance == null || __instance.held == null)
                return;
            ItemPlaceAlign.ApplyFromPointer(__instance.held);
        }
    }

    [HarmonyPatch(typeof(GoPointer), nameof(GoPointer.PickUpItem))]
    internal static class ItemPlaceAlignPickupLayerPatch
    {
        private static void Postfix(PickupableItem item)
        {
            if (!ItemPlaceAlign.Enabled() || !ItemPlaceAlign.UsesSurfaceAlign(item))
                return;
            ItemPlaceAlign.SetHeldLayers(item, ItemPlaceAlign.IgnoreRaycastLayer);
            if (item is ShipItemPipe)
                ItemPlaceAlign.ResetPipePlace(item);
        }
    }

    [HarmonyPatch(typeof(GoPointer), nameof(GoPointer.DropItem))]
    internal static class ItemPlaceAlignDropLayerPatch
    {
        private static void Prefix(PickupableItem ___heldItem)
        {
            if (!ItemPlaceAlign.Enabled() || !ItemPlaceAlign.UsesSurfaceAlign(___heldItem))
                return;
            ItemPlaceAlign.SetHeldLayers(___heldItem, ItemPlaceAlign.DefaultLayer);
        }
    }

    [HarmonyPatch(typeof(PickupableItem), nameof(PickupableItem.OnScroll))]
    internal static class ItemPlaceAlignPipeScrollPatch
    {
        private static bool Prefix(PickupableItem __instance, float input)
        {
            if (!ItemPlaceAlign.Enabled() || !(__instance is ShipItemPipe))
                return true;
            if (!ItemPlaceAlign.IsPlacing(__instance))
                return true;

            ItemPlaceAlign.AddPipeYaw(__instance, input);
            return false;
        }
    }
}
