# Shadow Tomorin Three-Phase Boss Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在现有敌人 CardIntent 框架上实现正式的三阶段影灯首领，以 1200 HP、阶段累计伤害额度、跨阶段 Carry Token、准备周期收藏品、有效牌 X 结算和双层风险软锁，替换 Stage 中的 CrychicPhatom 占位首领。

**Architecture:** 影灯从入场到死亡只持有一个 `EnemyCardCombatState` 和一个正式 `EnemyCardDeckId`；阶段模板是该牌组注册内容的一部分，阶段迁移在旧行动完成后的 Idle 安全点通过候选状态原子替换。规划器先做定义级静态评分，再冻结完整 DFS 行动并运行纯内存投影与风险评分；执行、实时展示和重连共同读取已冻结的实际卡身份、Replay 与 X 元数据。

**Tech Stack:** .NET 9、C#、Godot 4.5.1、MegaCrit StS2 API、BaseLib、HarmonyLib、xUnit CardIntent harness、PowerShell 结构测试。

**Spec:** `docs/superpowers/specs/2026-08-30-shadow-tomorin-boss-design.md`

## Global Constraints

- 影灯单人基准最大 HP 为 `1200`；P1、P2 累计伤害额度分别为 `216`、`432`，P3 剩余 HP 为 `552`。
- 多人模式对最大 HP、P1 额度与 P2 额度使用同一遭遇缩放函数；攻击风险始终取单名存活玩家承受伤害的最大值，不累加玩家人数。
- 阶段伤害溢出必须截断，不能从 P1 穿透到 P2，也不能从 P2 穿透到 P3。
- 达到阶段额度只设置 `PendingPhase`；不得调用 `SetMoveImmediate`，不得取消、替换或重新规划已经公开的旧阶段行动。
- 阶段迁移只能发生在 `RuntimePhase == Idle`、`PreparedAction == null`、即时栈为空且旧行动生命周期全部提交之后。
- 阶段迁移只允许修改牌组和施加/替换阶段状态；全流程不存在固定逐回合行动，也不存在第 7、8 回合的固定强化行为。
- 第 7 回合必须因累计状态、Replay 与牌组压缩进入明显惩罚区；第 8 回合只允许由同一组自然积累机制进入软狂暴。
- `CarryAcrossPhase` 卡牌在 Draw、Current、Retained、Discard、Exhaust 任一区域都必须保留原对象、`InstanceKey`、区域、顺序和 `ReplayCount`；Available 与 Consumed 收藏品也全部保留。
- 阶段迁移直接删除非 Carry 来源，不触发 Exhaust、HeartBeat 或成功出牌钩子。
- 角色卡池敌人版 X 统一使用 `BaseX = max(0, 6 - N)`；同一实际实例的本体与全部 Replay 使用同一个 `FrozenX`，Replay 不增加 `N`。
- `SenzaihyoumeiToken` 在首次执行前按 Exhaust 中不同 `EnemyCardId` 数量冻结 `Multiplier`；达到五种时为 2，否则为 1。
- `N=2`、翻倍、`ReplayCount=1` 时，本体与 Replay 各执行 8 次命中，总计 16 次命中、128 基础伤害，整张卡结束后 `N` 只增加 1。
- P2/P3 每个敌方行动准备周期最多生成一件共享的随机收藏品增量；三个候选共享同一选择，只有最终行动提交才写入真实 Available。
- 完整软锁为 P1 `48/90`、P2 `72/135`、P3 `96/190`；静态快速锁为 P1 `38/72`、P2 `58/108`、P3 `77/152`。
- 第三个候选只有在完整投影 `IsComplete == true` 时才可标记 `ForcedOverLock`；投影不完整、未知修改器或步骤上限截断都必须进入配置/模拟故障，不允许固定保底行动。
- 完整总风险使用行动结束后的总存量，不改成只计算本行动增量；`ProjectedDamage` 不得再次乘力量、易伤、命中次数或 Replay。
- 正式目录、预加载、时间线与生成链不得包含 `Utakotoba`、`UtakotobaToken` 或“诗超绊”。
- 复用现有 `ShadowTomorin.tscn` 和现有 Tomorin 头像资源；本计划不新增或修改 Godot 资源，因此最终使用 `dotnet build`，不要求 `dotnet publish`。
- 测试中的期望值优先从 `ShadowTomorinBalance`、阶段模板和评分规则读取，避免在测试中复制另一份可漂移的配置字面量；公式示例只用来验证关系和结算次序。
- 仓库已有用户改动；每次提交只暂存当前任务列出的文件，不回退、不覆盖无关改动。

---

## File Structure

### Core planning and runtime

- Create `Scripts/Enemy/CardIntents/EnemyCardPlanningRules.cs`: 正式通用规划规则、加权配方、静态/完整软锁与槽位谓词。
- Modify `Scripts/Enemy/CardIntents/EnemyActionMetric.cs`: 保留开发测试指标并加入正式阶段指标。
- Create `Scripts/Enemy/CardIntents/EnemyCardPhase.cs`: 阶段枚举、阶段模板、阶段状态与迁移结果。
- Create `Scripts/Enemy/CardIntents/EnemyCardContentDirectory.cs`: 一个 DeckId 对完整定义目录、收藏品目录和阶段模板的不可变注册内容。
- Create `Scripts/Enemy/CardIntents/EnemyCardProgram.cs`: 素材、直接效果与 Compose 结果的显式有序程序。
- Create `Scripts/Enemy/CardIntents/EnemyEffectiveCardLedger.cs`: `N`、`FrozenN`、`FrozenX`、倍率与已计数状态。
- Create `Scripts/Enemy/CardIntents/EnemyActionRiskCalculator.cs`: 完整投影的 Attack/Survival/Engine/Deferred 四项风险。
- Modify `Scripts/Enemy/CardIntents/EnemyCardDefinition.cs`: 增加 Carry、来源阶段、静态提示、效果分类、有序程序与可执行条件。
- Modify `Scripts/Enemy/CardIntents/EnemyCardScoreProfile.cs`: 修复心之壁自赋值并扩展正式静态评分字段。
- Modify `Scripts/Enemy/CardIntents/EnemyActionRecipe.cs`: 支持权重、DefinitionId 槽位谓词和 Compose 数量约束。
- Modify `Scripts/Enemy/CardIntents/EnemyActionMetricPlanner.cs`: 加权指标、准备库存增量、两层评分和第三候选规则。
- Modify `Scripts/Enemy/CardIntents/EnemyCardCombatState.cs`: 阶段、准备增量、X ledger、迁移候选与原子应用。
- Modify `Scripts/Enemy/CardIntents/EnemyCardDeckRegistry.cs`: 注册完整内容目录，并按阶段创建新模板实例。
- Modify `Scripts/Enemy/CardIntents/EnemyPreparedResolutionPlanner.cs`: 按显式程序构造 DFS 步骤并冻结有效牌元数据。
- Modify `Scripts/Enemy/CardIntents/PreparedEnemyResolutionPlan.cs`: 冻结单元引用实际执行实例的 X 快照。
- Modify `Scripts/Enemy/CardIntents/PreparedEnemyCardAction.cs`: 保存准备增量、双层评分和投影完整性诊断。
- Modify `Scripts/Enemy/CardIntents/EnemyCardExecutionEngine.cs`: 按冻结 ledger 结算、计数并触发敌人能力钩子。
- Modify `Scripts/Enemy/CardIntents/EnemyCardSimulationContext.cs`: 记录结束状态、牌区、库存和有效牌 ledger。
- Modify `Scripts/Enemy/CardIntents/LiveActionProjection.cs`: 输出结束状态摘要与逐实例 X 元数据。
- Modify `Scripts/Enemy/CardIntents/EnemyActionProjectionService.cs`: 生成软锁可消费的完整、无副作用投影。
- Modify `Scripts/Enemy/CardIntents/EnemyAbilityHookDispatcher.cs`: 为执行和模拟提供同序能力适配。
- Modify `Scripts/Enemy/CardIntents/CardIntentMoveState.cs`: 使用通用规则与内容目录，在行动结算后调用 Idle 安全点钩子。
- Modify `Scripts/Enemy/CardIntents/CardIntentMoveRuntime.cs`: 将结算完成、Fault 和阶段迁移按顺序通知状态。
- Modify `Scripts/Enemy/CardIntents/BaseCardIntentMonsterModel.cs`: 按 DeckId 解析正式目录，移除测试目录硬编码。
- Modify `Scripts/Enemy/CardIntents/EnemyCardRuntimeSyncState.cs`: 协议升级并恢复阶段、准备增量、风险与 X 元数据。

### Shadow Tomorin content

- Create `Scripts/Enemy/CardIntents/ShadowTomorin/ShadowTomorinBalance.cs`: HP、阶段额度、锁、权重、收藏品权重及数值模型唯一来源。
- Create `Scripts/Enemy/CardIntents/ShadowTomorin/ShadowTomorinEffects.cs`: 影灯专用条件效果与稳定程序节点。
- Create `Scripts/Enemy/CardIntents/ShadowTomorin/ShadowTomorinCardCatalog.cs`: 正式来源牌、Token 与 Carry 定义。
- Create `Scripts/Enemy/CardIntents/ShadowTomorin/ShadowTomorinCollectionCatalog.cs`: 六种收藏品定义与加权选择。
- Create `Scripts/Enemy/CardIntents/TomorinEnemyCollectionCatalogFactory.cs`: 测试目录与正式目录共同使用的生产级收藏品定义工厂，避免正式代码依赖 `Test` 命名空间。
- Create `Scripts/Enemy/CardIntents/ShadowTomorin/ShadowTomorinDeck.cs`: 正式 DeckId、三阶段模板和完整目录注册。
- Create `Scripts/Enemy/CardIntents/ShadowTomorin/ShadowTomorinRules.cs`: 三阶段加权行为指标与 DefinitionId 谓词。
- Create `Scripts/Powers/EnemyPowers/ShadowTomoriFormPower.cs`: P2/P3 准备周期收藏品状态标记。
- Create `Scripts/Powers/EnemyPowers/CardIntentHeartBeatPower.cs`: 正常生命周期 Exhaust 的格挡能力标记。
- Create `Scripts/Powers/EnemyPowers/CardIntentUnwantedSixthPower.cs`: 单行动内每次独立获得格挡后的心之壁能力标记。
- Create `Scripts/Powers/EnemyPowers/ShadowTomorinDamageGatePower.cs`: 关闭 Power 自身的二次多人缩放，由怪物实际最大 HP 比例提供唯一缩放值。
- Modify `Scripts/Powers/EnemyPowers/EnemyMaxDamageReceivedPower.cs`: 支持关闭“敌方回合开始自动移除”，使零余额 Gate 保留到 Idle 迁移。
- Create `Scripts/Enemy/ShadowTomorin.cs`: 单状态三阶段首领、阶段 Gate 与 Idle 迁移协调。
- Create `Scripts/Encounters/ShadowTomorinBoss.cs`: 正式 Boss Encounter。
- Modify `Scripts/Acts/Stage.cs`: Stage 枚举正式影灯遭遇。
- Modify `Scripts/Stage/StageRoomResolver.cs`: Stage Boss 节点解析为影灯；不改变 Glory 等其他章节的 Crychic 配置。
- Modify `STS2_Tomorin_Mod/localization/eng/monsters.json`, `zhs/monsters.json`: 怪物名和阶段提示。
- Modify `STS2_Tomorin_Mod/localization/eng/encounters.json`, `zhs/encounters.json`: 遭遇标题。
- Modify `STS2_Tomorin_Mod/localization/eng/powers.json`, `zhs/powers.json`: 影灯阶段能力文本。

### Tests

- Create `tests/CardIntentHarness/PlanningRulesTests.testcs`.
- Create `tests/CardIntentHarness/PhaseMigrationTests.testcs`.
- Create `tests/CardIntentHarness/PreparationInventoryDeltaTests.testcs`.
- Create `tests/CardIntentHarness/EffectiveCardLedgerTests.testcs`.
- Create `tests/CardIntentHarness/ActionRiskTests.testcs`.
- Create `tests/CardIntentHarness/ShadowTomorinCatalogTests.testcs`.
- Create `tests/CardIntentHarness/ShadowTomorinBossStateTests.testcs`.
- Modify `tests/CardIntentHarness/ActionPlannerTests.testcs`.
- Modify `tests/CardIntentHarness/FrozenResolutionPlanTests.testcs`.
- Modify `tests/CardIntentHarness/ExecutionEngineTests.testcs`.
- Modify `tests/CardIntentHarness/LiveProjectionTests.testcs`.
- Modify `tests/CardIntentHarness/ReconnectStateTests.testcs`.
- Modify `tests/CardIntentHarness/ModelDbBootstrap.testcs`.
- Modify `tests/CardIntentModelDbHarness/CardIntentCatalogModelDbTests.testcs`.
- Modify `tests/CardIntent.Tests.ps1`.
- Modify `tests/Stage.Tests.ps1`.

---

