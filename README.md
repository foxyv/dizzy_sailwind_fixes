# Dizzy Sailwind Fixes

BepInEx 5 plugin for [Sailwind](https://store.steampowered.com/app/1764530/Sailwind/) that collects small gameplay and QoL fixes. Each fix is a Harmony patch and can be toggled in config.

This repo follows the same plugin layout as [dizzy_sailwind_mods](https://github.com/foxyv/dizzy_sailwind_mods) (Dizzy Gamma / Dizzy Calendar).

Compatible with [ModVersionChecker](https://github.com/bryon82/SailwindModVersionChecker) when installed (optional; not a dependency). GitHub release tags must stay `vX.Y.Z` matching `PluginVersion`.

## Build

```powershell
dotnet build src\Dizzy.Fixes\Dizzy.Fixes.csproj -c Release
```

A successful build copies `Dizzy.Fixes.dll` to `BepInEx\plugins\Dizzy.Fixes\` in the Sailwind install (`D:\SteamLibrary\steamapps\common\Sailwind` by default). Override with:

```powershell
dotnet build src\Dizzy.Fixes\Dizzy.Fixes.csproj -c Release -p:SailwindDir=C:\path\to\Sailwind
```

Skip deploy: `-p:DeployOnBuild=false`

## Fixes

| Fix | Default | What it changes |
|-----|---------|-----------------|
| `PreventInventoryWithdrawSip` | on | Pulling a bottle or soup bowl from an inventory slot (number keys 1–5) no longer takes a sip. Vanilla does that because the item appears on top of the mouth collider while the inventory UI is collapsed. Holding drink still sips on the first press. |
| `PreventFailedTradeBookSale` | on | Selling a commodity in the trade book no longer drops the market price (or pays gold) unless a crate/barrel is actually removed from the warehouse. Vanilla updates supply first, then can fail to find the physical good after a long session. |
| `SkipUncleanableHullCleaning` | on | Cleaning a boat that cannot get dirty (e.g. Jong) no longer charges a hull-cleaning fee or aborts the rest of the shipyard order. Vanilla can take the money, throw, and revert sails/parts when you leave. |
| `StabilizeStoveItemHover` | on | Hovering food or a pot on a stove no longer flickers between the stove and the item. Vanilla keeps the first look-ray hit, so the outline swaps every physics frame while you cook. |
| `PreferHangingItemOnHook` | on | Looking at a lantern (or other hangable) on a lamp hook selects the hanging item, not the hook. Vanilla hits the hook collider first, so a locked (nailed) hook keeps focus and you cannot click the item. |
| `MuteCameraModeSound` | on | Toggling third-person boat camera (C) no longer plays the UI click. Vanilla plays `buttonClick` when entering and leaving that view. |
| `PreferDroppedAnchorLook` | on | Looking at a dropped or set anchor still selects it when it is sitting in or under the ground. Vanilla's look ray hits terrain first, especially after placing the hook by hand on land, so you cannot click it. |
| `RightClickNearestDockMooring` | on | Right-click while holding a mooring line throws it to the nearest free dock cleat. Vanilla left-click needs a precise 1.8 m look on the post, which dock mesh often blocks (especially Gold Rock). |
| `RightClickNearestDockMooringFeet` | 15 | How far (in in-game feet) that right-click will search for a free cleat. |
| `PreferMooredDockLineLook` | on | Looking at a mooring line tied to a cleat still selects the line when dock mesh or the boat-push collider is in front of it. Vanilla's look ray hits the quay first, so you cannot click the knot. |
| `RightClickBoatMooringCastOff` | on | Right-click the boat end of a tied mooring line to cast off, with the same pickup sound as unmooring the dock knot. Vanilla right-click picks up the coil (same as left-click) to pay the line in or out; left-click still does that. |
| `HideInventoryOnNeedsWarning` | on | Hunger, thirst, and sleep warnings show only the status bars. Vanilla scales hotbar items with the whole needs UI, so they stack on the bars whenever a need flashes. |
| `PourSoupIntoMug` | on | Click an empty mug or cup on a soup pot to fill it, then drink. Leftover fractional soup can still be drunk, dumped, or emptied onto the pot. Vanilla only pours water into the pot and drinks from the pot; an empty mug is placed instead. Vanilla mugs only spill at exactly 1, 2, or 3 units, so a 0.4 cup would otherwise lock. |
| `KeepChipLogDeployed` | on | After the chip log has been in the water, waves that lift it pay the line out instead of winding it in. Until you throw a newly bought log, the bobber stays on the reel so the line does not run out to the stall with no speed reading until you reload. The toss can still reel in the air. Right-click still winds it back. |
| `PreventBoatCacheSpawnLoop` | on | Coming back to a boat skips saved items with a missing prefab instead of throwing. Vanilla throws mid-spawn and retries every frame, duplicating lanterns and other items already created. |
| `KeepMirageMountainMap` | on | The small chart in the buried village north of Mirage Mountain stays in inventory after you leave. Vanilla parents it to island scenery, so sailing away hides it, can destroy it as out of range, then respawn a copy and fail to save. |
| `SuppressBogusSailSnap` | on | Origin shift, sleep, and load no longer play a sail-snap crash. Vanilla only shifts the boat rigidbody, so a lateen hinge can slam and play the gybe sound. A real gybe still snaps. |
| `KeepLoadedSailsUnfurled` | on | Restore each purchased boat's reef (furled / unfurled / partial) from the save. Vanilla does not store reef length, so load used to unfurl every sail then furl every sail. New games still start furled. Older saves need one new save after you set the sails. |
| `DampenItemRoll` | on | Dropped bottles, mugs, fruit, and other capsule items stop rolling on a desk or deck instead of traveling a long way, and stay put after sleep or origin-shift. Vanilla only settles mesh-collider items when they sleep; capsules keep a very low angular drag and wake again. |
| `AlignPlacedItemToSurface` | on | Pipes and quadrants sit flush on the looked-at surface and follow it while the boat moves. Quadrants rest on the sighting beam; right-click while placing stands them on edge and they stay that way when dropped. While placing a pipe, scroll yaws it on the table and Q flips it over. Other items keep vanilla placement (camera rotation and world-up lift). |
| `KeepLookTextAboveSmoke` | on | Look and hold item text stays in front of pipe smoke. Vanilla world-space TextMesh shares the transparent queue with particles, and exhale smoke sits closer to the camera so it covers the label. |
| `HammerNailSeconds` | 1 | How long (seconds) to hold right-click to nail an item. Vanilla is 2. Lower is faster. Unlock is still instant. |
| `KeepMerchantSellScroll` | on | The merchant sell parchment stays open while you hold the item nearby, and follows the closer merchant when you walk between stalls. Vanilla closes it when the held item leaves the shopkeeper trigger, and will not open a second merchant while the first parchment is still up. |
| `PreferSittingItemLook` | on | A mug, pipe, or other small item sitting on a desk, crate, or deck stays selectable when the look ray hits that surface first. Vanilla keeps the first collider, so a settled or flush-placed item is often only clickable from one side. A direct hit stays on that item. When several items are close, the one under the crosshair wins over a nearer neighbor. |
| `SkipOtherFoodWhileHolding` | on | While holding food, the look ray skips other food on a shelf or drying rack so you can place what you're holding. Vanilla highlights those items, and clicking them picks up another apple instead of the rack. |
| `PreferCrateInventorySlot` | on | While a container's inventory is open, the grid blocks the look ray. The slot under the crosshair wins, including the corners between slot centers, so a click cannot place through the panel onto whatever is behind it. |
| `ReleaseDestroyedHeldItem` | on | Eating or otherwise destroying a held item lets go of it. Vanilla leaves the pointer and the stall's last-bought record on the destroyed object, so the look ray still treats you as holding food and the stall will not sell another apple until a different inventory item is picked up and put back. |
| `SkipSmoothOriginShift` | on | Crossing the 512 m floating-origin boundary no longer stalls ~2 seconds (wake fade / `waitingForShift`) before the world teleports. Vanilla `ShiftSmoothly` waits 100 physics ticks then moves 512 m, which feels like freeze-then-jerk. The teleport still happens in one frame. |
| `KeepCrateContentsWithBoat` | on | Crate contents stay with the crate's boat. Unsealing a firewood or hook box used to save those pieces as loose world objects, so a reload away from that boat dropped them. An overfull crate still opens; extra pieces stay inside with no square until something is removed. |
| `DropBigCratePastOtherCrates` | on | While carrying a two-handed item, the look ray ignores other objects so left-click can drop it. Vanilla highlights the next object, and a highlighted object blocks the drop. |
| `DelayBoatCacheSpawn` | off | Debug only. After each load, a boat's loose items appear first and its crates wait several frames. Crate contents then miss the crate and fall out. Leave this off while playing. |

Toggles live in `BepInEx\config\com.dizzy.sailwind.fixes.cfg`.

## Adding a fix

1. Bind a `ConfigEntry<bool>` in `FixesConfig` (default **on**, describe the vanilla behavior it changes).
2. Add a Harmony patch class under `src/Dizzy.Fixes/Patches/`.
3. Return early from the patch when the toggle is off.
4. Keep `PluginVersion` in `Plugin.cs` in sync with `<Version>` in the `.csproj`.

Inspect game types in ILSpy against `Sailwind_Data\Managed\Assembly-CSharp.dll`. Do not commit game assemblies.

## Package

```powershell
.\scripts\package-release.ps1
```

Install layout inside the zip:

```
Dizzy.Fixes/
  Dizzy.Fixes.dll
```

Copy the `Dizzy.Fixes` folder into `BepInEx\plugins\`.
