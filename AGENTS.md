# AGENTS.md

This file provides guidance to Codex (Codex.ai/code) when working with code in this repository.

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

### Adding Enemy Intent Cards and Collection Displays

Enemy Intent domain cards and game `CardModel` objects have different lifecycles:

- `BaseEnemyCard` is an enemy-combat domain object. Its registered deck factory must return a fresh `BaseEnemyCard` instance every time.
- `CardModel` derives from `AbstractModel` and is registered in `ModelDb`. After `ModelDb` initialization, never create a display/template model with `new TCardModel()` or `Activator.CreateInstance(typeof(TCardModel))`; doing so attempts duplicate registration and throws `DuplicateModelException`, often while `CardListIntent.AssetPaths` is being initialized.
- Enemy-card definitions must reuse `ModelDb.Card<TCardModel>()` for their `CardModel`. A real player-owned combat copy, when actually required, must be created through the run/combat-state card creation API instead of a constructor.
- Collection display definitions must use the typed `Definition<TCardModel>(...)` registration in `CardIntentTestCollectionCatalog`. It stores a resolver backed by `ModelDb.Card<TCardModel>()`; both asset preloading and `EnemyIntentTimeline` must call `EnemyCollectionDefinition.ResolveCardModel()`.
- Preload every card model that can appear in the intent timeline, including source cards, immediate cards, compose tokens, consumed cards, and consumed collections. Derive paths from the canonical ModelDb model through `EnemyCardDeckRegistry.GetCardModelAssetPaths`; do not duplicate portrait-path rules by hand.
- When adding a new enemy Intent card or collection, extend the normal timeline tests and the real-ModelDb regression in `tests/CardIntentModelDbHarness/CardIntentCatalogModelDbTests.testcs`. The latter must verify that asset preloading and consumed-collection timeline construction reuse the exact registered `ModelDb.Card<T>()` object without constructing another model.

If only C# definitions change, run `dotnet build`. Run `dotnet publish` as well when the new card adds or changes Godot resources such as portraits or scenes.

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

## Enemies

### CrychicPhatom (Crychic亡灵)

Boss enemy with a unique **CrychicRemember** buff + revive mechanic.

**Files:**
- `Scripts/Enemy/CrychicPhatom.cs` — enemy body
- `Scripts/Powers/EnemyPowers/CrychicRememberPower.cs` — 7-stage state machine buff
- `Scripts/Afflictions/CrychicEnergyCurse.cs` — affliction: -1 energy on play
- `Scripts/Afflictions/CrychicExhaustCurse.cs` — affliction: Exhaust + Ethereal
- `Scripts/Afflictions/CrychicDiscardCurse.cs` — affliction: player chooses 2 cards to discard on play
- `Scripts/Afflictions/CrychicDrawLessCurse.cs` — affliction: draw 1 less next turn
- `Scripts/Afflictions/CrychicSelfDamageCurse.cs` — affliction: take 6 damage on play
- `Scripts/Afflictions/CrychicDamageReduceCurse.cs` — affliction: damage taken halved (marker for stage 3)
- `STS2_Tomorin_Mod/localization/{eng,zhs}/powers.json` — CrychicRememberPower localization

**Phase 1** (500-550 HP): Cycles 3 moves — heavy single hit (25) → multi-hit (10×3) → heal (50) + block (30)

**Phase 2** (after revive): Opens with 99 Vulnerable on both sides (once), then cycles — heavy hit (35) → multi-hit (3×10) → attack (20) + block (55)

**Revive:** On first HP bar death, collects total CrychicRemember stacks from all players. New HP = 300 + totalStacks × 20 × playerCount. Recycles discard + exhausted non-status cards back to draw pile, assigning 5 afflictions in round-robin order.

### CrychicRememberPower

7-stage state machine buff applied to all players at combat start (1 stack).

| Stage (`Amount%7`) | Effect | Hook |
|---|---|---|
| 1 | Take 10 unblockable damage at turn end | `BeforeTurnEnd` |
| 2 | Cards entering hand gain Exhaust | `AfterCardDrawn` |
| 3 | Damage taken halved (via affliction); auto +1 at turn start | `ModifyHpLostBeforeOstyLate` + `BeforeHandDraw` |
| 4 | Cards cost +1 energy to play | `BeforeCardPlayed` |
| 5 | All enemies gain +1 Str +5 Dex at turn end | `BeforeTurnEnd` |
| 6 | Damage dealt +50%; auto +1 at turn start | `ModifyDamageMultiplicative` + `BeforeHandDraw` |
| 0 | Force end player turn → +1 → enemy turn | `BeforeHandDraw` + `AfterPlayerTurnStart` fallback |

Stages 3 and 6 auto-advance at turn start via `BeforeHandDraw`. Stage 0 forces end turn then advances. Stages 1/2/4/5 do not auto-advance (external logic handles it).

## Afflictions

Custom afflictions extend `BaseAfflictionModel` (`Scripts/Afflictions/Base/BaseAfflictionModel.cs`) which extends the game's `AfflictionModel` (from `MegaCrit.Sts2.Core.Models.Afflictions`).

### Creating an Affliction

```csharp
using STS2_Tomorin_Mod.Afflictions.Base;

public class MyAffliction : BaseAfflictionModel
{
    // Called when affliction is applied to a card
    public override void AfterApplied() { /* sync setup */ }
    // Called before affliction is removed
    public override void BeforeRemoved() { /* sync cleanup */ }
    // Called when the afflicted card is played
    public override async Task OnPlay(PlayerChoiceContext ctx, Creature? target) { /* effect */ }
    // Standard combat hooks also available (ShouldReceiveCombatHooks => true)
}
```

### Applying Afflictions

Use non-generic overload to avoid pool registration:
```csharp
await CardCmd.Afflict(new MyAffliction(), card, 1);
// or batch:
await CardCmd.AfflictAndPreview<Bound>(cards, amount, CardPreviewStyle.None); // requires pool
```

### Removing Afflictions
```csharp
CardCmd.ClearAffliction(card); // if card.Affliction is MyAffliction
```

### Key AfflictionModel Hooks
- `AfterApplied()` — sync, after affliction successfully applied
- `BeforeRemoved()` — sync, before affliction cleared
- `OnPlay(PlayerChoiceContext, Creature?)` — async, when afflicted card is played
- `CanAfflict(CardModel)` — virtual, filter which cards can receive this affliction
- Standard combat hooks via `ShouldReceiveCombatHooks => true`: `AfterCardPlayed`, `BeforeCardPlayed`, `AfterCardDrawn`, etc.

## Path Configuration

The `.csproj` auto-detects platform and sets `SteamLibraryPath`, `Sts2Path`, and `ModsPath`. If the build fails with path errors, check that StS2 is installed at the expected Steam library location or adjust the path variables in the `.csproj`.
