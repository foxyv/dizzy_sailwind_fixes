# Sailwind Game Notes

What we learned about vanilla Sailwind while building Dizzy Sailwind Fixes (Unity 2019.1, Mono, `Assembly-CSharp`). Collected 2026-10-03 for updating the Sailwind modding skills.

Sources are noted per section: **decompile** = `tools/inspect` against `Sailwind_Data/Managed/Assembly-CSharp.dll`; **data** = values read from the game's asset files with UnityPy; **playtest** = observed in game. Anything not verified is marked.

## Scale, coordinates and time

Source: decompile (`FloatingOriginManager`, `Sun`), data (`Sun` in `level24`).

| Quantity | Value | Where it comes from |
| --- | --- | --- |
| World unit | 1 m | Unity convention; fits the 1.8-unit look reach |
| 1 degree of latitude or longitude | 9,000 units (9 km) | `GetGlobeCoords` = `(position - outCurrentOffset - globeOffset) / 9000` |
| Latitude / longitude axes | z = latitude, x = longitude | `Sun` tilts the sky by `-z` and sets local time from `x / 15` |
| World origin | 36° latitude | `globeOffset = (0, 0, -36) * 9000` |
| Map shape | Flat | No cosine term, so a degree of longitude is 9 km everywhere |
| 1 nautical mile (1 minute of arc) | 150 units | 9,000 / 60 |
| Time speed | 0.008 game hours per real second | `Sun.timescale` (scene value; `initialTimescale` is copied from it in `Start`) |
| 1 game hour | 125 real seconds | 1 / 0.008 / 3600 |
| 1 game day | 3,000 real seconds (50 min) | `Sun.GetRealtimeDayLength()` = `24 / timescale` |
| Time-skip sleep | 9x speed | `Sun.Update`: `Sleep.timeskipSleep` → `timescale = initialTimescale * 9` |
| 1 in-game knot | 1.2 m/s (about 2.33 real knots) | 150 m per 125 s |

- **Local time:** `localTime = globalTime + x / 15`, so each degree of longitude is 4 minutes, as in reality. `globalTime` past 24 increments `GameState.day`, calls `DayLogs.instance.NewDaySheets()` and fires `Sun.OnNewDay`.
- **Dawn:** `dawnBorder = -0.147 * z + 10.853`, clamped to [6.16, 6.8], when `dawnBorderFromLatitude` is on.
- **`Speedometer`** (debug display) shows `velocity * 1.944` "knots" and `* 3.6` km/h, which are real-world units in world units per real second, not in-game knots.

## Floating origin

Source: decompile (`FloatingOriginManager`).

- **When it shifts:** when the shifter object passes `shiftDistance` from the origin on x or z, the world moves back by one `shiftDistance` step. `shiftDistance` is a scene value; our `SkipSmoothOriginShift` comment says 512 m (not re-verified this session).
- **Smooth shift:** `ShiftSmoothly(x, z)` fades boat wakes over `smoothShiftFrames` (50) fixed updates with `GameState.waitingForShift` set, then calls private `Shift(int x, int z)`. The wait is the 1-2 s freeze the fix removes.
- **Shift itself:** `Shift` starts `NewShift`, which calls `PrepareForShifting` / `RestoreMomentum` on registered `ShiftingRigidbody`s. Private field `shiftingSmoothly` guards re-entry.
- **Offset:** `outCurrentOffset` holds the accumulated shift; save positions subtract it.

## Look and aim (`GoPointer`)

Source: decompile (`GoPointer.DoRaycast`, `GoPointerButton`), data (layer names from `TagManager` in `globalgamemanagers`).

- **Look ray:** `Physics.Raycast(ray, out hit, 1.8f, -604165)`, with the global trigger setting. The ray is `raycastRay`, or the mouse ray when `debugEditorPointer` is set. **F3** logs the hit collider's name.
- **When vanilla doesn't aim:**
  - With a mouse crosshair pointer (`PointerType.crosshairMouse`) in a cursor menu (`GameState.inCursorMenu`), `DoRaycast` returns early and **keeps** the current target.
  - While `GameState.sleeping`, `GameState.inBed` or `BoatCamera.on`, it **clears** the target.
