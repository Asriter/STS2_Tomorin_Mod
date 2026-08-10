# FeedTheCat Event Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement the fixed Act 2 first event `FeedTheCat` with first-layer multiplayer voting, independent second-layer choices, route marker relics, localization, verification, and project documentation.

**Architecture:** `FeedTheCat` remains a non-shared `CustomEventModel`. Native non-shared `EventSynchronizer` broadcasts first-layer option clicks for each player; a `FeedTheCatVoteCoordinator` aggregates those votes, lets the host resolve the final branch once, and broadcasts the branch via `PlayerChoiceSynchronizer` using `PlayerChoiceResult.FromIndex`. The second layer uses ordinary per-player `EventOption` handlers.

**Tech Stack:** C#/.NET 9, Godot 4.5.1, Slay the Spire 2 `sts2.dll`, BaseLib 3.3.3, HarmonyLib, PowerShell focused static tests, Basic Memory CLI for TODO update.

---

## File Map

- Create `Scripts/Events/FeedTheCat.cs`: event pages, option handlers, reward/penalty actions, final branch application.
- Create `Scripts/Events/FeedTheCatVoteCoordinator.cs`: per-event vote aggregation, host branch resolution, branch sync, idempotency, cleanup.
- Create `Scripts/Relics/MatchaParfait.cs`: reward route marker relic.
- Create `Scripts/Relics/EmptyParfait.cs`: penalty route marker relic.
- Modify `Scripts/RelicPools/TomorinRelicPool.cs`: register both marker relics.
- Modify `Scripts/Patch/FixedFirstEventPatch.cs`: use `ModelDb.Event<FeedTheCat>()` and restore `IsAllowed`.
- Modify or create `STS2_Tomorin_Mod/localization/eng/events.json`: English FeedTheCat event text.
- Modify or create `STS2_Tomorin_Mod/localization/zhs/events.json`: Chinese FeedTheCat event text.
- Modify `STS2_Tomorin_Mod/localization/eng/relics.json`: English marker relic text.
- Modify `STS2_Tomorin_Mod/localization/zhs/relics.json`: Chinese marker relic text.
- Modify or create `tests/FeedTheCat.Tests.ps1`: focused static tests for required code and localization wiring.
- Modify `CLAUDE.md`: add FeedTheCat implementation notes.
- Modify or create `日志.txt`: implementation log.
- Modify or create `文档.txt`: requirement/interface documentation.
- Edit Basic Memory note `tomorin-mod/todo-list`: remove `FeedTheCat 固定事件实现` after implementation and verification.

## Task 1: Focused Static Tests

**Files:**
- Create/Modify: `tests/FeedTheCat.Tests.ps1`
- Read-only context: `tests/FixedFirstEvent.Tests.ps1`

- [ ] **Step 1: Write failing static tests**

Add tests that fail before implementation:

```powershell
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot

function Assert-Contains($Path, $Pattern, $Message) {
    $content = Get-Content -LiteralPath (Join-Path $root $Path) -Raw -ErrorAction Stop
    if ($content -notmatch $Pattern) {
        throw $Message
    }
}

function Assert-FileExists($Path) {
    if (-not (Test-Path -LiteralPath (Join-Path $root $Path))) {
        throw "Missing file: $Path"
    }
}

Assert-FileExists "Scripts/Events/FeedTheCat.cs"
Assert-FileExists "Scripts/Events/FeedTheCatVoteCoordinator.cs"
Assert-FileExists "Scripts/Relics/MatchaParfait.cs"
Assert-FileExists "Scripts/Relics/EmptyParfait.cs"
Assert-FileExists "STS2_Tomorin_Mod/localization/eng/events.json"
Assert-FileExists "STS2_Tomorin_Mod/localization/zhs/events.json"

Assert-Contains "Scripts/Events/FeedTheCat.cs" "class\s+FeedTheCat\s*:\s*CustomEventModel" "FeedTheCat must extend CustomEventModel."
Assert-Contains "Scripts/Events/FeedTheCat.cs" "override\s+bool\s+IsShared\s*=>\s*false" "FeedTheCat must remain non-shared."
Assert-Contains "Scripts/Events/FeedTheCat.cs" "CardPileCmd\.AddCurseToDeck<Debt>" "Debt option must use native Debt."
Assert-Contains "Scripts/Events/FeedTheCat.cs" "CardSelectCmd\.FromDeckForRemoval" "Remove option must use native deck removal selection."
Assert-Contains "Scripts/Events/FeedTheCat.cs" "CardSelectCmd\.FromDeckForUpgrade" "Upgrade option must use native deck upgrade selection."
Assert-Contains "Scripts/Events/FeedTheCat.cs" "PlayerCmd\.LoseGold\(200m" "Gold option must lose exactly 200 gold."
Assert-Contains "Scripts/Events/FeedTheCatVoteCoordinator.cs" "PlayerChoiceResult\.FromIndex" "Final branch must be synchronized as an index."
Assert-Contains "Scripts/Events/FeedTheCatVoteCoordinator.cs" "SyncLocalChoice" "Host must broadcast final branch."
Assert-Contains "Scripts/Events/FeedTheCatVoteCoordinator.cs" "WaitForRemoteChoice" "Clients must wait for host final branch."
Assert-Contains "Scripts/Patch/FixedFirstEventPatch.cs" "ModelDb\.Event<FeedTheCat>\(\)" "Fixed first event must target FeedTheCat."
Assert-Contains "Scripts/Patch/FixedFirstEventPatch.cs" "!fixedEvent\.IsAllowed\(concreteRunState\)" "Fixed first event must keep IsAllowed guard."
Assert-Contains "Scripts/RelicPools/TomorinRelicPool.cs" "ModelDb\.Relic<MatchaParfait>\(\)" "MatchaParfait must be registered."
Assert-Contains "Scripts/RelicPools/TomorinRelicPool.cs" "ModelDb\.Relic<EmptyParfait>\(\)" "EmptyParfait must be registered."
Assert-Contains "STS2_Tomorin_Mod/localization/eng/events.json" "FEED_THE_CAT\.pages\.INITIAL\.description" "English event localization must include initial page."
Assert-Contains "STS2_Tomorin_Mod/localization/zhs/events.json" "FEED_THE_CAT\.pages\.INITIAL\.description" "Chinese event localization must include initial page."
```

- [ ] **Step 2: Run tests and verify RED**

Run:

```powershell
powershell -ExecutionPolicy Bypass -File tests/FeedTheCat.Tests.ps1
```

Expected: FAIL because `Scripts/Events/FeedTheCat.cs` and related files do not exist yet.

## Task 2: Vote Coordinator

**Files:**
- Create: `Scripts/Events/FeedTheCatVoteCoordinator.cs`

- [ ] **Step 1: Implement coordinator**

Create a coordinator with these behaviors:

```csharp
namespace STS2_Tomorin_Mod.Events;

internal enum FeedTheCatBranch
{
    Reward = 0,
    Penalty = 1,
}
```

The coordinator must:

- Use `RunManager.Instance.EventSynchronizer.Events.OfType<FeedTheCat>()` as the source of all player event instances.
- Record one vote per `Player.NetId`; duplicate votes must be ignored.
- Reserve exactly one `PlayerChoiceSynchronizer` choice id for the host/final-branch player per event start.
- On host/non-client, resolve once after every player has voted.
- Use `PlayerChoiceResult.FromIndex((int)branch)` with `SyncLocalChoice`.
- On clients, call `WaitForRemoteChoice` exactly once and apply the received branch to all FeedTheCat instances.
- Use host player's `FeedTheCat.Rng` for ties.
- Clean coordinator state in `OnEventFinished`.

- [ ] **Step 2: Run static test**

Run:

```powershell
powershell -ExecutionPolicy Bypass -File tests/FeedTheCat.Tests.ps1
```

Expected: still FAIL because event/relic/localization files are not complete.

## Task 3: Event Model

**Files:**
- Create: `Scripts/Events/FeedTheCat.cs`

- [ ] **Step 1: Implement event pages**

Create `FeedTheCat : CustomEventModel` with:

- `[Pool]` is not required; BaseLib `CustomEventModel(autoAdd: true)` registers the event.
- `public override bool IsShared => false;`
- `public override bool IsAllowed(IRunState runState) => runState.CurrentActIndex == 1;`
- `GenerateInitialOptions()` returns two vote options:
  - reward route vote
  - penalty route vote
- Vote handlers call coordinator and set local event page to a waiting page.
- `ApplyFinalBranch(FeedTheCatBranch branch)` grants the route marker relic once and switches to the matching second-layer page.

