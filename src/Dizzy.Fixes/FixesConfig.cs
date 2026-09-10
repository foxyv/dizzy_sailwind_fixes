using BepInEx.Configuration;

namespace Dizzy.Fixes
{
    internal static class FixesConfig
    {
        internal static ConfigEntry<bool> PreventInventoryWithdrawSip;
        internal static ConfigEntry<bool> PreventFailedTradeBookSale;
        internal static ConfigEntry<bool> SkipUncleanableHullCleaning;
        internal static ConfigEntry<bool> StabilizeStoveItemHover;
        internal static ConfigEntry<bool> PreferHangingItemOnHook;
        internal static ConfigEntry<bool> MuteCameraModeSound;
        internal static ConfigEntry<bool> PreferDroppedAnchorLook;
        internal static ConfigEntry<bool> RightClickNearestDockMooring;
        internal static ConfigEntry<float> RightClickNearestDockMooringFeet;
        internal static ConfigEntry<bool> PreferMooredDockLineLook;
        internal static ConfigEntry<bool> RightClickBoatMooringCastOff;
        internal static ConfigEntry<bool> HideInventoryOnNeedsWarning;
        internal static ConfigEntry<bool> PourSoupIntoMug;
        internal static ConfigEntry<bool> KeepChipLogDeployed;
        internal static ConfigEntry<bool> PreventBoatCacheSpawnLoop;
        internal static ConfigEntry<bool> KeepMirageMountainMap;
        internal static ConfigEntry<bool> SuppressBogusSailSnap;
        internal static ConfigEntry<bool> SuppressBogusSailSnapLog;

        internal static void Bind(ConfigFile config)
        {
            PreventInventoryWithdrawSip = config.Bind(
                "Fixes",
                "PreventInventoryWithdrawSip",
                true,
                "Do not sip from a bottle or soup bowl when pulling it from an inventory slot (number keys 1-5). Vanilla drinks once because the item appears overlapping the mouth collider.");

            PreventFailedTradeBookSale = config.Bind(
                "Fixes",
                "PreventFailedTradeBookSale",
                true,
                "Do not drop the market price or pay gold unless the trade-book sale actually removes a crate/barrel from the warehouse. Vanilla updates supply first, then can fail to find the physical good.");

            SkipUncleanableHullCleaning = config.Bind(
                "Fixes",
                "SkipUncleanableHullCleaning",
                true,
                "If a boat cannot get dirty (no cleanable hull, e.g. Jong), hull cleaning costs nothing and cannot abort the rest of the shipyard order. Vanilla can charge, then throw and revert sails/parts on exit.");

            StabilizeStoveItemHover = config.Bind(
                "Fixes",
                "StabilizeStoveItemHover",
                true,
                "Do not flicker the hover outline between a stove and the food/pot sitting on it. Vanilla keeps the first raycast hit, which swaps every physics frame while you cook.");

            PreferHangingItemOnHook = config.Bind(
                "Fixes",
                "PreferHangingItemOnHook",
                true,
                "Click the lantern or other hangable on a lamp hook instead of the hook itself. Vanilla hits the hook collider first, so a locked (nailed) hook keeps look-focus and the hanging item cannot be picked up.");

            MuteCameraModeSound = config.Bind(
                "Fixes",
                "MuteCameraModeSound",
                true,
                "Do not play the UI click when toggling third-person boat camera (C / CameraMode). Vanilla plays buttonClick on both enter and exit.");

            PreferDroppedAnchorLook = config.Bind(
                "Fixes",
                "PreferDroppedAnchorLook",
                true,
                "Do not lose look-focus on a dropped or set anchor to terrain. Vanilla's look ray hits the ground first, especially when you place the hook by hand on land, so the buried collider cannot be clicked.");

            RightClickNearestDockMooring = config.Bind(
                "Fixes",
                "RightClickNearestDockMooring",
                true,
                "Right-click while holding a mooring line throws it to the nearest free dock cleat. Vanilla left-click needs a 1.8 m look on the tiny post, which dock mesh often blocks.");

            RightClickNearestDockMooringFeet = config.Bind(
                "Fixes",
                "RightClickNearestDockMooringFeet",
                15f,
                "How far (in feet) right-click will search for a free dock cleat while holding a mooring line. Default 15. This is in-game feet (world units), not a real-world metric conversion.");

            PreferMooredDockLineLook = config.Bind(
                "Fixes",
                "PreferMooredDockLineLook",
                true,
                "Click a mooring line on a dock cleat even when dock mesh or the boat-push collider is in front of it. Vanilla's look ray hits the quay first, so the tied knot cannot be picked up.");

            RightClickBoatMooringCastOff = config.Bind(
                "Fixes",
                "RightClickBoatMooringCastOff",
                true,
                "Right-click the boat end of a tied mooring line to cast off. Vanilla right-click picks up the coil (same as left-click) to pay the line in or out; left-click still does that.");

            HideInventoryOnNeedsWarning = config.Bind(
                "Fixes",
                "HideInventoryOnNeedsWarning",
                true,
                "Do not draw hotbar items on top of the hunger, thirst, or sleep bars. Vanilla scales those items with the whole needs UI, so they stack on the bars whenever a need flashes.");

            PourSoupIntoMug = config.Bind(
                "Fixes",
                "PourSoupIntoMug",
                true,
                "Pour soup from a pot into a mug or cup, then drink it. Vanilla only pours water into the pot and drinks from the pot itself; clicking an empty mug places it instead.");

            KeepChipLogDeployed = config.Bind(
                "Fixes",
                "KeepChipLogDeployed",
                true,
                "Do not reel the chip log in when waves lift the chip after it is in the water. Vanilla auto-returns whenever the bobber is airborne, including chop. The toss still reels until the chip hits the water so the line does not pay out in the air. Right-click still reels it in.");

            PreventBoatCacheSpawnLoop = config.Bind(
                "Fixes",
                "PreventBoatCacheSpawnLoop",
                true,
                "When a boat comes back into range, skip saved items whose prefab is missing instead of throwing. Vanilla throws mid-spawn and retries every frame, duplicating lanterns and other items already created.");

            KeepMirageMountainMap = config.Bind(
                "Fixes",
                "KeepMirageMountainMap",
                true,
                "Keep the small Mirage Mountain village chart (prefab 165) after you leave the island. Vanilla parents it to island scenery, so sailing away hides it, can destroy it as out of range, then respawn a copy and fail to save.");

            SuppressBogusSailSnap = config.Bind(
                "Fixes",
                "SuppressBogusSailSnap",
                true,
                "Do not play the sail-snap crash or jerk the hull on floating-origin shift, sleep, or load. Vanilla sail hinges stay dynamic while only the boat rigidbody is shifted, so a lateen (especially the Big Dhow) slams the hull. A real gybe still snaps.");

            SuppressBogusSailSnapLog = config.Bind(
                "Fixes",
                "SuppressBogusSailSnapLog",
                true,
                "Log origin-shift sail freeze, muted snaps, skipped wind force, and allowed real gybes to BepInEx/LogOutput.log. Also plays a UI beep when the world origin actually jumps. Turn off after testing.");
        }
    }
}
