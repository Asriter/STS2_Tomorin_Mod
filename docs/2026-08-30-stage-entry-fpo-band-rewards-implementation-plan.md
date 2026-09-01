# 第四层资格、FPO 原版结算与 BandMember 奖励实施计划（已执行）

> **执行状态：** 代码、自动化测试、构建与 Spec 自审均已完成；仅设计文档 TODO 中列出的真实游戏端到端验收尚待执行。

**Goal:** 将第四层资格改为同一玩家持有任意候选遗物对并击败 FPO，移除 FPO 专用奖励补丁，并让 BandMember Elite 只在达到各自本体奖励条件后于 Encounter 胜利结算发放对应遗物。

**Architecture:** `StageEligibility` 以集合交集计算单玩家资格，`StageRunProgressModifier` 仅保存 FPO 击败状态。四个乐队本体在原奖励触发点统一调用 `BandMemberRelicRewardLifecycle`；原始 Boss 继续即时发奖，Elite 向 `BandMemberEncounter` 记录左右资格。现有 `HookAfterCombatEndPatch` 显式派发 Encounter 的 `AfterCombatEnd`，再统一过滤玩家已持有及待领取遗物。

**Tech Stack:** .NET 9、C#、Godot/StS2 模型 API、HarmonyLib、xUnit、PowerShell 回归脚本。

**Spec:** `docs/2026-08-30-stage-entry-fpo-and-band-member-rewards-design.md`

## Global Constraints

- 保留当前工作区内用户已有未提交修改，不执行 reset、checkout、clean 或覆盖式回退。
- 当前任务只修改 C#、测试和文档；不运行 `dotnet publish`。
- 测试不得断言敌人、遗物、金币、卡牌或其他配置对象的固定属性数值。
- 不兼容含旧 FPO 奖励状态字段的进行中存档；相关字段与补丁彻底删除。
- Anon 逃跑不取得遗物资格；Taki 第三阶段锁血玩家后逃跑不取得遗物资格。
- 标准精英奖励、四个原始 Boss 的遗物奖励和四个 Elite 的 `ShouldGrantBossReward => false` 必须保留。

---

### Task 1: Stage 单玩家候选遗物门槛

**Files:**
- Create: `tests/StageHarness/StageHarness.csproj`
- Create: `tests/StageHarness/Directory.Build.props`
- Create: `tests/StageHarness/StageEligibilityTests.testcs`
- Modify: `Scripts/Stage/StageEligibility.cs`
- Modify: `tests/Stage.Tests.ps1`

**Interfaces:**
- Consumes: `Player.Relics`、`StageRunProgressModifier.HasDefeatedFullPowerOblivionis`、`HasAdjacentUniqueStageCandidate`。
- Produces: `MinimumRequiredStageRelicCount`、`HasMinimumRequiredRelics(Player)`、`CoversMinimumRequiredRelics<T>(IEnumerable<T>, IReadOnlySet<T>, int)`。

- [x] **Step 1: 建立 StageHarness 项目**

复制现有 `BandMemberEncounterHarness` 的跨平台 StS2 路径与临时输出结构，项目只编译 `*.testcs`，引用主项目、`sts2.dll`、xUnit 和 Test SDK。

- [x] **Step 2: 写门槛行为失败测试**

测试使用独立字符串集合，不依赖 ModelDb：

```csharp
[Fact]
public void AnyCandidatePairQualifiesButSplitOrDuplicateOwnershipDoesNot()
{
    IReadOnlySet<string> required = new HashSet<string>
        { "Anon", "Taki", "Soyo", "Raana" };
    string[] candidatePair = ["Anon", "Taki"];

    Assert.True(StageEligibility.CoversMinimumRequiredRelics(
        candidatePair, required, candidatePair.Length));
    Assert.False(StageEligibility.CoversMinimumRequiredRelics(
        candidatePair.Take(candidatePair.Length - 1), required, candidatePair.Length));
    Assert.False(StageEligibility.CoversMinimumRequiredRelics(
        candidatePair.SelectMany(item => new[] { item, item }).Take(candidatePair.Length),
        required,
        candidatePair.Length));
}
```

另写门槛越界抛错、有效组合超集仍成立、无关 ID 不计入的独立用例。

- [x] **Step 3: 运行 RED**

Run:

```powershell
dotnet test tests/StageHarness/StageHarness.csproj --nologo --verbosity minimal
```

Expected: FAIL，因为 `CoversMinimumRequiredRelics` 尚不存在。

- [x] **Step 4: 实现最小集合逻辑并恢复 IsEligible 完整条件链**

