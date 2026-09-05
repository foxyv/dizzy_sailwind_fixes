using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Dizzy.Fixes
{
    // Vanilla EconomyUI.SellGood() bumps IslandMarket supply (price drop) and
    // pays gold *before* IslandMarketWarehouseArea.SellGood() tries to destroy
    // a crate. That warehouse call can return false ("Failed to sell - crate/
    // barrel not full") after a long session when the trigger list is stale,
    // so the good stays in the yard but the price already moved.
    [HarmonyPatch(typeof(EconomyUI), nameof(EconomyUI.SellGood))]
    internal static class EconomyUiSellGoodPatch
    {
        private static bool Prefix(EconomyUI __instance)
        {
            if (!FixesConfig.PreventFailedTradeBookSale.Value)
                return true;

            var ui = Traverse.Create(__instance);
            var island = ui.Field("currentIsland").GetValue<IslandMarket>();
            var currency = ui.Field("currentPlayerCurrency").GetValue<Currency>();
            if (island == null)
            {
                Plugin.Log.LogWarning("PreventFailedTradeBookSale: currentIsland missing, using vanilla sell.");
                return true;
            }

            int goodIndex = __instance.currentSelectedGood;
            if (island.currentPlayerGoods == null
                || goodIndex < 0
                || goodIndex >= island.currentPlayerGoods.Length
                || island.currentPlayerGoods[goodIndex] <= 0)
            {
                return false;
            }

            if (currency != (Currency)island.GetPortRegion() && !island.allowCurrencyConversion)
                return false;

            IslandMarketWarehouseArea warehouse = island.GetWarehouseArea();
            if (warehouse == null)
            {
                Plugin.Log.LogWarning("PreventFailedTradeBookSale: warehouse missing, using vanilla sell.");
                return true;
            }

            if (!warehouse.SellGood(goodIndex))
                return false;

            int sellPrice = Traverse.Create(__instance)
                .Method("GetSellPrice", island.GetPortIndex(), goodIndex)
                .GetValue<int>();
            island.SellGood(goodIndex);
            PlayerGold.currency[(int)currency] += sellPrice;

            int itemIndex = PrefabsDirectory.GoodToItemIndex(goodIndex);
            GameObject prefab = PrefabsDirectory.instance.directory[itemIndex];
            DayLogs.instance.dayLogs[(int)currency].LogTransaction(sellPrice, prefab.GetComponent<ShipItem>());
            EconomyUIReceiptScribe.instance.AddTransaction(goodIndex, -1, sellPrice, (int)currency);
            UISoundPlayer.instance.PlayGoldSound();
            __instance.RefreshPage();
            return false;
        }
    }

    [HarmonyPatch(typeof(IslandMarketWarehouseArea), nameof(IslandMarketWarehouseArea.ValidateList))]
    internal static class WarehouseValidateListPatch
    {
        private static bool Prefix(IslandMarketWarehouseArea __instance)
        {
            if (!FixesConfig.PreventFailedTradeBookSale.Value)
                return true;

            WarehouseSync.Validate(__instance);
            return false;
        }
    }

    internal static class WarehouseSync
    {
        internal static void Validate(IslandMarketWarehouseArea area)
        {
            var traverse = Traverse.Create(area);
            var goods = traverse.Field("goodsInArea").GetValue<List<Good>>();
            var market = traverse.Field("market").GetValue<IslandMarket>();
            if (goods == null || market == null || market.currentPlayerGoods == null)
                return;

            goods.RemoveAll(g => g == null);

            for (int i = goods.Count - 1; i >= 0; i--)
            {
                if (!IsGoodValid(area, goods[i]))
                    goods.RemoveAt(i);
            }

            Collider col = area.GetComponent<Collider>();
            if (col != null && col.enabled)
            {
                Collider[] hits = Physics.OverlapBox(
                    col.bounds.center,
                    col.bounds.extents,
                    Quaternion.identity,
                    ~0,
                    QueryTriggerInteraction.Collide);

                foreach (Collider hit in hits)
                {
                    if (hit == null)
                        continue;

                    Good good = hit.GetComponent<Good>();
                    if (good == null || goods.Contains(good))
                        continue;

                    ShipItem item = good.GetComponent<ShipItem>();
                    if (item == null || !item.sold || good.GetMissionIndex() != -1)
                        continue;
                    if (!IsGoodValid(area, good))
                        continue;

                    goods.Add(good);
                }
            }

            for (int i = 0; i < market.currentPlayerGoods.Length; i++)
                market.currentPlayerGoods[i] = 0;

            foreach (Good good in goods)
            {
                int index = GetGoodIndex(area, good);
                if (index >= 0 && index < market.currentPlayerGoods.Length)
                    market.currentPlayerGoods[index]++;
            }
        }

        private static bool IsGoodValid(IslandMarketWarehouseArea area, Good good)
        {
            return Traverse.Create(area).Method("IsGoodValid", good).GetValue<bool>();
        }

        private static int GetGoodIndex(IslandMarketWarehouseArea area, Good good)
        {
            return Traverse.Create(area).Method("GetGoodIndex", good).GetValue<int>();
        }
    }
}
