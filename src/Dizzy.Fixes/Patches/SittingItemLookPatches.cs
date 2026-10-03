using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

namespace Dizzy.Fixes
{
    // Vanilla look is one Physics.Raycast that keeps the first collider.
    // Several small items have a collider smaller than their model (mug
    // handle, quadrant arc, compass and chronometer rims, kettle spout),
    // and the pipe carries a mouth trigger with no button, so aiming at
    // those parts selects the desk behind them or nothing at all.
    //
    // Only when vanilla found nothing usable (no hit, a collider without a
    // button, or a surface such as a crate, barrel, or shelf) pick the
    // small item whose rendered model the ray enters first, in front of
    // the vanilla hit. Items behind walls, decks, or crate lids start past
    // that hit and are ignored. Candidates follow vanilla's click rules:
    // nailed items stay unclickable with empty hands, and a held item only
    // targets what it can be used on.
    internal static class SittingItemLook
    {
        internal const float MaxDistance = 1.8f;
        internal const int LayerMask = -604165;

        private const int DroppedItemLayerMask = 1 << 0;
        private const float SearchRadius = 0.25f;
        private const float MaxPartHalfSize = 0.45f;
        private const float SurfaceTolerance = 0.01f;

        private static readonly Collider[] Overlaps = new Collider[64];
        private static readonly ConditionalWeakTable<ShipItem, ModelPart[]> Models = new ConditionalWeakTable<ShipItem, ModelPart[]>();
        private static readonly Dictionary<int, ShipItem> LastPicks = new Dictionary<int, ShipItem>();

        private sealed class ModelPart
        {
            internal Renderer Renderer;
            internal MeshFilter Filter;
        }

        internal static bool Enabled()
        {
            return FixesConfig.PreferSittingItemLook != null
                && FixesConfig.PreferSittingItemLook.Value;
        }

        internal static Ray MakeRay(bool debugEditorPointer, Ray raycastRay)
        {
            Ray ray = debugEditorPointer && Camera.main != null
                ? Camera.main.ScreenPointToRay(Input.mousePosition)
                : raycastRay;
            return new Ray(ray.origin, ray.direction.normalized);
        }

        internal static ShipItem TakeLastPick(GoPointer pointer)
        {
            int id = pointer.GetInstanceID();
            ShipItem last;
            if (!LastPicks.TryGetValue(id, out last))
                return null;
            LastPicks.Remove(id);
            return last;
        }

        internal static void RememberPick(GoPointer pointer, ShipItem item)
        {
            LastPicks[pointer.GetInstanceID()] = item;
        }

        internal static GoPointerButton ButtonFrom(RaycastHit hit)
        {
            return ButtonFrom(hit.collider);
        }

        internal static GoPointerButton ButtonFrom(Collider collider)
        {
            if (collider == null)
                return null;
            if (collider.CompareTag("ItemSubcollider") && collider.transform.parent != null)
                collider = collider.transform.parent.GetComponent<Collider>();
            return collider != null ? collider.GetComponent<GoPointerButton>() : null;
        }

        internal static ShipItem Find(
            GoPointer pointer,
            PickupableItem held,
            bool debugEditorPointer,
            Ray raycastRay,
            RaycastHit hit,
            GoPointerButton pointed,
            out float distance)
        {
            distance = 0f;
            if (!VanillaLooks(pointer))
                return null;
            if (BigCrateCarry.Enabled() && BigCrateCarry.IsDropOnlyCarry(held))
                return null;
            if (pointed != null && (pointed != ButtonFrom(hit.collider) || !pointed.allowPlacingItems))
                return null;

            if (pointed == null)
            {
                ShipItem owner = OwnerOfChildCollider(hit.collider);
                if (IsCandidate(owner, held))
                {
                    distance = hit.distance;
                    return owner;
                }
            }

            float hitDistance = hit.collider != null ? hit.distance : MaxDistance;
            float limit = pointed == null
                ? hitDistance + SurfaceTolerance
                : hitDistance - SurfaceTolerance;
            return NearestVisible(
                MakeRay(debugEditorPointer, raycastRay),
                Mathf.Min(limit, MaxDistance),
                held,
                out distance);
        }

        private static bool VanillaLooks(GoPointer pointer)
        {
            if (pointer.type == GoPointer.PointerType.crosshairMouse && GameState.inCursorMenu)
                return false;
            return !GameState.sleeping && !GameState.inBed && !BoatCamera.on;
        }

        private static ShipItem OwnerOfChildCollider(Collider collider)
        {
            if (collider == null || collider.GetComponent<GoPointerButton>() != null)
                return null;
            Transform parent = collider.transform.parent;
            return parent != null ? parent.GetComponentInParent<ShipItem>() : null;
        }

        private static bool IsCandidate(ShipItem item, PickupableItem held)
        {
            if (item == null || item == held || item.unclickable || item.held != null)
                return false;
            if (item.big || item.allowPlacingItems || !item.gameObject.activeInHierarchy)
                return false;

            ItemRigidbody body = item.GetItemRigidbody();
            if (body != null && body.inStove)
                return false;

            if (held != null)
                return held.AllowOnItemClick(item);
            return !item.nailed || IsNailedClickable(item);
        }

        private static bool IsNailedClickable(ShipItem item)
        {
            System.Type type = item.GetType();
            return type == typeof(ShipItemCrate)
                || type == typeof(ShipItemBottle)
                || type == typeof(ShipItemBed);
        }

