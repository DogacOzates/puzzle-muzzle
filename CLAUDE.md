# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Puzzle Muzzle — an iOS number-path puzzle game built with Unity **6000.4.2f1** (URP, uGUI, Input System). The player drags chains of cells; a chain ending on a number cell of matching length completes a colored block. Ships to the App Store (id6739918641).

There is no CLI build/test workflow: the project is developed through the Unity Editor and built for iOS (Xcode project output lives in `IOS Build/`). There are no unit tests. To verify C# compiles without the Editor, `Assembly-CSharp.csproj` exists but the source of truth is Unity's own compilation.

## Key architecture: everything is created from code

`Assets/Scenes/SampleScene.unity` is essentially empty. `GameManager.AutoInitialize()` (a `[RuntimeInitializeOnLoadMethod]` in `Assets/GameManager.cs`) bootstraps the entire game at runtime: it creates the camera and every manager GameObject (AudioManager, GridManager, InputHandler, MonetizationManager, iCloudSyncManager, GameCenterManager, HapticManager, ThemeManager, UIManager) in code. There are **no prefabs**; the entire UI is built procedurally in `UIManager.cs` (~3400 lines) and sprites are generated at runtime by `SpriteGenerator.cs` (plus PNGs under `Assets/Resources/icons/`).

Consequences:
- Order of creation in `GameManager.Awake()` matters (e.g. iCloudSyncManager must exist before saved progress is read; ThemeManager awakes before UIManager.Initialize reads `IsDarkMode`).
- Any new UI must be added in code inside `UIManager.cs`, matching its existing patterns, and must respect `ThemeManager.Instance` colors + the `ThemeManager.OnThemeChanged` event (dark mode support).

## Level system (1800 levels)

Index layout in `LevelDatabase` (`Assets/LevelData.cs`):
- 0–2: handcrafted (pre-tutorial, tutorial, easy) — `TutorialController` runs on levels 0 and 1
- 3–299: square campaign (`LevelGenerator.GenerateCampaign`)
- 300–599: hexagon campaign (`GenerateHexagonCampaign`)
- 600–899: triangle/"ThreeGen" campaign (`GenerateThreeGenCampaign`)
- 900–1799: "Sequence" campaigns (`GenerateSequenceCampaign`, 300 per shape: 900–1199 square, 1200–1499 hexagon, 1500–1799 triangle) — different gameplay: one continuous chain; numbered cells are waypoints whose value is the cumulative step count (never resets), the final number equals the playable cell count. Later tiers add blocked cells. Marked by `LevelData.sequenceMode`; chain/hint logic branches on `GridManager.IsSequenceMode`. Level select shows 6 tabs (2 rows of 3).

`LevelGenerator.cs` is deterministic procedural generation. Levels are served in priority order:
1. **Bundled JSON** `Assets/Resources/generated_levels.json` (preferred; loaded once at startup)
2. Per-level disk cache at `persistentDataPath/generated-level-cache-v{N}` (bump `GeneratedLevelCacheVersion` when generation logic changes)
3. On-demand generation + background prefetch of the next level

**If you change `LevelGenerator` logic, you must regenerate the bundled asset** via Unity menu **Debug → Generate Bundled Levels Asset** (or **Debug → Regenerate Triangle Levels Only** for indices 600–899), otherwise players keep getting the stale bundled levels.

`SolutionPath` stores flat coord pairs (`x0,y0,x1,y1,...`); last pair is the number cell.

## Grid geometry

`GridManager.cs` supports four cell shapes (`CellShape`: Square, Pentagon, Hexagon, ThreeGen/triangle). Cell sizing/spacing constants are carefully derived (see comments at the top of GridManager) — pentagon/hex use a column-offset hex grid, triangles use up/down-pointing alternation. Camera framing is computed in `GameManager.AdjustCameraToGrid()` with shape-specific padding. Touch/drag chain logic lives in `InputHandler.cs` + `GridManager.ActiveChain`.

## Monetization & platform services

- **Ads**: AdMob via `LevelGateAdsBridge` (scheduled interstitials between levels, rewarded ads for hints). Initialized only after ATT consent (`ATTManager` → `MonetizationManager.InitializeAds()`).
- **IAP**: Unity IAP via `NoAdsIapBridge` — No Ads (non-consumable) + consumable hint packs (product IDs in `MonetizationManager`). Always guard purchases with `IsStoreReady`.
- **iOS-only integrations** (all `#if UNITY_IOS && !UNITY_EDITOR` style): Game Center (`GameCenterManager`), iCloud KVS progress sync (`iCloudSyncManager`), Keychain (`KeychainHelper`), local notifications (`DailyNotificationManager`), native share sheet (`NativeShare` + `Assets/Plugins/iOS/NativeShareBridge.mm`).
- `Assets/Editor/IOSPostBuild.cs` runs after every iOS build to inject Info.plist keys, Xcode capabilities and frameworks — add new plist/capability requirements there, not by hand-editing the Xcode project.

## Persistence conventions

All state is in `PlayerPrefs` with dotted keys, e.g. `progress.savedLevelIndex` (campaign progress, also synced to iCloud), `hints.free`, `tutorial.v2.done`, `daily.*` (reward + challenge streaks), `referral.*`, `monetization.noads.purchased`, `theme.darkMode`. Campaign unlock always derives from `progress.savedLevelIndex`, never from the currently loaded level (daily challenge / online levels must not inflate progress).

## Editor debug tooling

Unity menu **Debug** (from `Assets/Editor/`):
- Unlock All Levels / Load Last Level (Play Mode) — `LevelDebugMenu.cs`
- Generate Bundled Levels Asset / Regenerate Triangle Levels Only — `BundledLevelAssetBuilder.cs`

## Conventions

- One flat `Assets/*.cs` script per system; managers are singletons or reached via references wired in `GameManager.Awake()`.
- Debug logs wrapped in `#if UNITY_EDITOR || DEVELOPMENT_BUILD` where they'd ship otherwise.
- Frame rate is capped to 60fps in `GameManager.Awake()` (thermal protection on ProMotion devices) — don't remove.