```csharp
public const int MinimumRequiredStageRelicCount = 2;

public static bool HasMinimumRequiredRelics(Player player) =>
    CoversMinimumRequiredRelics(
        player.Relics.Select(relic => relic.Id),
        RequiredStageRelics,
        MinimumRequiredStageRelicCount);

public static bool CoversMinimumRequiredRelics<T>(
    IEnumerable<T> held,
    IReadOnlySet<T> required,
    int minimumRequiredCount)
    where T : notnull
{
    if (minimumRequiredCount <= 0 || minimumRequiredCount > required.Count)
        throw new ArgumentOutOfRangeException(nameof(minimumRequiredCount));

    HashSet<T> heldSet = held.ToHashSet();
    return required.Count(heldSet.Contains) >= minimumRequiredCount;
}
```

`IsEligible` 最后必须使用 `runState.Players.Any(HasMinimumRequiredRelics)`，并恢复 Stage 邻接与 FPO 进度判断。

- [x] **Step 5: 更新 Stage 结构回归并运行 GREEN**

删除无条件 `return true` 的临时断言空间；增加完整条件链检查。运行 StageHarness 和 `pwsh -File tests/Stage.Tests.ps1`，确认通过。

---

### Task 2: FPO 只记录击败进度并交还原版奖励

**Files:**
- Test in: `tests/StageHarness/StageEligibilityTests.testcs`
- Modify: `Scripts/Stage/StageRunProgressModifier.cs`
- Modify: `Scripts/Patch/StageFpoProgressPatch.cs`
- Delete: `Scripts/Patch/StageBossRewardLifecyclePatch.cs`
- Modify: `tests/Stage.Tests.ps1`

**Interfaces:**
- Consumes: `Hook.AfterDeath` 的 `IRunState`、`Creature`、`wasRemovalPrevented`。
- Produces: 仅 `HasDefeatedFullPowerOblivionis`、`MarkFullPowerOblivionisDefeated()`、`Find(IRunState)`。

- [x] **Step 1: 写 FPO 状态失败回归**

在 xUnit 中验证 `MarkFullPowerOblivionisDefeated` 首次转换、重复调用幂等；在 PowerShell 回归中先改为要求奖励生命周期文件不存在，且 Stage 进度源码不再出现奖励状态成员。

- [x] **Step 2: 运行 RED**

Run:

```powershell
pwsh -File tests/Stage.Tests.ps1
```

Expected: FAIL，因为奖励生命周期文件和状态机仍存在。

- [x] **Step 3: 精简 Modifier 与死亡 Patch**

删除所有奖励身份字段、方法、坐标转换与 `StageBossRewardState`。Postfix 精简为：

```csharp
private static void Postfix(
    IRunState runState,
    Creature creature,
    bool wasRemovalPrevented)
```

只在未阻止的 FPO 真实死亡时调用幂等进度方法。

- [x] **Step 4: 删除奖励补丁并运行 GREEN**

删除 `StageBossRewardLifecyclePatch.cs`，更新 `Stage.Tests.ps1` 文件清单和断言，运行 StageHarness、Stage PowerShell 回归与 `dotnet build`。

---

### Task 3: 四个本体同源奖励资格生命周期

**Files:**
- Create: `Scripts/Enemy/BandMemberRelicRewardLifecycle.cs`
- Modify: `Scripts/Enemy/Anon.cs`
- Modify: `Scripts/Enemy/Taki.cs`
- Modify: `Scripts/Enemy/Soyo.cs`
- Modify: `Scripts/Enemy/Raana.cs`
- Modify: `tests/BandBossRelicRewards.Tests.ps1`
- Modify: `tests/BandMemberEncounter.Tests.ps1`

**Interfaces:**
- Consumes: 四个本体当前的真实奖励触发分支、`ShouldGrantBossReward`、当前 `CombatRoom`。
- Produces: `RecordEarnedAndGrantBossReward<TRelic>(Creature, BandMemberKind, bool)`。

- [x] **Step 1: 写触发位置失败回归**

更新现有脚本：四个本体必须在原奖励分支调用统一生命周期；Anon 的逃跑状态方法和 Taki 的 `RunCallBack` 不得调用；四个 Elite 仍关闭 Boss 房即时奖励。

- [x] **Step 2: 运行 RED**

Run:

```powershell
pwsh -File tests/BandBossRelicRewards.Tests.ps1
```

Expected: FAIL，因为统一生命周期尚不存在。

- [x] **Step 3: 新增生命周期出口**

实现 Spec 第 5.5 节的完整方法：BandMemberEncounter 中记录资格；`shouldGrantBossReward` 为真时调用现有 `BandBossRelicReward.Add<TRelic>`。

