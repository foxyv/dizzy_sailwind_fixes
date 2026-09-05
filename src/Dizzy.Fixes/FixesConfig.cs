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
        }
    }
}
