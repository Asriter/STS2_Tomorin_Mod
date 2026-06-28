# Soyo Mask True Phase Rework Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Rework Soyo into a mask-task pressure phase plus true-output phase, with damage reduction represented by a separate False Mask power.

**Architecture:** `Soyo` owns phase transitions and move state selection. `SoyoEstrangementPower` owns the clamped counter API and notifies Soyo after counter changes. `SoyoPhaseControllerPower` owns side-turn timing and once-per-combat easter eggs. `SoyoTaskPower` owns task settlement and exposes a single success-completion API.

**Tech Stack:** C# / .NET 9, Godot 4.5.1, MegaCrit StS2 APIs, BaseLib power models, PowerShell source-contract regression tests.

---

### Task 1: Add Regression Coverage

**Files:**
- Modify: `tests/SoyoTaskPowerBehavior.Tests.ps1`

- [ ] **Step 1: Add source-contract assertions**

Extend the PowerShell test to check these contracts:
- `Soyo.cs` has `TruePhaseThreshold = 6`.
- `Soyo.cs` exposes `EnterTruePhase`, `EnterMaskPhase`, `RefreshPhaseAfterCounterChanged`, and `RefreshPhaseForPlayerTurnStart`.
- `MaskHealMove` no longer modifies `SoyoEstrangementPower`.
- `SoyoMaskedDamageReductionPower.cs` exists and contains the late HP-loss multiplier, player-turn-end decay, and flash hook.
- `SoyoEstrangementPower.cs` exposes `SetAmount` and `Clear`, and calls `RefreshPhaseAfterCounterChanged`.
- `SoyoTaskPower.cs` exposes `CompleteCurrentTask`; success adds 2 counter; failure no longer adds `Missing` counter.
- `SoyoPhaseControllerPower.cs` uses bool once-per-combat easter flags, applies False Mask on enemy turn start while masked, sets Pride counter to 7, completes current task on DoEverything, clears counter and removes False Mask on Utakotoba.

- [ ] **Step 2: Run test to verify RED**

Run: `pwsh -NoProfile -ExecutionPolicy Bypass -File tests/SoyoTaskPowerBehavior.Tests.ps1`

Expected before implementation: failure mentioning missing new Soyo phase API, missing `SoyoMaskedDamageReductionPower.cs`, or old task/counter behavior.

### Task 2: Implement Soyo Phase APIs and Move Changes

**Files:**
- Modify: `Scripts/Enemy/Soyo.cs`

- [ ] **Step 1: Update constants and phase methods**

Change threshold to 6. Replace `RefreshPhaseByEstrangement()` with:
- `EnterTruePhase()`
- `EnterMaskPhase()`
- `RefreshPhaseAfterCounterChanged()`
- `RefreshPhaseForPlayerTurnStart()`

`EnterTruePhase()` removes all `SoyoMaskedDamageReductionPower`. `EnterMaskPhase()` only changes phase and move intent.

- [ ] **Step 2: Update moves**

Remove the mask heal `counter -2` behavior. Keep true move `counter -2`.

### Task 3: Add False Mask Power

**Files:**
- Create: `Scripts/Powers/EnemyPowers/SoyoPowers/SoyoMaskedDamageReductionPower.cs`

- [ ] **Step 1: Implement power**

Create `SoyoMaskedDamageReductionPower : BasePowerModel` with:
- `PowerType.Buff`
- `PowerStackType.Counter`
- `ModifyHpLostBeforeOstyLate(...) => amount * 0.25m` only for owner with positive stacks
- player-turn-end stack decay and removal at 0
- `AfterModifyingHpLostBeforeOsty()` flash

### Task 4: Update Counter and Task APIs

**Files:**
- Modify: `Scripts/Powers/EnemyPowers/SoyoPowers/SoyoEstrangementPower.cs`
- Modify: `Scripts/Powers/EnemyPowers/SoyoPowers/SoyoTaskPower.cs`

- [ ] **Step 1: Update counter API**

Keep `GetAmount()` and `Modify()`. Add `SetAmount(...)` and `Clear(...)`. After every change or set, call `RefreshPhaseAfterCounterChanged()` when owner monster is Soyo.

- [ ] **Step 2: Update task settlement**

Split task settlement into success and failure helpers. Success applies reward and counter +2. Failure applies penalty only. Add `CompleteCurrentTask(...)` that settles the current task as success once.

### Task 5: Update Phase Controller and Easter Eggs

**Files:**
- Modify: `Scripts/Powers/EnemyPowers/SoyoPowers/SoyoPhaseControllerPower.cs`

- [ ] **Step 1: Update turn flow**

Player turn start calls `RefreshPhaseForPlayerTurnStart()`, then removes task if true or applies random task if mask. Enemy turn start applies one `SoyoMaskedDamageReductionPower` stack only while mask.

- [ ] **Step 2: Update easter eggs**

Replace per-player HashSets with bool flags. Pride sets counter to 7 and stuns. DoEverything applies Weak/Vulnerable and completes current task if present. Utakotoba clears counter and removes False Mask.

### Task 6: Update Localization and Project Docs

**Files:**
- Modify: `STS2_Tomorin_Mod/localization/eng/powers.json`
- Modify: `STS2_Tomorin_Mod/localization/zhs/powers.json`
- Modify: `STS2_Tomorin_Mod/localization/eng/cards.json`
- Modify: `STS2_Tomorin_Mod/localization/zhs/cards.json`
- Modify: `CLAUDE.md`
- Modify: `日志.txt`
- Modify: `文档.txt`

- [ ] **Step 1: Update power and task localization**

Rename displayed counter meaning to Mask Cracks / 假面裂痕, add False Mask / 虚伪的假面 text, update task success/failure descriptions, and add once-per-Soyo-combat notes to visible easter egg card descriptions.

- [ ] **Step 2: Update docs**

Update `CLAUDE.md` Soyo section. Append implementation record to `日志.txt`. Append requirement/API notes to `文档.txt`.

### Task 7: Verify

**Files:**
- Test: `tests/SoyoTaskPowerBehavior.Tests.ps1`

- [ ] **Step 1: Run source-contract test**

Run: `pwsh -NoProfile -ExecutionPolicy Bypass -File tests/SoyoTaskPowerBehavior.Tests.ps1`

Expected: `Soyo task power behavior checks passed.`

- [ ] **Step 2: Build**

Run: `dotnet build`

Expected: exit code 0.

- [ ] **Step 3: Inspect git diff**

Run: `git diff -- Scripts/Enemy/Soyo.cs Scripts/Powers/EnemyPowers/SoyoPowers/SoyoEstrangementPower.cs Scripts/Powers/EnemyPowers/SoyoPowers/SoyoTaskPower.cs Scripts/Powers/EnemyPowers/SoyoPowers/SoyoPhaseControllerPower.cs Scripts/Powers/EnemyPowers/SoyoPowers/SoyoMaskedDamageReductionPower.cs STS2_Tomorin_Mod/localization/eng/powers.json STS2_Tomorin_Mod/localization/zhs/powers.json STS2_Tomorin_Mod/localization/eng/cards.json STS2_Tomorin_Mod/localization/zhs/cards.json tests/SoyoTaskPowerBehavior.Tests.ps1 CLAUDE.md 日志.txt 文档.txt`

Expected: changes are limited to requested Soyo mechanism, tests, and requested docs.

---

## Plan Self-Review

- Spec coverage: Every confirmed design point maps to Tasks 2-6; verification maps to Task 7.
- Placeholder scan: No open placeholders remain.
- Type consistency: Method and class names match the approved design and current namespace `STS2_Tomorin_Mod.Powers`.