### Task 1: Generalize planning rules and fix static heart-wall scoring

**Files:**
- Create: `Scripts/Enemy/CardIntents/EnemyCardPlanningRules.cs`
- Modify: `Scripts/Enemy/CardIntents/EnemyCardScoreProfile.cs`
- Modify: `Scripts/Enemy/CardIntents/EnemyCardScoreCalculator.cs`
- Modify: `Scripts/Enemy/CardIntents/EnemyCardDefinition.cs`
- Modify: `Scripts/Enemy/CardIntents/Test/CardIntentTestRules.cs`
- Modify: `Scripts/Enemy/CardIntents/CardIntentMoveState.cs`
- Modify: `Scripts/Enemy/CardIntents/EnemyActionMetricPlanner.cs`
- Create: `tests/CardIntentHarness/PlanningRulesTests.testcs`
- Modify: `tests/CardIntentHarness/ActionPlannerTests.testcs`

**Interfaces:**
- Consumes: existing `EnemyActionRecipe`, `EnemyCardScoreContext`, `EnemyCardScore`.
- Produces: `EnemySoftLockLimits`, `EnemyWeightedActionRecipe`, `EnemyCardPlanningRules`; all later phase rules and planner calls depend on these exact types.

- [ ] **Step 1: Write failing tests for the generic rule object and heart-wall contribution**

```csharp
public sealed class PlanningRulesTests
{
    [Fact]
    public void HeartWallIsStoredAndContributesItsConfiguredWeight()
    {
        var profile = new EnemyCardScoreProfile(atField: 5m);
        var score = new EnemyCardScoreCalculator().CalculateProfiles([profile]);

        Assert.Equal(profile.AtField * EnemyCardScoreWeights.HeartWall, score.Total);
        Assert.NotEqual(decimal.Zero, score.Total);
    }

    [Fact]
    public void WeightedRecipesRejectDuplicateMetricsAndNonPositiveWeights()
    {
        var recipe = new EnemyActionRecipe(EnemyActionMetric.Gain, [EnemyCardTag.Defense]);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new EnemyWeightedActionRecipe(recipe, weight: 0));
        Assert.Throws<ArgumentException>(() => new EnemyCardPlanningRules(
            new EnemySoftLockLimits(1m, 1m),
            new EnemySoftLockLimits(1m, 1m),
            maxCandidateAttempts: 3,
            stepLimit: 256,
            recipes:
            [
                new EnemyWeightedActionRecipe(recipe, 1),
                new EnemyWeightedActionRecipe(recipe, 1)
            ]));
    }
}
```

- [ ] **Step 2: Run the focused tests and verify the missing types/property fail**

Run: `dotnet test tests\CardIntentHarness\CardIntentHarness.csproj --nologo --verbosity minimal --filter "FullyQualifiedName~PlanningRulesTests"`

Expected: FAIL because `EnemyCardPlanningRules`, `EnemyCardScoreWeights`, `AtField`, or `CalculateProfiles` does not exist.

- [ ] **Step 3: Add the generic rules and the complete static formula**

```csharp
public static class EnemyCardScoreWeights
{
    public const decimal Block = 0.65m;
    public const decimal Strength = 10m;
    public const decimal Dexterity = 6m;
    public const decimal HeartWall = 3m;
    public const decimal OtherPersistentPower = 5m;
    public const decimal Vulnerable = 6m;
    public const decimal OtherDebuff = 3m;
    public const decimal NormalCollection = 3m;
    public const decimal StarStone = 5m;
    public const decimal DeferredTokenHint = 0.5m;
}

public sealed record EnemySoftLockLimits(decimal Attack, decimal Total);

public sealed record EnemyWeightedActionRecipe
{
    public EnemyWeightedActionRecipe(EnemyActionRecipe recipe, int weight)
    {
        Recipe = recipe ?? throw new ArgumentNullException(nameof(recipe));
        Weight = weight > 0 ? weight : throw new ArgumentOutOfRangeException(nameof(weight));
    }

    public EnemyActionRecipe Recipe { get; }
    public int Weight { get; }
}

public class EnemyCardPlanningRules
{
    public EnemyCardPlanningRules(
        EnemySoftLockLimits staticLocks,
        EnemySoftLockLimits fullLocks,
        int maxCandidateAttempts,
        int stepLimit,
        IEnumerable<EnemyWeightedActionRecipe> recipes)
    {
        StaticLocks = staticLocks;
        FullLocks = fullLocks;
        MaxCandidateAttempts = maxCandidateAttempts > 0
            ? maxCandidateAttempts
            : throw new ArgumentOutOfRangeException(nameof(maxCandidateAttempts));
        StepLimit = stepLimit > 0 ? stepLimit : throw new ArgumentOutOfRangeException(nameof(stepLimit));
        WeightedRecipes = (recipes ?? throw new ArgumentNullException(nameof(recipes))).ToArray();
        if (WeightedRecipes.Length == 0 ||
            WeightedRecipes.Select(x => x.Recipe.Metric).Distinct().Count() != WeightedRecipes.Length)
            throw new ArgumentException("每个行动指标必须恰好注册一项正权重配方。", nameof(recipes));
    }

    public EnemySoftLockLimits StaticLocks { get; }
    public EnemySoftLockLimits FullLocks { get; }
    public int MaxCandidateAttempts { get; }
    public int StepLimit { get; }
    public IReadOnlyList<EnemyWeightedActionRecipe> WeightedRecipes { get; }
}
```

Expand `EnemyCardScoreProfile` to expose PascalCase fields `Attack`, `Block`, `Strength`, `Dexterity`, `AtField`, `OtherPersistentPower`, `Vulnerable`, `OtherDebuff`, `NormalCollection`, `StarStone`, `AbilityHint`, and `DeferredTokenHint`; assign `AtField = atField`. Retain the constructor's named `buffPowerStacks` argument and map it to `OtherPersistentPower` so the existing development catalog remains source-compatible. Implement `CalculateProfiles` with the exact static formula from the spec and make `Calculate(cards, context)` delegate to it. Update `EnemyCardDefinition.BuildSemanticFingerprint()` to read `ScoreProfile.AtField`.

Make `CardIntentTestRules : EnemyCardPlanningRules`, keep its `Default`, `ForTesting(...)`, `Recipes`, `StepLimit` and `InitialStarStoneCount` compatibility surface, and pass the development lock pair to both `staticLocks` and `fullLocks`. Change `CardIntentMoveState` and `EnemyActionMetricPlanner` fields/parameters to the base `EnemyCardPlanningRules`; existing tests may continue declaring `CardIntentTestRules` because it is the derived compatibility type.

- [ ] **Step 4: Run planner and scoring tests**

Run: `dotnet test tests\CardIntentHarness\CardIntentHarness.csproj --nologo --verbosity minimal --filter "FullyQualifiedName~PlanningRulesTests|FullyQualifiedName~ActionPlannerTests"`

Expected: PASS; existing test-monster behavior remains available through the compatibility factory.

- [ ] **Step 5: Commit the generic planning contract**

```powershell
git add Scripts/Enemy/CardIntents/EnemyCardPlanningRules.cs Scripts/Enemy/CardIntents/EnemyCardScoreProfile.cs Scripts/Enemy/CardIntents/EnemyCardScoreCalculator.cs Scripts/Enemy/CardIntents/EnemyCardDefinition.cs Scripts/Enemy/CardIntents/Test/CardIntentTestRules.cs Scripts/Enemy/CardIntents/CardIntentMoveState.cs Scripts/Enemy/CardIntents/EnemyActionMetricPlanner.cs tests/CardIntentHarness/PlanningRulesTests.testcs tests/CardIntentHarness/ActionPlannerTests.testcs
git commit -m "refactor: generalize enemy card planning rules"
```

### Task 2: Register phase-aware content and migrate Carry cards atomically

**Files:**
- Create: `Scripts/Enemy/CardIntents/EnemyCardPhase.cs`
- Create: `Scripts/Enemy/CardIntents/EnemyCardContentDirectory.cs`
- Modify: `Scripts/Enemy/CardIntents/EnemyCardDefinition.cs`
- Modify: `Scripts/Enemy/CardIntents/BaseEnemyCard.cs`
- Modify: `Scripts/Enemy/CardIntents/EnemyCardDeckRegistry.cs`
- Modify: `Scripts/Enemy/CardIntents/EnemyCardCombatState.cs`
- Modify: `Scripts/Enemy/CardIntents/BaseCardIntentMonsterModel.cs`
- Modify: `Scripts/Enemy/CardIntents/Test/CardIntentTestDeck.cs`
- Create: `tests/CardIntentHarness/PhaseMigrationTests.testcs`
- Modify: `tests/CardIntentHarness/DomainIdentityTests.testcs`

**Interfaces:**
- Consumes: `EnemyCardPlanningRules` from Task 1 and existing `EnemyCollectionCatalog`.
- Produces: `EnemyCardPhase`, `EnemyCardPhaseTemplate`, `EnemyCardContentDirectory`, `EnemyCardPhaseTransitionCandidate`, `EnemyCardCombatState.RequestPhase`, `BuildPhaseTransitionCandidate`, `ApplyPhaseTransition`.

- [ ] **Step 1: Write failing migration tests across all five zones**

```csharp
[Theory]
[InlineData(EnemyCardZone.Draw)]
[InlineData(EnemyCardZone.Current)]
[InlineData(EnemyCardZone.Retained)]
[InlineData(EnemyCardZone.Discard)]
[InlineData(EnemyCardZone.Exhaust)]
public void TransitionPreservesCarryIdentityZoneOrderAndReplay(EnemyCardZone zone)
{
    var fixture = PhaseFixture.Create();
    BaseEnemyCard carry = fixture.PlaceCarry(zone, replayCount: 2);
    BaseEnemyCard ordinary = fixture.PlaceOrdinary(zone);

    fixture.State.RequestPhase(EnemyCardPhase.Phase2);
    EnemyCardPhaseTransitionCandidate candidate = fixture.State.BuildPhaseTransitionCandidate(
        fixture.Directory.GetPhase(EnemyCardPhase.Phase2), fixture.Random);
    fixture.State.ApplyPhaseTransition(candidate);

    Assert.Same(carry, fixture.State.Find(carry.InstanceKey));
    Assert.Equal(zone, fixture.State.FindZone(carry.InstanceKey));
    Assert.Equal(2, fixture.State.Find(carry.InstanceKey).ReplayCount);
    Assert.False(fixture.State.Contains(ordinary.InstanceKey));
}

[Fact]
public void TransitionRefusesPreparedExecutingOrImmediateState()
{
    var fixture = PhaseFixture.Create();
    fixture.State.RequestPhase(EnemyCardPhase.Phase2);
    fixture.State.PushImmediateForTesting();

    Assert.Throws<InvalidOperationException>(() => fixture.State.BuildPhaseTransitionCandidate(
        fixture.Directory.GetPhase(EnemyCardPhase.Phase2), fixture.Random));
}
```

- [ ] **Step 2: Run the phase tests and verify they fail on missing APIs**

Run: `dotnet test tests\CardIntentHarness\CardIntentHarness.csproj --nologo --verbosity minimal --filter "FullyQualifiedName~PhaseMigrationTests"`

Expected: FAIL because phase state, content directory, Carry and migration APIs do not exist.

- [ ] **Step 3: Implement immutable phase content and candidate-state migration**

```csharp
public enum EnemyCardPhase { None = 0, Phase1 = 1, Phase2 = 2, Phase3 = 3 }

public sealed record EnemyCardPhaseTemplate(
    EnemyCardPhase Phase,
    IReadOnlyList<Func<BaseEnemyCard>> SourceFactories,
    EnemyCardPlanningRules PlanningRules,
    int InitialSourceInstanceCount);

public sealed class EnemyCardContentDirectory
{
    public EnemyCardContentDirectory(
        EnemyCardDeckId deckId,
        EnemyCardPhase initialPhase,
        IEnumerable<EnemyCardPhaseTemplate> phases,
        IReadOnlyDictionary<EnemyCardId, Func<BaseEnemyCard>> definitionFactories,
        EnemyCollectionCatalog collectionCatalog);

    public EnemyCardDeckId DeckId { get; }
    public EnemyCardPhase InitialPhase { get; }
    public IReadOnlyDictionary<EnemyCardId, Func<BaseEnemyCard>> DefinitionFactories { get; }
    public EnemyCollectionCatalog CollectionCatalog { get; }
    public EnemyCardPhaseTemplate GetPhase(EnemyCardPhase phase);
    public BaseEnemyCard CreateDefinition(EnemyCardId cardId);
}

public sealed record EnemyCardPhaseTransitionCandidate(
    EnemyCardPhase From,
    EnemyCardPhase To,
    long NextRevision,
    EnemyCardCombatState CandidateState);
```

Add these definition properties and include both in `SemanticFingerprint`:

```csharp
public bool CarryAcrossPhase { get; }
public EnemyCardEffectClass EffectClasses { get; }
```

Define the effect classification separately from planner Tags:

```csharp
[Flags]
public enum EnemyCardEffectClass
{
    None = 0,
    CollectionConsumer = 1 << 0,
    Control = 1 << 1,
    Finisher = 1 << 2,
    HeartWallConsumer = 1 << 3,
    ImmediateAttackProducer = 1 << 4,
    DelayedTokenProducer = 1 << 5
}
```

Add `BaseEnemyCard.SourcePhase` and an internal assign-once `AssignSourcePhase(EnemyCardPhase phase)`. Source phase is runtime instance metadata, not definition metadata: reused definitions such as Hitoshizuku can be instantiated in P2 and P3, and generated Carry Tokens record the phase in which their instance was created. Phase deck creation assigns the template phase; generated cards assign the current `ActivePhase`.

Expose `BaseEnemyCard.CarryAcrossPhase => Definition.CarryAcrossPhase` and `BaseEnemyCard.EffectClasses => Definition.EffectClasses` as read-only conveniences; never copy either value into mutable instance state.

`BuildPhaseTransitionCandidate` must clone the authoritative state, remove only non-Carry instances from each zone without lifecycle callbacks, preserve collection objects and sequence counters, add fresh next-phase sources to Draw, shuffle only the newly composed Draw using the authoritative random source, reset `LastMetric`, set `ActivePhase`, clear `PendingPhase`, increment `PhaseRevision`, and return without mutating the live state. `ApplyPhaseTransition` validates the candidate revision and swaps all fields before raising one state-change event.

Extend `EnemyCardDeckRegistry.Register` to accept `EnemyCardContentDirectory`; retain the existing two-argument overload for simple domain fixtures by creating a single `None` phase and a directory limited to the probed factories. Add `GetContentDirectory(deckId)`, `CreatePhaseDeck(deckId, phase)`, `ResolveDefinition(deckId, cardId)`, and `GetCollectionCatalog(deckId)`. Change `CardIntentTestDeck.EnsureRegistered()` to explicitly construct its directory from every `CardIntentTestCardCatalog.AllDefinitions` factory and `CardIntentTestCollectionCatalog.Catalog`, so generated Tokens and consumed collections remain reconnectable. Change reconnect resolution in `BaseCardIntentMonsterModel` to use registry methods instead of directly naming either test catalog.

- [ ] **Step 4: Run identity, phase and reconnect smoke tests**

Run: `dotnet test tests\CardIntentHarness\CardIntentHarness.csproj --nologo --verbosity minimal --filter "FullyQualifiedName~PhaseMigrationTests|FullyQualifiedName~DomainIdentityTests|FullyQualifiedName~ReconnectStateTests"`

Expected: PASS; the legacy test deck still resolves through the generic directory.

- [ ] **Step 5: Commit phase-aware content registration**

```powershell
git add Scripts/Enemy/CardIntents/EnemyCardPhase.cs Scripts/Enemy/CardIntents/EnemyCardContentDirectory.cs Scripts/Enemy/CardIntents/EnemyCardDefinition.cs Scripts/Enemy/CardIntents/BaseEnemyCard.cs Scripts/Enemy/CardIntents/EnemyCardDeckRegistry.cs Scripts/Enemy/CardIntents/EnemyCardCombatState.cs Scripts/Enemy/CardIntents/BaseCardIntentMonsterModel.cs Scripts/Enemy/CardIntents/Test/CardIntentTestDeck.cs tests/CardIntentHarness/PhaseMigrationTests.testcs tests/CardIntentHarness/DomainIdentityTests.testcs
git commit -m "feat: add atomic enemy card phase migration"
```

### Task 3: Freeze one preparation-cycle collection delta and add weighted phase recipes

**Files:**
- Modify: `Scripts/Enemy/CardIntents/EnemyActionRecipe.cs`
- Modify: `Scripts/Enemy/CardIntents/EnemyActionMetric.cs`
- Modify: `Scripts/Enemy/CardIntents/EnemyCardPlanningRules.cs`
- Modify: `Scripts/Enemy/CardIntents/EnemyCardCombatState.cs`
- Modify: `Scripts/Enemy/CardIntents/EnemyActionMetricPlanner.cs`
- Modify: `Scripts/Enemy/CardIntents/PreparedEnemyCardAction.cs`
- Create: `tests/CardIntentHarness/PreparationInventoryDeltaTests.testcs`
- Modify: `tests/CardIntentHarness/ActionPlannerTests.testcs`

**Interfaces:**
- Consumes: phase template and generic rules from Tasks 1–2.
- Produces: `EnemyActionSlotRule`, `EnemyCandidateConstraints`, `EnemyPreparedPreActionInventoryDelta`, `EnemyPreparationCycle` and weighted selection without consecutive metrics.

- [ ] **Step 1: Write failing tests for one shared delta and constrained fallback selection**

```csharp
[Fact]
public void ThreeCandidateAttemptsShareOneFrozenCollectionAndCommitItOnce()
{
    var fixture = PreparationFixture.WithRejectedFirstAndSecondCandidates();
    PreparedEnemyCardAction action = fixture.Planner.Prepare(fixture.State, fixture.Context);

    Assert.Equal(3, action.SoftLockDiagnostic.CandidateAttemptCount);
    Assert.Single(action.PreActionInventoryDelta.AddedAvailable);
    Assert.Single(fixture.State.CollectionQueue.Where(x =>
        x.InstanceId == action.PreActionInventoryDelta.AddedAvailable[0].InstanceId));
    Assert.Equal(1, fixture.Random.CollectionSelectionCalls);
}

[Fact]
public void FailedPreparationKeepsFrozenDiagnosticButDoesNotMutateInventory()
{
    var fixture = PreparationFixture.WithAllIncompleteCandidates();
    int before = fixture.State.CollectionQueue.Count;

    Assert.Throws<EnemyCandidatePlanningException>(() =>
        fixture.Planner.Prepare(fixture.State, fixture.Context));

    Assert.Equal(before, fixture.State.CollectionQueue.Count);
    Assert.NotNull(fixture.State.FrozenPreparationDelta);
}
```

- [ ] **Step 2: Run the focused tests and verify missing preparation-cycle APIs**

Run: `dotnet test tests\CardIntentHarness\CardIntentHarness.csproj --nologo --verbosity minimal --filter "FullyQualifiedName~PreparationInventoryDeltaTests"`

Expected: FAIL because the delta and cycle types are absent.

- [ ] **Step 3: Implement weighted recipes, stable predicates and atomic inventory commit**

```csharp
public sealed record EnemyActionSlotRule(
    EnemyCardTag? RequiredTag,
    IReadOnlySet<EnemyCardId>? AllowedDefinitionIds = null,
    bool MustMatchSelectedComposeMaterial = false);

public sealed record EnemyCandidateConstraints(
    int MaxComposeSources,
    int MaxImmediateAttackComposeSources,
    int MaxComposeSourcesProducingImmediateAttack);

public sealed record EnemyPreparedPreActionInventoryDelta(
    IReadOnlyList<EnemyCollectionInstance> AddedAvailable)
{
    public static EnemyPreparedPreActionInventoryDelta Empty { get; } = new([]);
}

public sealed class EnemyPreparationCycle
{
    public EnemyPreparationCycle(
        EnemyCollectionInstance? frozenPreparationCollection,
        EnemyPreparedPreActionInventoryDelta delta)
    {
        FrozenPreparationCollection = frozenPreparationCollection;
        Delta = delta ?? throw new ArgumentNullException(nameof(delta));
    }

    public EnemyCollectionInstance? FrozenPreparationCollection { get; }
    public EnemyPreparedPreActionInventoryDelta Delta { get; }
}
```

Extend the enum without renaming the development-test values:

```csharp
public enum EnemyActionMetric
{
    Gain,
    Attack,
    ComposeTest,
    Fortify,
    Pressure,
    Compose,
    Burst,
    Growth
}
```

Change `EnemyActionRecipe` to contain `IReadOnlyList<EnemyActionSlotRule> Slots` and `EnemyCandidateConstraints Constraints`. Weighted selection uses a single integer roll over eligible recipes after removing `LastMetric`; remaining weights are renormalized by the roll range. `FillRecipe` must enforce constraints both for matched slots and random fallback. The compose-material slot resolves against the already selected Compose source's `EnemyMaterialRequest`, not a new tag.

Extend `EnemyPlanningContext` with:

```csharp
public Func<EnemyCardCombatState, IEnemyCardRandomSource,
    EnemyPreparationCycle> CreatePreparationCycle { get; }
```

Call it exactly once before the candidate loop. Store both `FrozenPreparationCollection` and its `PreparedPreActionInventoryDelta`; apply the same delta to each candidate planning inventory. `CommitPreparedAction` atomically appends the delta and action; rejected candidates never touch live inventory. If all candidates are structurally incomplete, retain the frozen selection, delta and fault diagnostic for reconnect, but do not append it to Available.

- [ ] **Step 4: Run planner, inventory and material tests**

Run: `dotnet test tests\CardIntentHarness\CardIntentHarness.csproj --nologo --verbosity minimal --filter "FullyQualifiedName~PreparationInventoryDeltaTests|FullyQualifiedName~ActionPlannerTests|FullyQualifiedName~MaterialResolverTests|FullyQualifiedName~CollectionInventoryTests"`

Expected: PASS; a preparation cycle advances the collection RNG once regardless of candidate count.

- [ ] **Step 5: Commit preparation-cycle semantics**

```powershell
git add Scripts/Enemy/CardIntents/EnemyActionRecipe.cs Scripts/Enemy/CardIntents/EnemyActionMetric.cs Scripts/Enemy/CardIntents/EnemyCardPlanningRules.cs Scripts/Enemy/CardIntents/EnemyCardCombatState.cs Scripts/Enemy/CardIntents/EnemyActionMetricPlanner.cs Scripts/Enemy/CardIntents/PreparedEnemyCardAction.cs tests/CardIntentHarness/PreparationInventoryDeltaTests.testcs tests/CardIntentHarness/ActionPlannerTests.testcs
git commit -m "feat: freeze enemy preparation inventory deltas"
```

### Task 4: Replace implicit compose timing with an explicit ordered card program

**Files:**
- Create: `Scripts/Enemy/CardIntents/EnemyCardProgram.cs`
- Modify: `Scripts/Enemy/CardIntents/EnemyCardDefinition.cs`
- Modify: `Scripts/Enemy/CardIntents/BaseEnemyCard.cs`
- Modify: `Scripts/Enemy/CardIntents/EnemyPreparedResolutionPlanner.cs`
- Modify: `Scripts/Enemy/CardIntents/PreparedEnemyResolutionPlan.cs`
- Modify: `Scripts/Enemy/CardIntents/EnemyCardExecutionEngine.cs`
- Modify: `Scripts/Enemy/CardIntents/EnemyActionProjectionService.cs`
- Modify: `Scripts/Enemy/CardIntents/EnemyCardRuntimeSyncState.cs`
- Modify: `tests/CardIntentHarness/FrozenResolutionPlanTests.testcs`
- Modify: `tests/CardIntentHarness/ExecutionEngineTests.testcs`

**Interfaces:**
- Consumes: existing `PreparedConsumedCardStep`, `PreparedConsumedCollectionStep`, `PreparedComposeResultStep`, `PreparedDirectEffectsStep`.
- Produces: `EnemyCardProgramOperationKind`, `EnemyCardProgramOperation`, `EnemyCardResolutionProgram`, `IEnemyCardPlayCondition`.

- [ ] **Step 1: Write failing order and conditional-retain tests**

```csharp
[Fact]
public void ImmediateComposeCanResolveBeforeSourceDirectEffects()
{
    var definition = FixtureCard.ComposeBeforeDirect();
    PreparedEnemyCardUnitPlan unit = ResolutionFixture.Plan(definition);

    Assert.Collection(unit.OrderedSteps,
        step => Assert.IsType<PreparedConsumedCardStep>(step),
        step => Assert.IsType<PreparedComposeResultStep>(step),
        step => Assert.IsType<PreparedDirectEffectsStep>(step));
}

[Fact]
public void FailedPlayConditionCreatesNoUnitAndUsesRetainDisposition()
{
    var fixture = ResolutionFixture.WithHeartWall(amount: 3m);
    PreparedEnemyCardSource source = fixture.Plan(FixtureCard.RequiresHeartWall(amount: 4m));

    Assert.Empty(source.Units);
    Assert.Equal(EnemyCardZone.Retained, fixture.Transaction.FindZone(source.SourceKey));
}
```

- [ ] **Step 2: Run the resolution tests and verify the current fixed order fails**

Run: `dotnet test tests\CardIntentHarness\CardIntentHarness.csproj --nologo --verbosity minimal --filter "FullyQualifiedName~FrozenResolutionPlanTests|FullyQualifiedName~ExecutionEngineTests"`

Expected: FAIL because compose is currently placed by implicit timing and conditions cannot fail before unit creation.

- [ ] **Step 3: Add validated ordered programs and play conditions**