- [ ] **Step 2: Implement reward route options**

Reward route second layer:

- Heal 20% max HP with `CreatureCmd.Heal(Owner.Creature, Math.Ceiling(Owner.Creature.MaxHp * 0.2m), true)`.
- Remove one removable card with `CardSelectCmd.FromDeckForRemoval(...)` and `CardPileCmd.RemoveFromDeck(...)`.
- Upgrade one upgradable card with `CardSelectCmd.FromDeckForUpgrade(...)` and `CardCmd.Upgrade(..., CardPreviewStyle.EventLayout)`.
- Lock remove option when no deck card has `IsRemovable`.
- Lock upgrade option when no deck card has `IsUpgradable`.
- Finish event after an option resolves.

- [ ] **Step 3: Implement penalty route options**

Penalty route second layer:

- Lose 200 gold with `PlayerCmd.LoseGold(200m, Owner, GoldLossType.Spent)`.
- Lock gold option when `Owner.Gold < 200`.
- Gain native `Debt` with `CardPileCmd.AddCurseToDeck<Debt>(Owner)`.
- Finish event after an option resolves.

- [ ] **Step 4: Run build**

Run:

```powershell
dotnet build
```

Expected: compile errors only if signatures need adjustment; fix against actual `sts2.dll` APIs until 0 errors.

## Task 4: Relics and Registration

**Files:**
- Create: `Scripts/Relics/MatchaParfait.cs`
- Create: `Scripts/Relics/EmptyParfait.cs`
- Modify: `Scripts/RelicPools/TomorinRelicPool.cs`

- [ ] **Step 1: Implement marker relics**

Both relics extend `BaseRelicModel`, declare `[Pool(typeof(TomorinRelicPool))]`, and do not override combat hooks.

- [ ] **Step 2: Register in relic pool**

Add both:

```csharp
ModelDb.Relic<MatchaParfait>(),
ModelDb.Relic<EmptyParfait>(),
```

- [ ] **Step 3: Run static test**

Run:

```powershell
powershell -ExecutionPolicy Bypass -File tests/FeedTheCat.Tests.ps1
```

Expected: still FAIL until fixed event patch/localization are complete.

## Task 5: Fixed Event Patch

**Files:**
- Modify: `Scripts/Patch/FixedFirstEventPatch.cs`

- [ ] **Step 1: Replace fixed event target**

Change:

```csharp
var fixedEvent = ModelDb.Event<WoodCarvings>();
```

to:

```csharp
var fixedEvent = ModelDb.Event<FeedTheCat>();
```

Add `using STS2_Tomorin_Mod.Events;`.

- [ ] **Step 2: Restore IsAllowed guard**

Use:

```csharp
if (!fixedEvent.IsAllowed(concreteRunState))
{
    return;
}
```

- [ ] **Step 3: Run focused tests**

Run:

```powershell
powershell -ExecutionPolicy Bypass -File tests/FixedFirstEvent.Tests.ps1
powershell -ExecutionPolicy Bypass -File tests/FeedTheCat.Tests.ps1
```

Expected: `FixedFirstEvent` test should pass after updating expected target if the existing test asserts `WoodCarvings`; FeedTheCat test may still wait for localization.

## Task 6: Localization

**Files:**
- Create/Modify: `STS2_Tomorin_Mod/localization/eng/events.json`
- Create/Modify: `STS2_Tomorin_Mod/localization/zhs/events.json`
- Modify: `STS2_Tomorin_Mod/localization/eng/relics.json`
- Modify: `STS2_Tomorin_Mod/localization/zhs/relics.json`

- [ ] **Step 1: Add event localization**

Use keys matching BaseLib/EventModel slug format:

```json
"FEED_THE_CAT.title": "...",
"FEED_THE_CAT.pages.INITIAL.description": "...",
"FEED_THE_CAT.pages.INITIAL.options.VoteReward.title": "...",
"FEED_THE_CAT.pages.INITIAL.options.VoteReward.description": "...",
"FEED_THE_CAT.pages.INITIAL.options.VotePenalty.title": "...",
"FEED_THE_CAT.pages.INITIAL.options.VotePenalty.description": "...",
"FEED_THE_CAT.pages.WAITING.description": "...",
"FEED_THE_CAT.pages.REWARD.description": "...",
"FEED_THE_CAT.pages.REWARD.options.Heal.title": "...",
"FEED_THE_CAT.pages.REWARD.options.RemoveCard.title": "...",
"FEED_THE_CAT.pages.REWARD.options.UpgradeCard.title": "...",
"FEED_THE_CAT.pages.PENALTY.description": "...",
"FEED_THE_CAT.pages.PENALTY.options.LoseGold.title": "...",
"FEED_THE_CAT.pages.PENALTY.options.GainDebt.title": "...",
"FEED_THE_CAT.pages.COMPLETE.description": "..."
```

