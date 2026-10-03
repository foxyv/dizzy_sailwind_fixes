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
        internal static ConfigEntry<float> RightClickNearestDockMooringArcDegrees;
        internal static ConfigEntry<bool> PreferMooredDockLineLook;
        internal static ConfigEntry<bool> RightClickBoatMooringCastOff;
        internal static ConfigEntry<bool> HideInventoryOnNeedsWarning;
        internal static ConfigEntry<bool> PourSoupIntoMug;
        internal static ConfigEntry<bool> KeepChipLogDeployed;
        internal static ConfigEntry<bool> PreventBoatCacheSpawnLoop;
        internal static ConfigEntry<bool> KeepMirageMountainMap;
        internal static ConfigEntry<bool> SuppressBogusSailSnap;
        internal static ConfigEntry<bool> KeepLoadedSailsUnfurled;
        internal static ConfigEntry<bool> DampenItemRoll;
        internal static ConfigEntry<bool> AlignPlacedItemToSurface;
        internal static ConfigEntry<bool> KeepLookTextAboveSmoke;
        internal static ConfigEntry<float> HammerNailSeconds;
        internal static ConfigEntry<bool> KeepMerchantSellScroll;
        internal static ConfigEntry<bool> PreferSittingItemLook;
        internal static ConfigEntry<bool> SkipOtherFoodWhileHolding;
        internal static ConfigEntry<bool> SkipOtherFirewoodWhileHolding;
        internal static ConfigEntry<bool> ReleaseDestroyedHeldItem;
        internal static ConfigEntry<bool> SkipSmoothOriginShift;
        internal static ConfigEntry<bool> KeepCrateContentsWithBoat;
        internal static ConfigEntry<bool> DropBigCratePastOtherCrates;
        internal static ConfigEntry<bool> KeepMissionListPage;
        internal static ConfigEntry<bool> DelayBoatCacheSpawn;

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
                "Right-click while holding a mooring line throws it to a free dock cleat in front of you. Vanilla left-click needs a 1.8 m look on the tiny post, which dock mesh often blocks.");

            RightClickNearestDockMooringFeet = config.Bind(
                "Fixes",
                "RightClickNearestDockMooringFeet",
                15f,
                "How far (in feet) right-click will search for a free dock cleat while holding a mooring line. Default 15. This is in-game feet (world units), not a real-world metric conversion.");

            RightClickNearestDockMooringArcDegrees = config.Bind(
                "Fixes",
                "RightClickNearestDockMooringArcDegrees",
                10f,
                "How wide (in degrees) the forward arc is when throwing a mooring line. Only a free cleat inside that arc can be hit, and the one closest to the middle of the arc wins. Default 10 is the full width, 5 degrees either side of where you are facing.");

            PreferMooredDockLineLook = config.Bind(
                "Fixes",
                "PreferMooredDockLineLook",
                true,
                "Click a mooring line on a dock cleat even when dock mesh or the boat-push collider is in front of it. Vanilla's look ray hits the quay first, so the tied knot cannot be picked up.");

            RightClickBoatMooringCastOff = config.Bind(
                "Fixes",
                "RightClickBoatMooringCastOff",
                true,
                "Right-click the boat end of a tied mooring line to cast off. Plays the same pickup sound as unmooring the dock knot. Vanilla right-click picks up the coil (same as left-click) to pay the line in or out; left-click still does that.");

            HideInventoryOnNeedsWarning = config.Bind(
                "Fixes",
                "HideInventoryOnNeedsWarning",
                true,
                "Do not draw hotbar items on top of the hunger, thirst, or sleep bars. Vanilla scales those items with the whole needs UI, so they stack on the bars whenever a need flashes.");

            PourSoupIntoMug = config.Bind(
                "Fixes",
                "PourSoupIntoMug",
                true,
                "Pour soup from a pot into a mug or cup, then drink it. Leftover fractional soup (vanilla mugs only spill at 1, 2, or 3 units) can still be drunk, dumped, or poured back. Vanilla only pours water into the pot and drinks from the pot itself; clicking an empty mug places it instead.");

            KeepChipLogDeployed = config.Bind(
                "Fixes",
                "KeepChipLogDeployed",
                true,
                "When waves lift the chip log after it has been thrown and the bobber has already been in the water, pay the line out instead of winding it in. Vanilla auto-returns whenever the bobber is airborne, including chop. Until you throw a log, the bobber stays on the reel. A bobber left behind in the world, including on the Fort Aestrin log, is pulled back. The toss can still reel in the air. Right-click still reels it in.");

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
                "Do not play the sail-snap crash on floating-origin shift, sleep, or load. Vanilla sail hinges stay dynamic while only the boat rigidbody is shifted, so a lateen can slam and play the gybe sound. A real gybe still snaps. Does not freeze sail physics (that overwrote boom angle and broke save/load).");

            KeepLoadedSailsUnfurled = config.Bind(
                "Fixes",
                "KeepLoadedSailsUnfurled",
                true,
                "Remember each purchased boat's reef (furled / unfurled / in between) in the save and restore it on load. Vanilla has no reef field, so LoadGame unfurls every sail then furls every sail during the disclaimer. New games still start furled. Saves made before this fix have no reef data yet: set the sails how you want, then save once.");

            DampenItemRoll = config.Bind(
                "Fixes",
                "DampenItemRoll",
                true,
                "Dropped bottles, mugs, fruit, and other capsule items stop rolling on a desk or deck instead of traveling a long way, and stay settled after sleep or origin-shift. Vanilla only settles mesh-collider items to kinematic when they sleep; capsules keep a very low angularDrag and the next physics tick wakes them.");

            AlignPlacedItemToSurface = config.Bind(
                "Fixes",
                "AlignPlacedItemToSurface",
                true,
                "Sit a pipe or quadrant flush on the looked-at surface instead of tilting with the camera and lifting along world up. Other items keep vanilla placement. Quadrants rest on the sighting beam; right-click while placing stands them on edge and that pose stays when dropped. While placing a pipe, scroll turns it on the table and Q flips it over. The preview follows the table while the boat moves. Vanilla copies the pointer rotation and offsets with Vector3.up, so a walk mesh that is not world-aligned leaves the item at an angle.");

            KeepLookTextAboveSmoke = config.Bind(
                "Fixes",
                "KeepLookTextAboveSmoke",
                true,
                "Keep look and hold item text in front of pipe smoke. Vanilla world-space TextMesh shares the transparent queue with particles, and exhale smoke sits closer to the camera so it covers the label.");

            HammerNailSeconds = config.Bind(
                "Fixes",
                "HammerNailSeconds",
                1f,
                new ConfigDescription(
                    "How long (seconds) to hold right-click to nail an item. Vanilla is 2. Lower is faster. Unlock is still instant.",
                    new AcceptableValueRange<float>(0.05f, 10f)));

            KeepMerchantSellScroll = config.Bind(
                "Fixes",
                "KeepMerchantSellScroll",
                true,
                "Keep the merchant sell parchment open while you hold the item nearby, and move it to the closer merchant when you walk between stalls. Vanilla closes it when the held item leaves the shopkeeper trigger, and will not open a second merchant while the first parchment is still up.");

            PreferSittingItemLook = config.Bind(
                "Fixes",
                "PreferSittingItemLook",
                true,
                "Click a small item anywhere you can see it, including a mug handle, the quadrant arc, a compass rim, a kettle spout, or the pipe mouthpiece. In vanilla, aiming at those parts selects the table behind the item or nothing at all.");

            SkipOtherFoodWhileHolding = config.Bind(
                "Fixes",
                "SkipOtherFoodWhileHolding",
                true,
                "While holding food, the look ray skips other food on a shelf or drying rack so you can place what you're holding. Vanilla highlights those items, so the rack never stays targeted.");

            SkipOtherFirewoodWhileHolding = config.Bind(
                "Fixes",
                "SkipOtherFirewoodWhileHolding",
                true,
                "While holding a piece of firewood, the look ray skips other pieces so left click can set it down. Vanilla highlights the next piece, and a highlighted object blocks the drop.");

            ReleaseDestroyedHeldItem = config.Bind(
                "Fixes",
                "ReleaseDestroyedHeldItem",
                true,
                "Eating or otherwise destroying a held item lets go of it. Vanilla leaves the pointer and the stall's last-bought record on the destroyed object, so the look ray still treats you as holding food and the stall will not sell another apple until a different inventory item is picked up and put back.");

            SkipSmoothOriginShift = config.Bind(
                "Fixes",
                "SkipSmoothOriginShift",
                true,
                "Do not stall ~2 seconds (wake fade + waitingForShift) before a floating-origin teleport. Vanilla ShiftSmoothly waits 100 physics ticks then moves the world 512 m, which feels like freeze-then-jerk. The shift still happens in one frame and still prepares/restores boat momentum.");

            KeepCrateContentsWithBoat = config.Bind(
                "Fixes",
                "KeepCrateContentsWithBoat",
                true,
                "Keep crate contents on the same boat as the crate. Unsealing a firewood or hook box never gives those pieces a boat save parent, so a reload away from that boat spawns them in the world, they look for the crate once, and they fall out. Opening a crate with more pieces than squares no longer throws and drops the rest.");

            DropBigCratePastOtherCrates = config.Bind(
                "Fixes",
                "DropBigCratePastOtherCrates",
                true,
                "While carrying a two-handed item, the look ray ignores other objects so left-click can drop it, except an object that item can use, the merchant sell button, and boat ladders. Climbing a ladder keeps the carried item. A held barrel can still be clicked on another barrel of the same liquid to refill. Vanilla highlights the next object, and a highlighted object blocks the drop.");

            KeepMissionListPage = config.Bind(
                "Fixes",
                "KeepMissionListPage",
                true,
                "Accepting a port mission keeps the mission list on the current page. The accepted mission drops out and later missions shift into that page. Vanilla reloads page 1 while the page number stays where it was.");

            DelayBoatCacheSpawn = config.Bind(
                "Debug",
                "DelayBoatCacheSpawn",
                false,
                "Debug only. After each load, spawn a boat's loose items first and hold its crates for several frames. Crate contents look for the crate once and miss it, so they fall out. Leave this off while playing.");
        }
    }
}