```csharp
public enum EnemyCardProgramOperationKind
{
    ConsumeMaterials,
    ComposeResult,
    DirectEffects
}

public sealed record EnemyCardProgramOperation(EnemyCardProgramOperationKind Kind);

public sealed class EnemyCardResolutionProgram
{
    public EnemyCardResolutionProgram(IEnumerable<EnemyCardProgramOperation> operations);
    public IReadOnlyList<EnemyCardProgramOperation> Operations { get; }
    public string Fingerprint { get; }
}

public interface IEnemyCardPlayCondition
{
    string ProgramId { get; }
    bool CanPlan(EnemyPreparedPlanningState state, BaseEnemyCard card);
    bool CanSimulate(EnemyCardSimulationContext context);
    bool CanExecute(EnemyCardExecutionContext context);
}
```

Validate that a definition with material requests contains one `ConsumeMaterials`, a Compose definition contains one `ComposeResult`, and a definition with effects contains one `DirectEffects`; reject duplicate or missing operations. Put the ordered program fingerprint and condition `ProgramId` in `SemanticFingerprint` and reconnect validation. `TryBuildUnit` checks `CanPlan` before reserving materials, then iterates the program to append prepared steps. Simulation and execution revalidate the frozen condition only to detect state corruption; they never select a different branch.

Keep the existing `BaseEnemyCard` constructor source-compatible during this task: when callers omit `EnemyCardResolutionProgram`, translate `EnemyCardCustomExecutionTiming.BeforeBaseEffects` to `ConsumeMaterials → ComposeResult → DirectEffects` and `AfterBaseEffects` to `ConsumeMaterials → DirectEffects → ComposeResult`, omitting operations that the definition does not need. The registered semantic fingerprint stores only the resulting explicit program, so the compatibility enum does not create a second execution path.

Use `ConsumeMaterials → ComposeResult → DirectEffects` for Hitoshizuku and Mayoiuta. Use `ConsumeMaterials → DirectEffects → ComposeResult` for NamelessPaper, WantBeYourGod, Senzaihyoumei, SongOfBeHuman and Haruhikage. Cards without Compose use only applicable operations.

- [ ] **Step 4: Run plan, execution, projection and reconnect tests**

Run: `dotnet test tests\CardIntentHarness\CardIntentHarness.csproj --nologo --verbosity minimal --filter "FullyQualifiedName~FrozenResolutionPlanTests|FullyQualifiedName~ExecutionEngineTests|FullyQualifiedName~LiveProjectionTests|FullyQualifiedName~ReconnectStateTests"`

Expected: PASS; source and immediate child ordering is identical in plan, projection, execution and restoration.

- [ ] **Step 5: Commit explicit card programs**

```powershell
git add Scripts/Enemy/CardIntents/EnemyCardProgram.cs Scripts/Enemy/CardIntents/EnemyCardDefinition.cs Scripts/Enemy/CardIntents/BaseEnemyCard.cs Scripts/Enemy/CardIntents/EnemyPreparedResolutionPlanner.cs Scripts/Enemy/CardIntents/PreparedEnemyResolutionPlan.cs Scripts/Enemy/CardIntents/EnemyCardExecutionEngine.cs Scripts/Enemy/CardIntents/EnemyActionProjectionService.cs Scripts/Enemy/CardIntents/EnemyCardRuntimeSyncState.cs tests/CardIntentHarness/FrozenResolutionPlanTests.testcs tests/CardIntentHarness/ExecutionEngineTests.testcs
git commit -m "feat: order enemy card resolution programs"
```

### Task 5: Freeze effective-card count and X metadata per actual instance

**Files:**
- Create: `Scripts/Enemy/CardIntents/EnemyEffectiveCardLedger.cs`
- Modify: `Scripts/Enemy/CardIntents/EnemyCardCombatState.cs`
- Modify: `Scripts/Enemy/CardIntents/EnemyPreparedResolutionPlanner.cs`
- Modify: `Scripts/Enemy/CardIntents/PreparedEnemyResolutionPlan.cs`
- Modify: `Scripts/Enemy/CardIntents/PreparedEnemyCardAction.cs`
- Modify: `Scripts/Enemy/CardIntents/EnemyCardExecutionEngine.cs`
- Modify: `Scripts/Enemy/CardIntents/EnemyCardSimulationContext.cs`
- Modify: `Scripts/Enemy/CardIntents/LiveActionProjection.cs`
- Create: `tests/CardIntentHarness/EffectiveCardLedgerTests.testcs`
- Modify: `tests/CardIntentHarness/FrozenResolutionPlanTests.testcs`
- Modify: `tests/CardIntentHarness/ExecutionEngineTests.testcs`

**Interfaces:**
- Consumes: `ExecutingCardKey` already present on every prepared unit.
- Produces: `EnemyFrozenEffectiveCardState`, `EnemyEffectiveCardLedger`, `PreparedEnemyCardAction.EffectiveCardStates`.

- [ ] **Step 1: Write failing tests for body/replay reuse and DFS immediate ordering**

```csharp
[Fact]
public void ReplayReusesFrozenXAndCountsTheActualCardOnce()
{
    var ledger = new EnemyEffectiveCardLedger(initialCount: 2);
    var key = new EnemyCardInstanceKey("X_TOKEN");
    EnemyFrozenEffectiveCardState frozen = ledger.Begin(key, isX: true, multiplier: 2);

    ledger.Complete(key, anyUnitSucceeded: true);

    Assert.Equal(Math.Max(0, 6 - frozen.FrozenN) * frozen.Multiplier, frozen.FrozenX);
    EnemyFrozenEffectiveCardState replayFrozen = ledger.Begin(key, isX: true, multiplier: 1);
    Assert.Equal(frozen.FrozenN, replayFrozen.FrozenN);
    Assert.Equal(frozen.FrozenX, replayFrozen.FrozenX);
    Assert.Equal(frozen.Multiplier, replayFrozen.Multiplier);
    Assert.Equal(3, ledger.CompletedEffectiveCardCount);
}

[Fact]
public void ImmediateChildCompletesBeforeParentButParentReplayKeepsOriginalX()
{
    EffectiveLedgerFixture result = EffectiveLedgerFixture.PlanParentReplayWithImmediateChild();

    Assert.Equal(result.ParentBeforeChild.FrozenX, result.ParentAfterChildReplay.FrozenX);
    Assert.Equal(result.ParentBeforeChild.FrozenN, result.ParentAfterChildReplay.FrozenN);
    Assert.Equal(result.ParentBeforeChild.FrozenN, result.Child.FrozenN);
    Assert.True(result.Child.CountedBeforeParent);
}
```

- [ ] **Step 2: Run the ledger tests and verify missing frozen metadata**

Run: `dotnet test tests\CardIntentHarness\CardIntentHarness.csproj --nologo --verbosity minimal --filter "FullyQualifiedName~EffectiveCardLedgerTests"`

Expected: FAIL because `EnemyEffectiveCardLedger` and per-instance frozen state do not exist.

- [ ] **Step 3: Implement the per-instance ledger and thread it through all paths**

```csharp
public sealed record EnemyFrozenEffectiveCardState(
    EnemyCardInstanceKey ExecutingCardInstanceKey,
    int FrozenN,
    int? FrozenX,
    int Multiplier,
    bool WasCounted);

public sealed class EnemyEffectiveCardLedger
{
    public EnemyEffectiveCardLedger(int initialCount = 0);
    public int CompletedEffectiveCardCount { get; }
    public IReadOnlyDictionary<EnemyCardInstanceKey, EnemyFrozenEffectiveCardState> States { get; }
    public EnemyFrozenEffectiveCardState Begin(
        EnemyCardInstanceKey key,
        bool isX,
        int multiplier);
    public void Complete(EnemyCardInstanceKey key, bool anyUnitSucceeded);
}
```

`Begin` uses `FrozenN = CompletedEffectiveCardCount`, `FrozenX = isX ? Math.Max(0, 6 - FrozenN) * multiplier : null`, and returns the existing state unchanged on every Replay. `Complete` increments the count only once and only when at least one unit succeeded. The resolution planner owns one ledger per whole candidate; it begins an actual card before its first unit, recursively completes Immediate children before returning, and completes the parent only after all parent Replay units. The prepared action stores an immutable copy. Simulation and execution validate against that copy; no path recomputes X.

Add an `EnemyFrozenXAttackAllEffect` whose hit count is read by `ExecutingCardInstanceKey` from the frozen action metadata. `FinalHitCount == 0` emits a successful zero-hit unit and still allows the card to be counted.

- [ ] **Step 4: Run ledger, plan, engine and projection tests**

Run: `dotnet test tests\CardIntentHarness\CardIntentHarness.csproj --nologo --verbosity minimal --filter "FullyQualifiedName~EffectiveCardLedgerTests|FullyQualifiedName~FrozenResolutionPlanTests|FullyQualifiedName~ExecutionEngineTests|FullyQualifiedName~LiveProjectionTests"`

Expected: PASS; Replay never changes `N`, and every consumer reads the same frozen X.

- [ ] **Step 5: Commit effective-card X semantics**

```powershell
git add Scripts/Enemy/CardIntents/EnemyEffectiveCardLedger.cs Scripts/Enemy/CardIntents/EnemyCardCombatState.cs Scripts/Enemy/CardIntents/EnemyPreparedResolutionPlanner.cs Scripts/Enemy/CardIntents/PreparedEnemyResolutionPlan.cs Scripts/Enemy/CardIntents/PreparedEnemyCardAction.cs Scripts/Enemy/CardIntents/EnemyCardExecutionEngine.cs Scripts/Enemy/CardIntents/EnemyCardSimulationContext.cs Scripts/Enemy/CardIntents/LiveActionProjection.cs tests/CardIntentHarness/EffectiveCardLedgerTests.testcs tests/CardIntentHarness/FrozenResolutionPlanTests.testcs tests/CardIntentHarness/ExecutionEngineTests.testcs
git commit -m "feat: freeze enemy effective-card X values"
```

### Task 6: Project end state and calculate the full four-part risk score

**Files:**
- Create: `Scripts/Enemy/CardIntents/EnemyActionRiskCalculator.cs`
- Modify: `Scripts/Enemy/CardIntents/EnemyCardSimulationContext.cs`
- Modify: `Scripts/Enemy/CardIntents/LiveActionProjection.cs`
- Modify: `Scripts/Enemy/CardIntents/EnemyActionProjectionService.cs`
- Create: `tests/CardIntentHarness/ActionRiskTests.testcs`
- Modify: `tests/CardIntentHarness/LiveProjectionTests.testcs`

**Interfaces:**
- Consumes: full prepared action and effective-card metadata from Tasks 4–5.
- Produces: `EnemyProjectionEndState`, `EnemyProjectedCardZoneState`, `EnemyActionRiskScore`, `EnemyActionRiskCalculator.Calculate`.

- [ ] **Step 1: Write failing relational tests for attack, end-state and deferred risk**

```csharp
[Fact]
public void AttackRiskUsesTheMostExposedLivingPlayerWithoutDoubleScaling()
{
    LiveActionProjection projection = RiskFixture.TwoPlayersWithDifferentDamage();
    EnemyActionRiskScore score = new EnemyActionRiskCalculator().Calculate(
        projection, RiskFixture.Phase3Context());

    Assert.Equal(projection.Units
        .SelectMany(x => x.Targets)
        .GroupBy(x => x.TargetId)
        .Max(g => g.Sum(x => x.TotalDamage)), score.AttackRisk);
}

[Fact]
public void EndStockContributesEvenWhenTheActionAddsNoNewStock()
{
    EnemyActionRiskScore withStock = RiskFixture.ScoreNoOpWithExistingStock();
    EnemyActionRiskScore withoutStock = RiskFixture.ScoreNoOpWithoutStock();

    Assert.True(withStock.SurvivalRisk > withoutStock.SurvivalRisk);
    Assert.True(withStock.EngineRisk > withoutStock.EngineRisk);
}

[Fact]
public void CarryTokenBodyAndReplayAreCountedExactlyOnceEach()
{
    var oneBody = RiskFixture.CarryToken(replayCount: 0);
    var bodyAndReplay = RiskFixture.CarryToken(replayCount: 1);

    Assert.Equal(oneBody.DeferredRisk * 2m, bodyAndReplay.DeferredRisk);
}
```

- [ ] **Step 2: Run risk tests and verify end-state data is absent**

Run: `dotnet test tests\CardIntentHarness\CardIntentHarness.csproj --nologo --verbosity minimal --filter "FullyQualifiedName~ActionRiskTests"`

Expected: FAIL because the projection has no end-state snapshot or risk calculator.

- [ ] **Step 3: Implement immutable end-state projection and exact formulas**

