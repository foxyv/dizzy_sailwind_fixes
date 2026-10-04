# Bug Tracker

Bugs to fix later. Add new entries at the top of **Open**, and move an entry to **Fixed** once its fix ships. **Watch list** holds known risks nobody has reported yet; check it when a new bug comes in.

Copy this template for a new entry:

```markdown
### Short title

- **Reported:** YYYY-MM-DD
- **Area:** look and aim / held items / save and load / shops / sailing / UI
- **Steps:** what you did when it happened
- **Expected:** what should happen
- **Actual:** what happens instead
- **Notes:** how often, related fixes, ideas
```

## Open

### Fix descriptions are hard to understand

- **Reported:** 2026-10-03 (several users)
- **Area:** config / docs
- **Steps:** Read a fix's description in `com.dizzy.sailwind.fixes.cfg`, in the in-game ConfigurationManager, or in the README fixes table.
- **Expected:** A short, plain line on what the fix changes for the player, so users can decide whether to turn it off.
- **Actual:** Users report the descriptions are bad: hard to follow, and they don't explain what each fix does.
- **Notes:** The 32 descriptions in `FixesConfig.cs` average 44 words; the longest are `AlignPlacedItemToSurface` (92), `KeepChipLogDeployed` (78), `DropBigCratePastOtherCrates` (65), `KeepCrateContentsWithBoat` (61) and `KeepLoadedSailsUnfurled` (60). Many explain vanilla internals instead of player-visible behavior, e.g. "world-space TextMesh shares the transparent queue" (`KeepLookTextAboveSmoke`), "kinematic ... angularDrag" (`DampenItemRoll`), "ShiftSmoothly waits 100 physics ticks" (`SkipSmoothOriginShift`). The same text is repeated in the README table, so fix both together. Keep the internals in code comments; the description should say what the player sees with the fix on vs off. Ask the reporting users which fixes confused them most. Related: the mooring distance entry below (labeled feet, actually meters).

### Allow using items from the third-person view (C) with co-op and player model mods

- **Reported:** 2026-10-03
- **Source:** Feature idea; vanilla behavior with the SailwindCoop and SailwindPlayerModel mods.
- **Area:** look and aim / third-person camera
- **Steps:** With SailwindCoop and SailwindPlayerModel installed, press C to switch to the third-person view and try to use or pick up an item.
- **Expected:** Possibly allow aiming and using items from that view, since those mods make it a real third-person player view, not just a boat-steering camera.
- **Actual:** Items can't be targeted or used from that view.
- **Notes:** C is vanilla `BoatCamera` (the `CameraMode` toggle `MuteCameraModeSound` also patches). Vanilla `GoPointer.DoRaycast` clears the target whenever `BoatCamera.on`, and since 0.3.2 the look fixes follow the same rule through `LookRay.VanillaAims`. Allowing use would mean letting `DoRaycast` aim while `BoatCamera.on` (likely only when SailwindPlayerModel is installed), casting from the right point (the player model's view, not the orbiting camera), and dropping the `BoatCamera.on` check from `VanillaAims` in the same case. Check how SailwindCoop and SailwindPlayerModel handle C before deciding.

### Container or barrel highlights while placing an item on it

- **Reported:** 2026-10-03
- **Source:** Vanilla behavior; not caused by `AlignPlacedItemToSurface`.
- **Area:** look and aim / item placement
- **Steps:** Hold a pipe or quadrant (or any placeable item) over a crate, container or barrel to set it down.
- **Expected:** Only the placement preview shows; the surface you're placing onto doesn't light up.
- **Actual:** The container or barrel shows its look highlight the whole time you're placing.
- **Notes:** Vanilla keeps calling `Look()` on the surface while you hold an item over it (`GoPointer.DoRaycast`), and its place preview (`GoPointer.LateUpdate`) depends on that surface staying `pointedAtButton`, so `GoPointerButton.UpdateColor` draws the looked-at outline. Tables have no visible outline, so it only shows on items like crates and barrels. A fix would have to hide the outline without clearing `pointedAtButton`, e.g. a prefix on `UpdateColor` for the placement target (see how `DropBigCrateOutlinePatch` suppresses outlines). Decide first whether it applies only to pipes and quadrants or to every placed item.

### Mooring throw distance is labeled feet but is meters

- **Reported:** 2026-10-02
- **Area:** mooring / config
- **Steps:** Read the `RightClickNearestDockMooringFeet` description in `com.dizzy.sailwind.fixes.cfg` or the README fixes table.
- **Expected:** The label matches the unit the value is used in.
- **Actual:** The config and README say "in-game feet (world units)", but `DockMooringSnap.Range` uses the value directly as world units. Sailwind's world units are meters (Unity's convention; 9,000 units per degree of latitude, about 1.8 units of arm reach), so the default 15 is about 15 m, not 15 ft.
- **Notes:** Fix the wording in `FixesConfig.cs` and `README.md`. Renaming the key (for example to `RightClickNearestDockMooringMeters`) would orphan the value in existing configs, so either keep the key and fix only the text, or migrate the old value.

### Items from an open crate get added to a nearby stove

- **Reported:** 2026-10-02
- **Area:** stoves / crates
- **Steps:** Place a crate next to a stove, then open the crate.
- **Expected:** The crate's items stay in the crate's grid.
- **Actual:** Items in the crate grid that overlap the stove are sometimes added to the stove.
- **Notes:** Happens only sometimes. Not investigated yet.

## Watch list

### Items thrown out of a player house are never cleaned up

