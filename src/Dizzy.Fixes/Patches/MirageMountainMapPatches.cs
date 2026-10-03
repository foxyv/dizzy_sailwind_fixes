using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Dizzy.Fixes
{
    // Prefab 165 ("map mirage mountain") is a WorldItemSpawner chart. Vanilla
    // parents it to island scenery and only RegisterToSave() when item.held.
    // Inventory does not unparent. Leaving the island deactivates scenery
    // (the map vanishes), then ItemRigidbody range-destroys sold items with
    // no walk collider past 600 m. The spawner sees a dead item and clones
    // it. PrepareSaveData NREs on null chartData.lines and DoSaveGame never
    // clears busy ("not ready to save").
    //
    // 0.2.9/0.2.10 unparented every sold pickup and broke hanging lanterns /
    // boat cargo. This patch only touches prefab 165.
    internal static class MirageMountainMap
    {
        internal const int PrefabIndex = 165;
        private const string NameNeedle = "map mirage mountain";

        private static bool _loggedWorld;
        private static bool _loggedSpawnerItem;
        private static bool _loggedSpawners;

        internal static bool Enabled()
        {
            return FixesConfig.KeepMirageMountainMap != null
                && FixesConfig.KeepMirageMountainMap.Value;
        }

        internal static bool IsTarget(ShipItem item)
        {
            if (item == null)
                return false;

            SaveablePrefab saveable = item.GetComponent<SaveablePrefab>();
            if (saveable != null && saveable.prefabIndex == PrefabIndex)
                return true;

            string name = item.gameObject.name;
            return name != null
                && name.IndexOf(NameNeedle, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        internal static void UnparentToWorld(ShipItem item)
        {
            if (!ShouldUnparent(item))
                return;

            Transform world = GetWorld();
            if (world == null)
                return;
            if (item.transform.parent == world)
                return;

            item.transform.parent = world;

            SaveablePrefab saveable = item.GetComponent<SaveablePrefab>();
            if (saveable != null)
                saveable.RegisterToSave();
        }

        internal static bool ShouldUnparent(ShipItem item)
        {
            if (!IsTarget(item) || !item.sold)
                return false;
            if (IsOnBoat(item))
                return false;
            if (IsHangable(item))
                return false;
            return IsIslandParented(item);
        }

        internal static bool ShouldSkipDestroy(ShipItem item)
        {
            if (!Enabled() || !IsTarget(item))
                return false;
            if (item.held)
                return true;

            int slot = TryInventorySlot(item);
            return slot >= 0 && slot < 100;
        }

        internal static bool IsOnBoat(ShipItem item)
        {
            if (item == null)
                return false;
            return item.currentActualBoat != null || item.currentWalkCol != null;
        }

        internal static bool IsHangable(ShipItem item)
        {
            if (item == null)
                return false;

            HangableItem hangable = item.GetComponent<HangableItem>();
            if (hangable != null && hangable.IsHanging())
                return true;

            if (item.GetComponent<ShipItemLampHook>() != null)
                return true;

            ItemRigidbody body = item.GetItemRigidbody();
            return body != null && body.attached;
        }

        internal static bool IsIslandParented(ShipItem item)
        {
            if (item == null)
                return false;

            Transform t = item.transform.parent;
            int guard = 0;
            while (t != null && guard < 16)
            {
                if (t.GetComponent<IslandHorizon>() != null)
                    return true;
                if (t.GetComponent<IslandPerformanceSwitcher>() != null)
                    return true;
                if (t.GetComponent<IslandSceneryScene>() != null)
                    return true;
                t = t.parent;
                guard++;
            }

            return false;
        }

        internal static Transform GetWorld()
        {
            if (FloatingOriginManager.instance == null)
            {
                if (!_loggedWorld)
                {
                    Plugin.Log.LogWarning("KeepMirageMountainMap: FloatingOriginManager.instance is missing; leaving vanilla parent.");
                    _loggedWorld = true;
                }

                return null;
            }

            return FloatingOriginManager.instance.transform;
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
                Plugin.Log.LogWarning("KeepMirageMountainMap: GetCurrentInventorySlot failed: " + e.Message);
                return -1;
            }
        }

        internal static void ClearHotbarSlot(ShipItem item)
        {
            if (item == null || GPButtonInventorySlot.inventorySlots == null)
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
                Plugin.Log.LogWarning("KeepMirageMountainMap: pruned " + removed + " destroyed save prefab(s) before save.");
        }

        internal static void ReleaseFromSpawner(WorldItemSpawner spawner)
        {
            Traverse itemField = Traverse.Create(spawner).Field("item");
            if (!itemField.FieldExists())
            {
                if (!_loggedSpawnerItem)
                {
                    Plugin.Log.LogWarning("KeepMirageMountainMap: WorldItemSpawner.item is missing; leaving vanilla pickup.");
                    _loggedSpawnerItem = true;
                }

                return;
            }

            ShipItem item = itemField.GetValue<ShipItem>();
            if (item == null || item.held || !IsTarget(item))
                return;
            if (TryInventorySlot(item) < 0)
                return;

            if (ItemSpawners.instance == null || ItemSpawners.instance.cooldowns == null)
            {
                if (!_loggedSpawners)
                {
                    Plugin.Log.LogWarning("KeepMirageMountainMap: ItemSpawners.cooldowns is missing; leaving vanilla respawn.");
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
    internal static class MirageMapUnparentOnPickupPatch
    {
        private static void Postfix(ShipItem __instance)
        {
            if (!MirageMountainMap.Enabled())
                return;

            MirageMountainMap.UnparentToWorld(__instance);
        }
    }

    [HarmonyPatch(typeof(ShipItem), nameof(ShipItem.OnEnterInventory))]
    internal static class MirageMapUnparentOnInventoryPatch
    {
        private static void Postfix(ShipItem __instance)
        {
            if (!MirageMountainMap.Enabled())
                return;

            MirageMountainMap.UnparentToWorld(__instance);
        }
    }

    [HarmonyPatch(typeof(WorldItemSpawner), "Update")]
    internal static class MirageMapSpawnerInventoryTakenPatch
    {
        private static void Postfix(WorldItemSpawner __instance)
        {
            if (!MirageMountainMap.Enabled())
                return;
            if (__instance == null || !Application.isPlaying || !GameState.playing)
                return;

            MirageMountainMap.ReleaseFromSpawner(__instance);
        }
    }

    [HarmonyPatch(typeof(ShipItem), nameof(ShipItem.DestroyItem))]
    internal static class MirageMapSkipRangeDestroyPatch
    {
        private static bool Prefix(ShipItem __instance)
        {
            return !MirageMountainMap.ShouldSkipDestroy(__instance);
        }

        private static void Postfix(ShipItem __instance, bool __runOriginal)
        {
            // The prefix keeps a held or slotted map; its slot must stay too.
            if (!__runOriginal)
                return;
            if (!MirageMountainMap.Enabled())
                return;
            if (!MirageMountainMap.IsTarget(__instance))
                return;

            MirageMountainMap.ClearHotbarSlot(__instance);
        }
    }

    [HarmonyPatch(typeof(SaveLoadManager), nameof(SaveLoadManager.SaveGame))]
    internal static class MirageMapPruneDestroyedPrefabsPatch
    {
        private static void Prefix()
        {
            if (!MirageMountainMap.Enabled())
                return;

            MirageMountainMap.PruneDestroyedPrefabs();
        }
    }

    [HarmonyPatch(typeof(SaveablePrefab), nameof(SaveablePrefab.PrepareSaveData))]
    internal static class MirageMapChartDataSavePatch
    {
        private static void Prefix(SaveablePrefab __instance)
        {
            if (!MirageMountainMap.Enabled())
                return;

            MirageMountainMap.EnsureChartDataLists(__instance);
        }
    }
}
