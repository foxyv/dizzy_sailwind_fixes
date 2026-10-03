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
        public const string PluginVersion = "0.3.2";

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

            Log.LogInfo($"{PluginName} v{PluginVersion} loaded (PreventInventoryWithdrawSip={FixesConfig.PreventInventoryWithdrawSip.Value}, PreventFailedTradeBookSale={FixesConfig.PreventFailedTradeBookSale.Value}, SkipUncleanableHullCleaning={FixesConfig.SkipUncleanableHullCleaning.Value}, StabilizeStoveItemHover={FixesConfig.StabilizeStoveItemHover.Value}, PreferHangingItemOnHook={FixesConfig.PreferHangingItemOnHook.Value}, MuteCameraModeSound={FixesConfig.MuteCameraModeSound.Value}, PreferDroppedAnchorLook={FixesConfig.PreferDroppedAnchorLook.Value}, RightClickNearestDockMooring={FixesConfig.RightClickNearestDockMooring.Value}, RightClickNearestDockMooringFeet={FixesConfig.RightClickNearestDockMooringFeet.Value}, RightClickNearestDockMooringArcDegrees={FixesConfig.RightClickNearestDockMooringArcDegrees.Value}, PreferMooredDockLineLook={FixesConfig.PreferMooredDockLineLook.Value}, RightClickBoatMooringCastOff={FixesConfig.RightClickBoatMooringCastOff.Value}, HideInventoryOnNeedsWarning={FixesConfig.HideInventoryOnNeedsWarning.Value}, PourSoupIntoMug={FixesConfig.PourSoupIntoMug.Value}, KeepChipLogDeployed={FixesConfig.KeepChipLogDeployed.Value}, PreventBoatCacheSpawnLoop={FixesConfig.PreventBoatCacheSpawnLoop.Value}, KeepMirageMountainMap={FixesConfig.KeepMirageMountainMap.Value}, SuppressBogusSailSnap={FixesConfig.SuppressBogusSailSnap.Value}, KeepLoadedSailsUnfurled={FixesConfig.KeepLoadedSailsUnfurled.Value}, DampenItemRoll={FixesConfig.DampenItemRoll.Value}, AlignPlacedItemToSurface={FixesConfig.AlignPlacedItemToSurface.Value}, KeepLookTextAboveSmoke={FixesConfig.KeepLookTextAboveSmoke.Value}, HammerNailSeconds={FixesConfig.HammerNailSeconds.Value}, KeepMerchantSellScroll={FixesConfig.KeepMerchantSellScroll.Value}, PreferSittingItemLook={FixesConfig.PreferSittingItemLook.Value}, SkipOtherFoodWhileHolding={FixesConfig.SkipOtherFoodWhileHolding.Value}, SkipOtherFirewoodWhileHolding={FixesConfig.SkipOtherFirewoodWhileHolding.Value}, ReleaseDestroyedHeldItem={FixesConfig.ReleaseDestroyedHeldItem.Value}, SkipSmoothOriginShift={FixesConfig.SkipSmoothOriginShift.Value}, KeepCrateContentsWithBoat={FixesConfig.KeepCrateContentsWithBoat.Value}, DropBigCratePastOtherCrates={FixesConfig.DropBigCratePastOtherCrates.Value}, KeepMissionListPage={FixesConfig.KeepMissionListPage.Value}, DelayBoatCacheSpawn={FixesConfig.DelayBoatCacheSpawn.Value}).");
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();

            if (Instance == this)
                Instance = null;
        }
    }
}
