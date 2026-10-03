using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Dizzy.Fixes
{
    // Shopkeeper.OnTriggerEnter opens BuyItemUI when a held sold item
    // overlaps the NPC trigger. OnTriggerExit closes it. The held item
    // sits ~1.15 m in front of the camera, so looking at a counter
    // (place-preview) or a small look twitch leaves the trigger and the
    // sell parchment vanishes until you re-enter.
    // Vanilla will not open a second merchant while activeItem is set,
    // so walking stall-to-stall kept the first parchment. Stick to the
    // closest shopkeeper in range.
    internal static class MerchantSellUi
    {
        private const float CloseDistance = 6f;
        private const float SwitchBias = 0.35f;

        internal static bool Enabled()
        {
            return FixesConfig.KeepMerchantSellScroll != null
                && FixesConfig.KeepMerchantSellScroll.Value;
        }

        internal static bool CanOfferSell(ShipItem item)
        {
            if (item == null || !item.sold || !item.held)
                return false;

            Good good = item.GetComponent<Good>();
            if (good != null && good.GetMissionIndex() != -1)
                return false;

            return true;
        }

        internal static bool ShouldKeepOpen(ShipItem item)
        {
            if (!Enabled() || !CanOfferSell(item))
                return false;

            BuyItemUI ui = BuyItemUI.instance;
            return ui != null && ui.activeItem == item;
        }

        internal static void Tick(BuyItemUI ui)
        {
            if (!Enabled() || ui == null)
                return;

            Traverse t = Traverse.Create(ui);
            Traverse selling = t.Field("playerIsSelling");
            Traverse keeperField = t.Field("activeShopkeeper");
            if (!selling.FieldExists() || !keeperField.FieldExists())
            {
                WarnOnce();
                return;
            }

            if (!selling.GetValue<bool>())
                return;

            ShipItem item = ui.activeItem;
            if (item == null || !CanOfferSell(item))
            {
                ui.DeactivateUI();
                return;
            }

            Vector3 from = From(item);
            Shopkeeper current = keeperField.GetValue<Shopkeeper>();
            Shopkeeper closest = ClosestKeeper(from);
            if (closest == null)
            {
                ui.DeactivateUI();
                return;
            }

            if (current == closest)
            {
                ui.transform.position = closest.transform.position;
                return;
            }

            if (current != null && current.gameObject.activeInHierarchy)
            {
                float currentDist = Vector3.Distance(from, current.transform.position);
                float closestDist = Vector3.Distance(from, closest.transform.position);
                if (closestDist + SwitchBias >= currentDist)
                {
                    ui.transform.position = current.transform.position;
                    return;
                }
            }

            Attach(ui, t, closest, item);
        }

        internal static void Consider(Shopkeeper candidate, ShipItem item)
        {
            if (!Enabled() || candidate == null || !CanOfferSell(item))
                return;

            BuyItemUI ui = BuyItemUI.instance;
            if (ui == null)
                return;

            Traverse t = Traverse.Create(ui);
            Traverse selling = t.Field("playerIsSelling");
            Traverse keeperField = t.Field("activeShopkeeper");
            if (!selling.FieldExists() || !keeperField.FieldExists())
            {
                WarnOnce();
                return;
            }

            if (ui.activeItem != null && ui.activeItem != item)
                return;

            if (ui.activeItem == null || !ui.menu.activeInHierarchy)
            {
                ui.ActivateUI(item, candidate, candidate.GetLocalPrice(item).ToString(), playerSelling: true);
                return;
            }

            if (!selling.GetValue<bool>())
                return;

            Vector3 from = From(item);
            Shopkeeper current = keeperField.GetValue<Shopkeeper>();
            if (current == candidate)
                return;

            if (current != null && current.gameObject.activeInHierarchy)
            {
                float currentDist = Vector3.Distance(from, current.transform.position);
                float candidateDist = Vector3.Distance(from, candidate.transform.position);
                if (candidateDist + SwitchBias >= currentDist)
                    return;
            }

            Attach(ui, t, candidate, item);
        }

        private static void Attach(BuyItemUI ui, Traverse t, Shopkeeper keeper, ShipItem item)
        {
            t.Field("activeShopkeeper").SetValue(keeper);
            t.Field("playerIsSelling").SetValue(true);
            ui.activeItem = item;
            ui.transform.position = keeper.transform.position;
            if (ui.buyText != null)
                ui.buyText.text = "Sell\n" + item.name + "?";
            if (ui.buttonText != null)
                ui.buttonText.text = "Sell (" + keeper.GetLocalPrice(item) + ")";
        }

        private static Vector3 From(ShipItem item)
        {
            if (Camera.main != null)
                return Camera.main.transform.position;
            if (item != null)
                return item.transform.position;
            return Vector3.zero;
        }

        // Shopkeepers live in island scenes that load and unload. Each one
        // registers as it starts; ones destroyed with their island are
        // pruned here, instead of searching the scene every frame.
        private static readonly List<Shopkeeper> Keepers = new List<Shopkeeper>();

        internal static void Register(Shopkeeper keeper)
        {
            if (keeper != null && !Keepers.Contains(keeper))
                Keepers.Add(keeper);
        }

        private static Shopkeeper ClosestKeeper(Vector3 from)
        {
            Shopkeeper best = null;
            float bestDist = CloseDistance;
            for (int i = Keepers.Count - 1; i >= 0; i--)
            {
                Shopkeeper keeper = Keepers[i];
                if (keeper == null)
                {
                    Keepers.RemoveAt(i);
                    continue;
                }

                if (!keeper.gameObject.activeInHierarchy)
                    continue;

                float dist = Vector3.Distance(from, keeper.transform.position);
                if (dist >= bestDist)
                    continue;

                bestDist = dist;
                best = keeper;
            }

            return best;
        }

        private static bool _loggedMissing;

        private static void WarnOnce()
        {
            if (_loggedMissing || Plugin.Log == null)
                return;
            _loggedMissing = true;
            Plugin.Log.LogWarning("KeepMerchantSellScroll: BuyItemUI fields are missing; leaving vanilla sell parchment.");
        }
    }

    [HarmonyPatch(typeof(Shopkeeper), "Start")]
    internal static class MerchantSellRegisterPatch
    {
        private static void Postfix(Shopkeeper __instance)
        {
            MerchantSellUi.Register(__instance);
        }
    }

    [HarmonyPatch(typeof(Shopkeeper), "OnTriggerEnter")]
    internal static class MerchantSellTriggerEnterPatch
    {
        private static void Postfix(Shopkeeper __instance, Collider other)
        {
            if (other == null)
                return;

            MerchantSellUi.Consider(__instance, other.GetComponent<ShipItem>());
        }
    }

    [HarmonyPatch(typeof(Shopkeeper), "OnTriggerExit")]
    internal static class MerchantSellTriggerExitPatch
    {
        private static bool Prefix(Collider other)
        {
            if (other == null)
                return true;

            ShipItem item = other.GetComponent<ShipItem>();
            return !MerchantSellUi.ShouldKeepOpen(item);
        }
    }

    [HarmonyPatch(typeof(BuyItemUI), "Update")]
    internal static class MerchantSellDistancePatch
    {
        private static void Postfix(BuyItemUI __instance)
        {
            if (__instance == null || __instance.menu == null || !__instance.menu.activeInHierarchy)
                return;

            MerchantSellUi.Tick(__instance);
        }
    }
}
