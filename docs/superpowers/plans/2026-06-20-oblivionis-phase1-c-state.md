# Oblivionis Phase 1 C-State Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Change Oblivionis phase 1 so C-position behavior uses a death-count-specific move loop, while non-C behavior remains the existing 18 HP heal.

**Architecture:** Keep the existing C-position manager as the source of teammate death count through `killRegistry.Count`. Build explicit phase 1 C-position `MoveState` loops in `Oblivionis`, and have `CenterPositionManagerPower` pass the current dead teammate count when Oblivionis becomes C-position or when a teammate dies while Oblivionis is already C-position.

**Tech Stack:** C# / .NET 9, Godot 4.5.1, Slay the Spire 2 mod APIs, BaseLib, CodeGraph for code navigation.

---

### Task 1: Add Oblivionis Phase 1 C-Position State Loops

**Files:**
- Modify: `Scripts/Enemy/Oblivionis.cs`

- [ ] **Step 1: Replace phase 1 constants with explicit C/non-C values**

Use constants for each requested damage/heal/debuff value:

```csharp
private const int Phase1NonCHealAmount = 18;
private const int Phase1CHealAmount = 30;
private const int Phase1LessDrawAmount = 1;
private const int Phase1VulnerableAmount = 1;
private const int Phase1MultiHitCount = 3;
```

Add helpers for ascension-scaled values:

```csharp
private static int Phase1HighDamage =>
    AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 28, 25);

private static int Phase1MediumDamage =>
    AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 23, 20);

private static int Phase1HighMultiDamage =>
    AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 9, 8);

private static int Phase1LowMultiDamage =>
    AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 7, 6);
```

- [ ] **Step 2: Add fields/properties for C-position state entries**

Replace the single `_cState` storage with four C-position entry states, and expose a method for state selection:

```csharp
private MoveState _phase1CNoDeadState1;
private MoveState _phase1COneDeadState1;
private MoveState _phase1CTwoDeadState1;
private MoveState _phase1CThreeDeadState1;
private int _phase1DeadAllyCount;
```

Keep `NonCState`, `Phase2State`, `DeadState`, and `WaitRelive` public properties unchanged for existing callers.

- [ ] **Step 3: Add C-position selection methods**

Add this public method:

```csharp
public void SetPhase1CStateByDeadAllies(int deadAllyCount, bool forceTransition = true)
{
    _phase1DeadAllyCount = Math.Clamp(deadAllyCount, 0, 3);
    SetMoveImmediate(GetPhase1CEntryState(_phase1DeadAllyCount), forceTransition);
}
```

Add this private helper:

```csharp
private MoveState GetPhase1CEntryState(int deadAllyCount)
{
    return Math.Clamp(deadAllyCount, 0, 3) switch
    {
        0 => _phase1CNoDeadState1,
        1 => _phase1COneDeadState1,
        2 => _phase1CTwoDeadState1,
        _ => _phase1CThreeDeadState1
    };
}
```

Add `using System;` to support `Math.Clamp`.

- [ ] **Step 4: Build explicit phase 1 C-position MoveStates**

In `GenerateMoveStateMachine`, replace old `CState` creation with four loops:

```csharp
var noDeadS1 = new MoveState("OBLIVIONIS_P1_C_0_S1", Phase1CNoDeadS1Move,
    new SingleAttackIntent(Phase1HighDamage), new DebuffIntent());
var noDeadS2 = new MoveState("OBLIVIONIS_P1_C_0_S2", Phase1CNoDeadS2Move,
    new MultiAttackIntent(Phase1HighMultiDamage, Phase1MultiHitCount));
var noDeadS3 = new MoveState("OBLIVIONIS_P1_C_0_S3", Phase1CNoDeadS3Move,
    new HealIntent(), new DebuffIntent());
```

Repeat for 1-dead, 2-dead, and 3-dead loops:

