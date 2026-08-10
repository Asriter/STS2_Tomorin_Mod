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
- `FixedFirstEventPatch` — replaces the first Act 2 event with `FeedTheCat`

### Events

#### FeedTheCat

Fixed Act 2 first event. It is implemented as a non-shared `CustomEventModel` so the second layer can use normal per-player `EventOption` choices.

**Files:**
- `Scripts/Events/FeedTheCat.cs` — event pages and reward/penalty options
- `Scripts/Events/FeedTheCatVoteCoordinator.cs` — first-layer vote aggregation and final branch synchronization
- `Scripts/Patch/FixedFirstEventPatch.cs` — forces `ModelDb.Event<FeedTheCat>()` as the first event in Act 2
- `Scripts/Relics/MatchaParfait.cs` — reward-route marker relic
- `Scripts/Relics/EmptyParfait.cs` — penalty-route marker relic
- `STS2_Tomorin_Mod/localization/{eng,zhs}/events.json` — event text

**Flow:** each player independently chooses a first-layer vote. The host waits until all `FeedTheCat` event instances have voted, applies majority rule, breaks ties once with the event RNG, and broadcasts the final branch through `PlayerChoiceSynchronizer` using `PlayerChoiceResult.FromIndex`. Route marker relics are only granted after the final synchronized branch is received.

**Reward branch:** obtain `MatchaParfait`, then independently choose one of: heal 20% max HP, remove a card, or upgrade a card. Remove/upgrade options are locked when no valid deck card exists.

**Penalty branch:** obtain `EmptyParfait`, then independently choose one of: lose 200 gold or gain native `MegaCrit.Sts2.Core.Models.Cards.Debt`. The gold option is locked below 200 gold.

`FeedTheCat.IsAllowed` intentionally returns true only during the fixed-event authorization window in `FixedFirstEventPatch`, preventing the BaseLib custom event registration from putting the event into the random shared event pool.

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

### Raana (要乐奈)

Boss enemy body only; Encounter and Act Boss registration are intentionally not handled here.

**Files:**
- `Scripts/Enemy/Raana.cs` - enemy body, route detection, move state machine, S3 cleanse, S4 interest branch switching
- `Scripts/Powers/EnemyPowers/RaanaPowers/RaanaInterestPower.cs` - visible shared interest counter
- `Scripts/Powers/EnemyPowers/RaanaPowers/RaanaUnwellPower.cs` - visible debuff, Raana outgoing damage multiplier 0.75
- `Scripts/Powers/EnemyPowers/RaanaPowers/RaanaRisingMoodPower.cs` - visible buff, enemy-turn-end +1 Strength
- `Scripts/Cards/Collections/LeftoverBuffet.cs` - reused S3 status card
- `Scripts/Relics/EmptyParfait.cs` / `Scripts/Relics/MatchaParfait.cs` - FeedTheCat route marker relics
- `STS2_Tomorin_Mod/localization/{eng,zhs}/monsters.json`
- `STS2_Tomorin_Mod/localization/{eng,zhs}/powers.json`

`Raana.CustomVisualPath` currently reuses `soyo_boss.tscn` as a placeholder because this body-only task does not add Godot visual resources. Replace it with a Raana-specific enemy scene when the encounter/visual resource work is implemented.

**Entry route priority:** if any player has `EmptyParfait`, Raana enters empowered state and gains `RaanaRisingMoodPower`. Otherwise if any player has `MatchaParfait`, Raana enters weakened state, gains 18 Block, and starts with one-time Sleep. With neither relic, Raana defaults to empowered state. `EmptyParfait` has priority over `MatchaParfait`.

**Move loop:** empowered route starts `S1 -> S2 -> S3 -> dynamic S4 -> S1`. Weakened route starts `Sleep -> S1 -> S2 -> S3 -> dynamic S4 -> S1`. Sleep sets a pending flag; before the next S1 attack Raana receives 4 stacks of `RaanaUnwellPower`.

