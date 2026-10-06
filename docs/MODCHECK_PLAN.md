# Modcheck Cleanup Plan

Plan for the 46 warnings modcheck reported on 0.3.0 (2026-10-02). Work one step at a time. Each step ends with: build, modcheck, deploy to the Dizzy Sept 2026 pack, playtest, commit.

Release 0.3.1 after step 4 (low-risk steps), and 0.3.2 after steps 5-7.

## Steps

- [x] **0. Land pending work.** Playtest the shared look raycast (`LookRayPatches.cs`), then commit it with `docs/BUG_TRACKER.md` and this plan.
- [x] **1. Release tooling** (SW111 x2, SW106). `package-release.ps1` reads the version from `Plugin.cs` and always builds with `-p:DeployOnBuild=false`. The csproj deploy target that copied the DLL into the game folder is removed. No gameplay change.
- [x] **2. Duplicate crate inventory** (SW605 x2, `CrateContentsSavePatches.cs:61`, `:188`). Vanilla `ShipItemCrate.OnLoad` checks its private `crateInventory` field, not `GetComponent`, so a `CrateInventory` added by the fix first leads to two. Set that field when the fix adds one. Test: open, reload and empty crates on a boat.
- [x] **3. Trade-book zone check** (SW608, `TradeBookFailedSalePatches.cs:101`). `OverlapBox` uses the world-aligned `col.bounds` with `Quaternion.identity`, so a rotated warehouse zone gets a box that reaches past it. Gather candidates in the trigger's bounding sphere, then keep only goods that overlap the real trigger shape (`Physics.ComputePenetration`). Test: trade-book sales at a couple of ports.
- [x] **4. Per-frame scene search and allocation** (SW401, SW402). Register shopkeepers as they start (pruning ones destroyed with their island) instead of searching the scene every frame while the sell UI is open (`MerchantSellUiPatches.cs:168`). Fill a reused list of `cakeslice.Outline` in the big-crate outline code (`BigCrateCarryPatches.cs:67`) instead of allocating every child component and comparing type names. Test: merchant sell UI, carrying big crates.
- [x] **Release 0.3.1.**
- [x] **5. Per-frame reflection** (SW403 x20). Replace `Traverse.Create` with `AccessTools.FieldRefAccess` and cached method delegates. No behavior change. Three commits:
    - [x] **5a. Look and UI:** `LookTextSmokePatches`, `SoupMugPatches` (look-text prompt), `MerchantSellUiPatches`, `HideInventoryOnNeedsWarningPatches`.
    - [x] **5b. Held items:** `InventoryWithdrawSipPatches`, `ItemPlaceAlignPatches`, `SoupMugPatches` (spill).
    - [x] **5c. World and physics:** `BoatCacheSpawnPatches`, `OriginShiftWaitPatches`, `SailHingeSnapPatches`, `MirageMountainMapPatches`.
- [x] **6. Look fixes in menus, sleep and third-person camera** (SW502 x6). Decompile vanilla `GoPointer.DoRaycast` and add one shared guard so the look fixes skip whenever vanilla doesn't aim. Leave the roll fix running during sleep, since keeping items settled after sleep is part of its job.
- [x] **7. Stop at the nearest wall** (SW501): **won't fix.** Vanilla's own look ray skips the layers many walls are on (12 OnlyPlayerCol+Paintable, 19 IgnoreSmallItems), so vanilla already targets tables and stoves through walls, and players don't mind placing through walls. Blocking only the walls the look ray hits would change three look fixes for little gain. Could return later as an optional, off-by-default fix.
- [x] **Release 0.3.2.**
- [x] **8. Review only** (SW206 x2, trade-book SW601). All three are by design; no code changes. The pipe scroll patch only acts on `ShipItemPipe`, which doesn't override `OnScroll`. The Mirage pickup patch only acts on the chart (prefab 165, a `ShipItemFoldable`) and skips hangables, so `ShipItemLight` never matters. `WarehouseSync.Validate` does everything vanilla `ValidateList` does, plus pruning destroyed goods, rescanning the yard and rebuilding the counts. One shared gap went to the bug tracker watch list.

## Handled or by design

All 17 remaining warnings are handled or by design; modcheck can't tell.

| Rule | Where | Why it stays |
| --- | --- | --- |
| SW605 | `CrateContentsSavePatches.cs` | `InventoryOf` reuses an existing `CrateInventory` and hands the one it adds to `ShipItemCrate.crateInventory`, so vanilla `OnLoad` keeps it. modcheck flags any `AddComponent<CrateInventory>`. |
| SW402 | `BigCrateCarryPatches.cs` | `SuppressOutline` fills a reused list, so it allocates nothing; a per-button cache would miss outlines Dizzy.FirewoodBundle adds to bundle logs. modcheck flags any `GetComponentsInChildren` per frame. |
| SW502 | `HouseItemsPatches.cs` | Added in 0.3.3. Vanilla's 600 m destroy countdown ignores sleep, so the reset that keeps house items must ignore it too. |
| SW502 | `ItemRollPatches.cs` | Vanilla freezes items while sleeping; the roll fix keeping an already-frozen capsule frozen afterward is how items stay settled after sleep. |
| SW501 | `LookRayPatches.cs` | Won't fix (step 7): the look fixes follow vanilla, which also targets through walls. `Cast` is a shared cache; each fix checks distance itself. |
| SW206 | `ItemPlaceAlignPatches.cs` | The pipe scroll patch only acts on `ShipItemPipe`; the five types that skip it (mooring rope, compass, fishing rod, scroll, spyglass) aren't pipes. |
| SW206 | `MirageMountainMapPatches.cs` | The pickup patch only acts on the Mirage chart and skips hangables; `ShipItemLight` is a hangable. |
| SW601 | `TradeBookFailedSalePatches.cs` | `WarehouseSync.Validate` replaces `ValidateList` on purpose: it keeps vanilla's invalid-good removal and adds null pruning, a yard rescan and a recount. The warehouse area is the market's only stock counter. |
| SW604 | `LoadSailUnfurlPatches.cs` | Reef save now merges into the loaded data instead of rebuilding it. |
| SW603 | `SoupMugPatches.cs` | Mug destroy keeps saved soup when the boat unloads or sinks the mug. |
| SW606 | `UncleanableHullCleaningPatches.cs` | Finalizer swallows only after gold was charged, on purpose. |
| SW601 | `MirageMountainMapPatches.cs` | Skipping `DestroyItem` protects a held or slotted map; the slot bug is fixed. |
| SW801 x4 | FirewoodBundle, Nudge, Calendar overlaps | FirewoodBundle and Nudge are safe; Dizzy.Calendar is retired and never deployed (its `ItemRigidbody.FixedUpdate` overlap is now reported on `HouseItemsPatches.cs`). |
| SW801 | `CrateStovePatches.cs` | Added in 0.3.6. Dizzy.FirewoodBundle's `StoveFuelTrigger.InsertFuel` prefix also only skips the insert (bundles stay out of the fire), with no other side effects, so order doesn't matter: the insert is skipped if either says so. |