        private static ShipItem NearestVisible(Ray ray, float limit, PickupableItem held, out float distance)
        {
            distance = 0f;
            if (limit <= 0f)
                return null;

            int count = Physics.OverlapCapsuleNonAlloc(
                ray.origin,
                ray.origin + ray.direction * limit,
                SearchRadius,
                Overlaps,
                DroppedItemLayerMask,
                QueryTriggerInteraction.Collide);

            ShipItem best = null;
            float bestEnter = limit;
            for (int i = 0; i < count; i++)
            {
                ShipItem item = Overlaps[i] != null ? Overlaps[i].GetComponent<ShipItem>() : null;
                if (item == null || item == best || !IsCandidate(item, held))
                    continue;

                float enter;
                if (!RayEntersModel(item, ray, out enter) || enter > bestEnter)
                    continue;

                bestEnter = enter;
                best = item;
            }

            if (best != null)
                distance = bestEnter;
            return best;
        }

        private static bool RayEntersModel(ShipItem item, Ray ray, out float enter)
        {
            enter = float.MaxValue;
            bool entered = false;
            ModelPart[] parts = Models.GetValue(item, CollectModel);
            Transform root = item.transform;
            for (int i = 0; i < parts.Length; i++)
            {
                Renderer renderer = parts[i].Renderer;
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                    continue;
                if (!renderer.transform.IsChildOf(root))
                    continue;

                Bounds local;
                if (!TryLocalBounds(parts[i], out local))
                    continue;

                Transform transform = renderer.transform;
                Vector3 half = Vector3.Scale(local.extents, transform.lossyScale);
                if (Mathf.Abs(half.x) > MaxPartHalfSize
                    || Mathf.Abs(half.y) > MaxPartHalfSize
                    || Mathf.Abs(half.z) > MaxPartHalfSize)
                    return false;

                Matrix4x4 toLocal = transform.worldToLocalMatrix;
                float partEnter;
                if (!RayEntersBox(toLocal.MultiplyPoint3x4(ray.origin), toLocal.MultiplyVector(ray.direction), local, out partEnter))
                    continue;

                if (partEnter < enter)
                {
                    enter = partEnter;
                    entered = true;
                }
            }

            return entered;
        }

        private static ModelPart[] CollectModel(ShipItem item)
        {
            Renderer[] renderers = item.GetComponentsInChildren<Renderer>(true);
            List<ModelPart> parts = new List<ModelPart>(renderers.Length);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer is SkinnedMeshRenderer)
                {
                    parts.Add(new ModelPart { Renderer = renderer });
                    continue;
                }

                if (!(renderer is MeshRenderer))
                    continue;

                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                if (filter != null)
                    parts.Add(new ModelPart { Renderer = renderer, Filter = filter });
            }

            return parts.ToArray();
        }

        private static bool TryLocalBounds(ModelPart part, out Bounds bounds)
        {
            SkinnedMeshRenderer skinned = part.Renderer as SkinnedMeshRenderer;
            if (skinned != null)
            {
                bounds = skinned.localBounds;
                return true;
            }

            Mesh mesh = part.Filter != null ? part.Filter.sharedMesh : null;
            bounds = mesh != null ? mesh.bounds : default(Bounds);
            return mesh != null;
        }

        // The ray is in the box's local space, so the parameter along it
        // stays in world units.
        private static bool RayEntersBox(Vector3 origin, Vector3 direction, Bounds box, out float enter)
        {
            enter = 0f;
            float exit = float.MaxValue;
            Vector3 min = box.min;
            Vector3 max = box.max;
            for (int axis = 0; axis < 3; axis++)
            {
                float from = origin[axis];
                float step = direction[axis];
                if (Mathf.Abs(step) < 1e-8f)
                {
                    if (from < min[axis] || from > max[axis])
                        return false;
                    continue;
                }

                float near = (min[axis] - from) / step;
                float far = (max[axis] - from) / step;
                if (near > far)
                {
                    float swap = near;
                    near = far;
                    far = swap;
                }

                if (near > enter)
                    enter = near;
                if (far < exit)
                    exit = far;
                if (enter > exit)
                    return false;
            }

            return true;
        }
    }

    // Low priority so the stove, hook, anchor, and mooring look fixes decide
    // first. A target they set no longer matches the vanilla hit and is left
    // alone.
    [HarmonyPatch(typeof(GoPointer), "DoRaycast")]
    [HarmonyPriority(Priority.Low)]
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
            ShipItem lastPick = SittingItemLook.TakeLastPick(__instance);
            if (!SittingItemLook.Enabled())
                return;

            // With an item in hand, vanilla keeps the previous target when the
            // ray lands on an item it cannot use. Never let a pick from this
            // fix linger that way: treat it as nothing and look again.
            GoPointerButton pointed = ___pointedAtButton;
            bool stalePick = pointed != null
                && pointed == lastPick
                && pointed != SittingItemLook.ButtonFrom(___hit);
            if (stalePick)
                pointed = null;

            float distance;
            ShipItem item = SittingItemLook.Find(
                __instance,
                ___heldItem,
                ___debugEditorPointer,
                ___raycastRay,
                ___hit,
                pointed,
                out distance);
            if (item == null)
            {
                if (stalePick)
                {
                    ___pointedAtButton.ForceUnlook();
                    ___pointedAtButton = null;
                    ___currentLookDistance = 0f;
                }

                return;
            }

            if (___pointedAtButton != null && ___pointedAtButton != item)
                ___pointedAtButton.ForceUnlook();

            ___pointedAtButton = item;
            item.Look(__instance);
            ___currentLookDistance = distance;
            SittingItemLook.RememberPick(__instance, item);
        }
    }
}
