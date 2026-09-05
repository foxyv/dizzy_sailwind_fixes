using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace Dizzy.Fixes
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInProcess("Sailwind.exe")]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.dizzy.sailwind.fixes";
        public const string PluginName = "Dizzy Sailwind Fixes";
        public const string PluginVersion = "0.1.0";

        internal static ManualLogSource Log;
        internal static Plugin Instance;

        private Harmony _harmony;

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            FixesConfig.Bind(Config);

            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll(typeof(Plugin).Assembly);

            Log.LogInfo($"{PluginName} v{PluginVersion} loaded (PreventInventoryWithdrawSip={FixesConfig.PreventInventoryWithdrawSip.Value}, PreventFailedTradeBookSale={FixesConfig.PreventFailedTradeBookSale.Value}, SkipUncleanableHullCleaning={FixesConfig.SkipUncleanableHullCleaning.Value}, KeepMooredBoats={FixesConfig.KeepMooredBoats.Value}, StabilizeStoveItemHover={FixesConfig.StabilizeStoveItemHover.Value}).");
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();

            if (Instance == this)
                Instance = null;
        }
    }
}