- **Target rules:**
  - A collider tagged `ItemSubcollider` resolves to its parent's collider.
  - Buttons with `unclickable` are skipped.
  - Holding an item ignores the held item itself. Pointing at another item needs `heldItem.AllowOnItemClick(button)`, unless the target isn't a `ShipItem` and isn't a bed.
  - Nailed items are only targetable when their type is exactly `ShipItemCrate`, `ShipItemBottle` or `ShipItemBed`.
- **Placement preview** (`GoPointer.LateUpdate`): when the held item isn't `big` and `pointedAtButton.allowPlacingItems`, the item is positioned at `transform.forward * currentLookDistance + Vector3.up * furniturePlaceHeight`. The surface stays `pointedAtButton` and keeps getting `Look()`, so a container or barrel you place onto shows its look outline.
- **Private members mods use:** `pointedAtButton` (`GoPointerButton`), `hit` (`RaycastHit`), `heldItem`, `currentLookDistance`, `raycastRay`, `debugEditorPointer`, and the method `bool AltButtonHeld()`.

### Layers

| # | Name | Look ray |
| --- | --- | --- |
| 0 | Default | hits |
| 1 | TransparentFX | hits |
| 2 | Ignore Raycast | **skipped** |
| 4 | Water | hits |
| 5 | UI | hits |
| 8 | WalkCols | hits |
| 9 | Clouds1 | hits |
| 10 | Clouds2 | hits |
| 11 | Player | **skipped** |
| 12 | OnlyPlayerCol+Paintable | **skipped** (many walls; vanilla targets through them) |
| 13 | BoatCapsule | **skipped** |
| 14 | TerrainDepth | hits |
| 15 | Painter | hits |
| 16 | invis | **skipped** (invisible helper colliders, e.g. the boat-push collider at docks) |
| 17 | NoPlayerLight | hits |
| 18 | SmallItems | hits |
| 19 | IgnoreSmallItems | **skipped** |
| 20 | ShipItemSubcollider | hits |
| 21 | SailColChecker | hits |
| 22 | FloatingHints | hits |
| 23 | WorldUI | hits |
| 24 | WindShadow | hits |
| 25 | Boat | hits |
| 26 | ItemInCrate | hits (items inside a crate) |
| 27 | ChartObjects | hits |

### Outlines (`GoPointerButton`)

- **Where outlines come from:** `GoPointerButton.Start` adds a `cakeslice.Outline` (in `Assembly-CSharp`) to its own GameObject. Other mods can add more, e.g. Dizzy.FirewoodBundle on bundle logs.
- **`UpdateColor` priority:**
  1. Clicked or sticky-clicked: color 0.
  2. `enableRedOutline && !forceDisableRedOutline`: red, color 2.
  3. `isLookedAt || overrideEnableOutline`: color 1.
  4. Flash timer running: color 0.
  5. Otherwise: disabled.

## Item lifecycle and saving

Source: decompile (`SaveablePrefab`, `ShipItem`, `ItemRigidbody`, `SaveLoadManager`), playtest.

### Save parent codes (`SaveablePrefab.GetParentObject()`)

| Value | Meaning |
| --- | --- |
| -1 | Loose in the world |
| > 0 | Belongs to a `SaveableObject` with that `sceneIndex`: boats (10-90) or player houses (201-206) |
| -2 | Cached by its boat or house when out of range; `ShipItem.ProcessSaveable` destroys it |
| -3 | On a sunk boat; destroyed during recovery, layer set to 2 |

- **Saving positions** (`PrepareSaveData`): relative to the parent object (`InverseTransformPoint`) when the parent is > 0, otherwise the world position minus `outCurrentOffset`.
- **Loading** (`SaveablePrefab.Load`): positions the item relative to its parent but does **not** set `transform.parent`. Boat and house triggers re-parent it later. It restores food, soup, kettle and chart data and registers it to save. If the item was in a hotbar slot, it returns there two frames later.
- **`ShipItem.OnLoad`** runs from the `LoadAfterDelay` coroutine after `WaitForEndOfFrame`, so one frame after the item spawns. Code in a `LoadGame` postfix runs before any item's `OnLoad`.
- **`ShipItem.DestroyItem`:** `ExitBoat()`, `SaveablePrefab.Unregister()`, then `Object.Destroy`. Vanilla calls it for sales, missions, the cache marker -2, recovery and the range destroy below.
- **`SaveLoadManager.SaveGame`:** saves every current prefab, then appends the cached list of every `SaveableObject` with `localItems.HasLocalItems()`. On load, a saved item with parent > 0 goes into that object's `BoatLocalItems` cache instead of spawning.
- **`GameState.modData`** (`Dictionary<string, string>`) is saved and loaded with the save; mods store their data there under their own key.

