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
| Chip log knot | 1 real knot = 0.514 m/s | The chip log dial reads real-world knots (see Chip log below), so it shows about 2.3x the in-game knots (nautical miles per game hour) |

- **Local time:** `localTime = globalTime + x / 15`, so each degree of longitude is 4 minutes, as in reality. `globalTime` past 24 increments `GameState.day`, calls `DayLogs.instance.NewDaySheets()` and fires `Sun.OnNewDay`.
- **Dawn:** `dawnBorder = -0.147 * z + 10.853`, clamped to [6.16, 6.8], when `dawnBorderFromLatitude` is on.
- **Time scale is fixed:** only the editor-only `Debugger` keys (Keypad 7: x1, Keypad 9: x100) change `Sun.initialTimescale`; no game setting does.
- **Earth curvature:** `IslandHorizon.ApplyNewHorizon` drops islands by `d^2 / (2 * 515662)`, so the horizon uses a radius of about 516 km.
- **`Speedometer`** shows `velocity * 1.944` "knots" and `* 3.6` km/h, real-world units of world units per real second. It's part of the hidden build debug mode: hold P+N and press T (`Debugger.buildDebugModeOn`), which also turns on god mode and works in normal builds. Speedometers sit on a few boats and test objects in `level24`.

## Floating origin

Source: decompile (`FloatingOriginManager`).

- **When it shifts:** when the shifter object passes `shiftDistance` (**512 m**, read from `level24`) from the origin on x or z, the world moves back by one `shiftDistance` step.
- **Smooth shift:** `ShiftSmoothly(x, z)` fades boat wakes over `smoothShiftFrames` (50) fixed updates with `GameState.waitingForShift` set, then calls private `Shift(int x, int z)`. The wait is the 1-2 s freeze the fix removes.
- **Shift itself:** `Shift` starts `NewShift`, which calls `PrepareForShifting` / `RestoreMomentum` on registered `ShiftingRigidbody`s. Private field `shiftingSmoothly` guards re-entry.
- **Offset:** `outCurrentOffset` holds the accumulated shift; save positions subtract it.

## Look and aim (`GoPointer`)

Source: decompile (`GoPointer.DoRaycast`, `GoPointerButton`), data (layer names from `TagManager` in `globalgamemanagers`).

