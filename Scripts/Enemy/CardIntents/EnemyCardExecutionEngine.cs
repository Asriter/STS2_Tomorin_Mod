namespace STS2_Tomorin_Mod.Enemy.CardIntents;

/// <summary>
/// 只消费准备阶段递归冻结计划，并按相同 DFS 顺序提交真实战斗效果与权威牌区变化。
/// </summary>
public sealed class EnemyCardExecutionEngine
{
    private readonly IEnemyAbilityHookDispatcher _abilityHooks;

    /// <summary>
    /// 创建只读冻结计划的敌人牌结算引擎。
    /// </summary>
    /// <param name="materialResolver">保留给既有构造调用方的素材解析器；执行阶段不会使用。</param>
    /// <param name="abilityHooks">敌人版能力钩子分发器。</param>
    public EnemyCardExecutionEngine(
        EnemyCardMaterialResolver? materialResolver = null,
        IEnemyAbilityHookDispatcher? abilityHooks = null)
    {
        _ = materialResolver;
        _abilityHooks = abilityHooks ?? new EnemyAbilityHookDispatcher();
    }

    /// <summary>
    /// 结算当前权威状态唯一冻结行动，随机参数仅用于兼容既有接口且绝不调用。
    /// </summary>
    /// <param name="state">五牌区与收藏品的权威状态。</param>
    /// <param name="context">真实战斗命令上下文。</param>
    /// <param name="random">兼容调用方的随机源；执行阶段禁止推进。</param>
    /// <param name="stepLimit">准备与执行共享的有限步骤上限。</param>
    /// <param name="eventSink">可选稳定逻辑事件接收器。</param>
    /// <returns>行动完成、正常中断或结构故障后的任务。</returns>
    public async Task ExecutePreparedActionAsync(
        EnemyCardCombatState state,
        EnemyCardExecutionContext context,
        IEnemyCardRandomSource random,
        int stepLimit,
        Action<EnemyCardResolutionEvent>? eventSink = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(random);
        if (stepLimit < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(stepLimit));
        }