```csharp
public sealed record EnemyProjectedCardZoneState(
    EnemyCardInstanceKey InstanceKey,
    EnemyCardId CardId,
    EnemyCardZone Zone,
    EnemyCardPhase SourcePhase,
    bool CarryAcrossPhase,
    int ReplayCount);

public sealed record EnemyProjectionEndState(
    decimal EnemyBlock,
    decimal Strength,
    decimal Dexterity,
    decimal HeartWall,
    IReadOnlyDictionary<string, decimal> EnemyPowers,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, decimal>> TargetPowers,
    IReadOnlyList<EnemyProjectedCardZoneState> Cards,
    IReadOnlyList<EnemyCollectionInstance> AvailableCollections,
    IReadOnlyList<EnemyCollectionInstance> ConsumedCollections);

public sealed record EnemyActionRiskScore(
    decimal AttackRisk,
    decimal SurvivalRisk,
    decimal EngineRisk,
    decimal DeferredRisk)
{
    public decimal TotalRisk => AttackRisk + SurvivalRisk + EngineRisk + DeferredRisk;
}

public sealed record EnemyActionRiskContext(
    EnemyCardPhase Phase,
    int PhaseInitialTemplateInstanceCount,
    EnemyCardContentDirectory ContentDirectory);
```

Implement the formulas verbatim from spec §15.3:

```text
AttackRisk = max living-player summed ProjectedDamage
SurvivalRisk = 0.65*EndBlock + 6*EndDexterity + 3*EndHeartWall + DefensivePowerRisk
EngineRisk = 10*EndStrength + AbilityRisk + 6*EndTargetVulnerable
             + 3*EndOtherTargetDebuff + CollectionInventoryRisk + CompressionRisk
DeferredRisk = ReactiveRisk + CarryTokenRisk + ReplayGrowthRisk
```

Use phase compression weights P1/P2/P3 = 0/1/3. Only reusable non-Carry sources from the active phase remain in the denominator. Use zone coefficients Retained/Draw-or-Discard/Exhaust = 0.75/0.45/0.15. Expand one Token body plus exactly `ReplayCount` replays. Limit future chain recursion to three hops, multiply each hop by 0.6 and feasibility 1/0.5/0.25/0. Ensure consumed collections do not contribute inventory risk.

`LiveActionProjection` must carry `EndState`, `EffectiveCardStates`, `IsComplete`, and diagnostics. Any missing X state, unknown modifier, missing effect simulation adapter or step-limit truncation sets `IsComplete = false`.

- [ ] **Step 4: Run projection and risk tests**

Run: `dotnet test tests\CardIntentHarness\CardIntentHarness.csproj --nologo --verbosity minimal --filter "FullyQualifiedName~ActionRiskTests|FullyQualifiedName~LiveProjectionTests"`

Expected: PASS; risk is computed from the projected end state without mutating battle state.

- [ ] **Step 5: Commit full action-risk projection**

```powershell
git add Scripts/Enemy/CardIntents/EnemyActionRiskCalculator.cs Scripts/Enemy/CardIntents/EnemyCardSimulationContext.cs Scripts/Enemy/CardIntents/LiveActionProjection.cs Scripts/Enemy/CardIntents/EnemyActionProjectionService.cs tests/CardIntentHarness/ActionRiskTests.testcs tests/CardIntentHarness/LiveProjectionTests.testcs
git commit -m "feat: score complete enemy action risk"
```

### Task 7: Insert the complete projection gate before authoritative commit

**Files:**
- Modify: `Scripts/Enemy/CardIntents/EnemyActionMetricPlanner.cs`
- Modify: `Scripts/Enemy/CardIntents/PreparedEnemyCardAction.cs`
- Modify: `Scripts/Enemy/CardIntents/CardIntentMoveState.cs`
- Modify: `Scripts/Enemy/CardIntents/CardIntentMoveRuntime.cs`
- Modify: `tests/CardIntentHarness/ActionPlannerTests.testcs`
- Modify: `tests/CardIntentHarness/LiveProjectionTests.testcs`

**Interfaces:**
- Consumes: static score and `EnemyActionRiskCalculator`.
- Produces: `EnemyCandidateCommitMode`, `EnemyCandidateRejection`, expanded `EnemySoftLockDiagnostic`, `EnemyCandidatePlanningException`.

- [ ] **Step 1: Write failing tests for both gates and incomplete final candidates**

```csharp
[Fact]
public void StaticPassAndFullFailureRejectsWithoutCommittingCandidateState()
{
    var fixture = PlannerGateFixture.StaticPassFullFailThenPass();
    EnemyCardPlanningStateSnapshot before = fixture.State.CreatePlanningSnapshot();

    PreparedEnemyCardAction action = fixture.Planner.Prepare(fixture.State, fixture.Context);

    Assert.Equal(2, action.SoftLockDiagnostic.CandidateAttemptCount);
    Assert.Equal(before.InstanceKeys, fixture.FirstRejectedObservedAuthority.InstanceKeys);
}

[Fact]
public void CompleteThirdCandidateMayForceButIncompleteThirdCandidateFaults()
{
    var complete = PlannerGateFixture.ThreeOverLockCompleteCandidates();
    Assert.Equal(EnemyCandidateCommitMode.ForcedOverLock,
        complete.Prepare().SoftLockDiagnostic.CommitMode);

    var incomplete = PlannerGateFixture.ThirdCandidateIncomplete();
    Assert.Throws<EnemyCandidatePlanningException>(() => incomplete.Prepare());
    Assert.Null(incomplete.State.PreparedAction);
}
```

- [ ] **Step 2: Run planner gate tests and verify current final-attempt behavior is too permissive**

Run: `dotnet test tests\CardIntentHarness\CardIntentHarness.csproj --nologo --verbosity minimal --filter "FullyQualifiedName~ActionPlannerTests"`

Expected: FAIL because the planner only has one score layer and forces the last static candidate.

- [ ] **Step 3: Implement the two-gate candidate loop**

```csharp
public enum EnemyCandidateCommitMode { WithinLocks, ForcedOverLock }
public enum EnemyCandidateRejectionReason
{
    StaticOverLock,
    FullOverLock,
    IncompleteProjection,
    PlanningFault
}

public sealed record EnemyCandidateRejection(
    int Attempt,
    EnemyCandidateRejectionReason Reason,
    string Diagnostic);

public sealed record EnemySoftLockDiagnostic(
    EnemyCardScore StaticScore,
    EnemyActionRiskScore FullScore,
    EnemySoftLockLimits StaticLocks,
    EnemySoftLockLimits FullLocks,
    int CandidateAttemptCount,
    IReadOnlyList<EnemyCandidateRejection> Rejections,
    EnemyCandidateCommitMode CommitMode,
    bool ProjectionIsComplete,
    IReadOnlyList<string> ProjectionDiagnostics);
```

Add `EnemyCardPhase Phase` to `PreparedEnemyCardAction` and require it to equal the state's `ActivePhase` at construction and commit. This value is included in the diagnostic DTO and is the phase used to choose both lock sets; no scoring path reads `PendingPhase`.

For attempts 1–2: reject above static locks before full freeze; after a static pass, freeze the full candidate, project it before `CommitPreparedAction`, reject incomplete or above full locks. For attempt 3: always build the full candidate even if static is above lock; commit above-lock only when the full projection is complete, with `ForcedOverLock`. If all attempts are incomplete/faulted, mark runtime Faulted with candidate diagnostics and throw `EnemyCandidatePlanningException`; do not build a fallback action. Preserve RNG progression but do not copy any rejected candidate zones, inventory or Replay changes to authority.

- [ ] **Step 4: Run all planner/projection focused tests**

Run: `dotnet test tests\CardIntentHarness\CardIntentHarness.csproj --nologo --verbosity minimal --filter "FullyQualifiedName~ActionPlannerTests|FullyQualifiedName~PreparationInventoryDeltaTests|FullyQualifiedName~ActionRiskTests|FullyQualifiedName~LiveProjectionTests"`

Expected: PASS; commit occurs only after complete full projection.

- [ ] **Step 5: Commit the dual soft-lock gate**

```powershell
git add Scripts/Enemy/CardIntents/EnemyActionMetricPlanner.cs Scripts/Enemy/CardIntents/PreparedEnemyCardAction.cs Scripts/Enemy/CardIntents/CardIntentMoveState.cs Scripts/Enemy/CardIntents/CardIntentMoveRuntime.cs tests/CardIntentHarness/ActionPlannerTests.testcs tests/CardIntentHarness/LiveProjectionTests.testcs
git commit -m "feat: gate enemy actions with full risk projection"
```

### Task 8: Version and restore phase, preparation, score and X state

**Files:**
- Modify: `Scripts/Enemy/CardIntents/EnemyCardRuntimeSyncState.cs`
- Modify: `Scripts/Enemy/CardIntents/BaseCardIntentMonsterModel.cs`
- Modify: `Scripts/Enemy/CardIntents/CardIntentMoveState.cs`
- Modify: `tests/CardIntentHarness/ReconnectStateTests.testcs`

**Interfaces:**
- Consumes: all runtime state introduced in Tasks 2–7.
- Produces: schema version 3 DTOs for phase, delta, effective-card state and two score snapshots.

- [ ] **Step 1: Write failing round-trip and rejection tests**

```csharp
[Fact]
public void RoundTripPreservesCarryInExhaustFrozenXAndPendingPreparation()
{
    ReconnectFixture source = ReconnectFixture.ShadowPreparedWithCarryAndX();
    EnemyCardRuntimeSyncState dto = source.Capture();
    ReconnectFixture target = ReconnectFixture.FreshShadow();

    Assert.True(target.TryRestore(dto, out string reason), reason);
    Assert.Equal(source.State.ActivePhase, target.State.ActivePhase);
    Assert.Equal(source.State.PendingPhase, target.State.PendingPhase);
    Assert.Equal(source.State.EffectiveCardLedger.States, target.State.EffectiveCardLedger.States);
    Assert.Equal(source.State.ExhaustPile.Single(x => x.CarryAcrossPhase).InstanceKey,
        target.State.ExhaustPile.Single(x => x.CarryAcrossPhase).InstanceKey);
}

[Fact]
public void RestoreRejectsMissingFrozenXWithoutChangingAuthority()
{
    var fixture = ReconnectFixture.ShadowPreparedWithCarryAndX();
    EnemyCardRuntimeSyncState corrupt = fixture.CaptureWithoutRequiredX();
    EnemyCardPlanningStateSnapshot before = fixture.State.CreatePlanningSnapshot();

    Assert.False(fixture.TryRestore(corrupt, out _));
    Assert.Equal(before.InstanceKeys, fixture.State.CreatePlanningSnapshot().InstanceKeys);
}
```

- [ ] **Step 2: Run reconnect tests and verify the old schema cannot express the state**

Run: `dotnet test tests\CardIntentHarness\CardIntentHarness.csproj --nologo --verbosity minimal --filter "FullyQualifiedName~ReconnectStateTests"`

Expected: FAIL because the DTO lacks phase, preparation, full score and X fields.

- [ ] **Step 3: Extend schema and validate in a temporary state before swap**

Set `EnemyCardRuntimeSyncState.CurrentSchemaVersion = 3` and add:

```csharp
public EnemyCardPhase ActivePhase { get; init; }
public EnemyCardPhase PendingPhase { get; init; }
public long PhaseRevision { get; init; }
public EnemyCollectionRuntimeState? FrozenPreparationCollection { get; init; }
public EnemyPreparedPreActionInventoryDeltaSyncState? FrozenPreparationDelta { get; init; }
public int CompletedEffectiveCardCount { get; init; }
public IReadOnlyList<EnemyFrozenEffectiveCardSyncState> EffectiveCardStates { get; init; } = [];
public EnemyStaticScoreSyncState? StaticScore { get; init; }
public EnemyActionRiskScoreSyncState? FullScore { get; init; }
public EnemyCandidateCommitMode? CommitMode { get; init; }
public bool? ProjectionIsComplete { get; init; }
public IReadOnlyList<string> ProjectionDiagnostics { get; init; } = [];
```

Each card DTO must also carry `SourcePhase` and definition `CarryAcrossPhase`; restore verifies them against the registered content directory rather than trusting the wire. Verify: phase transition legality, unique instances across five zones, collection sequence monotonicity, exact prepared-action identities, X metadata for every X execution key, no duplicate `WasCounted`, score/commit-mode closure, and no `ForcedOverLock` with incomplete projection. Build and validate a temporary state, then call `ApplyValidatedCombatState` once. Never reshuffle, regenerate a preparation collection or recompute X during restore.

- [ ] **Step 4: Run reconnect and identity tests**

Run: `dotnet test tests\CardIntentHarness\CardIntentHarness.csproj --nologo --verbosity minimal --filter "FullyQualifiedName~ReconnectStateTests|FullyQualifiedName~DomainIdentityTests|FullyQualifiedName~EffectiveCardLedgerTests"`

Expected: PASS; corrupt snapshots leave the prior authority unchanged.

- [ ] **Step 5: Commit schema version 3**

```powershell
git add Scripts/Enemy/CardIntents/EnemyCardRuntimeSyncState.cs Scripts/Enemy/CardIntents/BaseCardIntentMonsterModel.cs Scripts/Enemy/CardIntents/CardIntentMoveState.cs tests/CardIntentHarness/ReconnectStateTests.testcs
git commit -m "feat: sync shadow enemy card phase state"
```

