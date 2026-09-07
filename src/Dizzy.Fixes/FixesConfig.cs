using BepInEx.Configuration;

namespace Dizzy.Fixes
{
    internal static class FixesConfig
    {
        internal static ConfigEntry<bool> PreventInventoryWithdrawSip;
        internal static ConfigEntry<bool> PreventFailedTradeBookSale;
        internal static ConfigEntry<bool> SkipUncleanableHullCleaning;
        internal static ConfigEntry<bool> KeepMooredBoats;
        internal static ConfigEntry<bool> StabilizeStoveItemHover;
        internal static ConfigEntry<bool> PreferHangingItemOnHook;
        internal static ConfigEntry<bool> MuteCameraModeSound;
        internal static ConfigEntry<bool> PreferDroppedAnchorLook;
        internal static ConfigEntry<bool> RightClickNearestDockMooring;
        internal static ConfigEntry<float> RightClickNearestDockMooringFeet;
        internal static ConfigEntry<bool> PreferMooredDockLineLook;

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

            KeepMooredBoats = config.Bind(
                "Fixes",
                "KeepMooredBoats",
                true,
                "Keep dock lines and anchors from resetting when you leave a port and come back. Vanilla sleeps distant boats and, on wake, reties springs at the drifted distance and can pop the anchor.");

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
        }
    }
}
