using HarmonyLib;
using UnityEngine;

namespace Dizzy.Fixes
{
    // Item physics live on the ItemRigidbody twin. Vanilla angularDrag is
    // mass * 0.1 (often ~0.05-0.1) and only promotes a sleeping body to
    // kinematic when a mesh collider exists. Capsule bottles/mugs/fruit
    // keep rolling on a desk or deck. Do not freeze X/Z (drink tilt, hooks)
    // and do not add colliders on the visual ShipItem.
    internal static class ItemRoll
    {
        private const float AngularDragFloor = 3f;

        internal static bool Enabled()
        {
            return FixesConfig.DampenItemRoll != null
                && FixesConfig.DampenItemRoll.Value;
        }

        internal static void ApplyAngularDrag(Rigidbody body)
        {
            if (!Enabled() || body == null)
                return;

            float vanilla = body.mass * 0.1f;
            body.angularDrag = Mathf.Max(vanilla, AngularDragFloor);
        }

        internal static void MaybeSettleCapsule(
            Rigidbody body,
            ShipItem item,
            CapsuleCollider capsuleCol,
            MeshCollider meshCol,
            float dynamicColTimer,
            bool attached,
            bool inStove,
            Transform currentBox,
            Transform currentInventorySlot)
        {
            if (!Enabled() || body == null || item == null)
                return;
            if (body.isKinematic)
                return;
            if (meshCol != null || capsuleCol == null)
                return;
            if (dynamicColTimer > 0f)
                return;
            if (!item.sold || item.held || item.nailed || attached || inStove)
                return;
            if (currentBox != null || currentInventorySlot != null)
                return;
            if (!body.IsSleeping())
                return;

            body.isKinematic = true;
        }
    }

    [HarmonyPatch(typeof(ItemRigidbody), "Start")]
    internal static class ItemRollStartPatch
    {
        private static void Postfix(Rigidbody ___rigidbody)
        {
            if (!ItemRoll.Enabled())
                return;
            if (___rigidbody == null)
            {
                if (!_logged)
                {
                    Plugin.Log.LogWarning("DampenItemRoll: ItemRigidbody.rigidbody is missing; leaving vanilla drag.");
                    _logged = true;
                }

                return;
            }

            ItemRoll.ApplyAngularDrag(___rigidbody);
        }

        private static bool _logged;
    }

    [HarmonyPatch(typeof(ItemRigidbody), nameof(ItemRigidbody.UpdateMass))]
    internal static class ItemRollUpdateMassPatch
    {
        private static void Postfix(Rigidbody ___rigidbody)
        {
            if (!ItemRoll.Enabled())
                return;
            ItemRoll.ApplyAngularDrag(___rigidbody);
        }
    }

    [HarmonyPatch(typeof(ItemRigidbody), "FixedUpdate")]
    internal static class ItemRollSettlePatch
    {
        private static void Postfix(
            Rigidbody ___rigidbody,
            ShipItem ___item,
            CapsuleCollider ___capsuleCol,
            MeshCollider ___meshCol,
            float ___dynamicColTimer,
            bool ___attached,
            bool ___inStove,
            Transform ___currentBox,
            Transform ___currentInventorySlot)
        {
            if (!ItemRoll.Enabled())
                return;
            if (___rigidbody == null || ___item == null)
            {
                if (!_logged)
                {
                    Plugin.Log.LogWarning("DampenItemRoll: ItemRigidbody settle fields are missing; leaving vanilla capsule roll.");
                    _logged = true;
                }

                return;
            }

            ItemRoll.MaybeSettleCapsule(
                ___rigidbody,
                ___item,
                ___capsuleCol,
                ___meshCol,
                ___dynamicColTimer,
                ___attached,
                ___inStove,
                ___currentBox,
                ___currentInventorySlot);
        }

        private static bool _logged;
    }
}