- [ ] **Step 2: Add relic localization**

Use project relic key format:

```json
"STS2_TOMORIN_MOD-MATCHA_PARFAIT.title": "...",
"STS2_TOMORIN_MOD-MATCHA_PARFAIT.description": "...",
"STS2_TOMORIN_MOD-MATCHA_PARFAIT.flavor": "...",
"STS2_TOMORIN_MOD-EMPTY_PARFAIT.title": "...",
"STS2_TOMORIN_MOD-EMPTY_PARFAIT.description": "...",
"STS2_TOMORIN_MOD-EMPTY_PARFAIT.flavor": "..."
```

- [ ] **Step 3: Run static tests and build**

Run:

```powershell
powershell -ExecutionPolicy Bypass -File tests/FeedTheCat.Tests.ps1
dotnet build
```

Expected: static test PASS; build 0 errors.

## Task 7: Documentation and Memory Cleanup

**Files:**
- Modify/Create: `CLAUDE.md`
- Modify/Create: `日志.txt`
- Modify/Create: `文档.txt`
- Edit Basic Memory note: `tomorin-mod/todo-list`

- [ ] **Step 1: Update local docs**

Add concise FeedTheCat notes to `CLAUDE.md`, including:

- fixed Act 2 first event behavior
- non-shared event plus coordinator architecture
- route marker relics
- verification command

Append `日志.txt` with implemented files and verification results.

Write `文档.txt` with requirement and interface summary:

- class names
- public/internal helpers
- branch rules
- option lock rules
- sync behavior

- [ ] **Step 2: Remove Basic Memory TODO**

Use CLI because MCP is constrained:

```powershell
basic-memory tool edit-note "tomorin-mod/todo-list" --project-id 03b23de7-f66e-42ae-860e-21a3d7927820 --local --operation find_replace --find-text "<exact FeedTheCat TODO section>" --content ""
```

Verify:

```powershell
basic-memory tool read-note "TODO List" --project-id 03b23de7-f66e-42ae-860e-21a3d7927820 --local --include-frontmatter
```

Expected: `FeedTheCat 固定事件实现` no longer appears.

## Task 8: Final Verification and Review

**Files:**
- All files touched above.

- [ ] **Step 1: Run focused static tests**

Run:

```powershell
powershell -ExecutionPolicy Bypass -File tests/FixedFirstEvent.Tests.ps1
powershell -ExecutionPolicy Bypass -File tests/FeedTheCat.Tests.ps1
```

Expected: both PASS.

- [ ] **Step 2: Run compile verification**

Run:

```powershell
dotnet build
```

Expected: 0 errors. Existing warnings may remain.

- [ ] **Step 3: Decide publish**

If no new Godot resources/images/scenes were added, `dotnet publish` is not required by project rules. If relic image resources are added, run:

```powershell
dotnet publish
```

Expected: 0 errors or report Godot export warning exactly.

- [ ] **Step 4: Review diff**

Run:

```powershell
git diff -- Scripts/Events Scripts/Relics Scripts/RelicPools Scripts/Patch STS2_Tomorin_Mod/localization tests CLAUDE.md 日志.txt 文档.txt
```

Expected: diff only contains FeedTheCat-related changes and docs.

## Self-Review

- Spec coverage: covered fixed Act 2 event, first-layer voting, host final branch, independent second layer, route marker relics, option locks, Debt source, patch, localization, verification, docs, and Basic Memory TODO cleanup.
- Placeholder scan: no `TBD` or unspecified implementation step remains; known command and file paths are listed.
- Type consistency: `FeedTheCatBranch`, `FeedTheCat`, `FeedTheCatVoteCoordinator`, `MatchaParfait`, `EmptyParfait`, `PlayerChoiceResult.FromIndex`, and `ModelDb.Event<FeedTheCat>()` are used consistently.