**Interest counter:** `RaanaInterestPower` stores interest in `Amount`, displays `DisplayAmount => Amount`, clamps at 0, and refreshes S4 preview when interest changes. Playing player-side cards adds interest: default +1, `CardRarity.Uncommon` +2, `CardRarity.Rare` +5. Exhausting player-side cards adds +1, except `LeftoverBuffet` changes interest by -2. Thresholds scale by player count: low `< 18 * players`, mid `18 * players <= interest < 30 * players`, high `>= 30 * players`.

**S3 cleanse/parfait:** S3 removes Raana debuffs by runtime type, skips `RaanaUnwellPower`, heals 8 HP per removed debuff type, and adds 2 `LeftoverBuffet` cards to each living player's hand. It then sets S4 follow-up from the current interest counter.

**S4 branches:** S4 is split into three independent `MoveState`s. Low interest deals 28/32 and gives all living players 2 Weak. Mid interest deals 28/32 and gives all living players 2 Vulnerable. High interest deals 10/11 x3 and gives Raana 1 Strength. Each S4 clears interest after resolving and returns to S1; clearing interest during S4 must not force the preview back to low S4.

### Soyo (Nagasaki Soyo)

Boss enemy body only; Encounter and Act patch registration are intentionally not handled here.

**Files:**
- `Scripts/Enemy/Soyo.cs` - enemy body and mask/true phase move state machine
- `Scripts/Powers/EnemyPowers/SoyoPowers/SoyoEstrangementPower.cs` - Mask Cracks counter, clamped at 0
- `Scripts/Powers/EnemyPowers/SoyoPowers/SoyoMaskedDamageReductionPower.cs` - False Mask damage reduction window
- `Scripts/Powers/EnemyPowers/SoyoPowers/SoyoPhaseControllerPower.cs` - side-turn phase timing and easter egg triggers
- `Scripts/Powers/EnemyPowers/SoyoPowers/SoyoTaskPower.cs` - shared random task powers
- `STS2_Tomorin_Mod/localization/{eng,zhs}/powers.json` - Soyo power/task localization

**Phase switching:** The threshold is 6, and Soyo enters true phase when Mask Cracks are greater than 6. Counter changes during mask phase can immediately push Soyo into true phase. True phase only falls back to mask at player-turn start when the counter is 6 or lower. If Soyo starts the player turn in true phase and falls back to mask, she does not auto-gain 1 Mask Crack that same turn.

**Mask phase:** cycles block+Weak, multi-attack, heal+Strength. When Soyo starts a player turn already in mask phase, Mask Cracks increase by 1; if that raises the counter above 6, she immediately enters true phase. At enemy-turn start, if Soyo is masked, she gains 1 stack of `SoyoMaskedDamageReductionPower`.

**False Mask:** `SoyoMaskedDamageReductionPower` reduces Soyo's HP loss by 75% while it has stacks. It loses 1 stack at player-turn end and is removed when Soyo enters true phase. Returning to mask phase at player-turn start does not immediately restore the damage reduction; it returns at enemy-turn start if Soyo is still masked.

**True phase:** cycles heavy attack + Wounds and multi-attack based on Mask Cracks. Each true-phase move reduces Mask Cracks by 2; when Mask Cracks are 6 or lower at the next player-turn start, Soyo returns to mask phase.

**Tasks:** all players share one task. Requirements scale by player count: 3 attacks, 3 skills, 4 played cards, or 4 total hand cards at turn end per player. Success applies the original reward and adds 2 Mask Cracks. Failure applies only the original penalty and does not increase Mask Cracks. `SoyoTaskPower.CompleteCurrentTask` settles the current task as success once.

**Easter eggs:** each easter egg is once per Soyo combat, not once per player. `PrideManSaki` plays the original voice line, stuns Soyo with `CreatureCmd.Stun`, sets Mask Cracks to 7, and immediately enters true phase. `DoEverything` plays the original voice line, gives Soyo 2 Weak and 2 Vulnerable, and completes the current task if one exists. `UtakotobaToken` plays the original voice line, preserves its original buff effect, clears Mask Cracks, and removes `SoyoMaskedDamageReductionPower` without forcing true phase.

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

### OblivionisBoss (Oblivionis + 4子Boss)

Complex boss encounter with **Center Position (C位)** mechanic, multi-phase transitions, and a hidden boss.

