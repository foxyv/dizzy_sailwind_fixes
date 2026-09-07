using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Dizzy.Fixes
{
    // Vanilla look is a single Physics.Raycast. A dropped or set anchor sits
    // in/under terrain, so dirt wins and click never reaches the capsule.
    // Manual place also skips OnCollisionEnter (held = trigger, then solid
    // already overlapping land), so the hook never "sets" and stays tiny.
    internal static class DroppedAnchorLook
    {
        internal const float MaxDistance = 1.8f;
        internal const int LayerMask = -604165;
        internal const float NearRadius = 0.4f;
        internal const float BuriedRadius = 0.55f;

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
            if (vanilla.GetComponent<PickupableItem>() != null)
                return true;
            if (vanilla.GetComponent<GPButtonRopeWinch>() != null)
                return true;
            if (vanilla.GetComponent<ShipItemStove>() != null)
                return true;
            if (vanilla.GetComponent<ShipItemLampHook>() != null)
                return true;
            return false;
        }

        internal static Anchor Find(
            GoPointerButton vanilla,
            bool debugEditorPointer,
            Ray raycastRay,
            RaycastHit hit)
        {
            if (ShouldKeep(vanilla))
                return null;

            Ray ray = MakeRay(debugEditorPointer, raycastRay);
            Anchor best = null;
            float bestDistance = float.MaxValue;

            ConsiderRay(ray, ref best, ref bestDistance);
            ConsiderSphere(ray, ref best, ref bestDistance);
            if (HitIsOnRay(hit, ray))
                ConsiderOverlap(hit.point, ray, ref best, ref bestDistance);

            return best;
        }

        internal static void RecoverGroundContacts(
            CapsuleCollider col,
            List<Collider> groundedCols,
            float ropeLimit)
        {
            if (col == null || groundedCols == null)
                return;
            if (ropeLimit <= 1f)
                return;

            Vector3 center = col.bounds.center;
            float radius = Mathf.Max(col.bounds.extents.magnitude, 0.25f);
            int count = Physics.OverlapSphereNonAlloc(
                center,
                radius,
                Overlaps,
                -1,
                QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider other = Overlaps[i];
                if (other == null || other == col)
                    continue;
                if (!IsGround(other))
                    continue;
                if (!groundedCols.Contains(other))
                    groundedCols.Add(other);
            }
        }

        private static bool IsGround(Collider collider)
        {
            return collider.CompareTag("Terrain")
                || collider.gameObject.layer == 14
                || collider.CompareTag("OceanBottom");
        }

        private static bool HitIsOnRay(RaycastHit hit, Ray ray)
        {
            if (hit.collider == null)
                return false;
            if (hit.distance < 0f || hit.distance > MaxDistance)
                return false;

            Vector3 toHit = hit.point - ray.origin;
            if (toHit.sqrMagnitude < 0.0001f)
                return true;
            return Vector3.Dot(toHit.normalized, ray.direction) > 0.9f;
        }

        private static void ConsiderRay(Ray ray, ref Anchor best, ref float bestDistance)
        {
            int count = Physics.RaycastNonAlloc(ray, Hits, MaxDistance, LayerMask);
            for (int i = 0; i < count; i++)
                ConsiderHit(Hits[i], ray, ref best, ref bestDistance);
        }

        private static void ConsiderSphere(Ray ray, ref Anchor best, ref float bestDistance)
        {
            int count = Physics.SphereCastNonAlloc(
                ray,
                NearRadius,
                Hits,
                MaxDistance,
                LayerMask,
                QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
                ConsiderHit(Hits[i], ray, ref best, ref bestDistance);
        }

        private static void ConsiderOverlap(Vector3 point, Ray ray, ref Anchor best, ref float bestDistance)
        {
            int count = Physics.OverlapSphereNonAlloc(
                point,
                BuriedRadius,
                Overlaps,
                LayerMask,
                QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Anchor anchor = AnchorFrom(Overlaps[i]);
                if (anchor == null)
                    continue;
                float distance = Vector3.Distance(ray.origin, anchor.transform.position);
                if (distance >= bestDistance || distance > MaxDistance + BuriedRadius)
                    continue;
                bestDistance = distance;
                best = anchor;
            }
        }

        private static void ConsiderHit(RaycastHit hit, Ray ray, ref Anchor best, ref float bestDistance)
        {
            Anchor anchor = AnchorFrom(hit.collider);
            if (anchor == null)
                return;
            float distance = hit.distance;
            if (distance <= 0f)
                distance = Vector3.Distance(ray.origin, anchor.transform.position);
            if (distance >= bestDistance || distance > MaxDistance + NearRadius)
                return;
            bestDistance = distance;
            best = anchor;
        }

        private static Anchor AnchorFrom(Collider collider)
        {
            if (collider == null)
                return null;
            if (collider.CompareTag("ItemSubcollider") && collider.transform.parent != null)
                collider = collider.transform.parent.GetComponent<Collider>();
            if (collider == null)
                return null;

            Anchor anchor = collider.GetComponent<Anchor>();
            if (anchor == null)
                anchor = collider.GetComponentInParent<Anchor>();
            if (anchor == null || anchor.held != null)
                return null;
            return anchor;
        }
    }

    [HarmonyPatch(typeof(GoPointer), "DoRaycast")]
    [HarmonyAfter("Dizzy.Fixes.StoveHoverFlickerPatch", "Dizzy.Fixes.OccupiedHookHoverPatch")]
    internal static class DroppedAnchorLookPatch
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
            if (!FixesConfig.PreferDroppedAnchorLook.Value)
                return;
            if (___heldItem != null)
                return;

            Anchor anchor = DroppedAnchorLook.Find(
                ___pointedAtButton,
                ___debugEditorPointer,
                ___raycastRay,
                ___hit);
            if (anchor == null || anchor == ___pointedAtButton)
                return;

            if (___pointedAtButton != null)
                ___pointedAtButton.ForceUnlook();

            ___pointedAtButton = anchor;
            anchor.Look(__instance);
            ___currentLookDistance = Vector3.Distance(__instance.transform.position, anchor.transform.position);
        }
    }

    [HarmonyPatch(typeof(Anchor), "ExtraFixedUpdate")]
    internal static class DroppedAnchorGroundContactPatch
    {
        private static void Postfix(
            Anchor __instance,
            CapsuleCollider ___col,
            float ___initialRadius,
            bool ___set,
            ConfigurableJoint ___joint,
            List<Collider> ___groundedCols)
        {
            if (!FixesConfig.PreferDroppedAnchorLook.Value)
                return;
            if (__instance.held != null)
                return;
            if (___col == null || ___joint == null)
                return;

            DroppedAnchorLook.RecoverGroundContacts(___col, ___groundedCols, ___joint.linearLimit.limit);

            if (___set || (___groundedCols != null && ___groundedCols.Count > 0))
                ___col.radius = ___initialRadius * 5f;
        }
    }
}