```csharp
var oneDeadS1 = new MoveState("OBLIVIONIS_P1_C_1_S1", Phase1COneDeadS1Move,
    new SingleAttackIntent(Phase1HighDamage));
var oneDeadS2 = new MoveState("OBLIVIONIS_P1_C_1_S2", Phase1CLowMultiMove,
    new MultiAttackIntent(Phase1LowMultiDamage, Phase1MultiHitCount));
var oneDeadS3 = new MoveState("OBLIVIONIS_P1_C_1_S3", Phase1COneDeadS3Move,
    new HealIntent(), new DebuffIntent());

var twoDeadS1 = new MoveState("OBLIVIONIS_P1_C_2_S1", Phase1CTwoOrThreeDeadS1Move,
    new SingleAttackIntent(Phase1MediumDamage));
var twoDeadS2 = new MoveState("OBLIVIONIS_P1_C_2_S2", Phase1CLowMultiMove,
    new MultiAttackIntent(Phase1LowMultiDamage, Phase1MultiHitCount));
var twoDeadS3 = new MoveState("OBLIVIONIS_P1_C_2_S3", Phase1CTwoDeadS3Move,
    new HealIntent());

var threeDeadS1 = new MoveState("OBLIVIONIS_P1_C_3_S1", Phase1CTwoOrThreeDeadS1Move,
    new SingleAttackIntent(Phase1MediumDamage));
var threeDeadS2 = new MoveState("OBLIVIONIS_P1_C_3_S2", Phase1CLowMultiMove,
    new MultiAttackIntent(Phase1LowMultiDamage, Phase1MultiHitCount));
```

Wire follow-ups:

```csharp
noDeadS1.FollowUpState = noDeadS2;
noDeadS2.FollowUpState = noDeadS3;
noDeadS3.FollowUpState = noDeadS1;

oneDeadS1.FollowUpState = oneDeadS2;
oneDeadS2.FollowUpState = oneDeadS3;
oneDeadS3.FollowUpState = oneDeadS1;

twoDeadS1.FollowUpState = twoDeadS2;
twoDeadS2.FollowUpState = twoDeadS3;
twoDeadS3.FollowUpState = twoDeadS1;

threeDeadS1.FollowUpState = threeDeadS2;
threeDeadS2.FollowUpState = threeDeadS1;
```

Assign entries:

```csharp
_phase1CNoDeadState1 = noDeadS1;
_phase1COneDeadState1 = oneDeadS1;
_phase1CTwoDeadState1 = twoDeadS1;
_phase1CThreeDeadState1 = threeDeadS1;
```

Add all new states to the `states` list before phase 2 states.

- [ ] **Step 5: Implement phase 1 C-position move methods**

Add common helpers:

```csharp
private async Task HealAliveEnemies(decimal amount)
{
    foreach (var enemy in base.CombatState.Enemies)
    {
        if (enemy.IsAlive)
            await CreatureCmd.Heal(enemy, amount);
    }
}

private async Task Phase1SingleAttack(int damage)
{
    await DamageCmd.Attack(damage).FromMonster(this)
        .WithHitFx("vfx/vfx_attack_blunt")
        .Execute(null);
}

private async Task Phase1MultiAttack(int damage)
{
    await DamageCmd.Attack(damage).WithHitCount(Phase1MultiHitCount)
        .FromMonster(this)
        .WithHitFx("vfx/vfx_attack_blunt")
        .Execute(null);
}
```

Implement the requested effects:

```csharp
private async Task Phase1CNoDeadS1Move(IReadOnlyList<Creature> targets)
{
    await Phase1SingleAttack(Phase1HighDamage);
    await PowerCmd.Apply<LessDrawNextTurnPower>(new ThrowingPlayerChoiceContext(), targets, Phase1LessDrawAmount, base.Creature, null);
}

private async Task Phase1CNoDeadS2Move(IReadOnlyList<Creature> targets)
{
    await Phase1MultiAttack(Phase1HighMultiDamage);
}

private async Task Phase1CNoDeadS3Move(IReadOnlyList<Creature> targets)
{
    await HealAliveEnemies(Phase1CHealAmount);
    await PowerCmd.Apply<VulnerablePower>(new ThrowingPlayerChoiceContext(), targets, Phase1VulnerableAmount, base.Creature, null);
}

private async Task Phase1COneDeadS1Move(IReadOnlyList<Creature> targets)
{
    await Phase1SingleAttack(Phase1HighDamage);
}

private async Task Phase1CLowMultiMove(IReadOnlyList<Creature> targets)
{
    await Phase1MultiAttack(Phase1LowMultiDamage);
}

private async Task Phase1COneDeadS3Move(IReadOnlyList<Creature> targets)
{
    await HealAliveEnemies(Phase1CHealAmount);
    await PowerCmd.Apply<VulnerablePower>(new ThrowingPlayerChoiceContext(), targets, Phase1VulnerableAmount, base.Creature, null);
}

private async Task Phase1CTwoOrThreeDeadS1Move(IReadOnlyList<Creature> targets)
{
    await Phase1SingleAttack(Phase1MediumDamage);
}

private async Task Phase1CTwoDeadS3Move(IReadOnlyList<Creature> targets)
{
    await HealAliveEnemies(Phase1CHealAmount);
}
```

Update `Phase1NonCMove` to call `HealAliveEnemies(Phase1NonCHealAmount)`.

- [ ] **Step 6: Run build**

Run:

```powershell
dotnet build
```

Expected: build exits with code 0.

### Task 2: Wire CenterPositionManagerPower to the New C-State Selector

**Files:**
- Modify: `Scripts/Powers/EnemyPowers/OblivionisPowers/CenterPositionManagerPower.cs`

- [ ] **Step 1: Change Oblivionis C-position switching**

In `SwitchToCState`, change the `Oblivionis` case from the old property assignment to the new method with the current dead teammate count. Because `SwitchToCState` currently has no access to `Data`, change its signature from:

```csharp
private static void SwitchToCState(MonsterModel? monster)
```

to:

```csharp
private static void SwitchToCState(MonsterModel? monster, int deadAllyCount)
```

Update the switch case:

```csharp
case Oblivionis o: o.SetPhase1CStateByDeadAllies(deadAllyCount); break;
```

Keep other enemy cases unchanged.

- [ ] **Step 2: Pass killRegistry.Count from all C-position switch sites**

Update both call sites:

```csharp
SwitchToCState(newTarget.Monster, data.killRegistry.Count);
```

and:

```csharp
SwitchToCState(enemy.Monster, data.killRegistry.Count);
```

- [ ] **Step 3: Restart Oblivionis C loop when teammate death count changes**

After `data.killRegistry.Add(creature.Monster!.GetType())` and after phase-2 threshold check, restart only if Oblivionis is currently the center creature and not transitioning to phase 2:

```csharp
if (data.killRegistry.Count < 4 &&
    data.centerPositionCreature?.Monster is Oblivionis oblivionis)
{
    oblivionis.SetPhase1CStateByDeadAllies(data.killRegistry.Count);
}
```

This satisfies the confirmed rule: teammate death switches to the corresponding state machine's first move only while Oblivionis is C-position.

- [ ] **Step 4: Run build**

Run:

```powershell
dotnet build
```

Expected: build exits with code 0.

### Task 3: Update Project Docs and Logs

**Files:**
- Modify: `CLAUDE.md`
- Modify: `日志.txt`
- Modify: `文档.txt`

- [ ] **Step 1: Update CLAUDE.md**

Add a concise note under the Oblivionis/C-position boss documentation:

```markdown
- Oblivionis phase 1 uses its existing non-C behavior while outside C position. When it is C position, its move loop depends on the number of defeated sub-boss allies and restarts at the first move whenever that count changes.
```

- [ ] **Step 2: Append implementation record to 日志.txt**

Append:

```text
2026-06-20
- 修改 Oblivionis 一阶段 C 位机制：根据已死亡队友数量切换不同状态机；队友死亡时若 Oblivionis 当前为 C 位，则切换到对应状态机第 1 招。
- 保留 Oblivionis 非 C 位旧行为：所有存活敌人回复 18 点生命值。
- CenterPositionManagerPower 负责在切换 C 位和队友死亡时传递死亡队友数量。
```

- [ ] **Step 3: Append requirement/interface notes to 文档.txt**

Append:

```text
Oblivionis 一阶段 C 位状态机接口说明

需求：
- Oblivionis 不在 C 位时，不受队友死亡数量影响，沿用非 C 位行为。
- Oblivionis 在 C 位时，根据 Doloris/Mortis/Timoris/Amoris 已死亡数量选择 0/1/2/3 死亡状态机。
- 队友死亡时，如果 Oblivionis 当前为 C 位，立即进入对应死亡数量状态机第 1 招。

新增接口：
- Oblivionis.SetPhase1CStateByDeadAllies(int deadAllyCount, bool forceTransition = true)
  - deadAllyCount 会被限制在 0..3。
  - 用于 CenterPositionManagerPower 在切换 C 位或死亡数量变化时设置一阶段 C 位入口状态。
```

- [ ] **Step 4: Final verification**

Run:

```powershell
dotnet build
```

Expected: build exits with code 0.

Then inspect:

```powershell
git diff -- Scripts/Enemy/Oblivionis.cs Scripts/Powers/EnemyPowers/OblivionisPowers/CenterPositionManagerPower.cs CLAUDE.md 日志.txt 文档.txt
```

Expected: only the planned behavior and documentation/log changes are present.

---

## Plan Self-Review

- Spec coverage: Covers C-position death-count loops, non-C behavior preservation, death-trigger restart at S1, C-position entry behavior, docs/log updates, and build verification.
- Placeholder scan: No TBD/TODO placeholders.
- Type consistency: `SetPhase1CStateByDeadAllies`, `deadAllyCount`, `Phase1C*Move`, `HealAliveEnemies`, and `SwitchToCState(..., int deadAllyCount)` are named consistently across tasks.
