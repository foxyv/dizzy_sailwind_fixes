# Dizzy Sailwind Fixes

BepInEx 5 plugin for [Sailwind](https://store.steampowered.com/app/1764530/Sailwind/) that collects small gameplay and QoL fixes. Each fix is a Harmony patch and can be toggled in config.

This repo follows the same plugin layout as [dizzy_sailwind_mods](https://github.com/foxyv/dizzy_sailwind_mods) (Dizzy Gamma / Dizzy Calendar).

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
| `HideInventoryOnNeedsWarning` | on | Hunger, thirst, and sleep warnings show only the status bars. Vanilla scales hotbar items with the whole needs UI, so they stack on the bars whenever a need flashes. |
| `PourSoupIntoMug` | on | Click an empty mug or cup on a soup pot to fill it, then drink. Vanilla only pours water into the pot and drinks from the pot; an empty mug is placed instead. |
| `KeepChipLogDeployed` | on | A thrown chip log stays out in rough seas after the chip hits the water. Vanilla reels it in whenever the chip is airborne, including chop. The toss still reels until splash so the line does not pay out in the air. Right-click still winds it back. |
| `PreventBoatCacheSpawnLoop` | on | Coming back to a boat skips saved items with a missing prefab instead of throwing. Vanilla throws mid-spawn and retries every frame, duplicating lanterns and other items already created. |
| `KeepMirageMountainMap` | on | The small chart in the buried village north of Mirage Mountain stays in inventory after you leave. Vanilla parents it to island scenery, so sailing away hides it, can destroy it as out of range, then respawn a copy and fail to save. |

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