### Boats and houses

- **Embark:** an item inside a collider tagged `EmbarkCol` for a few frames calls `EnterBoat`, which sets the parent to the boat's `sceneIndex`, the transform parent to the boat, and `currentWalkCol`.
- **Disembark:** `ExitBoat` runs once the item has left the embark collider for a few frames, unless sleeping, `disallowDisembarking` or `attached`. It doesn't need the item to be held, so trash thrown or dropped off a boat becomes a loose world item (-1). `OnEnterInventory` also calls `ExitBoat`.
- **House enter:** `OnTriggerEnter` with a collider tagged `House` calls `EnterHouse`, which sets the parent to the house's `sceneIndex` and the transform parent to the house.
- **House exit:** `ExitHouse` only clears the parent when the item is **held**. An item thrown, knocked or rolled out keeps the house as its parent.
- **`PlayerHouseEmbarker`:** sets `GameState.currentHouse` while the player is inside a `House` trigger.

### Range destroy (`ItemRigidbody.FixedUpdate`)

- **Distance check:** every 5-8 s (`distanceCheckTimer`), `outOfRange` = distance from `Camera.main` > **600 m**.
- **Destroy:** when `outOfRange && !GameState.recovering`, any `item.sold && item.currentWalkCol == null` item counts `framesUntilDestroy` up and is destroyed after 10 frames, unless it is on layer 26 (in a crate). It logs "is out of range and not on boat, destroying!".
- **What it catches:** loose world items as intended, but also house items and items that belong to a boat without standing on its walk collider. This was the player-housing bug, fixed by `KeepHouseItemsWhenAway`.
- **Kinematic (frozen) when any of these hold:**
  - not `GameState.playing`, recovering, sleeping, in bed, or in a shipyard
  - in a crate box or an inventory slot
  - `attached`, unsold, or nailed
  - the first 6 fixed frames after spawning
  - `outOfRange`
  - a mesh-collider item that is sleeping
- **Capsule items** never hit the sleeping branch, which is why they roll (`DampenItemRoll`).

### Boat and house item caching (`BoatLocalItems` + `BoatHorizon`)

- **`BoatHorizon.DistanceCheck`:** `closeToPlayer` while the distance to `Refs.observerMirror` is 1,000 m or less, or the object is `GameState.currentBoat`. When far, it rechecks only every 10-20 s.
- **Sinking does nothing:** `UpdateHorizonPos` calls `SetHeight`, whose body is empty, so boats and houses don't actually sink. `UpdateKinematic` makes the boat rigidbody kinematic when far, in a shipyard, loading, recovering and similar.
- **Registration:** `BoatHorizon.Awake` registers with a `BoatLocalItems` on its parent or itself.
- **Caching:** `BoatLocalItems.Update` caches when `!closeToPlayer && itemsLoaded && cache empty`. `CacheCurrentItems(-2)` stores `PrepareSaveData()` for every prefab whose parent is this `sceneIndex` and marks it -2, and the item then destroys itself.
- **Respawning:** when `closeToPlayer && IslandLoaded() && !itemsLoaded && cachedItems != null` and the rigidbody is null or kinematic, it calls `SpawnCachedItems` and sets `itemsLoaded`.
  - `IslandLoaded()` is true when `houseParentIsland <= 0`, otherwise when that scene build index is loaded.