        PreparedEnemyCardAction action = state.PreparedAction ??
            throw new InvalidOperationException("权威状态没有可执行的冻结行动。 ");
        ExecutionSession session = new(stepLimit, eventSink);
        state.BeginExecution();
        try
        {
            await SynchronizeCollectionPowerAsync(state, context);
            foreach (PreparedEnemyCardSource source in action.Sources)
            {
                ThrowIfStopped(context);
                if (!IsInSourceZone(state, source.SourceKey))
                {
                    session.Publish(
                        EnemyCardResolutionEventType.CardMarkedUnplayable,
                        source.SourceKey,
                        diagnostic: "来源牌已被更早冻结步骤作为素材消费。 ");
                    continue;
                }

                foreach (PreparedEnemyCardUnitPlan unit in source.Units)
                {
                    await ExecuteUnitPlanAsync(state, unit, context, session);
                    session.Publish(
                        EnemyCardResolutionEventType.CardResolved,
                        unit.ExecutingCardKey,
                        replayIndex: unit.ReplayIndex);
                }

                if (source.TruncationAttemptIndex is int truncation)
                {
                    session.Publish(
                        EnemyCardResolutionEventType.CardMarkedUnplayable,
                        source.SourceKey,
                        replayIndex: truncation);
                    if (truncation > 0)
                    {
                        session.Publish(
                            EnemyCardResolutionEventType.ReplayTruncated,
                            source.SourceKey,
                            replayIndex: truncation);
                    }
                }

                ApplySourceLifecycle(
                    state,
                    source.SourceCard,
                    successful: source.Units.Count > 0,
                    immediateFailure: false);
            }

            state.CompleteExecution();
            session.Publish(EnemyCardResolutionEventType.ActionCompleted);
        }
        catch (OperationCanceledException) when (context.ShouldStop)
        {
            session.Publish(EnemyCardResolutionEventType.ActionInterrupted);
            state.CompleteExecution();
        }
        catch (Exception exception)
        {
            EnemyCardRuntimePhase phaseBeforeFault = state.RuntimePhase;
            string diagnostic = $"敌人牌冻结计划结算失败：{exception.Message}";
            state.MarkFault(diagnostic);
            context.State.ReportFaultDiagnostic(
                state,
                "Execution",
                phaseBeforeFault.ToString(),
                exception);
            session.Publish(EnemyCardResolutionEventType.ExecutionFaulted, diagnostic: diagnostic);
        }
    }

    /// <summary>
    /// 验证实际执行实例后按冻结顺序提交一个成功重放单元。
    /// </summary>
    /// <param name="state">权威牌区状态。</param>
    /// <param name="unit">准备阶段冻结的一个成功单元。</param>
    /// <param name="context">真实战斗命令上下文。</param>
    /// <param name="session">有限步骤与事件会话。</param>
    /// <returns>本单元全部步骤完成后的任务。</returns>
    private async Task ExecuteUnitPlanAsync(
        EnemyCardCombatState state,
        PreparedEnemyCardUnitPlan unit,
        EnemyCardExecutionContext context,
        ExecutionSession session)
    {
        ThrowIfStopped(context);
        BaseEnemyCard executing = FindCard(state, unit.ExecutingCardKey);
        if (executing.CardId != unit.ExecutingCardId)
        {
            throw new InvalidOperationException(
                $"冻结单元实例 {unit.ExecutingCardKey} 的 CardId 与权威对象不匹配。 ");
        }

        foreach (PreparedEnemyResolutionStep step in unit.OrderedSteps)
        {
            await ExecuteStepAsync(state, executing, step, context, session, collectionProgram: null);
        }

        await _abilityHooks.AfterSuccessfulUnitAsync(context);
    }

    /// <summary>
    /// 按显式步骤种类验证预期区域并提交一个递归原子步骤。
    /// </summary>
    /// <param name="state">权威牌区状态。</param>
    /// <param name="executing">当前步骤所属实际执行卡牌。</param>
    /// <param name="step">冻结步骤。</param>
    /// <param name="context">真实战斗命令上下文。</param>
    /// <param name="session">有限步骤与事件会话。</param>
    /// <param name="collectionProgram">收藏品子步骤使用的共享效果程序。</param>
    /// <returns>原子步骤及其 DFS 子树完成后的任务。</returns>
    private async Task ExecuteStepAsync(
        EnemyCardCombatState state,
        BaseEnemyCard executing,
        PreparedEnemyResolutionStep step,
        EnemyCardExecutionContext context,
        ExecutionSession session,
        EnemyCollectionEffectProgram? collectionProgram)
    {
        ThrowIfStopped(context);
        session.Step();
        switch (step)
        {
            case PreparedDirectEffectsStep direct:
                await ExecuteDirectEffectsAsync(executing, direct, collectionProgram, context);
                break;

            case PreparedConsumedCardStep consumedCard:
            {
                BaseEnemyCard material = RequireCardInZone(
                    state.CurrentCards,
                    consumedCard.MaterialKey,
                    "当前牌区素材");
                state.MoveCard(material.InstanceKey, EnemyCardZone.Exhaust);
                session.Publish(EnemyCardResolutionEventType.CardConsumed, material.InstanceKey);
                if (consumedCard.ControlledChild is not null)
                {
                    await ExecuteUnitPlanAsync(state, consumedCard.ControlledChild, context, session);
                }

                break;
            }

            case PreparedConsumedCollectionStep consumedCollection:
            {
                EnemyCollectionInstance expected = state.CollectionInventory.Available.SingleOrDefault(item =>
                    item.CollectionInstanceId == consumedCollection.CollectionInstanceId) ??
                    throw new InvalidOperationException(
                        $"冻结收藏品 {consumedCollection.CollectionInstanceId} 不在预期可用区域。 ");
                if (!string.Equals(
                        expected.Definition.CollectionId,
                        consumedCollection.CollectionId,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("冻结收藏品实例与定义标识不匹配。 ");
                }

                EnemyCollectionInstance consumed = state.CollectionInventory.Consume(expected);
                session.Publish(
                    EnemyCardResolutionEventType.CollectionConsumed,
                    collectionId: consumed.CollectionInstanceId);
                EnemyCollectionEffectProgram program = EnemyCollectionEffectResolver.GetRequired(consumed.Definition);
                foreach (PreparedEnemyResolutionStep child in consumedCollection.Children)
                {
                    await ExecuteStepAsync(state, executing, child, context, session, program);
                }

                await SynchronizeCollectionPowerAsync(state, context);
                break;
            }

            case PreparedGeneratedCollectionStep generatedCollection:
            {
                if (state.CollectionInventory.NextSequence != generatedCollection.ExpectedSequence)
                {
                    throw new InvalidOperationException(
                        $"收藏品生成序号预期 {generatedCollection.ExpectedSequence}，实际 {state.CollectionInventory.NextSequence}。 ");
                }

                EnemyCollectionDefinition definition =
                    Test.CardIntentTestCollectionCatalog.Catalog.GetRequired(generatedCollection.CollectionId);
                EnemyCollectionInstance generated = state.CollectionInventory.Append(definition);
                session.Publish(
                    EnemyCardResolutionEventType.CollectionGenerated,
                    executing.InstanceKey,
                    generated.CollectionInstanceId);
                await SynchronizeCollectionPowerAsync(state, context);
                break;
            }

            case PreparedComposeResultStep compose:
                await ExecuteComposeStepAsync(state, compose, context, session);
                await _abilityHooks.AfterComposeAsync(context);
                break;

            case PreparedImmediateCardStep immediate:
                await ExecuteImmediateStepAsync(state, immediate, context, session);
                break;

            case PreparedRecoveryStep recovery:
                await ExecuteRecoveryStepAsync(state, recovery, context, session);
                break;

            default:
                throw new InvalidOperationException($"未知冻结结算步骤 {step.GetType().Name}。 ");
        }
    }

    /// <summary>
    /// 按卡牌定义或收藏品共享程序解析并执行完全匹配的直接效果列表。
    /// </summary>
    /// <param name="executing">当前实际执行卡牌。</param>
    /// <param name="step">冻结直接效果程序标识。</param>
    /// <param name="collectionProgram">收藏品子树中的可选共享程序。</param>
    /// <param name="context">真实战斗命令上下文。</param>
    /// <returns>全部直接效果按顺序执行后的任务。</returns>
    private static async Task ExecuteDirectEffectsAsync(
        BaseEnemyCard executing,
        PreparedDirectEffectsStep step,
        EnemyCollectionEffectProgram? collectionProgram,
        EnemyCardExecutionContext context)
    {
        IReadOnlyList<IEnemyCardEffectNode> effects = collectionProgram?.DirectEffects ?? executing.Definition.Effects;
        if (!effects.Select(effect => effect.ProgramId).SequenceEqual(step.EffectProgramIds, StringComparer.Ordinal))
        {
            throw new InvalidOperationException("冻结直接效果程序与当前定义顺序不一致。 ");
        }

        foreach (IEnemyCardEffectNode effect in effects)
        {
            await effect.ExecuteAsync(context);
        }
    }

    /// <summary>
    /// 验证并提交作词结果的现有增层或预计新实例。
    /// </summary>
    /// <param name="state">权威牌区状态。</param>
    /// <param name="compose">冻结作词步骤。</param>
    /// <param name="context">真实战斗命令上下文。</param>
    /// <param name="session">有限步骤与事件会话。</param>
    /// <returns>即时作词子牌完成后的任务。</returns>
    private async Task ExecuteComposeStepAsync(
        EnemyCardCombatState state,
        PreparedComposeResultStep compose,
        EnemyCardExecutionContext context,
        ExecutionSession session)
    {
        if (compose.IncreasesExistingReplay)
        {
            BaseEnemyCard existing = FindCard(state, compose.ResultInstanceKey);
            if (existing.CardId != compose.ResultCardId)
            {
                throw new InvalidOperationException("作词增层实例与冻结结果定义不匹配。 ");
            }

            existing.IncreaseReplayCount();
            return;
        }

        EnemyCardInstanceKey expectedKey = EnemyCardInstanceKey.FromRuntimeInstanceId(
            state.NextGeneratedCardSequence);
        if (expectedKey != compose.ResultInstanceKey)
        {
            throw new InvalidOperationException(
                $"作词生成实例预期 {compose.ResultInstanceKey}，实际下一实例为 {expectedKey}。 ");
        }

        BaseEnemyCard generated = Test.CardIntentTestCardCatalog.CreateCard(compose.ResultCardId);
        EnemyCardZone destination = compose.Timing == EnemyCardTokenTiming.Immediate
            ? EnemyCardZone.Current
            : EnemyCardZone.Retained;
        state.AddGeneratedCard(generated, destination);
        if (generated.InstanceKey != compose.ResultInstanceKey)
        {
            throw new InvalidOperationException("作词生成后的权威实例键与冻结计划不一致。 ");
        }

        if (compose.ImmediateChild is not null)
        {
            session.Publish(EnemyCardResolutionEventType.ImmediateCardQueued, generated.InstanceKey);
            await ExecuteUnitPlanAsync(state, compose.ImmediateChild, context, session);
            foreach (PreparedEnemyCardUnitPlan replayUnit in compose.AdditionalReplayUnits)
            {
                await ExecuteUnitPlanAsync(state, replayUnit, context, session);
            }

            ApplySourceLifecycle(state, generated, successful: true, immediateFailure: true);
        }
    }

    /// <summary>
    /// 验证准备阶段选中的抽牌实例并立即执行其冻结子单元。
    /// </summary>
    /// <param name="state">权威牌区状态。</param>
    /// <param name="immediate">冻结即时抽牌步骤。</param>
    /// <param name="context">真实战斗命令上下文。</param>
    /// <param name="session">有限步骤与事件会话。</param>
    /// <returns>即时子牌完成后的任务。</returns>
    private async Task ExecuteImmediateStepAsync(
        EnemyCardCombatState state,
        PreparedImmediateCardStep immediate,
        EnemyCardExecutionContext context,
        ExecutionSession session)
    {
        if (state.DrawPile.Count == 0)
        {
            foreach (EnemyCardInstanceKey key in state.DiscardPile.Select(card => card.InstanceKey).ToArray())
            {
                state.MoveCard(key, EnemyCardZone.Draw);
            }
        }

        BaseEnemyCard selected = RequireCardInZone(state.DrawPile, immediate.SelectedCardKey, "抽牌堆");
        state.MoveCard(selected.InstanceKey, EnemyCardZone.Current);
        session.Publish(EnemyCardResolutionEventType.ImmediateCardQueued, selected.InstanceKey);
        await ExecuteUnitPlanAsync(state, immediate.Child, context, session);
        foreach (PreparedEnemyCardUnitPlan replayUnit in immediate.AdditionalReplayUnits)
        {
            await ExecuteUnitPlanAsync(state, replayUnit, context, session);
        }

        ApplySourceLifecycle(state, selected, successful: true, immediateFailure: true);
    }

    /// <summary>
    /// 验证准备阶段选中的消耗对象并提交卡牌即时执行或收藏品恢复。
    /// </summary>
    /// <param name="state">权威牌区状态。</param>
    /// <param name="recovery">冻结回收步骤。</param>
    /// <param name="context">真实战斗命令上下文。</param>
    /// <param name="session">有限步骤与事件会话。</param>
    /// <returns>卡牌回收子单元完成后的任务。</returns>
    private async Task ExecuteRecoveryStepAsync(
        EnemyCardCombatState state,
        PreparedRecoveryStep recovery,
        EnemyCardExecutionContext context,
        ExecutionSession session)
    {
        if (recovery.Kind == EnemyPreparedRecoveryKind.Collection)
        {
            _ = state.CollectionInventory.Consumed.SingleOrDefault(item =>
                    item.CollectionInstanceId == recovery.SelectedInstanceId) ??
                throw new InvalidOperationException(
                    $"冻结回收收藏品 {recovery.SelectedInstanceId} 不在已消耗区域。 ");
            state.CollectionInventory.Recover(recovery.SelectedInstanceId);
            await SynchronizeCollectionPowerAsync(state, context);
            return;
        }

        EnemyCardInstanceKey key = new(recovery.SelectedInstanceId);
        BaseEnemyCard card = RequireCardInZone(state.ExhaustPile, key, "消耗牌区");
        state.MoveCard(card.InstanceKey, EnemyCardZone.Current);
        session.Publish(EnemyCardResolutionEventType.ImmediateCardQueued, card.InstanceKey);
        await ExecuteUnitPlanAsync(state, recovery.ImmediateCardChild!, context, session);
        foreach (PreparedEnemyCardUnitPlan replayUnit in recovery.AdditionalReplayUnits)
        {
            await ExecuteUnitPlanAsync(state, replayUnit, context, session);
        }

        ApplySourceLifecycle(state, card, successful: true, immediateFailure: true);
    }

    /// <summary>
    /// 按成功与失败语义把仍在来源区的实例移到最终区域。
    /// </summary>
    /// <param name="state">权威牌区状态。</param>
    /// <param name="source">待结束生命周期的来源实例。</param>
    /// <param name="successful">是否至少完成一个成功单元。</param>
    /// <param name="immediateFailure">即时来源失败时是否强制弃置。</param>
    private static void ApplySourceLifecycle(
        EnemyCardCombatState state,
        BaseEnemyCard source,
        bool successful,
        bool immediateFailure)
    {
        if (!IsInSourceZone(state, source.InstanceKey))
        {
            return;
        }

        EnemyCardZone destination = successful
            ? source.Definition.Lifecycle == EnemyCardLifecycle.Exhaust
                ? EnemyCardZone.Exhaust
                : EnemyCardZone.Discard
            : immediateFailure || source.Definition.FailureDisposition == EnemyCardFailureDisposition.Discard
                ? EnemyCardZone.Discard
                : EnemyCardZone.Retained;
        state.MoveCard(source.InstanceKey, destination);
    }

    /// <summary>
    /// 从指定权威区域取得唯一实例，否则报告结构故障。
    /// </summary>
    /// <param name="zone">预期实例所在区域。</param>
    /// <param name="key">冻结稳定实例键。</param>
    /// <param name="zoneName">中文诊断区域名。</param>
    /// <returns>区域中的唯一权威实例。</returns>
    private static BaseEnemyCard RequireCardInZone(
        IReadOnlyList<BaseEnemyCard> zone,
        EnemyCardInstanceKey key,
        string zoneName) =>
        zone.SingleOrDefault(card => card.InstanceKey == key) ??
        throw new InvalidOperationException($"冻结实例 {key} 不在预期{zoneName}。 ");

    /// <summary>
    /// 按五牌区规范顺序查找权威卡牌实例。
    /// </summary>
    /// <param name="state">权威牌区状态。</param>
    /// <param name="key">稳定实例键。</param>
    /// <returns>唯一匹配卡牌。</returns>
    private static BaseEnemyCard FindCard(EnemyCardCombatState state, EnemyCardInstanceKey key) =>
        state.DrawPile
            .Concat(state.CurrentCards)
            .Concat(state.RetainedCards)
            .Concat(state.DiscardPile)
            .Concat(state.ExhaustPile)
            .Single(card => card.InstanceKey == key);

    /// <summary>
    /// 判断实例是否仍可作为公开或即时来源执行。
    /// </summary>
    /// <param name="state">权威牌区状态。</param>
    /// <param name="key">稳定实例键。</param>
    /// <returns>实例位于当前区或保留区时为真。</returns>
    private static bool IsInSourceZone(EnemyCardCombatState state, EnemyCardInstanceKey key) =>
        state.CurrentCards.Concat(state.RetainedCards).Any(card => card.InstanceKey == key);

    /// <summary>
    /// 把当前可用收藏品顺序同步到可见 Power 投影。
    /// </summary>
    /// <param name="state">权威收藏品库存。</param>
    /// <param name="context">真实战斗命令上下文。</param>
    /// <returns>同步完成任务。</returns>
    private static Task SynchronizeCollectionPowerAsync(
        EnemyCardCombatState state,
        EnemyCardExecutionContext context) =>
        context.SynchronizeCollectionPowerAsync(
            state.CollectionQueue.Select(item => item.Definition.CollectionId).ToArray());

    /// <summary>
    /// 在每个递归步骤前把敌人离场转换为正常中断。
    /// </summary>
    /// <param name="context">真实战斗命令上下文。</param>
    private static void ThrowIfStopped(EnemyCardExecutionContext context)
    {
        if (context.ShouldStop)
        {
            throw new OperationCanceledException("敌人已死亡、离场或战斗结束。 ");
        }
    }

    /// <summary>
    /// 保存有限步骤计数和逻辑事件单调序号。
    /// </summary>
    private sealed class ExecutionSession
    {
        private readonly int _stepLimit;
        private readonly Action<EnemyCardResolutionEvent>? _eventSink;
        private long _eventSequence;
        private int _stepCount;

        /// <summary>
        /// 创建一次行动执行会话。
        /// </summary>
        /// <param name="stepLimit">总原子步骤上限。</param>
        /// <param name="eventSink">可选稳定事件接收器。</param>
        public ExecutionSession(int stepLimit, Action<EnemyCardResolutionEvent>? eventSink)
        {
            _stepLimit = stepLimit;
            _eventSink = eventSink;
        }

        /// <summary>
        /// 提交一个原子步骤并执行有限终止检查。
        /// </summary>
        public void Step()
        {
            if (++_stepCount > _stepLimit)
            {
                throw new InvalidOperationException("敌人牌结算超过有限步骤上限。 ");
            }
        }

        /// <summary>
        /// 按单调序号发布不持有 UI 对象的逻辑事件。
        /// </summary>
        /// <param name="type">事件类型。</param>
        /// <param name="cardKey">可选卡牌实例键。</param>
        /// <param name="collectionId">可选收藏品实例标识。</param>
        /// <param name="replayIndex">相关重放索引。</param>
        /// <param name="diagnostic">可选结构诊断。</param>
        public void Publish(
            EnemyCardResolutionEventType type,
            EnemyCardInstanceKey? cardKey = null,
            string? collectionId = null,
            int replayIndex = 0,
            string? diagnostic = null) =>
            _eventSink?.Invoke(new EnemyCardResolutionEvent(
                type,
                _eventSequence++,
                cardKey,
                collectionId,
                replayIndex,
                diagnostic));
    }
}
