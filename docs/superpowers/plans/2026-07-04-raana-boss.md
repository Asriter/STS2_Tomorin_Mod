# Raana Boss Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement the confirmed Raana boss body, interest counter, Unwell debuff, Rising Mood buff, localization, focused regression checks, and project documentation without adding encounter registration.

**Architecture:** Add one `CustomMonsterModel` implementation for Raana and three focused `BasePowerModel` classes. Raana owns the MoveState graph and S4 branch switching, while `RaanaInterestPower` is the only place that mutates interest and triggers S4 preview refresh. Keep event-route detection relic-based through the existing `EmptyParfait` and `MatchaParfait` relics.

**Tech Stack:** .NET 9, Godot 4.5.1 resources, BaseLib custom model APIs, MegaCrit StS2 combat commands, PowerShell-focused regression checks.

---

## Retrieval Evidence

- Basic Memory MCP was available but constrained project routing returned `blade-field-test` for `TODO List` and failed exact reads for the Tomorin note.
- CLI fallback succeeded with:
  - `basic-memory tool read-note 'tomorin-mod/designs/raana-boss-本体-兴趣值-counter-不调-三分支第四阶段-设计' --project-id '03b23de7-f66e-42ae-860e-21a3d7927820' --local --include-frontmatter`
  - Returned permalink: `tomorin-mod/designs/raana-boss-本体-兴趣值-counter-不调-三分支第四阶段-设计`
  - Returned file path: `designs/Raana Boss 本体 兴趣值 Counter 不调 三分支第四阶段 设计.md`
- TomorinMod TODO List was read with:
  - `basic-memory tool read-note 'TODO List' --project-id '03b23de7-f66e-42ae-860e-21a3d7927820' --local --include-frontmatter`
  - Current task present: `Raana Boss 本体实现`

## Pre-Execution Assessment

- Feasibility: feasible with current code structure. Existing bosses use `CustomMonsterModel`, `MonsterMoveStateMachine`, `MoveState`, `SetMoveImmediate`, `PowerCmd.Apply`, `PowerCmd.Remove`, `DamageCmd.Attack`, `CreatureCmd.GainBlock`, and `CardPileCmd.AddGeneratedCardToCombat` patterns needed for the design.
- Ambiguity: no design ambiguity requiring new product decisions. The spec already fixes relic priority, default route, Sleep timing, Unwell duration/decay, interest deltas, multiplayer threshold scaling, S3 cleanse behavior, and S4 live preview switching.
- Structural conflicts: no severe conflict. `EmptyParfait`, `MatchaParfait`, and FeedTheCat are currently uncommitted workspace changes, so implementation must build on them and avoid overwriting them.
- API risk: `DamageCmd.Attack(...).FromMonster(this).Execute(null)` appears to target the current monster target, while the design requires all living players. Existing code often passes `targets` to `PowerCmd.Apply`, but damage commands do not visibly pass `targets`. Implementation must verify whether `FromMonster(this)` already attacks all current targets in monster move execution; if not, use the available command API or loop living players without changing encounter scope.
- Resource risk: `BasePowerModel` resolves icons by class name. To avoid missing new image assets, the new Raana powers should either add icon files or override `CustomPackedIconPath`/`CustomBigIconPath` to reuse existing icons. Prefer reusing existing icons unless real assets already exist.
- Branch/worktree risk: repository is on `main` with many unrelated uncommitted changes. Do not start implementation until the user confirms whether to continue in-place or create/use an isolated worktree.

## Files

- Create: `Scripts/Enemy/Raana.cs`
- Create: `Scripts/Powers/EnemyPowers/RaanaPowers/RaanaInterestPower.cs`
- Create: `Scripts/Powers/EnemyPowers/RaanaPowers/RaanaUnwellPower.cs`
- Create: `Scripts/Powers/EnemyPowers/RaanaPowers/RaanaRisingMoodPower.cs`
- Create: `tests/RaanaBossBehavior.Tests.ps1`
- Modify: `STS2_Tomorin_Mod/localization/eng/monsters.json`
- Modify: `STS2_Tomorin_Mod/localization/zhs/monsters.json`
- Modify: `STS2_Tomorin_Mod/localization/eng/powers.json`
- Modify: `STS2_Tomorin_Mod/localization/zhs/powers.json`
- Modify after implementation: `CLAUDE.md`
- Modify after implementation: `日志.txt`
- Modify after implementation: `文档.txt`
- Modify after implementation: TomorinMod Basic Memory `TODO List.md` via CLI, removing `Raana Boss 本体实现`

### Task 1: Raana Power Models

**Files:**
- Create: `Scripts/Powers/EnemyPowers/RaanaPowers/RaanaInterestPower.cs`
- Create: `Scripts/Powers/EnemyPowers/RaanaPowers/RaanaUnwellPower.cs`
- Create: `Scripts/Powers/EnemyPowers/RaanaPowers/RaanaRisingMoodPower.cs`
- Test: `tests/RaanaBossBehavior.Tests.ps1`

- [ ] **Step 1: Add focused source checks for the three powers**