- **Bookkeeping:** `SpawnCachedItems` instantiates each prefab, calls `Load`, clears the cache and runs the private `SetGamestate()` coroutine, which clears `GameState.loadingBoatLocalItems` after 2 frames plus 2 fixed updates.
- **Sinking boats:** `CacheItemsOnSinking` marks items -3 and drops trade goods at `Recovery.GetCargoLossChance()`.
- **Public helpers:** `HasLocalItems()`, `GetCachedItems()`, `AddCachedItemFromSaveData()`, `SetItemsLoaded(bool)`, `SafeToEnableBoatPhysics()`.
- **Vanilla bug:** if `PrefabsDirectory.directory[prefabIndex]` is null, `SpawnCachedItems` throws and retries every frame, duplicating items (`PreventBoatCacheSpawnLoop`).

### What's in `level24` (main scene)

Source: data.

| Object | `BoatLocalItems.houseParentIsland` | `BoatHorizon` |
| --- | --- | --- |
| BOAT dhow small (10), medium (20), large (30) | -1 | on a child |
| BOAT medi small (40), medium (50) | -1 | on a child |
| BOAT junk large (70), medium (80), small singleroof (90) | -1 | on a child |
| house trigger (201) | 1 | same object |
| house trigger (202) | 9 | same object |
| house trigger (203)-(206) | -1 | same object |

The number in each name is its `sceneIndex`. `Sun` also lives in `level24`.

## Crates

Source: decompile (`ShipItemCrate`, `CrateInventory`).

- **`ShipItemCrate`** has a private `crateInventory` field. `OnLoad` adds a `CrateInventory` whenever that **field** is null, even if one is already attached, so code that adds one first must also set the field.
- **Unsealing:** `UnsealCrate` instantiates `containedPrefab` once per unit at `+100.5` y, inserts each into the crate on the next frame (`InsertItem` coroutine), then opens the crate UI.
- **`CrateInventory.InsertItem`:** sets `currentCrateId`, the rigidbody `attached`, `disableCol` and `inStove` to true, scales the item to `inventoryScale * 0.33`, and puts it and its children on layer 26. `LateUpdate` keeps contained items at the crate's position unless that crate's UI is open.
- **Opening:** `OnAltActivate` on a sold, empty crate opens its inventory, and a crate with units shows the seal UI. Picking a crate up closes its open inventory UI through the private field.
- **Contents and boats:** contents only store `currentCrateId`; unsealing never gives them a boat parent (`KeepCrateContentsWithBoat`).

## Shops and trade

Source: decompile (`IslandMarketWarehouseArea`, `EconomyUI`, `Shopkeeper`, `BuyItemUI`).

- **Warehouse yard:** `IslandMarketWarehouseArea` sits on the same GameObject as its `IslandMarket` (`market = GetComponent<IslandMarket>()`). It tracks `goodsInArea` with `OnTriggerEnter`/`OnTriggerExit` and keeps `market.currentPlayerGoods[goodIndex]` counts.
- **Which goods count:** goods that are sold, not on a mission (`GetMissionIndex() == -1`) and pass `IsGoodValid`, meaning full: crate `amount`, bottle `health` and salt `amount` must be at least the prefab's.
- **`ValidateList`** only removes invalid goods. It never prunes destroyed goods (calling `IsGoodValid` on one throws) and never adds goods the trigger missed. Its only caller is `SellGood`.
- **The sale bug:** `EconomyUI.SellGood` bumps market supply and pays gold **before** `IslandMarketWarehouseArea.SellGood` tries to destroy a good, so a failed warehouse sale ("Failed to sell - crate/barrel not full") still moved the price (`PreventFailedTradeBookSale`).
- **Unreliable exits:** Unity doesn't fire `OnTriggerExit` when a collider is disabled, so trigger lists can go stale.
- **Shopkeepers:** they live in island scenes and have a private `Start`. `BuyItemUI` private fields: `playerIsSelling` (`bool`), `activeShopkeeper` (`Shopkeeper`). Vanilla won't open a second merchant while `activeItem` is set.

## Shipyard

Source: decompile (`Shipyard`).

- **`ConfirmOrder`, in order:**
  1. Check for install errors, obstructed sails and part errors.
  2. Check that the player has enough gold for `currentOrderTotal` in `region`.
  3. Charge the gold and log the transaction.
  4. `InstallSails()`, then `ApplyCurrentOrder()`.
  5. Clean the hull if ordered.
  6. Repair if ordered.
  7. `ResetOrder()`, which snapshots `originalData`.
