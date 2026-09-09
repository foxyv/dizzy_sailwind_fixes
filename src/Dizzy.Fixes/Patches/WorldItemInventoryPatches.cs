using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Dizzy.Fixes
{
    // WorldItemSpawner parents sold pickups to island scenery and only
    // RegisterToSave() when item.held. Inventory does not unparent. Sailing
    // away deactivates the island (item vanishes from the hotbar), then
    // ItemRigidbody range-destroys it. The spawner can then duplicate it.
    // PrepareSaveData NRE on null chart lines leaves SaveLoadManager.busy true.
    //
    // 0.2.9 unparented every sold pickup and skipped DestroyItem for hotbar
    // items. That yanked boat items off the hull (jitter vs ItemRigidbody)
    // and left copies the spawner kept replacing.
    internal static class WorldItemInventory
    {
        private static bool _loggedSpawnerItem;
        private static bool _loggedWorld;
        private static bool _loggedSpawners;

        internal static bool Enabled()
        {
            return FixesConfig.KeepWorldItemsInInventory != null
                && FixesConfig.KeepWorldItemsInInventory.Value;
        }

        internal static void UnparentToWorld(ShipItem item)
        {
            if (item == null)
                return;

            Transform world = GetWorld();
            if (world == null)
                return;
            if (item.transform.parent == world)
                return;

            item.transform.parent = world;
        }

        internal static bool IsOnBoat(ShipItem item)
        {
            if (item == null)
                return false;
            return item.currentActualBoat != null || item.currentWalkCol != null;
        }

        internal static Transform GetWorld()
        {
            if (FloatingOriginManager.instance == null)
            {
                if (!_loggedWorld)
                {
                    Plugin.Log.LogWarning("KeepWorldItemsInInventory: FloatingOriginManager.instance is missing; leaving vanilla parent.");
                    _loggedWorld = true;
                }

                return null;
            }

            return FloatingOriginManager.instance.transform;
        }

        internal static bool IsInInventoryOrCarrier(ShipItem item)
        {
            return TryInventorySlot(item) >= 0;
        }

        internal static int TryInventorySlot(ShipItem item)
        {
            if (item == null)
                return -1;

            try
            {
                return item.GetCurrentInventorySlot();
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("KeepWorldItemsInInventory: GetCurrentInventorySlot failed: " + e.Message);
                return -1;
            }
        }

        internal static void ClearHotbarSlot(ShipItem item)
        {
            if (item == null)
                return;
            if (GPButtonInventorySlot.inventorySlots == null)
                return;

            for (int i = 0; i < GPButtonInventorySlot.inventorySlots.Length; i++)
            {
                GPButtonInventorySlot slot = GPButtonInventorySlot.inventorySlots[i];
                if (slot != null && slot.currentItem == item)
                    slot.currentItem = null;
            }
        }

        internal static void EnsureChartDataLists(SaveablePrefab prefab)
        {
            if (prefab == null)
                return;

            ShipItemFoldable foldable = prefab.GetComponent<ShipItemFoldable>();
            if (foldable == null || !foldable.allowCharting || foldable.mapChart == null)
                return;

            if (foldable.mapChart.chartData == null)
                foldable.mapChart.chartData = new ChartData();
            if (foldable.mapChart.chartData.lines == null)
                foldable.mapChart.chartData.lines = new List<ChartLine>();
            if (foldable.mapChart.chartData.points == null)
                foldable.mapChart.chartData.points = new List<ChartPoint>();
        }

        internal static void PruneDestroyedPrefabs()
        {
            if (SaveLoadManager.instance == null)
                return;

            List<SaveablePrefab> prefabs = SaveLoadManager.instance.GetCurrentPrefabs();
            if (prefabs == null)
                return;

            int removed = prefabs.RemoveAll(p => p == null);
            if (removed > 0)
                Plugin.Log.LogWarning("KeepWorldItemsInInventory: pruned " + removed + " destroyed save prefab(s) before save.");
        }

        internal static void ReleaseFromSpawner(WorldItemSpawner spawner)
        {
            Traverse itemField = Traverse.Create(spawner).Field("item");
            if (!itemField.FieldExists())
            {
                if (!_loggedSpawnerItem)
                {
                    Plugin.Log.LogWarning("KeepWorldItemsInInventory: WorldItemSpawner.item is missing; leaving vanilla pickup.");
                    _loggedSpawnerItem = true;
                }

                return;
            }

            ShipItem item = itemField.GetValue<ShipItem>();
            if (item == null || item.held)
                return;
            if (!IsInInventoryOrCarrier(item))
                return;

            if (ItemSpawners.instance == null || ItemSpawners.instance.cooldowns == null)
            {
                if (!_loggedSpawners)
                {
                    Plugin.Log.LogWarning("KeepWorldItemsInInventory: ItemSpawners.cooldowns is missing; leaving vanilla respawn.");
                    _loggedSpawners = true;
                }

                return;
            }

            int index = spawner.itemSpawnerIndex;
            if (index < 0 || index >= ItemSpawners.instance.cooldowns.Length)
                return;

            if (spawner.respawnTime <= 0f)
                ItemSpawners.instance.cooldowns[index] = -999f;
            else
                ItemSpawners.instance.cooldowns[index] = UnityEngine.Random.Range(
                    spawner.respawnTime * 0.75f,
                    spawner.respawnTime * 1.25f);

            SaveablePrefab saveable = item.GetComponent<SaveablePrefab>();
            if (saveable != null)
                saveable.RegisterToSave();

            ItemRigidbody body = item.GetItemRigidbody();
            if (body != null)
                body.debugForceKinematic = false;

            UnparentToWorld(item);
            itemField.SetValue(null);
        }
    }

    [HarmonyPatch(typeof(ShipItem), nameof(ShipItem.OnPickup))]
    internal static class WorldItemUnparentOnPickupPatch
    {
        private static void Postfix(ShipItem __instance)
        {
            if (!WorldItemInventory.Enabled())
                return;
            if (__instance == null || !__instance.sold)
                return;
            if (WorldItemInventory.IsOnBoat(__instance))
                return;

            WorldItemInventory.UnparentToWorld(__instance);
        }
    }

    [HarmonyPatch(typeof(ShipItem), nameof(ShipItem.OnEnterInventory))]
    internal static class WorldItemUnparentOnInventoryPatch
    {
        private static void Postfix(ShipItem __instance)
        {
            if (!WorldItemInventory.Enabled())
                return;
            if (__instance == null || !__instance.sold)
                return;

            WorldItemInventory.UnparentToWorld(__instance);
        }
    }

    [HarmonyPatch(typeof(WorldItemSpawner), "Update")]
    internal static class WorldItemSpawnerInventoryTakenPatch
    {
        private static void Postfix(WorldItemSpawner __instance)
        {
            if (!WorldItemInventory.Enabled())
                return;
            if (__instance == null || !Application.isPlaying || !GameState.playing)
                return;

            WorldItemInventory.ReleaseFromSpawner(__instance);
        }
    }

    [HarmonyPatch(typeof(ShipItem), nameof(ShipItem.DestroyItem))]
    internal static class WorldItemClearSlotOnDestroyPatch
    {
        private static void Postfix(ShipItem __instance)
        {
            if (!WorldItemInventory.Enabled())
                return;

            WorldItemInventory.ClearHotbarSlot(__instance);
        }
    }

    [HarmonyPatch(typeof(SaveLoadManager), nameof(SaveLoadManager.SaveGame))]
    internal static class WorldItemPruneDestroyedPrefabsPatch
    {
        private static void Prefix()
        {
            if (!WorldItemInventory.Enabled())
                return;

            WorldItemInventory.PruneDestroyedPrefabs();
        }
    }

    [HarmonyPatch(typeof(SaveablePrefab), nameof(SaveablePrefab.PrepareSaveData))]
    internal static class WorldItemChartDataSavePatch
    {
        private static void Prefix(SaveablePrefab __instance)
        {
            if (!WorldItemInventory.Enabled())
                return;

            WorldItemInventory.EnsureChartDataLists(__instance);
        }
    }
}
