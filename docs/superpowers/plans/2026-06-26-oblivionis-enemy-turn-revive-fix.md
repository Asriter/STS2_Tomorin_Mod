# Oblivionis Enemy-Turn Revive Fix Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix Oblivionis getting stuck at 0 HP and unattackable when it dies during its own enemy-turn attack.

**Architecture:** Keep the existing phase-2 revive flow owned by `CenterPositionManagerPower`, but make the `WaitRelive` move mandatory before its follow-up can be rolled. Keep `OblivionisHiddenRevivalPower` scoped to the hidden-boss path so it does not leave `isHalfDead` latched during normal phase-2 revival.

**Tech Stack:** C#/.NET 9, Godot 4.5.1, Slay the Spire 2 mod APIs, BaseLib.

---

### Task 1: Make WaitRelive Mandatory

**Files:**
- Modify: `Scripts/Enemy/Oblivionis.cs`

- [x] **Step 1: Update WaitRelive creation**

Change the `WaitRelive` state construction to:

```csharp
WaitRelive = new MoveState("Relive", WaitReliveMove, new BuffIntent(), new HealIntent())
{
    MustPerformOnceBeforeTransitioning = true
};
```

- [x] **Step 2: Verify behavior by code inspection**

Confirm `WaitRelive.FollowUpState = Phase2State;` remains unchanged and `WaitReliveMove()` still performs `SetMaxHp`, `Heal`, and removes `CenterPositionManagerPower` / `OblivionisHiddenRevivalPower`.

### Task 2: Clear Hidden Revival State on Non-Hidden Path

**Files:**
- Modify: `Scripts/Powers/EnemyPowers/OblivionisPowers/OblivionisHiddenRevivalPower.cs`

- [x] **Step 1: Return early for non-hidden path before escaping sub-bosses**

After the `wasRemovalPrevented` block, add:

```csharp
if (subBossCount < 4)
{
    GetInternalData<Data>().isHalfDead = false;
    return;
}
```

Keep the existing sub-boss escape loop below this guard so it only runs for the hidden-boss path.

- [x] **Step 2: Verify hidden path still clears state**

Confirm the existing `GetInternalData<Data>().isHalfDead = false;` near the end of the hidden path remains in place.

### Task 3: Documentation

**Files:**
- Modify: `CLAUDE.md`
- Modify: `文档.txt`
- Modify: `日志.txt`

- [x] **Step 1: Update CLAUDE.md**

Add a note under Oblivionis phase transitions explaining that `WaitRelive` must execute once, including when Oblivionis dies during its own enemy-turn attack from thorns or other reflected damage.

- [x] **Step 2: Update 文档.txt**

Add an Oblivionis revive edge-case note describing the enemy-turn death fix and non-hidden hidden-revival guard.

- [x] **Step 3: Update 日志.txt**

Add a dated change entry for the bug fix, touched files, and verification command.

### Task 4: Verification

**Files:**
- Build project

- [x] **Step 1: Run build**

Run:

```powershell
dotnet build
```

Expected: exit code 0.

- [x] **Step 2: Inspect diff**

Run:

```powershell
git diff -- Scripts/Enemy/Oblivionis.cs Scripts/Powers/EnemyPowers/OblivionisPowers/OblivionisHiddenRevivalPower.cs CLAUDE.md 文档.txt 日志.txt docs/superpowers/plans/2026-06-26-oblivionis-enemy-turn-revive-fix.md
```

Expected: only planned changes appear.