- **Look ray:** `Physics.Raycast(ray, out hit, 1.8f, -604165)`, with the global trigger setting. The ray is `raycastRay`, or the mouse ray when `debugEditorPointer` is set. **F3** logs the hit collider's name.
- **Triggers count:** `Physics.queriesHitTriggers` is **true** (`PhysicsManager` in `globalgamemanagers`), so the look ray stops on trigger colliders too, e.g. a stove's big trigger box.
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
| 2 | Ignore Raycast | **skipped** (1,044 colliders: NPCs, shopkeepers, cook and inn triggers, embark colliders, carts, and 18 solid boat `hull` colliders) |
| 4 | Water | hits |
| 5 | UI | hits |
| 8 | WalkCols | hits |
| 9 | Clouds1 | hits |
| 10 | Clouds2 | hits |
| 11 | Player | **skipped** (the player's embarker triggers) |
| 12 | OnlyPlayerCol+Paintable | **skipped** (only 5 in the data; at runtime boats carry a solid `hull player collider` here, so vanilla targets stoves and tables through a boat's hull) |
| 13 | BoatCapsule | **skipped** (one solid capsule per boat, 8 in `level24`) |
| 14 | TerrainDepth | hits |
| 15 | Painter | hits |
| 16 | invis | **skipped** (no colliders in the data; hotbar items are moved here at runtime) |
| 17 | NoPlayerLight | hits |
| 18 | SmallItems | hits |
| 19 | IgnoreSmallItems | **skipped** (no colliders in the data) |
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
- **Hotbar items keep the house:** `OnEnterInventory` only calls `ExitBoat()`, so an item put in the hotbar inside a house keeps the house as its parent. When the house caches at 1000 m it takes those hotbar items too; they return to their slots when it respawns (bug tracker).
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
- **`itemsLoaded`:** starts false in the scene. Only `SpawnCachedItems` (and `BoatDamage.LoadDamage` for a sunk boat) sets it true; `SaveableObject.Load` on a boat and `Recovery` set it false. So a boat or house never caches until it has respawned from a cache once, normally after the first reload with items on it.
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

In-game names: "BOAT dhow medium (20)" is the **Sanbuq** (confirmed in play). The others are unconfirmed; "dhow small (10)" is probably the Dhow.

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
- **Carried goods:** carrying a good out of the yard fires `OnTriggerExit` (`held=True`) and `RemoveGood`; carrying it back in fires `OnTriggerEnter` and `AddGood`. `OnTriggerEnter` never checks `held`, so a crate you're carrying counts as stock while you're inside the yard (probe, Gold Rock).
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

- **Chip log:** two vanilla prefabs, 92 (sold at Fort Aestrin; data name "92 chip log M") and 93 (Gold Rock; "93 chip log E"), with identical settings: `maxLength` 14 m, `minLength` 0.15, `minVelocity` 11, `autoReturnSpeed` 1, `bobberForceMult` 200, dial `tensionSpeed` 5, `pointerSpeed` 1, `callibrationMult` 28. `ChipLogRopeEnd` turns the needle by the bobber's total speed (world units per real second, smoothed, only while in the water with joint force over 4) x 28 degrees, which is **14.4 degrees per real knot**: the dial reads real-world knots (confirmed: 132 degrees read about 9, 190 degrees about 13). Because it follows the bobber, not the boat through the water, it spikes when a wave or the line jerks the bobber (13 kn shown at a steady 5 kn). The line joint is springy and a full 14 m line stretches past `maxLength`.
- **Sail hinge audio:** `SailHingeAudio` plays the gybe snap from the change in angular velocity against the private `lastVelocity` (`float`).
- **Mugs:** `Mug.Spill()` is private. `ShipItemBottle` and `ShipItemSoup` both have a private `bool drinking`.
- **`LookUI`:** private fields `controlsText`, `hintText`, `extraText`, `textLicon`, `textRIcon` (`TextMesh`), `mouseLIcon`, `mouseRIcon` (`Renderer`), `LMBicon`, `RMBicon` (`Material`) and `pointer` (`GoPointer`). It is world-space text that shares the transparent queue with smoke particles.
- **`PlayerNeedsUI`:** private `inventory` (`Transform`). The hotbar scales with the needs UI root.
- **`WorldItemSpawner`:** private `item` (`ShipItem`). It only spawns its item while the camera is within 100 m (otherwise it rechecks in 0.1 s). After pickup it waits `respawnTime` x 0.75-1.25, or never respawns when `respawnTime` <= 0. Spawned items are parented to the spawner's parent (island scenery) and frozen until picked up.
- **Boat camera:** **C** toggles the boat camera (`BoatCamera.SwitchOn`/`SwitchOff`), which plays the UI click. SailwindPlayerModel's follow view is built on it: with its `FollowCamera` option, its `PlayerOrbitCamera` patch on `BoatCamera.Update` cycles C through first person, Following (orbit behind the character) and Ship, and `BoatCamera.on` is true in both of the latter, so vanilla disables aiming. It exposes `PlayerOrbitCamera.Following`. SailwindCoop only reads `BoatCamera.on`.
- **Mirage Mountain village chart:** prefab 165, a `ShipItemFoldable`. Vanilla parents it to island scenery.
- **Island scenes:** `IslandHorizon` loads an island's scene additively within `islandLoadDistance` and unloads it beyond: 1,800 m for 36 islands, 1,200 m for 10 (the Lagoon islands and E swamp, jungle and onsen). That's before items unfreeze (600 m) or a house respawns (1,000 m). House 201's island is scene 1 (gold rock), house 202's is scene 9 (dragon cliffs).
- **Stove pots jitter:** a pot sitting on a stove re-enters a neighboring cook slot's trigger constantly (5,668 `OnTriggerEnter` calls in one session on the Sanbuq's stove). Vanilla ignores them because the slot is full; it's a likely cause of the hover flicker `StabilizeStoveItemHover` works around.

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
- **Debug probes:** a temporary `DebugProbesPatches.cs` (not committed) answered the runtime questions: keys Home (every collider on the look ray, all layers), End (items within 30 m that belong to a boat or house), Page Down (nearest chip log dial and its settings), plus passive warehouse-trigger and stove-slot logging. Home, End, Page Up/Down and Insert are unbound by the game and the Dizzy Sept 2026 mods; F8, F9 and F11 are taken by mod panels and F10 opens the pause menu.

## Unanswered questions

Answered on 2026-10-04 from the full decompile, the game data and the debug probes; the facts are folded into the sections above.

| Question | Answer | How |
| --- | --- | --- |
| `shiftDistance` | 512 m | data |
| When do island scenes load? | 1,800 m (36) or 1,200 m (10) from the island; before the 600 m unfreeze | decompile + data |
| Does a setting change `Sun.timescale`? | No; only editor-only debug keys | decompile |
| Which layer are the walls we targeted through? | The boat's `hull player collider` on layer 12 (seen on the Sanbuq) | Home probe |
| What's on layers 13 and 16? | 13: one capsule per boat; 16: no colliders, hotbar items at runtime | data + End probe |
| `Physics.queriesHitTriggers` | true | data |
| How do SailwindCoop / SailwindPlayerModel handle C? | PlayerModel's follow view is vanilla `BoatCamera` (aiming off); Coop doesn't touch aiming | decompile of both mods |
| Who sets `itemsLoaded`? | Only the cache respawn (and a sunk-boat load); boat loads and recovery clear it | decompile |
| Does `OnTriggerExit` fire when a good is picked up? | Yes, when carried out of the yard; held goods count while inside | probe |
| What does `WorldItemSpawner`'s 100 m check do? | Gates spawning | decompile |
| Chip log: in-game or real knots? | Real knots, 14.4 degrees per knot | Page Down probe + dial readings |
| Is `Speedometer` used in normal play? | Only in the P+N+T build debug mode | decompile + data |
| Crate food to stove | Confirmed: crate-linked food entered empty cook slots | stove probe |

### Still open

- **Why does the Fort Aestrin chip log perform so differently?** Partly answered. Prefab 92 (Fort Aestrin, M model) and prefab 93 (Gold Rock and Dragon Cliffs, E model) share every tuning value (line length, forces, return speed, dial multiplier and smoothing), but the shape differs (full-prefab diff of sharedassets15 vs sharedassets1):

    | What | 92 Fort Aestrin | 93 Gold Rock / Dragon Cliffs |
    | --- | --- | --- |
    | Body `BoxCollider` size (w x h x d) | 0.34 x 0.93 x 0.20 m | 0.34 x 0.73 x 0.12 m |
    | Body collider center (y, z) | +0.02, +0.06 | -0.02, +0.02 |
    | Line attachment (`rope att`) height | 0.465 m | 0.326 m |
    | Bobber rest (`rope end`) | 0.431 m up | 0.318 m up, 0.025 m forward |
    | Line joint anchor | set by hand (`autoConfigureConnectedAnchor` off), 0.395 m | automatic, 0.318 m |

    The dial follows the bobber's total speed, and in the Aestrin test (aboard the medi medium, 50) one reading showed 13 kn at a steady 5 kn when the bobber was yanked to 7.5 m/s. Theory: the taller, deeper body with a higher line attachment gives the line more leverage to rock the log when it goes taut, and each rock yanks the bobber. Nailing the log (which makes it kinematic) should remove that. To confirm: read both logs on the same boat in the same water, loose and nailed.
- **Why weren't 14 items on the small dhow (10) on its walk collider?** On the Sanbuq every item had a walk collider; the 14 items without one were on "BOAT dhow small (10)" in the housing test. Load next to the small dhow and press End.
- **In-game names of the other boats.** Only the Sanbuq ("dhow medium (20)") is confirmed.