Create `tests/RaanaBossBehavior.Tests.ps1` with checks that assert the class names, stack types, hook methods, interest thresholds, clamp to zero, `LeftoverBuffet` exhaust delta, S4 refresh call, Unwell 0.75 damage multiplier, Unwell decay/removal, and Rising Mood strength gain.

- [ ] **Step 2: Run the focused test to verify it fails**

Run: `powershell -ExecutionPolicy Bypass -File tests/RaanaBossBehavior.Tests.ps1`

Expected: FAIL because the Raana power files do not exist yet.

- [ ] **Step 3: Implement the three power classes**

Implement:
- `RaanaInterestPower : BasePowerModel`
  - `PowerType.Buff`
  - `PowerStackType.Counter`
  - `DisplayAmount => Amount`
  - `ModifyInterest(PlayerChoiceContext choiceContext, int delta, CardModel? source)`
  - low/high thresholds: `18 * Math.Max(1, CombatState.Players.Count)` and `30 * Math.Max(1, CombatState.Players.Count)`
  - `AfterCardPlayed`: player-owned cards only, Common/Status/basic default `+1`, `Uncommon +2`, `Rare +5`
  - `AfterCardExhausted`: player-owned cards only, normal `+1`, `LeftoverBuffet -2`
  - clamp minimum to 0, call `InvokeDisplayAmountChanged()`, and call `Raana.RefreshInterestMoveStateIfNeeded()`
- `RaanaUnwellPower : BasePowerModel`
  - `PowerType.Debuff`
  - `PowerStackType.Counter`
  - `ModifyDamageMultiplicative` returns `0.75m` when `dealer == Owner`
  - decay on `BeforeSideTurnEnd` or equivalent owner-side turn-end hook, remove at 0
- `RaanaRisingMoodPower : BasePowerModel`
  - `PowerType.Buff`
  - `PowerStackType.Single`
  - enemy-side turn-end hook applies `StrengthPower` amount 1 to owner if alive

- [ ] **Step 4: Run the focused test**

Run: `powershell -ExecutionPolicy Bypass -File tests/RaanaBossBehavior.Tests.ps1`

Expected: PASS for power checks, Raana monster checks still fail until Task 2.

### Task 2: Raana Monster Body and MoveState Graph

**Files:**
- Create: `Scripts/Enemy/Raana.cs`
- Test: `tests/RaanaBossBehavior.Tests.ps1`

- [ ] **Step 1: Extend the focused test with monster checks**

Add checks that assert:
- `Raana : CustomMonsterModel`
- `AfterAddedToRoom` applies `RaanaInterestPower`
- relic priority is `EmptyParfait` before `MatchaParfait`
- `MatchaParfait` route gains 18 block and starts at Sleep
- no-relic and `EmptyParfait` routes apply `RaanaRisingMoodPower`
- `GenerateMoveStateMachine` declares Sleep, S1, S2, S3, S4_LOW, S4_MID, S4_HIGH as separate `MoveState` instances
- S3 excludes `RaanaUnwellPower` from debuff cleanse
- S3 gives each living player 2 `LeftoverBuffet`
- `ResolveInterestMoveState`, `RefreshInterestMoveStateIfNeeded`, and `IsCurrentInterestPreviewState` exist
- S4 actions reset interest and return to S1

- [ ] **Step 2: Run the focused test to verify monster checks fail**

Run: `powershell -ExecutionPolicy Bypass -File tests/RaanaBossBehavior.Tests.ps1`

Expected: FAIL because `Scripts/Enemy/Raana.cs` does not exist.

- [ ] **Step 3: Implement `Raana.cs`**

Implement `Scripts/Enemy/Raana.cs` following existing boss style:
- Constants:
  - weakened entry block `18`
  - unwell stacks `4`
  - S1 `18 -> 21`
  - S2 `5 -> 6`, hit count `4`, block `25`
  - S3 heal per debuff type `8`, buffet per player `2`
  - S4 low/mid `28 -> 32`
  - S4 high `10 -> 11`, hit count `3`
- Fields:
  - `_sleepState`, `_s1State`, `_s2State`, `_s3State`, `_s4LowState`, `_s4MidState`, `_s4HighState`
  - `_applyUnwellOnNextRaanaTurnStart`
  - `_isResolvingS4`
- `AfterAddedToRoom`:
  - base call
  - apply `RaanaInterestPower`
  - if any player has `EmptyParfait`: apply `RaanaRisingMoodPower`, set S1
  - else if any player has `MatchaParfait`: gain 18 block, set Sleep
  - else: apply `RaanaRisingMoodPower`, set S1
- MoveState graph:
  - Sleep -> S1 -> S2 -> S3 -> dynamic S4 -> S1
  - S3 chooses S4 by calling `ResolveInterestMoveState()` before/when leaving S3
  - S4 states are separate instances with distinct intents
- S4 refresh:
  - `ResolveInterestMoveState()`
  - `RefreshInterestMoveStateIfNeeded()`
  - `IsCurrentInterestPreviewState()`
  - avoid resetting preview while clearing counter after S4 resolution

