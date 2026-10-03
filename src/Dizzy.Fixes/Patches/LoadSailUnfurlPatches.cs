using System.Collections.Generic;
using System.Globalization;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace Dizzy.Fixes
{
    // Vanilla SaveSailData has no reef length. RopeControllerSailReef.Update
    // unfurls every sail while currentlyLoading, then furls every sail while
    // justStarted, so a save always ended fully furled. Persist purchased-boat
    // reef currentLength in GameState.modData and apply it during those flags.
    internal static class LoadSailUnfurl
    {
        private const string ModDataKey = "Dizzy.Fixes.SailReef.v1";

        private static bool _loadedSave;
        private static readonly Dictionary<string, float> SavedLength = new Dictionary<string, float>();

        internal static bool Enabled()
        {
            return FixesConfig.KeepLoadedSailsUnfurled != null
                && FixesConfig.KeepLoadedSailsUnfurled.Value;
        }

        internal static void OnLoadGamePrefix()
        {
            _loadedSave = true;
        }

        internal static void OnLoadGamePostfix()
        {
            ReadModData();
        }

        internal static void OnNewGame()
        {
            _loadedSave = false;
            SavedLength.Clear();
        }

        internal static void OnSaveGame()
        {
            if (!Enabled())
                return;
            WriteModData();
        }

        internal static void AfterReefUpdate(RopeControllerSailReef reef)
        {
            if (!Enabled() || !_loadedSave || reef == null)
                return;
            if (!GameState.currentlyLoading && !GameState.justStarted)
                return;

            float saved;
            if (TryGetSavedLength(reef, out saved))
                reef.currentLength = saved;
        }

        private static bool TryGetSavedLength(RopeControllerSailReef reef, out float length)
        {
            length = 0f;
            if (reef == null)
                return false;
            string key = MakeKey(reef.sail);
            if (key == null)
                return false;
            return SavedLength.TryGetValue(key, out length);
        }

        private static string MakeKey(Sail sail)
        {
            if (sail == null || sail.shipRigidbody == null)
                return null;

            PurchasableBoat boat = sail.shipRigidbody.GetComponent<PurchasableBoat>();
            if (boat == null || !boat.isPurchased())
                return null;

            SaveableObject saveable = sail.shipRigidbody.GetComponent<SaveableObject>();
            if (saveable == null)
                return null;

            Mast mast = sail.GetComponentInParent<Mast>();
            if (mast == null)
                return null;

            return saveable.sceneIndex.ToString(CultureInfo.InvariantCulture)
                + ":" + mast.orderIndex.ToString(CultureInfo.InvariantCulture)
                + ":" + sail.mastOrder.ToString(CultureInfo.InvariantCulture)
                + ":" + sail.prefabIndex.ToString(CultureInfo.InvariantCulture);
        }

        private static string BoatOf(string key)
        {
            int colon = key.IndexOf(':');
            return colon > 0 ? key.Substring(0, colon) : key;
        }

        private static void ReadModData()
        {
            SavedLength.Clear();
            if (GameState.modData == null)
                return;

            string raw;
            if (!GameState.modData.TryGetValue(ModDataKey, out raw) || string.IsNullOrEmpty(raw))
                return;

            string[] lines = raw.Split(new[] { '\n' }, System.StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                int eq = line.IndexOf('=');
                if (eq <= 0 || eq >= line.Length - 1)
                    continue;

                string key = line.Substring(0, eq);
                float length;
                if (!float.TryParse(line.Substring(eq + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out length))
                    continue;
                if (length < 0f)
                    length = 0f;
                if (length > 1f)
                    length = 1f;
                SavedLength[key] = length;
            }
        }

        private static void WriteModData()
        {
            if (GameState.modData == null)
                GameState.modData = new Dictionary<string, string>();

            // Merge into the loaded data: FindObjectsOfType skips inactive
            // boats, whose entries must survive. Only boats seen here drop keys
            // for sails they no longer have.
            Dictionary<string, float> live = new Dictionary<string, float>();
            HashSet<string> liveBoats = new HashSet<string>();
            RopeControllerSailReef[] reefs = Object.FindObjectsOfType<RopeControllerSailReef>();
            if (reefs != null)
            {
                for (int i = 0; i < reefs.Length; i++)
                {
                    RopeControllerSailReef reef = reefs[i];
                    if (reef == null)
                        continue;
                    string key = MakeKey(reef.sail);
                    if (key == null)
                        continue;
                    live[key] = Mathf.Clamp01(reef.currentLength);
                    liveBoats.Add(BoatOf(key));
                }
            }

            List<string> stale = new List<string>();
            foreach (string key in SavedLength.Keys)
            {
                if (liveBoats.Contains(BoatOf(key)) && !live.ContainsKey(key))
                    stale.Add(key);
            }
            for (int i = 0; i < stale.Count; i++)
                SavedLength.Remove(stale[i]);
            foreach (KeyValuePair<string, float> pair in live)
                SavedLength[pair.Key] = pair.Value;

            if (SavedLength.Count == 0)
            {
                GameState.modData.Remove(ModDataKey);
                return;
            }

            StringBuilder sb = new StringBuilder();
            foreach (KeyValuePair<string, float> pair in SavedLength)
            {
                if (sb.Length > 0)
                    sb.Append('\n');
                sb.Append(pair.Key);
                sb.Append('=');
                sb.Append(pair.Value.ToString("G9", CultureInfo.InvariantCulture));
            }

            GameState.modData[ModDataKey] = sb.ToString();
        }
    }

    [HarmonyPatch(typeof(SaveLoadManager), "LoadGame")]
    internal static class LoadGameSailReefPatch
    {
        private static void Prefix()
        {
            LoadSailUnfurl.OnLoadGamePrefix();
        }

        private static void Postfix()
        {
            LoadSailUnfurl.OnLoadGamePostfix();
        }
    }

    [HarmonyPatch(typeof(SaveLoadManager), "SaveGame")]
    internal static class SaveGameSailReefPatch
    {
        private static void Prefix()
        {
            LoadSailUnfurl.OnSaveGame();
        }
    }

    [HarmonyPatch(typeof(StartMenu), "StartNewGame")]
    internal static class StartNewGameSailReefPatch
    {
        private static void Prefix()
        {
            LoadSailUnfurl.OnNewGame();
        }
    }

    [HarmonyPatch(typeof(RopeControllerSailReef), "Update")]
    internal static class LoadSailReefJustStartedPatch
    {
        private static void Postfix(RopeControllerSailReef __instance)
        {
            LoadSailUnfurl.AfterReefUpdate(__instance);
        }
    }
}
