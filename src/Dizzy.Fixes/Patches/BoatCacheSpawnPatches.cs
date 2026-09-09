using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Dizzy.Fixes
{
    // BoatLocalItems.SpawnCachedItems Instantiates every cached SavePrefabData.
    // If PrefabsDirectory.directory[prefabIndex] is null, Unity throws
    // ArgumentException. Update then never sets itemsLoaded / never clears
    // cachedItems, so the next frame spawns the same list again. Player.log
    // showed dhow large (30) doing this 491 times, instantiating
    // "110 lantern A" each pass.
    internal static class BoatCacheSpawn
    {
        internal static bool LoggedMissingField;

        internal static bool Enabled()
        {
            return FixesConfig.PreventBoatCacheSpawnLoop != null
                && FixesConfig.PreventBoatCacheSpawnLoop.Value;
        }

        internal static bool AlreadySpawned(int instanceId)
        {
            if (instanceId == 0 || SaveLoadManager.instance == null)
                return false;

            List<SaveablePrefab> prefabs = SaveLoadManager.instance.GetCurrentPrefabs();
            if (prefabs == null)
                return false;

            for (int i = 0; i < prefabs.Count; i++)
            {
                SaveablePrefab prefab = prefabs[i];
                if (prefab != null && prefab.instanceId == instanceId)
                    return true;
            }

            return false;
        }

        internal static GameObject PrefabOrNull(int index)
        {
            if (PrefabsDirectory.instance == null || PrefabsDirectory.instance.directory == null)
                return null;
            if (index < 0 || index >= PrefabsDirectory.instance.directory.Length)
                return null;
            return PrefabsDirectory.instance.directory[index];
        }
    }

    [HarmonyPatch(typeof(BoatLocalItems), "SpawnCachedItems")]
    internal static class BoatLocalItemsSpawnCachedItemsPatch
    {
        private static bool Prefix(BoatLocalItems __instance)
        {
            if (!BoatCacheSpawn.Enabled())
                return true;

            Traverse items = Traverse.Create(__instance);
            if (!items.Field("cachedItems").FieldExists())
            {
                if (!BoatCacheSpawn.LoggedMissingField)
                {
                    Plugin.Log.LogWarning("PreventBoatCacheSpawnLoop: cachedItems is missing; leaving vanilla spawn.");
                    BoatCacheSpawn.LoggedMissingField = true;
                }

                return true;
            }

            List<SavePrefabData> cached = items.Field("cachedItems").GetValue<List<SavePrefabData>>();
            GameState.loadingBoatLocalItems = true;

            if (cached == null)
            {
                Plugin.Log.LogWarning("PreventBoatCacheSpawnLoop: SpawnCachedItems with null cache on " + __instance.gameObject.name);
                Finish(__instance, items);
                return false;
            }

            int spawned = 0;
            int skippedMissing = 0;
            int skippedDup = 0;
            for (int i = 0; i < cached.Count; i++)
            {
                SavePrefabData data = cached[i];
                if (data == null)
                {
                    skippedMissing++;
                    continue;
                }

                if (BoatCacheSpawn.AlreadySpawned(data.instanceId))
                {
                    skippedDup++;
                    continue;
                }

                GameObject prefab = BoatCacheSpawn.PrefabOrNull(data.prefabIndex);
                if (prefab == null)
                {
                    skippedMissing++;
                    Plugin.Log.LogWarning("PreventBoatCacheSpawnLoop: skip missing prefab index " + data.prefabIndex + " on " + __instance.gameObject.name);
                    continue;
                }

                GameObject spawnedGo = UnityEngine.Object.Instantiate(prefab, Vector3.zero, Quaternion.identity);
                SaveablePrefab saveable = spawnedGo.GetComponent<SaveablePrefab>();
                if (saveable == null)
                {
                    UnityEngine.Object.Destroy(spawnedGo);
                    skippedMissing++;
                    continue;
                }

                saveable.Load(data);
                spawned++;
            }

            if (skippedMissing > 0 || skippedDup > 0)
            {
                Plugin.Log.LogInfo("PreventBoatCacheSpawnLoop: " + __instance.gameObject.name
                    + " spawned " + spawned
                    + ", skipped missing " + skippedMissing
                    + ", skipped already-loaded " + skippedDup
                    + " (vanilla list count " + cached.Count + ").");
            }

            Finish(__instance, items);
            return false;
        }

        private static void Finish(BoatLocalItems instance, Traverse items)
        {
            items.Field("cachedItems").SetValue(null);

            Traverse setGamestate = items.Method("SetGamestate");
            if (setGamestate.MethodExists())
            {
                IEnumerator routine = setGamestate.GetValue<IEnumerator>();
                if (routine != null)
                    instance.StartCoroutine(routine);
            }
            else
                GameState.loadingBoatLocalItems = false;
        }
    }
}