- [ ] **Step 4: Run the focused test**

Run: `powershell -ExecutionPolicy Bypass -File tests/RaanaBossBehavior.Tests.ps1`

Expected: PASS for monster and power source-level behavior checks.

### Task 3: Localization and Icon Handling

**Files:**
- Modify: `STS2_Tomorin_Mod/localization/eng/monsters.json`
- Modify: `STS2_Tomorin_Mod/localization/zhs/monsters.json`
- Modify: `STS2_Tomorin_Mod/localization/eng/powers.json`
- Modify: `STS2_Tomorin_Mod/localization/zhs/powers.json`
- Test: `tests/RaanaBossBehavior.Tests.ps1`

- [ ] **Step 1: Extend focused localization checks**

Assert localization keys exist:
- `STS2_TOMORIN_MOD-RAANA.name`
- `STS2_TOMORIN_MOD-RAANA_INTEREST_POWER.title`
- `STS2_TOMORIN_MOD-RAANA_INTEREST_POWER.description`
- `STS2_TOMORIN_MOD-RAANA_INTEREST_POWER.smartDescription`
- `STS2_TOMORIN_MOD-RAANA_UNWELL_POWER.title`
- `STS2_TOMORIN_MOD-RAANA_RISING_MOOD_POWER.title`

- [ ] **Step 2: Run the focused test to verify localization checks fail**

Run: `powershell -ExecutionPolicy Bypass -File tests/RaanaBossBehavior.Tests.ps1`

Expected: FAIL because the new localization keys are absent.

- [ ] **Step 3: Add localization entries**

Add English and Chinese monster/power strings. Include `{Amount}` in smart descriptions where the displayed counter matters.

- [ ] **Step 4: Avoid missing icon resources**

If no Raana icon files exist, override the new power icon paths to reuse an existing checked-in icon. Do not add Godot image resources unless actual image assets are available.

- [ ] **Step 5: Run the focused test**

Run: `powershell -ExecutionPolicy Bypass -File tests/RaanaBossBehavior.Tests.ps1`

Expected: PASS.

### Task 4: Build and Existing Regression Checks

**Files:**
- No planned source edits unless verification exposes compile issues.

- [ ] **Step 1: Run focused tests**

Run:
- `powershell -ExecutionPolicy Bypass -File tests/RaanaBossBehavior.Tests.ps1`
- `powershell -ExecutionPolicy Bypass -File tests\FeedTheCat.Tests.ps1`
- `powershell -ExecutionPolicy Bypass -File tests\SoyoTaskPowerBehavior.Tests.ps1`
- `powershell -ExecutionPolicy Bypass -File tests\FixedFirstEvent.Tests.ps1`

Expected: all pass.

- [ ] **Step 2: Run build**

Run: `dotnet build`

Expected: exit code 0.

- [ ] **Step 3: Decide whether publish is required**

If only C# and JSON localization changed, `dotnet build` is enough. If new or modified Godot resources are added, run `dotnet publish`.

### Task 5: Documentation, Memory TODO Cleanup, and Final Review

**Files:**
- Modify: `CLAUDE.md`
- Modify: `日志.txt`
- Modify: `文档.txt`
- Modify via CLI: TomorinMod Basic Memory `TODO List.md`

- [ ] **Step 1: Update `CLAUDE.md`**

Add a short Raana Boss section covering the implemented files, route relic priority, interest counter thresholds, Unwell, Rising Mood, S3 cleanse/LeftoverBuffet, and S4 three-branch behavior.

- [ ] **Step 2: Update `日志.txt`**

Append an entry with date `2026-07-04`, requirement summary, changed files, verification commands, and any known limitations.

- [ ] **Step 3: Update `文档.txt`**

Add requirement/interface notes for:
- `Raana`
- `RaanaInterestPower.ModifyInterest(PlayerChoiceContext, int, CardModel?)`
- `Raana.RefreshInterestMoveStateIfNeeded()`
- `Raana.ResolveInterestMoveState()`
- `RaanaUnwellPower`
- `RaanaRisingMoodPower`

- [ ] **Step 4: Remove the completed Basic Memory TODO**

Use CLI fallback, because MCP project routing was unreliable:

```powershell
basic-memory tool read-note 'TODO List' --project-id '03b23de7-f66e-42ae-860e-21a3d7927820' --local --include-frontmatter
basic-memory tool edit-note 'TODO List' --project-id '03b23de7-f66e-42ae-860e-21a3d7927820' --local --operation replace_section --section 'Raana Boss 本体实现' --content ''
```

If `edit-note` syntax differs, use its `--help`, then remove only the `Raana Boss 本体实现` section and re-read the note to verify removal.

- [ ] **Step 5: Request review and verify before completion**

Use `superpowers:requesting-code-review` if subagents are available. Independently re-run:
- `powershell -ExecutionPolicy Bypass -File tests/RaanaBossBehavior.Tests.ps1`
- `dotnet build`

Expected: both exit code 0 before reporting completion.
