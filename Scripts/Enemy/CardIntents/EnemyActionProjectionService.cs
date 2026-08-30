using System.Globalization;
using System.Text;
using STS2_Tomorin_Mod.Enemy.CardIntents.Test;

namespace STS2_Tomorin_Mod.Enemy.CardIntents;

/// <summary>
/// 保存实时投影所需的纯数据目标、有限步骤规则和未知修改器诊断。
/// </summary>
public sealed class EnemyActionProjectionInput
{
    /// <summary>
    /// 创建实时投影输入。
    /// </summary>
    /// <param name="targets">全部有效玩家及当前已知修正。</param>
    /// <param name="stepLimit">与实际结算相同的有限步骤上限。</param>
    /// <param name="unknownModifierIds">不能安全调用的第三方修改器标识。</param>
    public EnemyActionProjectionInput(
        IEnumerable<EnemySimulationTarget> targets,
        int stepLimit,
        IEnumerable<string>? unknownModifierIds = null,
        EnemyProjectionInitialState? initialState = null,
        EnemyCardContentDirectory? contentDirectory = null,
        EnemyActionRiskContext? riskContext = null)
    {
        Targets = Array.AsReadOnly((targets ?? throw new ArgumentNullException(nameof(targets))).ToArray());
        if (stepLimit < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(stepLimit));
        }

        StepLimit = stepLimit;
        UnknownModifierIds = Array.AsReadOnly((unknownModifierIds ?? []).ToArray());
        HasInitialState = initialState is not null;
        InitialState = initialState ?? new EnemyProjectionInitialState();
        ContentDirectory = contentDirectory ?? riskContext?.ContentDirectory;
        RiskContext = riskContext;
    }

    /// <summary>获取逐目标已知修正。</summary>
    public IReadOnlyList<EnemySimulationTarget> Targets { get; }

    /// <summary>获取有限步骤上限。</summary>
    public int StepLimit { get; }

    /// <summary>获取不能安全执行的未知第三方修改器。</summary>
    public IReadOnlyList<string> UnknownModifierIds { get; }

    /// <summary>获取行动开始前的总存量纯数据快照。</summary>
    public EnemyProjectionInitialState InitialState { get; }

    /// <summary>获取调用方是否显式提供了需要严格推进的初始结构快照。</summary>
    public bool HasInitialState { get; }

    /// <summary>获取用于解析生成链和收藏品的可选完整内容目录。</summary>
    public EnemyCardContentDirectory? ContentDirectory { get; }

    /// <summary>获取可选的完整行动评分上下文。</summary>
    public EnemyActionRiskContext? RiskContext { get; }
}

/// <summary>
/// 对冻结 DFS 行动执行无 RNG、无战斗命令的顺序模拟，并按完整输入指纹复用结果。
/// </summary>
public sealed class EnemyActionProjectionService
{
    private string? _cachedFingerprint;
    private LiveActionProjection? _cachedProjection;

    /// <summary>
    /// 根据冻结计划和当前纯数据修正重新计算或复用逐执行牌投影。
    /// </summary>
    /// <param name="action">不会被投影过程修改的冻结行动。</param>
    /// <param name="input">当前实时修正与诊断输入。</param>
    /// <returns>完整或显式标为不完整的实时投影。</returns>
    public LiveActionProjection Project(
        PreparedEnemyCardAction action,
        EnemyActionProjectionInput input)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(input);
        string fingerprint = BuildFingerprint(action, input);
        if (string.Equals(fingerprint, _cachedFingerprint, StringComparison.Ordinal) && _cachedProjection is not null)
        {
            return _cachedProjection;
        }

        EnemyCardSimulationContext simulation = new(
            input.Targets,
            input.StepLimit,
            action.EffectiveCardStates,
            input.InitialState,
            input.ContentDirectory,
            input.HasInitialState);
        foreach (string unknown in input.UnknownModifierIds)
        {
            simulation.MarkIncomplete($"未知第三方数值修改器未执行：{unknown}");
        }