### Task 9: Add Shadow-specific effect nodes and ability simulation hooks

**Files:**
- Create: `Scripts/Enemy/CardIntents/ShadowTomorin/ShadowTomorinEffects.cs`
- Create: `Scripts/Powers/EnemyPowers/ShadowTomoriFormPower.cs`
- Create: `Scripts/Powers/EnemyPowers/CardIntentHeartBeatPower.cs`
- Create: `Scripts/Powers/EnemyPowers/CardIntentUnwantedSixthPower.cs`
- Modify: `Scripts/Powers/EnemyPowers/CardIntentSorrowfulRainPower.cs`
- Modify: `Scripts/Powers/EnemyPowers/CardIntentAdayumePower.cs`
- Modify: `Scripts/Enemy/CardIntents/EnemyAbilityHookDispatcher.cs`
- Modify: `Scripts/Enemy/CardIntents/EnemyCardExecutionEngine.cs`
- Modify: `Scripts/Enemy/CardIntents/EnemyCardSimulationContext.cs`
- Modify: `tests/CardIntentHarness/ExecutionEngineTests.testcs`
- Modify: `tests/CardIntentHarness/LiveProjectionTests.testcs`

**Interfaces:**
- Consumes: `IEnemyCardEffectNode`, `IEnemyCardPlayCondition`, ordered program and end-state projection.
- Produces: stable effect builders used by the formal catalog and matched real/simulated ability hook dispatch.

- [ ] **Step 1: Write failing parity tests for all ability hook classes**

```csharp
[Theory]
[InlineData(ShadowAbilityId.SorrowfulRain)]
[InlineData(ShadowAbilityId.Adayume)]
[InlineData(ShadowAbilityId.HeartBeat)]
[InlineData(ShadowAbilityId.DuckAndCover)]
[InlineData(ShadowAbilityId.NameOfTear)]
[InlineData(ShadowAbilityId.UnwantedSixth)]
public async Task RealAndSimulatedAbilityHooksProduceEquivalentDeltas(ShadowAbilityId ability)
{
    AbilityParityFixture fixture = AbilityParityFixture.Create(ability);
    await fixture.ExecuteRealAsync();
    fixture.Simulate();

    Assert.Equal(fixture.RealBlockDelta, fixture.SimulatedBlockDelta);
    Assert.Equal(fixture.RealHeartWallDelta, fixture.SimulatedHeartWallDelta);
}

[Fact]
public async Task MigrationRemovalDoesNotTriggerHeartBeat()
{
    AbilityParityFixture fixture = AbilityParityFixture.HeartBeatWithMigrationRemoval();
    await fixture.MigrateAsync();
    Assert.Equal(decimal.Zero, fixture.BlockDelta);
}
```

- [ ] **Step 2: Run hook parity tests and verify missing adapters**

Run: `dotnet test tests\CardIntentHarness\CardIntentHarness.csproj --nologo --verbosity minimal --filter "FullyQualifiedName~ExecutionEngineTests|FullyQualifiedName~LiveProjectionTests"`

Expected: FAIL because the dispatcher only handles SorrowfulRain and Adayume execution hooks.

- [ ] **Step 3: Implement exact effect and hook contracts**

```csharp
public enum ShadowAbilityId
{
    SorrowfulRain,
    Adayume,
    HeartBeat,
    DuckAndCover,
    NameOfTear,
    UnwantedSixth
}

public interface IEnemyAbilityHookDispatcher
{
    Task BeforePreparationAsync(EnemyCardExecutionContext context);
    void SimulateBeforePreparation(EnemyCardSimulationContext context);
    Task AfterComposeAsync(EnemyCardExecutionContext context);
    void SimulateAfterCompose(EnemyCardSimulationContext context);
    Task AfterSuccessfulUnitAsync(EnemyCardExecutionContext context);
    void SimulateAfterSuccessfulUnit(EnemyCardSimulationContext context);
    Task AfterBlockGainAsync(EnemyCardExecutionContext context, decimal gainedBlock);
    void SimulateAfterBlockGain(EnemyCardSimulationContext context, decimal gainedBlock);
    Task AfterNormalLifecycleExhaustAsync(EnemyCardExecutionContext context, BaseEnemyCard card);
    void SimulateAfterNormalLifecycleExhaust(EnemyCardSimulationContext context, BaseEnemyCard card);
}
```

`ShadowTomoriFormPower` exposes the preparation provider used by the move state:

```csharp
public EnemyPreparationCycle CreatePreparationCycle(
    EnemyCardCombatState state,
    IEnemyCardRandomSource randomSource,
    IReadOnlyList<(EnemyCollectionDefinition Definition, int Weight)> weightedCollections);
```

The method consumes one authoritative weighted roll, reserves `state.CollectionInventory.NextSequence` in the returned instance, and does not mutate Available. If the Power is absent, the move state supplies `new EnemyPreparationCycle(null, EnemyPreparedPreActionInventoryDelta.Empty)`.

Implement:

- SorrowfulRain: every successful Compose adds 3 HeartWall per ability stack.
- Adayume: every successful execution unit, including each Replay, adds 1 block and 1 HeartWall per stack; it does not alter `N`.
- HeartBeat: a source entering Exhaust through normal lifecycle adds 2 block per stack; phase removal bypasses this hook.
- DuckAndCover: before each preparation, add block equal to current HeartWall; use existing `DuckAndCoverPower` as the active marker.
- NameOfTear: use existing non-stacking `NameOfTearPower`; reactive forecast multiplier is 1.5.
- UnwantedSixth: during the current complete action, every independent positive block grant adds 1 HeartWall per stack; guard against recursively treating the resulting HeartWall as block.

Add effect nodes with paired simulation/execution implementations for: dynamic `9 + 3*HeartWall` all-player damage, remove 4 HeartWall then gain 1 Strength, optional consume up to three Available collections, consume a non-Compose source, generate frozen weighted collections, and frozen-X all-player multi-hit. Every node has a stable `ProgramId`; no effect calls a player card's `OnPlay`.

- [ ] **Step 4: Run execution and projection parity tests**

Run: `dotnet test tests\CardIntentHarness\CardIntentHarness.csproj --nologo --verbosity minimal --filter "FullyQualifiedName~ExecutionEngineTests|FullyQualifiedName~LiveProjectionTests|FullyQualifiedName~ActionRiskTests"`

Expected: PASS; any unsupported hook marks projection incomplete instead of silently returning zero.

- [ ] **Step 5: Commit Shadow effect and ability adapters**

```powershell
git add Scripts/Enemy/CardIntents/ShadowTomorin/ShadowTomorinEffects.cs Scripts/Powers/EnemyPowers/ShadowTomoriFormPower.cs Scripts/Powers/EnemyPowers/CardIntentHeartBeatPower.cs Scripts/Powers/EnemyPowers/CardIntentUnwantedSixthPower.cs Scripts/Powers/EnemyPowers/CardIntentSorrowfulRainPower.cs Scripts/Powers/EnemyPowers/CardIntentAdayumePower.cs Scripts/Enemy/CardIntents/EnemyAbilityHookDispatcher.cs Scripts/Enemy/CardIntents/EnemyCardExecutionEngine.cs Scripts/Enemy/CardIntents/EnemyCardSimulationContext.cs tests/CardIntentHarness/ExecutionEngineTests.testcs tests/CardIntentHarness/LiveProjectionTests.testcs
git commit -m "feat: add shadow tomorin enemy card effects"
```

### Task 10: Register the exact three phase pools, tokens, collections and rules

**Files:**
- Create: `Scripts/Enemy/CardIntents/ShadowTomorin/ShadowTomorinBalance.cs`
- Create: `Scripts/Enemy/CardIntents/ShadowTomorin/ShadowTomorinCardCatalog.cs`
- Create: `Scripts/Enemy/CardIntents/ShadowTomorin/ShadowTomorinCollectionCatalog.cs`
- Create: `Scripts/Enemy/CardIntents/TomorinEnemyCollectionCatalogFactory.cs`
- Create: `Scripts/Enemy/CardIntents/ShadowTomorin/ShadowTomorinDeck.cs`
- Create: `Scripts/Enemy/CardIntents/ShadowTomorin/ShadowTomorinRules.cs`
- Modify: `Scripts/Enemy/CardIntents/Test/CardIntentTestCollectionCatalog.cs`
- Create: `tests/CardIntentHarness/ShadowTomorinCatalogTests.testcs`
- Modify: `tests/CardIntentHarness/ModelDbBootstrap.testcs`
- Modify: `tests/CardIntentModelDbHarness/CardIntentCatalogModelDbTests.testcs`

**Interfaces:**
- Consumes: generic content directory, programs, effect nodes, hooks and scores from Tasks 1–9.
- Produces: `ShadowTomorinBalance`, `ShadowTomorinCardCatalog.AllDefinitions : IReadOnlyDictionary<EnemyCardId, EnemyCardDefinition>`, `ShadowTomorinCardCatalog.CarryDefinitions : IReadOnlyList<EnemyCardDefinition>`, `ShadowTomorinCollectionCatalog.Catalog`, `ShadowTomorinDeck.EnsureRegistered`, per-phase rules.

- [ ] **Step 1: Write failing catalog invariants and ModelDb reuse tests**

```csharp
[Fact]
public void PhaseTemplatesMatchBalanceAndCarryContract()
{
    ShadowTomorinDeck.EnsureRegistered();
    EnemyCardContentDirectory directory = EnemyCardDeckRegistry.GetContentDirectory(
        ShadowTomorinDeck.DeckId);

    Assert.Equal(ShadowTomorinBalance.Phase1TemplateCount,
        directory.GetPhase(EnemyCardPhase.Phase1).SourceFactories.Count);
    Assert.DoesNotContain(directory.GetPhase(EnemyCardPhase.Phase1).SourceFactories.Select(x => x()),
        card => card.Tags.HasFlag(EnemyCardTag.Compose));
    Assert.All(ShadowTomorinCardCatalog.CarryDefinitions,
        definition => Assert.True(definition.CarryAcrossPhase));
    Assert.DoesNotContain(ShadowTomorinCardCatalog.AllDefinitions,
        definition => definition.CardId.Value.Contains("UTAKOTOBA", StringComparison.OrdinalIgnoreCase));
}

[Fact]
public void EveryDefinitionUsesTheRegisteredModelDbCard()
{
    IReadOnlyDictionary<Type, CardModel> registered = ShadowModelDbFixture.RegisteredCards;
    foreach (EnemyCardDefinition definition in ShadowTomorinCardCatalog.AllDefinitions.Values)
        Assert.Same(registered[definition.CardModel.GetType()], definition.CardModel);
}
```

- [ ] **Step 2: Run catalog and ModelDb harnesses and verify the formal directory is absent**

Run: `dotnet test tests\CardIntentHarness\CardIntentHarness.csproj --nologo --verbosity minimal --filter "FullyQualifiedName~ShadowTomorinCatalogTests"`

Run: `dotnet test tests\CardIntentModelDbHarness\CardIntentModelDbHarness.csproj --nologo --verbosity minimal`

Expected: FAIL because Shadow catalog and DeckId are not registered.

- [ ] **Step 3: Add the single source of balance values**

```csharp
public static class ShadowTomorinBalance
{
    public const int MaxHp = 1200;
    public const decimal Phase1DamageAllowance = 216m;
    public const decimal Phase2DamageAllowance = 432m;
    public const int MaxCandidateAttempts = 3;
    public const int MaxEffectiveCards = 6;
    public const int XMultiplierDefinitionThreshold = 5;
    public const int Phase1TemplateCount = 12;
    public const int Phase2TemplateCount = 11;
    public const int Phase3TemplateCount = 9;

    public static EnemySoftLockLimits StaticLocks(EnemyCardPhase phase) => phase switch
    {
        EnemyCardPhase.Phase1 => new(38m, 72m),
        EnemyCardPhase.Phase2 => new(58m, 108m),
        EnemyCardPhase.Phase3 => new(77m, 152m),
        _ => throw new ArgumentOutOfRangeException(nameof(phase))
    };

    public static EnemySoftLockLimits FullLocks(EnemyCardPhase phase) => phase switch
    {
        EnemyCardPhase.Phase1 => new(48m, 90m),
        EnemyCardPhase.Phase2 => new(72m, 135m),
        EnemyCardPhase.Phase3 => new(96m, 190m),
        _ => throw new ArgumentOutOfRangeException(nameof(phase))
    };
}
```

- [ ] **Step 4: Define all sources and tokens exactly once**

Use `ModelDb.Card<T>()` for display models and these exact factory multiplicities/effects:

| Phase | Definition | Count | Enemy effect | Lifecycle | Carry |
|---|---|---:|---|---|---|
| P1 | SorrowfulRain | 1 | ability: Compose → 3 HeartWall | Exhaust | no |
| P1 | Adayume | 1 | ability: each successful unit → 1 block + 1 HeartWall | Exhaust | no |
| P1 | HeartBeat | 1 | ability: normal lifecycle Exhaust → 2 block | Exhaust | no |
| P1 | DuckAndCover | 1 | ability: before preparation block = HeartWall | Exhaust | no |
| P1 | NameOfTear | 1 | non-stack ability: HeartWall reaction ×1.5 | Exhaust | no |
| P1 | BuildAtField | 2 | +2 HeartWall | Discard | no |
| P1 | DefendTomorin | 2 | +5 block | Discard | no |
| P1 | StrikeTomorin | 2 | all players 6 | Discard | no |
| P1 | TomorinPunch | 1 | all players 8, +8 block, +2 HeartWall | Discard | no |
| P2 | AtField | 2 | consume 1 Status collection, +13 block, +5 HeartWall | Discard | no |
| P2 | CannotBeingHuman | 1 | +1 Dexterity, +4 HeartWall | Discard | no |
| P2 | Woodlouse | 1 | +8 block, generate BrokenNote | Discard | no |
| P2 | UnwantedSixth | 1 | action ability, generate CrumpledPaper | Exhaust | no |
| P2 | PoetryOrLyrics | 1 | consume up to 3 Available; each +1 Dexterity +1 HeartWall | Exhaust | no |
| P2 | ThisNoNeed | 1 | consume 1 non-Compose source, all players 5, +5 block | Discard | no |
| P2 | HopeOnTheVoice | 1 | all players +1 Weak +1 Vulnerable, generate MidnightCoffee | Exhaust | no |
| P2 | Hitoshizuku | 1 | consume 1 Attack; immediate Token then all players 6 | Exhaust | no |
| P2 | WantBeYourGod | 1 | consume 1 Skill; +5 HeartWall; retained Token | Exhaust | no |
| P2 | TomorinPunch | 1 | all players 8, +8 block, +2 HeartWall | Discard | no |
| P3 | NamelessPaper | 2 | consume 1 Attack; all players 9 +1 Vulnerable; retained Song | Exhaust | no |
| P3 | Mayoiuta | 1 | consume 1 Attack; immediate Token then all players 6 +2 Vulnerable | Exhaust | no |
| P3 | Hitoshizuku | 1 | consume 1 Attack; immediate Token then all players 6 | Exhaust | no |
| P3 | Senzaihyoumei | 1 | consume 1 Status collection; retained X Token | Exhaust | no |
| P3 | SingFullPower | 1 | all players `9 + 3*HeartWall`, do not consume HeartWall | Discard | no |
| P3 | WhyPlayHaruhikage | 1 | all players 16, generate 2 frozen random collections | Discard | no |
| P3 | TomorinPunch | 1 | all players 8, +8 block, +2 HeartWall | Discard | no |
| P3 | WantToBeingHuman | 1 | require 4 HeartWall; remove 4, +1 Strength | Discard; failure Retain | no |
| Token | HitoshizukuToken | 1 family | all players 9×2, Immediate | Discard | yes |
| Token | WantBeYourGodToken | 1 family | +9 block +1 HeartWall, Retained next turn | Exhaust | yes |
| Token | MayoiutaToken | 1 family | all players 5×5, Immediate | Discard | yes |
| Token | SenzaihyoumeiToken | 1 family | all players 8×FrozenX, Retained next turn | Discard | yes |
| Token | SongOfBeHuman | 1 family | consume 2 Skill; +5 Dexterity +20 block; retained Haruhikage | failure Retain; success Exhaust | yes |
| Token | Haruhikage | 1 family | consume 2 Status collections; +20 HeartWall; retained Pride | failure Retain; success Exhaust | yes |
| Token | PrideManSaki | 1 family | all players 5×10 | Exhaust | yes |

Repeated generation of a Carry family finds the existing instance across all five zones and increments its `ReplayCount`; it never creates a second instance, including when the instance is already in Exhaust.

Register collection weights exactly as BrokenNote/CrumpledPaper/MidnightCoffee/ColdRedTea/LeftoverBuffet/StarStone = 25/20/15/15/15/10. Move the six production definitions and effect-program builders into `TomorinEnemyCollectionCatalogFactory.Create()`; both `CardIntentTestCollectionCatalog` and `ShadowTomorinCollectionCatalog` call that production factory. No file under `ShadowTomorin` may import `STS2_Tomorin_Mod.Enemy.CardIntents.Test`. All display models come from `ModelDb.Card<T>()`. StarStone is the only wildcard Compose material.

- [ ] **Step 5: Add exact phase rules and constraints**

Create weighted recipes:

```text
P1 Gain 55: Ability + (Ability or Gain) + Defense
P1 Fortify 25: Defense + Gain + any non-Attack
P1 Pressure 20: Attack + (Attack or Gain) + Defense
P2 Fortify 40: Defense + Gain + CollectionGenerator/explicit consumer IDs
P2 Compose 35: one Compose + matching frozen material + Defense/Gain
P2 Pressure 25: Attack + explicit Attack/Control IDs + Defense/Gain
P3 Burst 45: Attack + Attack + any + any
P3 Compose 40: Compose + matching material + Attack + Gain/Defense
P3 Growth 15: explicit SingFullPower/WantToBeingHuman IDs + Attack + any
```

P1 max Compose = 0; P2 max Compose = 1; P3 max Compose = 2, with at most one Immediate-attack Compose source and, when two Compose sources are selected, the other producing a delayed Token. Reset `LastMetric` on migration and exclude it from the next weighted roll.

- [ ] **Step 6: Run catalog, planner, ModelDb and timeline tests**

Run: `dotnet test tests\CardIntentHarness\CardIntentHarness.csproj --nologo --verbosity minimal --filter "FullyQualifiedName~ShadowTomorinCatalogTests|FullyQualifiedName~ActionPlannerTests|FullyQualifiedName~IntentTimelineTests"`

Run: `dotnet test tests\CardIntentModelDbHarness\CardIntentModelDbHarness.csproj --nologo --verbosity minimal`

Expected: PASS; all source, token, consumed-material and collection CardModels are exact `ModelDb` instances and all asset paths preload.

- [ ] **Step 7: Commit the formal Shadow content directory**

```powershell
git add Scripts/Enemy/CardIntents/ShadowTomorin/ShadowTomorinBalance.cs Scripts/Enemy/CardIntents/ShadowTomorin/ShadowTomorinCardCatalog.cs Scripts/Enemy/CardIntents/ShadowTomorin/ShadowTomorinCollectionCatalog.cs Scripts/Enemy/CardIntents/TomorinEnemyCollectionCatalogFactory.cs Scripts/Enemy/CardIntents/ShadowTomorin/ShadowTomorinDeck.cs Scripts/Enemy/CardIntents/ShadowTomorin/ShadowTomorinRules.cs Scripts/Enemy/CardIntents/Test/CardIntentTestCollectionCatalog.cs tests/CardIntentHarness/ShadowTomorinCatalogTests.testcs tests/CardIntentHarness/ModelDbBootstrap.testcs tests/CardIntentModelDbHarness/CardIntentCatalogModelDbTests.testcs
git commit -m "feat: register shadow tomorin card pools"
```

### Task 11: Implement the single-state boss and safe phase damage gates

**Files:**
- Modify: `Scripts/Powers/EnemyPowers/EnemyMaxDamageReceivedPower.cs`
- Create: `Scripts/Powers/EnemyPowers/ShadowTomorinDamageGatePower.cs`
- Modify: `Scripts/Enemy/CardIntents/CardIntentMoveState.cs`
- Modify: `Scripts/Enemy/CardIntents/CardIntentMoveRuntime.cs`
- Modify: `Scripts/Enemy/CardIntents/BaseCardIntentMonsterModel.cs`
- Create: `Scripts/Enemy/ShadowTomorin.cs`
- Create: `tests/CardIntentHarness/ShadowTomorinBossStateTests.testcs`

**Interfaces:**
- Consumes: `ShadowTomorinDeck`, phase transition candidate, preparation provider and runtime Idle hook.
- Produces: `BaseCardIntentMonsterModel.AfterCardIntentActionSettledAsync`, `ShadowTomorin.RequestNextPhase`, `TransitionPendingPhaseAtIdleAsync`.

- [ ] **Step 1: Write failing cap and old-action continuity tests**

```csharp
[Fact]
public async Task DamageOverflowStopsAtCurrentAllowanceAndOnlySetsPendingPhase()
{
    ShadowBossFixture fixture = await ShadowBossFixture.StartAsync();
    PreparedEnemyCardAction oldAction = fixture.State.PreparedAction!;

    await fixture.DealDamageAsync(ShadowTomorinBalance.Phase1DamageAllowance + 72m);

    Assert.Equal(EnemyCardPhase.Phase1, fixture.State.ActivePhase);
    Assert.Equal(EnemyCardPhase.Phase2, fixture.State.PendingPhase);
    Assert.Same(oldAction, fixture.State.PreparedAction);
    Assert.Equal(fixture.StartHp - ShadowTomorinBalance.Phase1DamageAllowance, fixture.CurrentHp);
}

[Fact]
public async Task OldPreparedActionCompletesBeforeIdleMigration()
{
    ShadowBossFixture fixture = await ShadowBossFixture.AtPhase1ThresholdWithPreparedAction();
    await fixture.ExecuteEnemyActionAsync();

    Assert.Equal(fixture.OldActionExpectedEvents, fixture.ExecutionEvents);
    Assert.Equal(EnemyCardPhase.Phase2, fixture.State.ActivePhase);
    Assert.Equal(EnemyCardPhase.None, fixture.State.PendingPhase);
    Assert.Null(fixture.State.PreparedAction);
}
```

- [ ] **Step 2: Run boss-state tests and verify the existing Power removes too early**

Run: `dotnet test tests\CardIntentHarness\CardIntentHarness.csproj --nologo --verbosity minimal --filter "FullyQualifiedName~ShadowTomorinBossStateTests"`

Expected: FAIL because there is no Shadow boss and the current damage gate always removes at enemy-turn start.

- [ ] **Step 3: Add a safe Idle hook and configurable damage-gate lifetime**

Add to `BaseCardIntentMonsterModel`:

```csharp
protected internal virtual Task AfterCardIntentActionSettledAsync(
    CardIntentMoveState state,
    CancellationToken cancellationToken = default) => Task.CompletedTask;
```

`CardIntentMoveRuntime.ExecuteCardsAsync` must await engine completion, finish all source lifecycle changes, clear `PreparedAction`, set `RuntimePhase = Idle`, and only then await this hook. Faulted or interrupted executions do not migrate until their state reaches the documented safe condition.

Add to `EnemyMaxDamageReceivedPower`:

```csharp
public bool RemoveAtEnemyTurnStartWhenDepleted { get; set; } = true;
public decimal RemainingAllowance => Math.Max(0m, Amount - DamageReceivedThisPhase);
```

Keep the current default for Taki. Shadow sets it to `false`, so the depleted gate continues to return zero damage through the old enemy action and any reaction damage until Idle migration.

Use a Shadow-specific subclass to prevent `PowerCmd.Apply` from scaling an already resolved allowance a second time:

```csharp
public sealed class ShadowTomorinDamageGatePower : EnemyMaxDamageReceivedPower
{
    public override bool ShouldScaleInMultiplayer => false;
}
```

- [ ] **Step 4: Implement `ShadowTomorin` with one self-looping CardIntent state**

```csharp
public sealed class ShadowTomorin : BaseCardIntentMonsterModel
{
    public const string StateId = "SHADOW_TOMORIN_CARD_LOOP";
    public override int MinInitialHp => ShadowTomorinBalance.MaxHp;
    public override int MaxInitialHp => ShadowTomorinBalance.MaxHp;
    public override string? CustomVisualPath =>
        "res://STS2_Tomorin_Mod/scenes/creature_visuals/enemies/ShadowTomorin.tscn";

    protected override MonsterMoveStateMachine GenerateMoveStateMachine();
    public override Task AfterAddedToRoom();
    protected internal override Task AfterCardIntentActionSettledAsync(
        CardIntentMoveState state,
        CancellationToken cancellationToken = default);
}
```

At room entry: register the Deck, initialize P1, install a scaled P1 gate and set its callback to `RequestNextPhase(Phase2)`. The callback only sets `PendingPhase` and emits phase-change presentation. Resolve the single encounter scale from the creature's actual HP: `scale = Creature.MaxHp / ShadowTomorinBalance.MaxHp`; apply `baseAllowance * scale` to `ShadowTomorinDamageGatePower`, whose automatic Power scaling is disabled. At Idle, build a candidate state first. P1→P2: apply the scaled P2 gate while the depleted P1 gate still blocks damage, activate `ShadowTomoriFormPower`, remove the old gate, then apply the candidate state once. P2→P3: remove the depleted P2 gate and apply the P3 candidate without a new gate. No transition calls `SetMoveImmediate`, reinitializes combat, or changes the self-loop MoveState.

The actual creature maximum HP is the authoritative multiplayer scale source for both phase allowances. Round both allowances with the same helper and midpoint rule; do not query player count or a second scale table.

- [ ] **Step 5: Run boss, phase, cap and legacy Taki tests/build**

