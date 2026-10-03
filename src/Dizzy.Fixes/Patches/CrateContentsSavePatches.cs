using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Dizzy.Fixes
{
    // Unseal spawns one piece per unit and InsertItem only stores the crate id.
    // Those pieces never EnterBoat, so they save as world objects while the crate
    // stays on the boat. A reload away from that boat spawns the pieces immediately,
    // they look for the crate once, and the crate is still cached.
    internal static class CrateContentsSave
    {
        internal static bool Enabled()
        {
            return FixesConfig.KeepCrateContentsWithBoat != null
                && FixesConfig.KeepCrateContentsWithBoat.Value;
        }

        internal static void CopyBoatParent(CrateInventory crate, ShipItem item)
        {
            if (crate == null || item == null)
                return;

            SaveablePrefab crateSave = crate.GetComponent<SaveablePrefab>();
            SaveablePrefab itemSave = item.GetComponent<SaveablePrefab>();
            if (crateSave == null || itemSave == null)
                return;

            int parent = crateSave.GetParentObject();
            if (parent > 0)
                itemSave.SetParentObject(parent);
        }

        internal static void RefileWorldContents()
        {
            if (SaveLoadManager.instance == null)
                return;

            List<SaveablePrefab> prefabs = SaveLoadManager.instance.GetCurrentPrefabs();
            if (prefabs == null)
                return;

            List<SaveablePrefab> snapshot = new List<SaveablePrefab>(prefabs);
            int inserted = 0;
            int cached = 0;
            for (int i = 0; i < snapshot.Count; i++)
            {
                SaveablePrefab saveable = snapshot[i];
                if (saveable == null || saveable.currentCrateId <= 0 || saveable.GetParentObject() > 0)
                    continue;

                ShipItem item = saveable.GetComponent<ShipItem>();
                if (item == null || item.GetCurrentInventorySlot() > -1)
                    continue;

                SaveablePrefab crate = FindLiveCrate(saveable.currentCrateId);
                if (crate != null)
                {
                    CrateInventory inventory = crate.GetComponent<CrateInventory>();
                    if (inventory == null)
                        inventory = crate.gameObject.AddComponent<CrateInventory>();
                    inventory.InsertItem(item);
                    inserted++;
                    continue;
                }

                if (MoveIntoCrateBoat(saveable, item))
                    cached++;
            }

            if (inserted > 0 || cached > 0)
            {
                Plugin.Log.LogInfo("KeepCrateContentsWithBoat: inserted " + inserted
                    + " loose crate items into a live crate, cached " + cached
                    + " with their crate's boat.");
            }
        }

        private static SaveablePrefab FindLiveCrate(int crateId)
        {
            List<SaveablePrefab> prefabs = SaveLoadManager.instance.GetCurrentPrefabs();
            if (prefabs == null)
                return null;

            for (int i = 0; i < prefabs.Count; i++)
            {
                SaveablePrefab prefab = prefabs[i];
                if (prefab == null || prefab.instanceId != crateId)
                    continue;
                if (prefab.GetComponent<ShipItemCrate>() != null || prefab.GetComponent<CrateInventory>() != null)
                    return prefab;
            }

            return null;
        }

        private static bool MoveIntoCrateBoat(SaveablePrefab saveable, ShipItem item)
        {
            SaveableObject[] objects = SaveLoadManager.instance.GetCurrentObjects();
            if (objects == null)
                return false;

            for (int i = 0; i < objects.Length; i++)
            {
                SaveableObject boat = objects[i];
                if (boat == null || boat.localItems == null || !boat.localItems.HasLocalItems())
                    continue;

                List<SavePrefabData> cache = boat.localItems.GetCachedItems();
                if (cache == null || !CacheHasId(cache, saveable.currentCrateId))
                    continue;

                if (!CacheHasId(cache, saveable.instanceId))
                {
                    SavePrefabData data = saveable.PrepareSaveData();
                    data.itemParentObject = boat.sceneIndex;
                    data.position = boat.transform.InverseTransformPoint(saveable.transform.position);
                    data.crateId = saveable.currentCrateId;
                    boat.localItems.AddCachedItemFromSaveData(data);
                }

                saveable.currentCrateId = 0;
                item.DestroyItem();
                return true;
            }

            return false;
        }

        private static bool CacheHasId(List<SavePrefabData> cache, int instanceId)
        {
            for (int i = 0; i < cache.Count; i++)
            {
                SavePrefabData data = cache[i];
                if (data != null && data.instanceId == instanceId)
                    return true;
            }

            return false;
        }
    }

    [HarmonyPatch(typeof(CrateInventory), "InsertItem")]
    internal static class KeepCrateContentsInsertPatch
    {
        private static void Postfix(CrateInventory __instance, ShipItem item)
        {
            if (!CrateContentsSave.Enabled())
                return;

            CrateContentsSave.CopyBoatParent(__instance, item);
        }
    }

    // The debug hold spawns loose pieces while the crate is still cached.
    // Put those pieces back on the held list so they spawn with the crate.
    [HarmonyPatch(typeof(SaveablePrefab), "Load")]
    internal static class KeepCrateContentsEarlySpawnPatch
    {
        private static void Postfix(SaveablePrefab __instance)
        {
            if (!CrateContentsSave.Enabled() || !BoatCacheSpawnDelay.SpawningWithoutCrates)
                return;
            if (__instance == null || __instance.currentCrateId <= 0)
                return;
            if (BoatCacheSpawnDelay.ReturnToCache == null)
                return;

            ShipItem item = __instance.GetComponent<ShipItem>();
            if (item == null || item.GetCurrentInventorySlot() > -1)
                return;

            List<SaveablePrefab> prefabs = SaveLoadManager.instance != null
                ? SaveLoadManager.instance.GetCurrentPrefabs()
                : null;
            if (prefabs != null)
            {
                for (int i = 0; i < prefabs.Count; i++)
                {
                    SaveablePrefab prefab = prefabs[i];
                    if (prefab == null || prefab.instanceId != __instance.currentCrateId)
                        continue;
                    if (prefab.GetComponent<ShipItemCrate>() == null && prefab.GetComponent<CrateInventory>() == null)
                        continue;

                    CrateInventory inventory = prefab.GetComponent<CrateInventory>();
                    if (inventory == null)
                        inventory = prefab.gameObject.AddComponent<CrateInventory>();
                    inventory.InsertItem(item);
                    return;
                }
            }

            SavePrefabData data = __instance.PrepareSaveData();
            data.crateId = __instance.currentCrateId;
            BoatCacheSpawnDelay.ReturnToCache.Add(data);
            __instance.currentCrateId = 0;
            item.DestroyItem();
        }
    }

    [HarmonyPatch(typeof(SaveLoadManager), "LoadGame")]
    internal static class KeepCrateContentsLoadPatch
    {
        private static void Postfix()
        {
            if (!CrateContentsSave.Enabled())
                return;

            CrateContentsSave.RefileWorldContents();
        }
    }

    [HarmonyPatch(typeof(CrateInventoryUI), "RefreshButtons")]
    internal static class KeepCrateContentsGridPatch
    {
        private static bool Prefix(CrateInventoryUI __instance)
        {
            if (!CrateContentsSave.Enabled())
                return true;
            if (__instance.buttons == null || __instance.currentCrate == null || __instance.currentCrate.containedItems == null)
                return true;
            if (__instance.currentCrate.containedItems.Count <= __instance.buttons.Length)
                return true;

            Vector2 dimensions = CrateGridSize(__instance);
            int columns = (int)dimensions.x;
            int rows = (int)dimensions.y;
            int column = 0;
            int row = 0;
            CrateInventoryButton[] buttons = __instance.buttons;
            for (int i = 0; i < buttons.Length; i++)
            {
                CrateInventoryButton button = buttons[i];
                if (button == null)
                    continue;
                if (row < rows)
                {
                    button.gameObject.SetActive(true);
                    button.ShowItem(null);
                }

                button.transform.localPosition = new Vector3(-column, row, 0f) * 0.5f;
                button.transform.localPosition += new Vector3(columns, -rows, 0f) * 0.25f;
                column++;
                if (column >= columns)
                {
                    column = 0;
                    row++;
                }
            }

            int shown = buttons.Length < __instance.currentCrate.containedItems.Count
                ? buttons.Length
                : __instance.currentCrate.containedItems.Count;
            Plugin.Log.LogInfo("KeepCrateContentsWithBoat: crate has "
                + __instance.currentCrate.containedItems.Count
                + " items and " + buttons.Length + " squares; showing " + shown + ".");
            for (int j = 0; j < shown; j++)
            {
                if (buttons[j] != null)
                    buttons[j].ShowItem(__instance.currentCrate.containedItems[j]);
            }

            return false;
        }

        private static Vector2 CrateGridSize(CrateInventoryUI ui)
        {
            MeshFilter filter = ui.currentCrate.GetComponent<MeshFilter>();
            Mesh mesh = filter != null ? filter.sharedMesh : null;
            Mesh[] meshes = ui.containerMeshes;
            if (mesh != null && meshes != null)
            {
                if (meshes.Length > 0 && mesh == meshes[0])
                    return new Vector2(4f, 3f);
                if (meshes.Length > 1 && mesh == meshes[1])
                    return new Vector2(5f, 4f);
                if (meshes.Length > 2 && mesh == meshes[2])
                    return new Vector2(6f, 5f);
                if (meshes.Length > 3 && mesh == meshes[3])
                    return new Vector2(4f, 2f);
            }

            return new Vector2(6f, 5f);
        }
    }
}
