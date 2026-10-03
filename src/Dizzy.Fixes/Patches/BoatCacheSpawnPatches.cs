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
        internal static readonly AccessTools.FieldRef<BoatLocalItems, List<SavePrefabData>> CachedItems = GameMembers.Field<BoatLocalItems, List<SavePrefabData>>("cachedItems");

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

    // Crate pieces search once, two end-of-frames after they spawn. On this save
    // the pieces and the crate are both boat items, so delaying the whole boat
    // still lets the search find the crate. Spawn the pieces first and hold the
    // crate itself past that search.
    internal static class BoatCacheSpawnDelay
    {
        internal const int Frames = 8;

        private static readonly Dictionary<int, int> CrateReleaseFrame = new Dictionary<int, int>();

        internal static bool HeldThisSpawn;
        internal static bool SpawningWithoutCrates;
        internal static List<SavePrefabData> ReturnToCache;

        internal static bool Enabled()
        {
            return FixesConfig.DelayBoatCacheSpawn != null
                && FixesConfig.DelayBoatCacheSpawn.Value;
        }

        internal static void Reset()
        {
            CrateReleaseFrame.Clear();
            HeldThisSpawn = false;
        }

        // True means skip the full spawn. The crate is still in the cache.
        internal static bool DeferCrates(BoatLocalItems boat)
        {
            if (!Enabled() || boat == null)
                return false;

            int id = boat.GetInstanceID();
            int frame = Time.frameCount;
            int release;
            if (!CrateReleaseFrame.TryGetValue(id, out release))
            {
                int crates = SpawnEverythingExceptCrates(boat);
                if (crates <= 0)
                    return false;

                release = frame + Frames;
                CrateReleaseFrame[id] = release;
                Plugin.Log.LogInfo("DelayBoatCacheSpawn: " + boat.gameObject.name + " held " + crates + " crates until frame " + release + ".");
                return true;
            }

            if (frame < release)
                return true;

            if (release != int.MinValue)
            {
                CrateReleaseFrame[id] = int.MinValue;
                Plugin.Log.LogInfo("DelayBoatCacheSpawn: releasing crates on " + boat.gameObject.name + ".");
            }

            return false;
        }

        private static int SpawnEverythingExceptCrates(BoatLocalItems boat)
        {
            if (BoatCacheSpawn.CachedItems == null)
                return 0;

            List<SavePrefabData> cached = BoatCacheSpawn.CachedItems(boat);
            if (cached == null || cached.Count == 0)
                return 0;

            List<SavePrefabData> held = new List<SavePrefabData>();
            int spawned = 0;
            int crates = 0;
            SpawningWithoutCrates = true;
            ReturnToCache = new List<SavePrefabData>();
            try
            {
            for (int i = 0; i < cached.Count; i++)
            {
                SavePrefabData data = cached[i];
                if (data == null)
                    continue;

                GameObject prefab = BoatCacheSpawn.PrefabOrNull(data.prefabIndex);
                bool isCrate = prefab != null && prefab.GetComponent<ShipItemCrate>() != null;
                if (isCrate || prefab == null)
                {
                    held.Add(data);
                    if (isCrate)
                        crates++;
                    continue;
                }

                if (BoatCacheSpawn.AlreadySpawned(data.instanceId))
                    continue;

                GameObject spawnedGo = UnityEngine.Object.Instantiate(prefab, Vector3.zero, Quaternion.identity);
                SaveablePrefab saveable = spawnedGo.GetComponent<SaveablePrefab>();
                if (saveable == null)
                {
                    UnityEngine.Object.Destroy(spawnedGo);
                    continue;
                }

                saveable.Load(data);
                spawned++;
            }
            }
            finally
            {
                SpawningWithoutCrates = false;
            }

            if (ReturnToCache != null)
            {
                for (int r = 0; r < ReturnToCache.Count; r++)
                {
                    SavePrefabData returned = ReturnToCache[r];
                    if (returned != null && !CacheHasId(held, returned.instanceId))
                        held.Add(returned);
                }

                ReturnToCache = null;
            }

            BoatCacheSpawn.CachedItems(boat) = held;
            if (spawned > 0)
                GameState.loadingBoatLocalItems = true;

            return crates;
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

    [HarmonyPatch(typeof(SaveLoadManager), "LoadGame")]
    internal static class DelayBoatCacheSpawnResetPatch
    {
        private static void Prefix()
        {
            if (BoatCacheSpawnDelay.Enabled())
                BoatCacheSpawnDelay.Reset();
        }
    }

    // Update sets itemsLoaded after SpawnCachedItems returns. A skipped spawn
    // would otherwise count as loaded and never retry, so the hold clears that flag.
    [HarmonyPatch(typeof(BoatLocalItems), "Update")]
    internal static class DelayBoatCacheSpawnRetryPatch
    {
        private static void Prefix()
        {
            BoatCacheSpawnDelay.HeldThisSpawn = false;
        }

        private static void Postfix(BoatLocalItems __instance)
        {
            if (!BoatCacheSpawnDelay.HeldThisSpawn)
                return;

            __instance.SetItemsLoaded(false);
        }
    }

    [HarmonyPatch(typeof(BoatLocalItems), "SpawnCachedItems")]
    internal static class BoatLocalItemsSpawnCachedItemsPatch
    {
        private static readonly Func<BoatLocalItems, IEnumerator> SetGamestate = GameMembers.Method<Func<BoatLocalItems, IEnumerator>>(typeof(BoatLocalItems), "SetGamestate");

        private static bool Prefix(BoatLocalItems __instance)
        {
            if (BoatCacheSpawnDelay.DeferCrates(__instance))
            {
                BoatCacheSpawnDelay.HeldThisSpawn = true;
                return false;
            }

            if (!BoatCacheSpawn.Enabled())
                return true;

            if (BoatCacheSpawn.CachedItems == null)
            {
                if (!BoatCacheSpawn.LoggedMissingField)
                {
                    Plugin.Log.LogWarning("PreventBoatCacheSpawnLoop: cachedItems is missing; leaving vanilla spawn.");
                    BoatCacheSpawn.LoggedMissingField = true;
                }

                return true;
            }

            List<SavePrefabData> cached = BoatCacheSpawn.CachedItems(__instance);
            GameState.loadingBoatLocalItems = true;

            if (cached == null)
            {
                Plugin.Log.LogWarning("PreventBoatCacheSpawnLoop: SpawnCachedItems with null cache on " + __instance.gameObject.name);
                Finish(__instance);
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

            Finish(__instance);
            return false;
        }

        private static void Finish(BoatLocalItems instance)
        {
            BoatCacheSpawn.CachedItems(instance) = null;

            if (SetGamestate != null)
            {
                IEnumerator routine = SetGamestate(instance);
                if (routine != null)
                    instance.StartCoroutine(routine);
            }
            else
                GameState.loadingBoatLocalItems = false;
        }
    }
}
