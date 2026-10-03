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

### Player housing despawns all items when you go too far away

- **Reported:** 2026-10-02
- **Area:** save and load / player housing
- **Steps:** Leave items in a player house, then travel far away from it.
- **Expected:** The items stay in the house and are there when you come back.
- **Actual:** All items in the house despawn.
- **Notes:** Not investigated yet. Open questions: does it happen at a set distance, do the items come back after a save and reload, and is it vanilla or caused by a mod? Vanilla parents an item to a house's save index when it enters the house (`ShipItem.EnterHouse`); compare with how boats cache their items out of range (`BoatLocalItems`), and check whether vanilla's out-of-range destroy also hits house items.

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