        foreach (PreparedEnemyCardSource source in action.Sources)
        {
            if (simulation.IsStepLimitReached)
            {
                break;
            }

            if (source.Units.Count == 0 && source.TruncationAttemptIndex is null)
            {
                simulation.MarkIncomplete($"来源牌 {source.SourceKey} 缺少冻结执行单元。");
                continue;
            }

            foreach (PreparedEnemyCardUnitPlan unit in source.Units)
            {
                if (simulation.IsStepLimitReached)
                {
                    break;
                }

                ProjectUnit(action, unit, source.SourceKey, simulation, input.ContentDirectory);
            }

            simulation.ApplyProjectedLifecycle(
                source.SourceKey,
                source.SourceCard.Definition,
                successful: source.Units.Count > 0);
        }

        LiveActionProjection projection = simulation.BuildProjection();
        if (input.RiskContext is not null)
        {
            projection = projection.WithRiskScore(
                new EnemyActionRiskCalculator().Calculate(projection, input.RiskContext));
        }

        _cachedFingerprint = fingerprint;
        _cachedProjection = projection;
        return _cachedProjection;
    }

    /// <summary>
    /// 主动使事件驱动缓存失效；下次读取仍会以完整输入指纹兜底。
    /// </summary>
    public void Invalidate()
    {
        _cachedFingerprint = null;
        _cachedProjection = null;
    }

    /// <summary>
    /// 投影一个卡牌执行单元，并保持所有递归子单元共享公开根来源。
    /// </summary>
    /// <param name="action">提供公开来源定义和测试目录解析上下文的冻结行动。</param>
    /// <param name="unit">当前实际执行牌的冻结单元。</param>
    /// <param name="expectedRootSourceKey">父计划要求继承的公开根来源。</param>
    /// <param name="simulation">纯内存模拟上下文。</param>
    private static void ProjectUnit(
        PreparedEnemyCardAction action,
        PreparedEnemyCardUnitPlan unit,
        EnemyCardInstanceKey expectedRootSourceKey,
        EnemyCardSimulationContext simulation,
        EnemyCardContentDirectory? contentDirectory)
    {
        simulation.BeginUnit(
            unit.RootSourceKey,
            unit.ExecutingCardKey,
            unit.ExecutingCardId,
            unit.ReplayIndex);
        try
        {
            if (unit.RootSourceKey != expectedRootSourceKey)
            {
                simulation.MarkIncomplete(
                    $"执行牌 {unit.ExecutingCardKey} 的根来源 {unit.RootSourceKey} 与父计划 {expectedRootSourceKey} 不一致。");
            }

            EnemyCardDefinition definition = ResolveCardDefinition(action, unit, contentDirectory);
            unit.ValidateFrozenDefinition(definition);
            bool requiresFrozenX = definition.Effects.Any(effect => effect is EnemyFrozenXAttackAllEffect);
            try
            {
                unit.ValidateFrozenEffectiveState(action, requiresFrozenX);
            }
            catch (InvalidOperationException exception)
            {
                simulation.MarkIncomplete(exception.Message);
            }

            if (!definition.PlayCondition.CanSimulate(simulation))
            {
                simulation.MarkIncomplete(
                    $"执行牌 {unit.ExecutingCardKey} 的冻结出牌条件在投影时不再成立。");
            }
            else
            {
                ProjectSteps(
                    action,
                    unit,
                    unit.OrderedSteps,
                    definition.Effects,
                    simulation,
                    contentDirectory);
            }
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            simulation.MarkIncomplete(
                $"执行牌 {unit.ExecutingCardKey} 的冻结投影失败：{exception.Message}");
        }

        simulation.CommitUnit();
    }

    /// <summary>
    /// 按冻结顺序投影直接效果、素材、收藏品、生成结果和即时子牌。
    /// </summary>
    /// <param name="action">冻结行动及其公开来源定义。</param>
    /// <param name="ownerUnit">当前承载结构变化的执行单元。</param>
    /// <param name="steps">待遍历的有序冻结步骤。</param>
    /// <param name="directEffects">本层直接效果步骤应解析到的共享效果节点。</param>
    /// <param name="simulation">纯内存模拟上下文。</param>
    private static void ProjectSteps(
        PreparedEnemyCardAction action,
        PreparedEnemyCardUnitPlan ownerUnit,
        IReadOnlyList<PreparedEnemyResolutionStep> steps,
        IReadOnlyList<IEnemyCardEffectNode> directEffects,
        EnemyCardSimulationContext simulation,
        EnemyCardContentDirectory? contentDirectory)
    {
        foreach (PreparedEnemyResolutionStep step in steps)
        {
            if (simulation.IsStepLimitReached)
            {
                return;
            }

            switch (step)
            {
                case PreparedDirectEffectsStep direct:
                    ProjectDirectEffects(direct, directEffects, ownerUnit, simulation);
                    break;
                case PreparedConsumedCardStep consumedCard:
                    simulation.MoveProjectedCard(consumedCard.MaterialKey, EnemyCardZone.Exhaust);
                    if (consumedCard.ControlledChild is not null)
                    {
                        ProjectUnit(action, consumedCard.ControlledChild, ownerUnit.RootSourceKey, simulation, contentDirectory);
                    }

                    break;
                case PreparedConsumedCollectionStep consumedCollection:
                    ProjectConsumedCollection(action, ownerUnit, consumedCollection, simulation, contentDirectory);
                    break;
                case PreparedGeneratedCollectionStep generatedCollection:
                    simulation.AddCollectionDelta(new EnemyCollectionProjection(
                        $"{generatedCollection.CollectionId}@{generatedCollection.ExpectedSequence}",
                        generatedCollection.CollectionId,
                        EnemyCollectionProjectionKind.Generated));
                    break;
                case PreparedComposeResultStep compose:
                    simulation.AddGeneratedCard(new EnemyGeneratedCardProjection(
                        compose.ResultInstanceKey,
                        compose.ResultCardId,
                        compose.Timing,
                        compose.IncreasesExistingReplay));
                    if (compose.ImmediateChild is not null)
                    {
                        ProjectUnit(action, compose.ImmediateChild, ownerUnit.RootSourceKey, simulation, contentDirectory);
                        simulation.ApplyProjectedLifecycle(
                            compose.ResultInstanceKey,
                            ResolveCardDefinition(action, compose.ImmediateChild, contentDirectory),
                            successful: true);
                    }

                    ProjectAdditionalReplayUnits(
                        action,
                        compose.AdditionalReplayUnits,
                        ownerUnit.RootSourceKey,
                        simulation,
                        contentDirectory);

                    break;
                case PreparedImmediateCardStep immediate:
                    simulation.MoveProjectedCard(immediate.SelectedCardKey, EnemyCardZone.Current);
                    ProjectUnit(action, immediate.Child, ownerUnit.RootSourceKey, simulation, contentDirectory);
                    ProjectAdditionalReplayUnits(
                        action,
                        immediate.AdditionalReplayUnits,
                        ownerUnit.RootSourceKey,
                        simulation,
                        contentDirectory);
                    simulation.ApplyProjectedLifecycle(
                        immediate.SelectedCardKey,
                        ResolveCardDefinition(action, immediate.Child, contentDirectory),
                        successful: true);
                    break;
                case PreparedRecoveryStep recovery:
                    ProjectRecovery(action, ownerUnit, recovery, simulation, contentDirectory);
                    break;
                default:
                    simulation.MarkIncomplete(
                        $"执行牌 {ownerUnit.ExecutingCardKey} 包含未知冻结步骤 {step.GetType().FullName}。");
                    break;
            }
        }
    }

    /// <summary>
    /// 验证冻结程序标识与共享效果节点严格一致后执行纯模拟。
    /// </summary>
    /// <param name="step">冻结直接效果步骤。</param>
    /// <param name="effects">当前卡牌或收藏品定义提供的共享效果节点。</param>
    /// <param name="unit">用于错误诊断的执行单元。</param>
    /// <param name="simulation">纯内存模拟上下文。</param>
    private static void ProjectDirectEffects(
        PreparedDirectEffectsStep step,
        IReadOnlyList<IEnemyCardEffectNode> effects,
        PreparedEnemyCardUnitPlan unit,
        EnemyCardSimulationContext simulation)
    {
        string[] resolvedIds = effects.Select(effect => effect.ProgramId).ToArray();
        if (!step.EffectProgramIds.SequenceEqual(resolvedIds, StringComparer.Ordinal))
        {
            simulation.MarkIncomplete(
                $"执行牌 {unit.ExecutingCardKey} 的直接效果程序与当前定义不一致。");
            return;
        }

        foreach (IEnemyCardEffectNode effect in effects)
        {
            if (simulation.IsStepLimitReached)
            {
                return;
            }

            effect.Simulate(simulation);
        }
    }

    /// <summary>
    /// 投影收藏品消费及其共享直接效果、即时牌或回收子步骤。
    /// </summary>
    /// <param name="action">冻结行动。</param>
    /// <param name="ownerUnit">消费收藏品的当前执行单元。</param>
    /// <param name="step">冻结收藏品消费步骤。</param>
    /// <param name="simulation">纯内存模拟上下文。</param>
    private static void ProjectConsumedCollection(
        PreparedEnemyCardAction action,
        PreparedEnemyCardUnitPlan ownerUnit,
        PreparedConsumedCollectionStep step,
        EnemyCardSimulationContext simulation,
        EnemyCardContentDirectory? contentDirectory)
    {
        EnemyCollectionDefinition definition = simulation.GetProjectedCollectionDefinition(
            step.CollectionInstanceId,
            step.CollectionId);
        EnemyCollectionEffectProgram program = EnemyCollectionEffectResolver.GetRequired(definition);
        simulation.AddCollectionDelta(new EnemyCollectionProjection(
            step.CollectionInstanceId,
            step.CollectionId,
            EnemyCollectionProjectionKind.Consumed));
        ProjectSteps(action, ownerUnit, step.Children, program.DirectEffects, simulation, contentDirectory);
    }

    /// <summary>
    /// 投影准备阶段冻结的卡牌或收藏品回收结果。
    /// </summary>
    /// <param name="action">冻结行动。</param>
    /// <param name="ownerUnit">触发回收的当前执行单元。</param>
    /// <param name="step">冻结回收步骤。</param>
    /// <param name="simulation">纯内存模拟上下文。</param>
    private static void ProjectRecovery(
        PreparedEnemyCardAction action,
        PreparedEnemyCardUnitPlan ownerUnit,
        PreparedRecoveryStep step,
        EnemyCardSimulationContext simulation,
        EnemyCardContentDirectory? contentDirectory)
    {
        if (step.Kind == EnemyPreparedRecoveryKind.Card)
        {
            if (step.ImmediateCardChild is not null)
            {
                EnemyCardInstanceKey recoveredKey = step.ImmediateCardChild.ExecutingCardKey;
                simulation.MoveProjectedCard(recoveredKey, EnemyCardZone.Current);
                ProjectUnit(action, step.ImmediateCardChild, ownerUnit.RootSourceKey, simulation, contentDirectory);
                simulation.ApplyProjectedLifecycle(
                    recoveredKey,
                    ResolveCardDefinition(action, step.ImmediateCardChild, contentDirectory),
                    successful: true);
            }

            ProjectAdditionalReplayUnits(
                action,
                step.AdditionalReplayUnits,
                ownerUnit.RootSourceKey,
                simulation,
                contentDirectory);

            return;
        }

        int separator = step.SelectedInstanceId.LastIndexOf('@');
        if (separator <= 0)
        {
            simulation.MarkIncomplete($"回收收藏品实例标识非法：{step.SelectedInstanceId}");
            return;
        }

        simulation.AddCollectionDelta(new EnemyCollectionProjection(
            step.SelectedInstanceId,
            step.SelectedInstanceId[..separator],
            EnemyCollectionProjectionKind.Recovered));
    }

    /// <summary>
    /// 按冻结重放索引顺序投影即时牌首单元之后的全部附加单元。
    /// </summary>
    /// <param name="action">冻结行动。</param>
    /// <param name="units">已经过连续索引校验的附加重放单元。</param>
    /// <param name="expectedRootSourceKey">父计划要求继承的公开根来源。</param>
    /// <param name="simulation">纯内存模拟上下文。</param>
    private static void ProjectAdditionalReplayUnits(
        PreparedEnemyCardAction action,
        IReadOnlyList<PreparedEnemyCardUnitPlan> units,
        EnemyCardInstanceKey expectedRootSourceKey,
        EnemyCardSimulationContext simulation,
        EnemyCardContentDirectory? contentDirectory)
    {
        foreach (PreparedEnemyCardUnitPlan unit in units)
        {
            if (simulation.IsStepLimitReached)
            {
                return;
            }

            ProjectUnit(action, unit, expectedRootSourceKey, simulation, contentDirectory);
        }
    }

    /// <summary>
    /// 从公开来源实例或显式测试目录解析执行牌定义，禁止按对象地址猜测。
    /// </summary>
    /// <param name="action">包含全部公开来源实例的冻结行动。</param>
    /// <param name="unit">待解析的实际执行单元。</param>
    /// <returns>与执行单元 CardId 完全一致的不可变定义。</returns>
    private static EnemyCardDefinition ResolveCardDefinition(
        PreparedEnemyCardAction action,
        PreparedEnemyCardUnitPlan unit,
        EnemyCardContentDirectory? contentDirectory)
    {
        BaseEnemyCard? publicCard = action.Sources
            .Select(source => source.SourceCard)
            .FirstOrDefault(card => card.InstanceKey == unit.ExecutingCardKey);
        if (publicCard is not null)
        {
            if (publicCard.CardId != unit.ExecutingCardId)
            {
                throw new InvalidOperationException(
                    $"实例 {unit.ExecutingCardKey} 的冻结 CardId 与公开定义不一致。");
            }

            return publicCard.Definition;
        }

        BaseEnemyCard resolved = contentDirectory?.CreateDefinition(unit.ExecutingCardId) ??
                                 CardIntentTestCardCatalog.CreateCard(unit.ExecutingCardId);
        if (resolved.CardId != unit.ExecutingCardId)
        {
            throw new InvalidOperationException($"目录未能稳定解析执行牌 {unit.ExecutingCardId}。");
        }

        return resolved.Definition;
    }

    /// <summary>
    /// 从冻结递归结构和全部已知实时输入创建不依赖对象地址的缓存指纹。
    /// </summary>
    /// <param name="action">冻结行动。</param>
    /// <param name="input">当前纯数据投影输入。</param>
    /// <returns>覆盖全部递归字段、顺序和输入修正的稳定文本。</returns>
    private static string BuildFingerprint(
        PreparedEnemyCardAction action,
        EnemyActionProjectionInput input)
    {
        StringBuilder builder = new();
        builder.Append(action.Metric).Append('|').Append(input.StepLimit);
        foreach (PreparedEnemyCardSource source in action.Sources)
        {
            builder.Append("|S:")
                .Append(source.SourceKey.Value)
                .Append(':')
                .Append(source.MaximumAttempts)
                .Append(':')
                .Append(source.TruncationAttemptIndex?.ToString(CultureInfo.InvariantCulture) ?? "-");
            foreach (PreparedEnemyCardUnitPlan unit in source.Units)
            {
                AppendUnitFingerprint(builder, unit);
            }
        }

        foreach (EnemyFrozenEffectiveCardState state in action.EffectiveCardStates.Values
                     .OrderBy(state => state.ExecutingCardInstanceKey.Value, StringComparer.Ordinal))
        {
            builder.Append("|X:")
                .Append(state.ExecutingCardInstanceKey.Value).Append(':')
                .Append(state.FrozenN).Append(':')
                .Append(state.FrozenX?.ToString(CultureInfo.InvariantCulture) ?? "-").Append(':')
                .Append(state.Multiplier).Append(':')
                .Append(state.WasCounted);
        }

        foreach (EnemySimulationTarget target in input.Targets)
        {
            builder.Append("|T:")
                .Append(target.TargetId)
                .Append(':')
                .Append(target.DamageMultiplier.ToString(CultureInfo.InvariantCulture))
                .Append(':')
                .Append(target.DebuffMultiplier.ToString(CultureInfo.InvariantCulture));
        }

        builder.Append("|IB:").Append(input.HasInitialState).Append(':')
            .Append(input.InitialState.ActivePhase).Append(':')
            .Append(input.InitialState.EnemyBlock.ToString(CultureInfo.InvariantCulture));
        foreach ((string powerId, decimal amount) in input.InitialState.EnemyPowers.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            builder.Append("|IP:").Append(powerId).Append(':')
                .Append(amount.ToString(CultureInfo.InvariantCulture));
        }

        foreach ((string targetId, IReadOnlyDictionary<string, decimal> powers) in
                 input.InitialState.TargetPowers.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            foreach ((string powerId, decimal amount) in powers.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                builder.Append("|ITP:").Append(targetId).Append(':').Append(powerId).Append(':')
                    .Append(amount.ToString(CultureInfo.InvariantCulture));
            }
        }

        foreach (EnemyProjectedCardZoneState card in input.InitialState.Cards
                     .OrderBy(card => card.InstanceKey.Value, StringComparer.Ordinal))
        {
            builder.Append("|IZ:").Append(card.InstanceKey.Value).Append(':')
                .Append(card.CardId.Value).Append(':').Append(card.Zone).Append(':')
                .Append(card.SourcePhase).Append(':').Append(card.CarryAcrossPhase).Append(':')
                .Append(card.ReplayCount);
        }

        foreach (EnemyCollectionInstance item in input.InitialState.AvailableCollections)
        {
            builder.Append("|ICA:").Append(item.CollectionInstanceId);
        }

        foreach (EnemyCollectionInstance item in input.InitialState.ConsumedCollections)
        {
            builder.Append("|ICC:").Append(item.CollectionInstanceId);
        }

        if (input.RiskContext is { } risk)
        {
            builder.Append("|RISK:").Append(risk.Phase).Append(':')
                .Append(risk.PhaseInitialTemplateInstanceCount).Append(':')
                .Append(risk.ContentDirectory.DeckId.Value);
            foreach (string powerId in risk.AdditionalDefensivePowerIds.Order(StringComparer.Ordinal))
            {
                builder.Append("|RDP:").Append(powerId);
            }

            foreach ((EnemyCardInstanceKey key, int count) in risk.PendingDeferredReplayIncrements
                         .OrderBy(pair => pair.Key.Value, StringComparer.Ordinal))
            {
                builder.Append("|RPI:").Append(key.Value).Append(':').Append(count);
            }
        }

        builder.Append("|U:")
            .Append(string.Join(",", input.UnknownModifierIds.Order(StringComparer.Ordinal)));
        return builder.ToString();
    }

    /// <summary>
    /// 递归追加一个执行单元的身份、素材和有序步骤。
    /// </summary>
    /// <param name="builder">正在构造的稳定指纹。</param>
    /// <param name="unit">待追加的冻结执行单元。</param>
    private static void AppendUnitFingerprint(StringBuilder builder, PreparedEnemyCardUnitPlan unit)
    {
        builder.Append("|N:")
            .Append(unit.RootSourceKey.Value).Append(':')
            .Append(unit.ExecutingCardKey.Value).Append(':')
            .Append(unit.ExecutingCardId.Value).Append(':')
            .Append(unit.ReplayIndex).Append(':')
            .Append(unit.Mode);
        foreach (EnemyMaterialReservation reservation in unit.MaterialReservations)
        {
            builder.Append("|M:");
            foreach (EnemyMaterialBinding binding in reservation.Bindings)
            {
                builder.Append(binding.RequirementIndex).Append(':')
                    .Append(binding.Candidate.CandidateId).Append(':')
                    .Append(binding.IsInspiration).Append(':')
                    .Append(binding.IsEpiphany).Append(';');
            }
        }

        foreach (PreparedEnemyResolutionStep step in unit.OrderedSteps)
        {
            AppendStepFingerprint(builder, step);
        }
    }

    /// <summary>
    /// 递归追加一种显式冻结步骤的全部稳定字段。
    /// </summary>
    /// <param name="builder">正在构造的稳定指纹。</param>
    /// <param name="step">待追加的冻结步骤。</param>
    private static void AppendStepFingerprint(StringBuilder builder, PreparedEnemyResolutionStep step)
    {
        switch (step)
        {
            case PreparedDirectEffectsStep direct:
                builder.Append("|D:").Append(string.Join(",", direct.EffectProgramIds));
                break;
            case PreparedConsumedCardStep consumedCard:
                builder.Append("|CC:").Append(consumedCard.MaterialKey.Value);
                if (consumedCard.ControlledChild is not null)
                {
                    AppendUnitFingerprint(builder, consumedCard.ControlledChild);
                }

                break;
            case PreparedConsumedCollectionStep collection:
                builder.Append("|CL:")
                    .Append(collection.CollectionInstanceId).Append(':')
                    .Append(collection.CollectionId);
                foreach (PreparedEnemyResolutionStep child in collection.Children)
                {
                    AppendStepFingerprint(builder, child);
                }

                break;
            case PreparedGeneratedCollectionStep generated:
                builder.Append("|GC:")
                    .Append(generated.CollectionId).Append(':')
                    .Append(generated.ExpectedSequence);
                break;
            case PreparedComposeResultStep compose:
                builder.Append("|CP:")
                    .Append(compose.ResultCardId.Value).Append(':')
                    .Append(compose.ResultInstanceKey.Value).Append(':')
                    .Append(compose.Timing).Append(':')
                    .Append(compose.IncreasesExistingReplay);
                if (compose.ImmediateChild is not null)
                {
                    AppendUnitFingerprint(builder, compose.ImmediateChild);
                }

                AppendAdditionalReplayFingerprint(builder, compose.AdditionalReplayUnits);

                break;
            case PreparedImmediateCardStep immediate:
                builder.Append("|IC:").Append(immediate.SelectedCardKey.Value);
                AppendUnitFingerprint(builder, immediate.Child);
                AppendAdditionalReplayFingerprint(builder, immediate.AdditionalReplayUnits);
                break;
            case PreparedRecoveryStep recovery:
                builder.Append("|R:")
                    .Append(recovery.Kind).Append(':')
                    .Append(recovery.SelectedInstanceId);
                if (recovery.ImmediateCardChild is not null)
                {
                    AppendUnitFingerprint(builder, recovery.ImmediateCardChild);
                }

                AppendAdditionalReplayFingerprint(builder, recovery.AdditionalReplayUnits);

                break;
            default:
                builder.Append("|X:").Append(step.GetType().AssemblyQualifiedName);
                break;
        }
    }


    /// <summary>
    /// 把即时子牌的全部附加重放单元写入稳定缓存指纹。
    /// </summary>
    /// <param name="builder">正在构造的稳定指纹。</param>
    /// <param name="units">按重放索引排序的附加单元。</param>
    private static void AppendAdditionalReplayFingerprint(
        StringBuilder builder,
        IReadOnlyList<PreparedEnemyCardUnitPlan> units)
    {
        foreach (PreparedEnemyCardUnitPlan unit in units)
        {
            AppendUnitFingerprint(builder, unit);
        }
    }
}
