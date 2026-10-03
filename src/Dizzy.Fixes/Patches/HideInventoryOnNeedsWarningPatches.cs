using HarmonyLib;
using UnityEngine;

namespace Dizzy.Fixes
{
    // ItemRigidbody.LateUpdate sizes slot items from PlayerNeedsUI's *root*
    // scale. Hunger/thirst/sleep warnings lerp that root to 1 while the
    // inventory child stays at 0, so hotbar meshes stack on the bars.
    internal static class NeedsUiInventoryPanel
    {
        private const float VisibleScale = 0.01f;

        private static readonly AccessTools.FieldRef<PlayerNeedsUI, Transform> Inventory = GameMembers.Field<PlayerNeedsUI, Transform>("inventory");

        private static bool _loggedMissing;

        internal static bool IsVisible()
        {
            PlayerNeedsUI ui = PlayerNeedsUI.instance;
            if (ui == null)
                return false;

            Transform inventory = Inventory != null ? Inventory(ui) : null;
            if (inventory == null)
            {
                if (!_loggedMissing)
                {
                    Plugin.Log.LogWarning("HideInventoryOnNeedsWarning: PlayerNeedsUI.inventory is missing; leaving vanilla item scale.");
                    _loggedMissing = true;
                }

                return true;
            }

            return inventory.localScale.x > VisibleScale;
        }
    }

    [HarmonyPatch(typeof(ItemRigidbody), nameof(ItemRigidbody.LateUpdate))]
    internal static class HideInventoryOnNeedsWarningPatch
    {
        private static void Postfix(ItemRigidbody __instance, Transform ___currentInventorySlot, ShipItem ___item)
        {
            if (!FixesConfig.HideInventoryOnNeedsWarning.Value)
                return;
            if (___currentInventorySlot == null)
                return;
            if (NeedsUiInventoryPanel.IsVisible())
                return;

            __instance.transform.localScale = Vector3.zero;
            if (___item != null)
                ___item.transform.localScale = Vector3.zero;
        }
    }
}
