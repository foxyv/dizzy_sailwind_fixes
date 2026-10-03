# Bug Tracker

Bugs to fix later. Add new entries at the top of **Open**, and move an entry to **Fixed** once its fix ships.

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

## Fixed

None yet.
