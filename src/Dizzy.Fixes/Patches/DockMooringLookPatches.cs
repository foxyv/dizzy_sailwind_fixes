using HarmonyLib;
using UnityEngine;

namespace Dizzy.Fixes
{
    // A moored line sits on the bollard. Vanilla look is a single ray, so
    // dock mesh (and DockPushCol while still aboard) wins and the knot
    // cannot be clicked. The rope collider is often a trigger.
    internal static class DockMooringLook
    {
        internal const float MaxDistance = 1.8f;
        internal const int LayerMask = -604165;
        internal const float NearRadius = 0.45f;
        internal const float BuriedRadius = 0.6f;

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
            if (vanilla.GetComponent<PickupableBoatMooringRope>() != null)
                return true;
            if (vanilla.GetComponent<MooringRopeLengthAdjuster>() != null)
                return true;
            if (vanilla.GetComponent<DockPushCol>() != null)
                return false;
            if (vanilla.GetComponent<GPButtonDockMooring>() != null)
                return false;
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

        internal static PickupableBoatMooringRope Find(
            GoPointerButton vanilla,
            bool debugEditorPointer,
            Ray raycastRay,
            RaycastHit hit)
        {
            if (ShouldKeep(vanilla))
                return null;

            Ray ray = MakeRay(debugEditorPointer, raycastRay);
            PickupableBoatMooringRope best = null;
            float bestDistance = float.MaxValue;

            ConsiderRay(ray, ref best, ref bestDistance);
            ConsiderSphere(ray, ref best, ref bestDistance);
            if (HitIsOnRay(hit, ray))
                ConsiderOverlap(hit.point, ray, ref best, ref bestDistance);

            return best;
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

        private static void ConsiderRay(Ray ray, ref PickupableBoatMooringRope best, ref float bestDistance)
        {
            RaycastHit[] hits;
            int count = LookRay.Cast(ray, true, out hits);
            for (int i = 0; i < count; i++)
                ConsiderHit(hits[i], ray, ref best, ref bestDistance);
        }

        private static void ConsiderSphere(Ray ray, ref PickupableBoatMooringRope best, ref float bestDistance)
        {
            int count = Physics.SphereCastNonAlloc(
                ray,
                NearRadius,
                Hits,
                MaxDistance,
                LayerMask,
                QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
                ConsiderHit(Hits[i], ray, ref best, ref bestDistance);
        }

        private static void ConsiderOverlap(Vector3 point, Ray ray, ref PickupableBoatMooringRope best, ref float bestDistance)
        {
            int count = Physics.OverlapSphereNonAlloc(
                point,
                BuriedRadius,
                Overlaps,
                LayerMask,
                QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
            {
                PickupableBoatMooringRope rope = RopeFrom(Overlaps[i]);
                if (rope == null)
                    continue;
                float distance = Vector3.Distance(ray.origin, rope.transform.position);
                if (distance >= bestDistance || distance > MaxDistance + BuriedRadius)
                    continue;
                bestDistance = distance;
                best = rope;
            }
        }

        private static void ConsiderHit(RaycastHit hit, Ray ray, ref PickupableBoatMooringRope best, ref float bestDistance)
        {
            PickupableBoatMooringRope rope = RopeFrom(hit.collider);
            if (rope == null)
                return;
            float distance = hit.distance;
            if (distance <= 0f)
                distance = Vector3.Distance(ray.origin, rope.transform.position);
            if (distance >= bestDistance || distance > MaxDistance + NearRadius)
                return;
            bestDistance = distance;
            best = rope;
        }

        private static PickupableBoatMooringRope RopeFrom(Collider collider)
        {
            if (collider == null)
                return null;
            if (collider.CompareTag("ItemSubcollider") && collider.transform.parent != null)
                collider = collider.transform.parent.GetComponent<Collider>();
            if (collider == null)
                return null;

            PickupableBoatMooringRope rope = collider.GetComponent<PickupableBoatMooringRope>();
            if (rope == null)
                rope = collider.GetComponentInParent<PickupableBoatMooringRope>();
            if (rope == null || rope.held != null || !rope.IsMoored())
                return null;
            return rope;
        }
    }

    [HarmonyPatch(typeof(GoPointer), "DoRaycast")]
    [HarmonyPriority(Priority.Normal - 30)]
    internal static class DockMooringLookPatch
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
            if (!FixesConfig.PreferMooredDockLineLook.Value)
                return;
            if (!LookRay.VanillaAims(__instance))
                return;
            if (___heldItem != null)
                return;

            PickupableBoatMooringRope rope = DockMooringLook.Find(
                ___pointedAtButton,
                ___debugEditorPointer,
                ___raycastRay,
                ___hit);
            if (rope == null || rope == ___pointedAtButton)
                return;

            if (___pointedAtButton != null)
                ___pointedAtButton.ForceUnlook();

            ___pointedAtButton = rope;
            rope.Look(__instance);
            ___currentLookDistance = Vector3.Distance(__instance.transform.position, rope.transform.position);
        }
    }
}