Run: `dotnet test tests\CardIntentHarness\CardIntentHarness.csproj --nologo --verbosity minimal --filter "FullyQualifiedName~ShadowTomorinBossStateTests|FullyQualifiedName~PhaseMigrationTests|FullyQualifiedName~ExecutionEngineTests"`

Run: `dotnet build --nologo`

Expected: PASS; Taki retains the default auto-removal behavior and Shadow retains a zeroed gate through the old action.

- [ ] **Step 6: Commit the Shadow boss state machine**

```powershell
git add Scripts/Powers/EnemyPowers/EnemyMaxDamageReceivedPower.cs Scripts/Powers/EnemyPowers/ShadowTomorinDamageGatePower.cs Scripts/Enemy/CardIntents/CardIntentMoveState.cs Scripts/Enemy/CardIntents/CardIntentMoveRuntime.cs Scripts/Enemy/CardIntents/BaseCardIntentMonsterModel.cs Scripts/Enemy/ShadowTomorin.cs tests/CardIntentHarness/ShadowTomorinBossStateTests.testcs
git commit -m "feat: implement shadow tomorin phase state machine"
```

### Task 12: Replace the Stage placeholder encounter and add localization/preload coverage

**Files:**
- Create: `Scripts/Encounters/ShadowTomorinBoss.cs`
- Modify: `Scripts/Acts/Stage.cs`
- Modify: `Scripts/Stage/StageRoomResolver.cs`
- Modify: `STS2_Tomorin_Mod/localization/eng/monsters.json`
- Modify: `STS2_Tomorin_Mod/localization/zhs/monsters.json`
- Modify: `STS2_Tomorin_Mod/localization/eng/encounters.json`
- Modify: `STS2_Tomorin_Mod/localization/zhs/encounters.json`
- Modify: `STS2_Tomorin_Mod/localization/eng/powers.json`
- Modify: `STS2_Tomorin_Mod/localization/zhs/powers.json`
- Modify: `tests/CardIntentModelDbHarness/CardIntentCatalogModelDbTests.testcs`
- Modify: `tests/CardIntent.Tests.ps1`
- Modify: `tests/Stage.Tests.ps1`

**Interfaces:**
- Consumes: `ShadowTomorin` and its registered content directory.
- Produces: `ShadowTomorinBoss` and Stage-only route replacement.

- [ ] **Step 1: Change structural tests first**

Update `tests/Stage.Tests.ps1` so the Boss node requires `ModelDb.Encounter<ShadowTomorinBoss>()`, `Stage.GenerateAllEncounters()` includes `ShadowTomorinBoss`, and the Stage source set no longer routes its Boss node to `CrychicPhatomBoss`. Do not assert that Crychic disappears from Glory boss patches or from unrelated content.

Update `tests/CardIntent.Tests.ps1` to require the formal Shadow directory, one persistent CardIntent state, no `SetMoveImmediate` in `ShadowTomorin.cs`, no `Utakotoba` in Shadow sources, and generic DeckId reconnect resolution.

- [ ] **Step 2: Run structural tests and verify they fail on the placeholder route**

Run: `powershell -ExecutionPolicy Bypass -File tests\Stage.Tests.ps1`

Run: `powershell -ExecutionPolicy Bypass -File tests\CardIntent.Tests.ps1`

Expected: FAIL because Stage still resolves Crychic and has no formal Shadow encounter/localization.

- [ ] **Step 3: Add the encounter and exact Stage-only route swap**

```csharp
public sealed class ShadowTomorinBoss : CustomEncounterModel
{
    public ShadowTomorinBoss() : base(RoomType.Boss, true) { }
    protected override bool HasCustomBackground => true;
    public override float GetCameraScaling() => 0.9f;
    public override string BossNodePath =>
        "res://STS2_Tomorin_Mod/images/enemy_headIcon/tomorin_boss_headIcon";
    public override string? CustomRunHistoryIconPath =>
        "res://STS2_Tomorin_Mod/images/enemy_headIcon/tomorin_boss_headIcon.png";
    public override string? CustomRunHistoryIconOutlinePath => CustomRunHistoryIconPath;
    public override MegaSkeletonDataResource? BossNodeSpineResource => null;
    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters() =>
        [(ModelDb.Monster<ShadowTomorin>().ToMutable(), null)];
    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
        [ModelDb.Monster<ShadowTomorin>().ToMutable()];
    public override bool IsValidForAct(ActModel act) => false;
}
```

Change only the Stage act and Stage room resolver from Crychic to Shadow. Keep `CrychicPhatom`, `CrychicPhatomBoss`, Glory boss selection patches and their assets intact because they remain independent content outside the Stage placeholder route.

- [ ] **Step 4: Add bilingual model keys and preload all timeline models**

Add these keys in both languages with localized values:

```text
STS2_TOMORIN_MOD-SHADOW_TOMORIN.name
STS2_TOMORIN_MOD-SHADOW_TOMORIN.moves.pendingPhaseTwo
STS2_TOMORIN_MOD-SHADOW_TOMORIN.moves.pendingPhaseThree
STS2_TOMORIN_MOD-SHADOW_TOMORIN_BOSS.title
STS2_TOMORIN_MOD-SHADOW_TOMORI_FORM_POWER.title
STS2_TOMORIN_MOD-SHADOW_TOMORI_FORM_POWER.description
STS2_TOMORIN_MOD-CARD_INTENT_HEART_BEAT_POWER.title
STS2_TOMORIN_MOD-CARD_INTENT_HEART_BEAT_POWER.description
STS2_TOMORIN_MOD-CARD_INTENT_UNWANTED_SIXTH_POWER.title
STS2_TOMORIN_MOD-CARD_INTENT_UNWANTED_SIXTH_POWER.description
```

Extend the real-ModelDb harness so asset preloading and `EnemyIntentTimeline` resolve source cards, Immediate cards, retained Tokens, consumed card models and collection cards from `ModelDb.Card<T>()` by reference identity. Explicitly assert Utakotoba models are absent from Shadow preload paths and timeline definitions.

- [ ] **Step 5: Run Stage, CardIntent and ModelDb tests**

Run: `powershell -ExecutionPolicy Bypass -File tests\Stage.Tests.ps1`

Run: `powershell -ExecutionPolicy Bypass -File tests\CardIntent.Tests.ps1`

Run: `dotnet test tests\CardIntentModelDbHarness\CardIntentModelDbHarness.csproj --nologo --verbosity minimal`

Expected: PASS; Stage routes to Shadow while unrelated Crychic registrations remain unchanged.

- [ ] **Step 6: Commit Stage encounter integration**

```powershell
git add Scripts/Encounters/ShadowTomorinBoss.cs Scripts/Acts/Stage.cs Scripts/Stage/StageRoomResolver.cs STS2_Tomorin_Mod/localization/eng/monsters.json STS2_Tomorin_Mod/localization/zhs/monsters.json STS2_Tomorin_Mod/localization/eng/encounters.json STS2_Tomorin_Mod/localization/zhs/encounters.json STS2_Tomorin_Mod/localization/eng/powers.json STS2_Tomorin_Mod/localization/zhs/powers.json tests/CardIntentModelDbHarness/CardIntentCatalogModelDbTests.testcs tests/CardIntent.Tests.ps1 tests/Stage.Tests.ps1
git commit -m "feat: replace stage boss with shadow tomorin"
```

### Task 13: Run complete regression and deterministic balance acceptance

**Files:**
- Modify only if a failing assertion reveals a defect in files owned by Tasks 1–12.
- Record no generated binaries, test output directories, `.pck` files or temporary Godot probe files.

**Interfaces:**
- Consumes: the complete implementation.
- Produces: verified implementation with clean targeted diff and captured acceptance evidence.

- [ ] **Step 1: Add deterministic seed-matrix acceptance without duplicating balance literals**

In `ShadowTomorinCatalogTests.testcs`, enumerate a stable seed set such as `Enumerable.Range(0, 256)`. For each phase, prepare multiple actions from a fresh state and assert:

```csharp
Assert.All(actions.Where(x => x.SoftLockDiagnostic.CommitMode == EnemyCandidateCommitMode.WithinLocks), action =>
{
    EnemySoftLockLimits limits = ShadowTomorinBalance.FullLocks(action.Phase);
    Assert.True(action.SoftLockDiagnostic.FullScore.AttackRisk <= limits.Attack);
    Assert.True(action.SoftLockDiagnostic.FullScore.TotalRisk <= limits.Total);
});
Assert.All(actions.Where(x => x.SoftLockDiagnostic.CommitMode == EnemyCandidateCommitMode.ForcedOverLock), action =>
    Assert.True(action.SoftLockDiagnostic.ProjectionIsComplete));
```

Also derive phase counts and weights from `ShadowTomorinBalance`/`ShadowTomorinRules`, verify nonzero occurrence of every eligible metric across the matrix, verify consecutive metrics differ, and verify no generated action violates Compose constraints. Do not require a particular card to appear in a particular single seed.

- [ ] **Step 2: Run both CardIntent harnesses**

Run: `dotnet test tests\CardIntentHarness\CardIntentHarness.csproj --nologo --verbosity minimal`

Run: `dotnet test tests\CardIntentModelDbHarness\CardIntentModelDbHarness.csproj --nologo --verbosity minimal`

Expected: both projects PASS with zero failed tests.

- [ ] **Step 3: Run repository structural suites**

Run: `powershell -ExecutionPolicy Bypass -File tests\CardIntent.Tests.ps1`

Run: `powershell -ExecutionPolicy Bypass -File tests\Stage.Tests.ps1`

Expected: both scripts finish successfully and report their final pass message.

- [ ] **Step 4: Build the mod**

Run: `dotnet build --nologo`

Expected: exit code 0. Do not run `dotnet publish` because the implementation reuses existing Godot resources and changes no `.tscn`, image, animation or material.

- [ ] **Step 5: Verify diff hygiene and forbidden-content invariants**

Run: `git diff --check`

Run: `rg -n "Utakotoba|UTAKOTOBA|诗超绊|SetMoveImmediate|InitializeFreshCardCombat" Scripts\Enemy\ShadowTomorin.cs Scripts\Enemy\CardIntents\ShadowTomorin tests\CardIntentHarness\ShadowTomorinCatalogTests.testcs`

Expected: `git diff --check` has no output. The search has no Shadow content hits for Utakotoba, no boss transition hits for `SetMoveImmediate`, and no phase transition hits for `InitializeFreshCardCombat`; test names or negative assertions may contain the forbidden card text.

- [ ] **Step 6: Inspect only the implementation diff and commit final test adjustments**

Run: `git status --short`

Run: `git diff -- Scripts/Enemy/CardIntents Scripts/Enemy/ShadowTomorin.cs Scripts/Powers/EnemyPowers Scripts/Encounters/ShadowTomorinBoss.cs Scripts/Acts/Stage.cs Scripts/Stage/StageRoomResolver.cs STS2_Tomorin_Mod/localization tests/CardIntentHarness tests/CardIntentModelDbHarness tests/CardIntent.Tests.ps1 tests/Stage.Tests.ps1`

If Task 13 changed test or implementation files, stage only those exact files and commit:

```powershell
git add tests/CardIntentHarness/ShadowTomorinCatalogTests.testcs
git commit -m "test: verify shadow tomorin balance invariants"
```

If no Task 13 file changed, do not create an empty commit.

---

## Execution Checkpoints

- After Task 4, the old development test monster must still plan, project, execute and reconnect using the generic framework.
- After Task 8, every newly introduced authoritative field must round-trip before formal Shadow content is registered.
- After Task 10, all Shadow definitions must resolve through `ModelDb` and all three phase templates must be constructible without the boss model.
- After Task 11, HP gates and phase continuity must be testable without Stage routing.
- After Task 12, Stage must use Shadow while Glory and unrelated Crychic content remain untouched.
- Task 13 is the only point where a complete-build success claim may be made.

## Manual Game Acceptance

After automated verification, run these in-game checks on a development save:

1. Enter the Stage Boss room in single player; verify 1200 HP and P1 card intent.
2. Deal one hit larger than the P1 allowance; verify HP stops at the P1 threshold and the already shown P1 action still resolves completely.
3. Verify the next prepared action uses P2 content and exactly one new collection appears in the enemy inventory.
4. Trigger P2 threshold while a Carry Token is in each testable zone over separate runs; verify identity and Replay remain unchanged after P3 migration.
5. Let the fight continue beyond the recommended kill window; verify growth comes from accumulated Strength, Dexterity, HeartWall, Replay and deck compression, with no fixed turn-number move.
6. Produce `SenzaihyoumeiToken` after five different exhausted definitions and one Replay; verify its body and Replay display and execute the same frozen hit count.
7. Save/reconnect with a prepared P3 action, a Carry Token in Exhaust and a frozen X action; verify no collection redraw, phase re-entry, reshuffle or X recalculation.
8. In multiplayer, compare HP and both phase allowances to the same encounter scale factor and verify attack-risk diagnostics report the maximum per-player damage rather than the sum.
