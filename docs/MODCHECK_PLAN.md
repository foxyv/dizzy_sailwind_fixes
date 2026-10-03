# Modcheck Cleanup Plan

Plan for the 46 warnings modcheck reported on 0.3.0 (2026-10-02). Work one step at a time. Each step ends with: build, modcheck, deploy to the Dizzy Sept 2026 pack, playtest, commit.

Release 0.3.1 after step 4 (low-risk steps), and 0.3.2 after steps 5-7.

## Steps

- [x] **0. Land pending work.** Playtest the shared look raycast (`LookRayPatches.cs`), then commit it with `docs/BUG_TRACKER.md` and this plan.
- [x] **1. Release tooling** (SW111 x2, SW106). `package-release.ps1` reads the version from `Plugin.cs` and always builds with `-p:DeployOnBuild=false`. The csproj deploy target that copied the DLL into the game folder is removed. No gameplay change.
- [x] **2. Duplicate crate inventory** (SW605 x2, `CrateContentsSavePatches.cs:61`, `:188`). Vanilla `ShipItemCrate.OnLoad` checks its private `crateInventory` field, not `GetComponent`, so a `CrateInventory` added by the fix first leads to two. Set that field when the fix adds one. Test: open, reload and empty crates on a boat.
- [ ] **3. Trade-book zone check** (SW608, `TradeBookFailedSalePatches.cs:101`). `OverlapBox` uses the world-aligned `col.bounds` with `Quaternion.identity`, so a rotated warehouse zone gets a box that reaches past it. Use the collider's rotated shape, or filter hits with `ClosestPoint`. Test: trade-book sales at a couple of ports.
- [ ] **4. Per-frame scene search and allocation** (SW401, SW402). Find shopkeepers once per scene load instead of every frame while the sell UI is open (`MerchantSellUiPatches.cs:168`). Cache each item's `Outline` components in the big-crate outline code (`BigCrateCarryPatches.cs:67`). Test: merchant sell UI, carrying big crates.
- [ ] **Release 0.3.1.**
- [ ] **5. Per-frame reflection** (SW403 x20). Replace `Traverse.Create` with `AccessTools.FieldRefAccess` and cached method delegates. No behavior change. Three commits:
    - [ ] **5a. Look and UI:** `LookTextSmokePatches`, `SoupMugPatches` (look-text prompt), `MerchantSellUiPatches`, `HideInventoryOnNeedsWarningPatches`.
    - [ ] **5b. Held items:** `InventoryWithdrawSipPatches`, `ItemPlaceAlignPatches`, `SoupMugPatches` (spill).
    - [ ] **5c. World and physics:** `BoatCacheSpawnPatches`, `OriginShiftWaitPatches`, `SailHingeSnapPatches`, `MirageMountainMapPatches`.
- [ ] **6. Look fixes in menus, sleep and third-person camera** (SW502 x6). Decompile vanilla `GoPointer.DoRaycast` and add one shared guard so the look fixes skip whenever vanilla doesn't aim. Leave the roll fix running during sleep, since keeping items settled after sleep is part of its job.
- [ ] **7. Stop at the nearest wall** (SW501, `LookRayPatches.cs`). `LookRay` records the nearest solid hit; the stove, hook and held-item fixes ignore anything past it. The anchor and mooring fixes keep looking through ground and dock mesh on purpose. Biggest behavior change: longest playtest.
- [ ] **Release 0.3.2.**
- [ ] **8. Review only** (SW206 x2, trade-book SW601). Decide whether compass, fishing rod, scroll, spyglass and mooring rope need pipe-style scroll handling, and whether lights matter for the Mirage map pickup patch. Confirm `WarehouseSync.Validate` covers everything vanilla `ValidateList` does. Record the decision or patch it.

## Not planned

These 9 warnings are handled or by design; modcheck can't tell.

| Rule | Where | Why it stays |
| --- | --- | --- |
| SW605 | `CrateContentsSavePatches.cs` | `InventoryOf` reuses an existing `CrateInventory` and hands the one it adds to `ShipItemCrate.crateInventory`, so vanilla `OnLoad` keeps it. modcheck flags any `AddComponent<CrateInventory>`. |
| SW604 | `LoadSailUnfurlPatches.cs` | Reef save now merges into the loaded data instead of rebuilding it. |
| SW603 | `SoupMugPatches.cs` | Mug destroy keeps saved soup when the boat unloads or sinks the mug. |
| SW606 | `UncleanableHullCleaningPatches.cs` | Finalizer swallows only after gold was charged, on purpose. |
| SW601 | `MirageMountainMapPatches.cs` | Skipping `DestroyItem` protects a held or slotted map; the slot bug is fixed. |
| SW801 x4 | FirewoodBundle, Nudge, Calendar overlaps | FirewoodBundle and Nudge are safe; Dizzy.Calendar is retired and never deployed. |
