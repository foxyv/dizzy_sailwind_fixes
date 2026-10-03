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

### Player housing despawns all items when you go too far away

- **Reported:** 2026-10-02
- **Source:** Vanilla bug (also reported without mods), so a candidate for a new fix in this mod.
- **Area:** save and load / player housing
- **Steps:** Leave items in a player house, then sail more than 1 km away from it.
- **Expected:** The items stay in the house and are there when you come back.
- **Actual:** All items in the house despawn once you're past about 1 km, and a save and restart does not bring them back. The items are lost.
- **Notes:** Not fixed yet.
    - **How vanilla handles it:** houses use the same caching as boats (`BoatLocalItems` has a `houseParentIsland` field for them). Past 1000 m, `BoatHorizon` clears `closeToPlayer`, `BoatLocalItems` caches every item parented to the house, sets their save parent to -2, and `ShipItem.ProcessSaveable` destroys them. Back in range, `BoatLocalItems.Update` respawns the cache, but only once `IslandLoaded()` reports the house's island scene loaded. `SaveLoadManager` saves every cached list and loads them back into the house's cache, so a restart should restore the items.
    - **Where to start:** reproduce in vanilla or with the mod and read Unity's `Player.log` for the house's `Caching out of range` and `spawning cached items` lines. They show whether the items were cached at all and whether the respawn ever ran.
    - **Leads:** the island scene never reports loaded, so `IslandLoaded()` blocks the respawn; the house's old item objects aren't destroyed when cached (for example, inactive with the island), so the -2 parent leaves them half-saved; or the items are parented to island scenery instead of the house and unload with the island scene (as with the Mirage Mountain chart).
    - **Our code:** `PreventBoatCacheSpawnLoop` (`BoatCacheSpawnPatches.cs`) replaces `SpawnCachedItems` and skips cached items whose instance ID is still registered (`AlreadySpawned`). It isn't the cause, since vanilla loses the items too, but a fix must keep that skip from dropping house items.

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

None yet.