- **Noted:** 2026-10-03 (review of the 0.3.3 `KeepHouseItemsWhenAway` fix; not expected to be a problem)
- **Area:** player housing / item cleanup
- **Possible symptoms:** junk piles up outside a player house over a long game, or an item thrown out of a house into the water is still floating there sessions later. Save files grow slightly.
- **Why it can happen:** vanilla `ShipItem.ExitHouse` only clears an item's house parent if the item is held when it leaves the house trigger. An item thrown, knocked, rolled or kicked out keeps the house as its save parent. Before 0.3.3, vanilla's 600 m range destroy deleted it anyway; `KeepHouseItemsWhenAway` now protects every unheld item whose save parent is a house, so it stays (frozen while you're away, cached and restored by the house at 1000 m). Dock trash and items dropped off a boat are not affected: leaving the boat's embark trigger runs `ExitBoat()` and resets the parent to -1, so they're still deleted at 600 m.
- **If a bug points here:** in `HouseItems.BelongsToSaveableObject` (`HouseItemsPatches.cs`), when the save parent is a house, only protect the item while it is inside that house's trigger collider (check its real shape, e.g. `ClosestPoint`). Leave boat items as they are.

### Trade book can count a good that left the warehouse yard without a trigger exit

- **Noted:** 2026-10-03 (modcheck plan step 8 review)
- **Area:** shops and trade
- **Possible symptoms:** the trade book lists more crates or barrels than are in the yard, or selling through it destroys a crate that is no longer there (for example one you've carried back to your boat).
- **Why it can happen:** vanilla tracks the yard with `OnTriggerEnter`/`OnTriggerExit` on `IslandMarketWarehouseArea`. Unity doesn't fire `OnTriggerExit` when a collider is disabled or switched to a trigger, so a good can leave without being removed. Our `WarehouseSync.Validate` (`TradeBookFailedSalePatches.cs`) prunes destroyed goods and adds goods the trigger missed, but keeps listed goods that still exist even if they no longer overlap the yard.
- **If a bug points here:** in `Validate`, also drop listed goods that no longer overlap the trigger, using the existing `Overlaps(col, hit)` check against each good's collider.

### Look fixes can target things behind walls, decks or hulls

- **Noted:** 2026-10-03 (modcheck SW501; plan step 7, marked won't fix)
- **Area:** look and aim
- **Possible symptoms:** a stove item, stove fuel, a lantern on a lamp hook, or a shelf or drying rack (while holding food or firewood) gets highlighted or clicked through a wall, deck, hull or terrain. It could also show up as the wrong target winning when something solid sits between you and it.
- **Why it can happen:** the stove (`StoveHoverFlickerPatches.cs`), lamp hook (`OccupiedHookHoverPatches.cs`) and held food/firewood (`HeldItemLookPatches.cs`) look fixes walk every hit from `LookRay.Cast` and pick the best target by their own rules, without stopping at the nearest solid collider. Vanilla's look ray also skips layers 12 (OnlyPlayerCol+Paintable) and 19 (IgnoreSmallItems), so vanilla itself targets tables and stoves through walls on those layers. That part is vanilla and players don't mind it.
- **Not affected:** the dropped anchor and mooring line fixes look through ground and dock mesh on purpose. `SittingItemLook` already only accepts items in front of vanilla's first hit.
- **If a bug points here:** the dropped step 7 approach was a `LookRay.BehindBlocker(ray, distance)` check. It finds the nearest non-trigger hit with no `GoPointerButton` on it or its parents (walls, decks, hulls, terrain; items and furniture never block), then each fix skips targets more than 5 cm past it. That only covers walls on layers the look ray hits. Walls on layers 12 and 19 would need a second cast with those layers added, but not Player (11), BoatCapsule (13) or invis (16), which would block everything on a boat or at a dock. Consider making it an optional, off-by-default fix.

## Fixed

### Player housing despawns all items when you go too far away

- **Reported:** 2026-10-02
- **Fixed:** 2026-10-03 in 0.3.3 (`KeepHouseItemsWhenAway`, `HouseItemsPatches.cs`)
- **Source:** Vanilla bug (also happens without mods).
- **Area:** save and load / player housing
- **Steps:** Leave items in a player house, then sail away from it. A save and reload near the house does not trigger it.
- **Actual (before the fix):** All items in the house are gone when you come back, and a save and reload does not bring them back.
- **Cause:** `ItemRigidbody.FixedUpdate` checks each item's distance from the camera every 5-8 s and destroys any sold item more than 600 m away that is not on a boat walk collider (`currentWalkCol == null`), logging "is out of range and not on boat, destroying!". House items are never on a walk collider, so they were deleted at 600 m, before the house's own `BoatLocalItems` could cache them at 1000 m. The six houses ("house trigger (201)" to "(206)" in `level24`) each have a `BoatLocalItems` and `BoatHorizon` like a boat.
- **Fix:** a prefix on `ItemRigidbody.FixedUpdate` keeps `framesUntilDestroy` at 0 for sold items whose save parent is a saveable object (index > 0) and that are not on a walk collider, so vanilla only freezes them while out of range. Past 1000 m the house or boat caches them as usual.
- **Also found:** the same 600 m destroy hit items that belong to a boat but aren't on its walk collider (14 items on the small dhow in the test, including a table, lantern, barrel and oar). The fix keeps those too, and the boat then caches them at 1000 m.
- **Verified:** playtest on the Dizzy Sept 2026 pack, 0.3.3: items in house 201 stayed after sailing 1+ km away and back, and after a save and reload while away. The BepInEx log showed 35 house items and 14 dhow items kept.