- [x] **Step 4: 在四个本体原触发分支接入**

Anon 删除 `_isAddReward`/`AddReward`，只在二阶段再次死亡分支调用；Taki 只在 `PhaseThreeClearCallBack` 调用；Soyo、Raana 只在自身死亡分支调用。

- [x] **Step 5: 运行 GREEN**

运行两份 Band PowerShell 回归，确认原始 Boss 行为和 Elite 禁止即时奖励边界同时成立。

---

### Task 4: Encounter 资格持久化与奖励过滤

**Files:**
- Create: `Scripts/Encounters/BandMemberEncounterRewardPolicy.cs`
- Modify: `Scripts/Encounters/BandMemberEncounter.cs`
- Create: `tests/BandMemberEncounterHarness/BandMemberRewardStateTests.testcs`
- Modify: `Scripts/Patch/HookAfterCombatEndPatch.cs`
- Modify: `tests/BandMemberEncounter.Tests.ps1`

**Interfaces:**
- Consumes: `BandMemberSelection`、左右槽位、四个本体记录的资格、`CombatRoom.ExtraRewards`、`Player.Relics`。
- Produces: `MarkRelicRewardEarned(BandMemberKind, string?)`、`AddEarnedForSelection(CombatRoom, BandMemberSelection, bool, bool)`、`AfterCombatEnd(CombatRoom)`。

- [x] **Step 1: 写 Encounter 状态失败测试**

使用反射驱动真实 `BandMemberEncounter`：合法成员—槽位记录对应侧资格；重复记录幂等；身份与槽位不匹配抛错；资格保存/恢复保持；缺失资格字段按假恢复；损坏字段显式失败。

- [x] **Step 2: 写奖励选择失败测试**

覆盖零侧、一侧、双侧资格；已拥有和已待领取过滤；多玩家独立过滤；重复结算幂等。测试期望用手工列出的遗物 ID/类型集合，不断言遗物属性数值。

- [x] **Step 3: 运行 RED**

Run:

```powershell
dotnet test tests/BandMemberEncounterHarness/BandMemberEncounterHarness.csproj --nologo --verbosity minimal
```

Expected: FAIL，因为资格状态和奖励辅助类尚不存在。

- [x] **Step 4: 实现资格状态、保存恢复和校验**

按 Spec 增加左右资格键与布尔字段、`MarkRelicRewardEarned`、缺失字段兼容和损坏状态显式失败。

- [x] **Step 5: 实现奖励映射与幂等过滤**

`AddEarnedForSelection` 只解析已取得资格的一侧，先验证全部映射，再逐玩家检查 `Player.Relics` 与 `room.ExtraRewards`，最后创建 mutable `RelicReward`。

- [x] **Step 6: 接入 AfterCombatEnd 并运行 GREEN**

仍有可攻击敌人时返回；胜利后用保存的选择和资格调用辅助类。由于原版 `Hook.AfterCombatEnd` 不会枚举 Encounter，复用现有 `HookAfterCombatEndPatch` 增加显式、可等待的 Encounter 派发。运行 Band Harness 与两份 Band PowerShell 回归。

---

### Task 5: 全量验证与交付审查

**Files:**
- Verify: 本计划全部文件
- Update if needed: `docs/2026-08-30-stage-entry-fpo-band-rewards-implementation-plan.md`

**Interfaces:**
- Consumes: Tasks 1–4 的最终实现。
- Produces: 可构建、目标测试通过且无格式错误的工作区变更。

- [x] **Step 1: 运行目标测试**

```powershell
dotnet test tests/StageHarness/StageHarness.csproj --nologo --verbosity minimal
dotnet test tests/BandMemberEncounterHarness/BandMemberEncounterHarness.csproj --nologo --verbosity minimal
pwsh -File tests/Stage.Tests.ps1
pwsh -File tests/BandMemberEncounter.Tests.ps1
pwsh -File tests/BandBossRelicRewards.Tests.ps1
```

- [x] **Step 2: 运行完整构建与格式检查**

```powershell
dotnet build
git diff --check
```

- [x] **Step 3: 对照 Spec 审查**

逐条核对：单玩家任意候选遗物对、FPO 原版奖励、Anon 逃跑无资格、Taki 锁血逃跑无资格、Soyo/Raana 真死资格、零/一/双奖励、持有与待领取过滤、原始 Boss 行为不回归。

- [x] **Step 4: 汇报保留的用户改动与本次变更**

只列出本次触及文件和验证结果；不提交、不清理、不打包 Godot 资源。