**Files:**
- `Scripts/Enemy/Oblivionis.cs` — Boss (600HP), Phase1/Phase2 state machine
- `Scripts/Enemy/FullPowerOblivionis.cs` — Hidden boss (1000HP)
- `Scripts/Enemy/Doloris.cs` — Sub-boss (200HP), default C位
- `Scripts/Enemy/Mortis.cs` — Sub-boss (200HP)
- `Scripts/Enemy/Timoris.cs` — Sub-boss (200HP)
- `Scripts/Enemy/Amoris.cs` — Sub-boss (200HP)
- `Scripts/Encounters/OblivionisBoss.cs` — Encounter registration
- `Scripts/Powers/EnemyPowers/OblivionisPowers/CenterPositionManagerPower.cs` — C位 manager (core)
- `Scripts/Powers/EnemyPowers/OblivionisPowers/OblivionisHiddenRevivalPower.cs` — Hidden boss revival (DoorRevivalPower pattern)
- `Scripts/Powers/EnemyPowers/OblivionisPowers/OblivionisPhase2Power.cs` — Phase 2: exhaust random hand card per turn
- `Scripts/Powers/EnemyPowers/OblivionisPowers/OblivionisHiddenInheritPower.cs` — Hidden: inherit sub-boss passive each turn
- `Scripts/Powers/EnemyPowers/OblivionisPowers/OblivionisHiddenBlockPower.cs` — Hidden: consecutive same-type block
- `Scripts/Powers/EnemyPowers/OblivionisPowers/MortisPassivePower.cs` — Mortis: Attack×0.5, non-Attack×2
- `Scripts/Powers/EnemyPowers/OblivionisPowers/TimorisPassivePower.cs` — Timoris: hit cap 3×playerCount per turn
- `Scripts/Powers/EnemyPowers/OblivionisPowers/AmorisPassivePower.cs` — Amoris: max 15 damage per hit
- `Scripts/Powers/EnemyPowers/OblivionisPowers/DolorisPassivePower.cs` — Doloris: draw-based damage reduction
- `Scripts/Powers/MortisKillBuffPower.cs` — +5 Thorns, +10 block/turn
- `Scripts/Powers/TimorisKillBuffPower.cs` — First Attack +50% damage
- `Scripts/Powers/AmorisKillBuffPower.cs` — 3rd Attack +1 energy
- `Scripts/Powers/DolorisKillBuffPower.cs` — +1 draw
- `Scripts/Cards/EnemyCards/PositionZero.cs` — Status card, select C位
- `Scripts/Cards/EnemyCards/PressureCurse.cs` — Curse card, 2-cost Inspiration, BloodLoss 2

**C位 Mechanic:** Non-C enemies gain Intangible. Players switch C位 by playing PositionZero (detected via `AfterCardPlayed`). Sub-bosses use self-looping CState/NonCState MoveStates switched by `SetMoveImmediate`. C位 death auto-switches in order: Doloris→Mortis→Timoris→Amoris→Oblivionis.

- Oblivionis phase 1 uses its existing non-C behavior while outside C position. When it is C position, its move loop depends on the number of defeated sub-boss allies and restarts at the first move whenever that count changes.

**Phase Transitions (in CenterPositionManagerPower.AfterDeath):**
- Boss death + 0 sub-bosses killed → Hidden Boss (OblivionisHiddenRevivalPower creates FullPowerOblivionis, sub-bosses escape)
- Boss death + 1-3 sub-bosses killed → living sub-bosses escape immediately, then Phase 2 (HP = current max + 500)
- Current implementation does not auto-transition when all 4 sub-bosses are killed. If Oblivionis later dies after all 4 max-HP reductions, phase 2 performs the same living sub-boss escape step and revives from current max HP + 500.

**Revive Edge Case:** If Oblivionis dies during its own enemy-turn move, such as from player Thorns gained after killing Mortis, `WaitRelive` must perform once before transitioning to phase-2 attacks. `OblivionisHiddenRevivalPower` should only keep half-dead state for the hidden-boss path; normal phase-2 revival is owned by `CenterPositionManagerPower`.

**Sub-Boss Kill Effects:** Each kill reduces Oblivionis max HP by 150 and grants all players a permanent buff power.

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