- **The cleaning bug:** cleaning calls `SaveableObject.GetCleanable()` with no null check, so boats that can't get dirty (e.g. the Jong) throw after the gold is charged. The order then reverts on exit (`CancelOrder` reloads `originalData`), losing paid work (`SkipUncleanableHullCleaning`).

## Smaller facts

Source: decompile unless noted.

- **Chip log:** `ChipLogRopeEnd` turns the dial pointer by the bobber's speed (world units per real second, smoothed, only while in the water with joint force over 4) times `callibrationMult`, which is 28 on both the E and M variants (data). That's 33.6 degrees per in-game knot. The dial numerals are separate meshes over a texture atlas (`chiplog paint Diffuse Color`), so the knot scale wasn't confirmed.
- **Sail hinge audio:** `SailHingeAudio` plays the gybe snap from the change in angular velocity against the private `lastVelocity` (`float`).
- **Mugs:** `Mug.Spill()` is private. `ShipItemBottle` and `ShipItemSoup` both have a private `bool drinking`.
- **`LookUI`:** private fields `controlsText`, `hintText`, `extraText`, `textLicon`, `textRIcon` (`TextMesh`), `mouseLIcon`, `mouseRIcon` (`Renderer`), `LMBicon`, `RMBicon` (`Material`) and `pointer` (`GoPointer`). It is world-space text that shares the transparent queue with smoke particles.
- **`PlayerNeedsUI`:** private `inventory` (`Transform`). The hotbar scales with the needs UI root.
- **`WorldItemSpawner`:** private `item` (`ShipItem`). Its update has a 100 m distance-from-camera check (what it gates wasn't checked).
- **Boat camera:** **C** toggles the boat camera (`BoatCamera.SwitchOn`/`SwitchOff`), which plays the UI click. With SailwindCoop and SailwindPlayerModel it works as a third-person player view, but vanilla still disables aiming while `BoatCamera.on`.
- **Mirage Mountain village chart:** prefab 165, a `ShipItemFoldable`. Vanilla parents it to island scenery.

## Working with the game files

- **Install:** `D:\SteamLibrary\steamapps\common\Sailwind`. The game folder stays vanilla; mods load from Sailwind Mod Synchronizer ModPacks through Doorstop.
- **Main scene:** about 735 asset files in `Sailwind_Data`. `level24` holds the main scene (Sun, boats, houses), and `sharedassets*.assets` hold prefab data such as the chip logs.
- **UnityPy (installed):**
  - `UnityPy.load(<Sailwind_Data>)` loads everything in a few minutes.
  - Read `MonoBehaviour`s with `obj.read(check_read=False)`, or they fail silently in a try/except.
  - Script typetrees are stripped, so read raw bytes. The header is the `m_GameObject` PPtr (12 bytes), `m_Enabled` (4, aligned), the `m_Script` PPtr (12), then `m_Name` (int length + bytes, aligned to 4). After that, serialized fields follow in declaration order: public fields and `[SerializeField]` ones.
  - Layer names are in the `TagManager` of `globalgamemanagers`.
- **Logs:**
  - Vanilla `Debug.Log` goes to `%USERPROFILE%\AppData\LocalLow\Raw Lion Workshop\Sailwind\Player.log`, which keeps only `Player.log` and `Player-prev.log`, so grab it right after a test.
  - BepInEx has `WriteUnityLog = false`, so vanilla lines are not in `LogOutput.log`.
- **ModPack library:** the manager's library (`%LOCALAPPDATA%\SailwindModSynchronizer\library\mods\<guid>\<version>`) is shared by version across packs. Deploying a test build under an already-released version overwrites that version in every pack that pins it, so bump the version first.

## Unanswered questions

Things we ran into but didn't settle. Each says what we know and how to answer it.

### World and time

- **What is `FloatingOriginManager.shiftDistance`?** Our `SkipSmoothOriginShift` comment says 512 m, but this session didn't read it. Read it from `level24` with UnityPy, the same way as `Sun.timescale`.
- **When do island scenes load and unload?** Out-of-range items unfreeze within 600 m of the camera, but we don't know whether an island's terrain and buildings are loaded by then. If they aren't, items could fall through the floor. Log `SceneManager.sceneLoaded`/`sceneUnloaded` with the player's distance while sailing toward and away from an island.
- **Is `Sun.timescale` ever changed by game settings?** It's 0.008 in the scene and only multiplied for time-skip sleep. We didn't check whether a menu option or difficulty changes it.

### Looking and aiming

- **Which layer are the walls we saw targeted through?** Tables and stoves were clickable through a wall. The look ray skips layers 12 (OnlyPlayerCol+Paintable) and 19 (IgnoreSmallItems), and we didn't confirm which one those walls use. Aim through such a wall and log every collider on a ray cast with all layers (`~0`), with its layer.
- **What exactly is on layers 13 (BoatCapsule) and 16 (invis)?** Both are skipped by the look ray; the names suggest a big capsule around each boat and invisible helper colliders. List colliders per layer from `level24` with UnityPy.
- **What is `Physics.queriesHitTriggers` set to?** Several look rays use the global trigger setting (`QueryTriggerInteraction.UseGlobal`). `LookRay` handles both values, but we never read the project setting. Read it from the `PhysicsManager` in `globalgamemanagers`.
- **How do SailwindCoop and SailwindPlayerModel handle C?** Vanilla treats C as the boat camera (`BoatCamera.on`) and disables aiming. We don't know whether those mods reuse `BoatCamera` or add their own camera. Decompile both mods before allowing item use from that view.

### Items, boats and houses

- **Who sets `BoatLocalItems.itemsLoaded` in a fresh session?** Caching only starts when it's true, and the scene starts it false for every boat and house. `SpawnCachedItems` (after a load) sets it, but we found no other caller in `SaveLoadManager`, `StartMenu`, `GameState` or `BoatHorizon`. A boat or house that never respawned from a cache this session may never cache at all. Search every type in `Assembly-CSharp` for `SetItemsLoaded`/`itemsLoaded`.
- **Why weren't 14 items on the small dhow on its walk collider?** The housing playtest showed a table, lantern, water barrel, oar, pipe, map and more with save parent 10 but `currentWalkCol == null`. `EnterBoat` sets `currentWalkCol`, so these items either never ran `EnterBoat` (e.g. spawned from a cache or save and not re-embarked) or lost it. Log `currentWalkCol` and the save parent for items on a boat right after a load and after a cache respawn.
- **Does `OnTriggerExit` fire when a good is picked up?** A held item's colliders become triggers, and Unity's rules for trigger-to-trigger contacts decide whether the warehouse yard sees it leave. Test by picking a crate up in a warehouse yard and logging `IslandMarketWarehouseArea.goodsInArea`.
- **What does `WorldItemSpawner`'s 100 m camera check control?** We only saw the check, not what it gates. Read `WorldItemSpawner.Update` in full.

### Instruments and UI

- **Does the chip log dial read in-game knots or real knots?** The pointer turns 28 degrees per m/s of bobber speed: 33.6 degrees per in-game knot, or 14.4 per real knot. The numerals are separate meshes over a texture atlas, so we couldn't read their angles or where the zero mark sits. Read the chip log mesh and measure each numeral's angle around the dial center, or sail due north at a steady speed and time one minute of latitude (150 m).
- **Is `Speedometer` used anywhere in normal play?** It shows real knots and km/h on a `TextMesh`, which looks like a debug tool. Find which prefabs or scenes carry it.

### Open bugs with a lead

- **Why does food from an open crate end up on a nearby stove?** (Open in `docs/BUG_TRACKER.md`.) `StoveCookTrigger.OnTriggerEnter` inserts any `CookableFood` that enters an empty cook slot, with no check for `currentCrateId` or layer 26. When a crate's grid is open, its items are laid out in the world at the grid squares, so food whose square overlaps a stove's cook slot can be pulled into the stove. "Sometimes" may come down to whether a square overlaps an empty slot. Confirm by opening a crate of food beside a stove and logging `StoveCookTrigger.OnTriggerEnter`.
