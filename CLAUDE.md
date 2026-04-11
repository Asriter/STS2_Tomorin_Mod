# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build Commands

```bash
# Deploy DLL + manifest to game mods folder (use this for development)
dotnet build

# Full publish: deploys DLL + exports Godot .pck file (required for new assets/scenes)
dotnet publish
```

> **Important:** `dotnet build` copies the DLL and manifest but not the `.pck`. Run `dotnet publish` when you add or modify Godot resources (images, scenes, animations, materials).

## Project Overview

A Slay the Spire 2 mod adding a new playable character **Tomorin** with a unique "Compose" mechanic. Built with .NET 9 / Godot 4.5.1, using HarmonyLib for game patching and the **BaseLib** framework (`Alchyr.Sts2.BaseLib`) for modding utilities.

**Mod ID:** `STS2_Tomorin_Mod` | **Namespace:** `STS2_Tomorin_Mod`

## Architecture

### Directory Layout

```
Scripts/
├── Cards/               — Card implementations + Base/BaseCardModel.cs
├── CardPools/           — TomorinCardPool (registers all cards)
├── Characters/          — Tomorin character model
├── Commands/            — ComposeCmd (compose mechanic logic)
├── Localization/DynamicVars/ — ComposeVar, InspirationVar
├── Relics/              — NormalPencil (starter relic)
├── RelicPools/          — TomorinRelicPool
└── PotionPools/         — TomorinPotionPool (empty placeholder)
STS2_Tomorin_Mod/        — Godot resource directory (images, scenes, localization)
├── Scripts/
│   ├── Base/            — CustomCardModel, PoolAttribute, CustomContentDictionary
│   ├── Patch/           — Harmony patches for game integration
│   └── View/            — Custom Godot node classes (GlobalClass)
├── localization/{eng,zhs}/ — JSON localization files
└── images/, scenes/, animations/, materials/
MainFile.cs              — Mod entry point ([ModInitializer])
Extensions/StringExtensions.cs — CardImagePath() / BigCardImagePath() helpers
```

### Core Mechanic: Compose

The **Compose** mechanic consumes cards of specific types from the player's hand to generate token cards.

Flow: `Mayoiuta.OnPlay()` → `ComposeCmd.Compose<MayoiutaToken>()` → exhausts 2 Attack cards → adds/upgrades `MayoiutaToken` in hand → triggers `AfterCompose()` hooks on relics.

- **ComposeCmd** (`Scripts/Commands/ComposeCmd.cs`): Static utilities `CanUseCompose()` and `Compose<T>()`.
- **ComposeVar** (`Scripts/Localization/DynamicVars/ComposeVar.cs`): `DynamicVar` holding `Dictionary<CardType, int>` for localization display.
- **CustomHookInterface** (`Scripts/Cards/Base/CustomHookInterface.cs`): Defines `AfterCompose()` — implement on relics/cards to react to compose.

### Adding a New Card

1. Create `Scripts/Cards/MyCard.cs` extending `BaseCardModel`:
   ```csharp
   [Pool(typeof(TomorinCardPool))]
   public class MyCard() : BaseCardModel(
       canonicalEnergyCost: 1,
       type: CardType.Attack,
       rarity: CardRarity.Common,
       targetType: TargetType.AnyEnemy)
   {
       protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(6m, ValueProp.Move)];
       protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play) { ... }
       protected override void OnUpgrade() { ... }
   }
   ```
2. Add the card class to `TomorinCardPool` in `Scripts/CardPools/TomorinCardPool.cs`.
3. Add localization entries in `STS2_Tomorin_Mod/localization/eng/cards.json` (and `zhs/cards.json`).
4. Add card image at `STS2_Tomorin_Mod/images/card_portraits/{slug}.png` and `big/{slug}.png`.

### Localization

Key format: `STS2_TOMORIN_MOD-{SLUG}.{field}` where `SLUG` is the class name uppercased with underscores.

Examples: `STS2_TOMORIN_MOD-STRIKE_TOMORIN.title`, `STS2_TOMORIN_MOD-NORMAL_PENCIL.flavor`

Supported languages: `eng`, `zhs` (Simplified Chinese).

### Harmony Patches

Patches live in `STS2_Tomorin_Mod/Scripts/Patch/` and integrate the mod with game internals:
- `ModelDbAllCharactersPatch` — registers Tomorin in the character list
- `CharacterModelCreateVisualsPatch` — injects `CustomNCreatureVisuals` for Tomorin
- `NEnergyCounterCreatePatch` — injects `CustomNEnergyCounter` for Tomorin

### Godot Custom Nodes

Custom visual nodes in `STS2_Tomorin_Mod/Scripts/View/` are marked `[GlobalClass]` (Godot C# requirement) and extend game base types: `NCreatureVisuals`, `NEnergyCounter`, `NCardTrail`, `NMerchantCharacter`, `NRestSiteCharacter`, `NSelectionReticle`.

These are referenced from Godot scene files (`.tscn`) in `STS2_Tomorin_Mod/scenes/`.

### Pool Registration

`CustomContentDictionary` (`STS2_Tomorin_Mod/Scripts/Base/`) uses a Harmony postfix on `ModelDb.InitIds` to automatically register all types annotated with `[Pool(typeof(SomePool))]` into their respective pools.

## Key Dependencies

- **Alchyr.Sts2.BaseLib** — base classes for cards, relics, potions, characters, pools
- **HarmonyLib** — runtime code patching
- **BepInEx.AssemblyPublicizer.MSBuild** — allows accessing private game members
- Game assemblies referenced from the StS2 installation directory (`Sts2DataDir`)

## Path Configuration

The `.csproj` auto-detects platform and sets `SteamLibraryPath`, `Sts2Path`, and `ModsPath`. If the build fails with path errors, check that StS2 is installed at the expected Steam library location or adjust the path variables in the `.csproj`.
