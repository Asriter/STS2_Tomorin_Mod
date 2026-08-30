using STS2_Tomorin_Mod.Cards;
using STS2_Tomorin_Mod.Cards.Base;
using STS2_Tomorin_Mod.Enemy.CardIntents.Test;
using STS2_Tomorin_Mod.Localization.CustomEnums;

namespace STS2_Tomorin_Mod.Enemy.CardIntents;

/// <summary>
/// 保存候选专用的五牌区、收藏品库存和生成序号事务副本。
/// </summary>
public sealed class EnemyPreparedPlanningState
{
    private readonly Dictionary<EnemyCardInstanceKey, int> _plannedReplayIncreases = [];

    /// <summary>
    /// 从完整权威区域复制可独立推进的准备事务。
    /// </summary>
    /// <param name="drawPile">候选抽牌堆。</param>
    /// <param name="currentCards">候选当前牌区。</param>
    /// <param name="retainedCards">候选保留区。</param>
    /// <param name="discardPile">候选弃牌堆。</param>
    /// <param name="exhaustPile">候选消耗堆。</param>
    /// <param name="collectionInventory">候选收藏品库存。</param>
    /// <param name="nextGeneratedCardSequence">下一张生成牌序号。</param>
    public EnemyPreparedPlanningState(
        IEnumerable<BaseEnemyCard> drawPile,
        IEnumerable<BaseEnemyCard> currentCards,
        IEnumerable<BaseEnemyCard> retainedCards,
        IEnumerable<BaseEnemyCard> discardPile,
        IEnumerable<BaseEnemyCard> exhaustPile,
        EnemyCollectionInventory collectionInventory,
        long nextGeneratedCardSequence)
    {
        ArgumentNullException.ThrowIfNull(drawPile);
        ArgumentNullException.ThrowIfNull(currentCards);
        ArgumentNullException.ThrowIfNull(retainedCards);
        ArgumentNullException.ThrowIfNull(discardPile);
        ArgumentNullException.ThrowIfNull(exhaustPile);
        DrawPile = drawPile.ToList();
        CurrentCards = currentCards.ToList();
        RetainedCards = retainedCards.ToList();
        DiscardPile = discardPile.ToList();
        ExhaustPile = exhaustPile.ToList();
        CollectionInventory = (collectionInventory ?? throw new ArgumentNullException(nameof(collectionInventory)))
            .CreateTransactionalClone();
        if (nextGeneratedCardSequence < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(nextGeneratedCardSequence));
        }

        NextGeneratedCardSequence = nextGeneratedCardSequence;
        AssertUniqueCards();
    }

    /// <summary>获取候选事务抽牌堆。</summary>
    public List<BaseEnemyCard> DrawPile { get; }

    /// <summary>获取候选事务当前牌区。</summary>
    public List<BaseEnemyCard> CurrentCards { get; }

    /// <summary>获取候选事务保留区。</summary>
    public List<BaseEnemyCard> RetainedCards { get; }

    /// <summary>获取候选事务弃牌堆。</summary>
    public List<BaseEnemyCard> DiscardPile { get; }

    /// <summary>获取候选事务消耗堆。</summary>
    public List<BaseEnemyCard> ExhaustPile { get; }

    /// <summary>获取候选事务收藏品库存。</summary>
    public EnemyCollectionInventory CollectionInventory { get; }

    /// <summary>获取下一张候选生成牌将使用的单调序号。</summary>
    public long NextGeneratedCardSequence { get; private set; }

    /// <summary>
    /// 判断实例是否仍位于公开来源允许执行的当前区或保留区。
    /// </summary>
    /// <param name="key">待定位实例键。</param>
    /// <returns>实例仍可作为来源执行时为真。</returns>
    public bool IsInSourceZone(EnemyCardInstanceKey key) =>
        CurrentCards.Concat(RetainedCards).Any(card => card.InstanceKey == key);

    /// <summary>
    /// 按五区规范顺序定位一个候选事务卡牌实例。
    /// </summary>
    /// <param name="key">稳定卡牌实例键。</param>
    /// <returns>唯一匹配的候选卡牌。</returns>
    public BaseEnemyCard FindCard(EnemyCardInstanceKey key) =>
        EnumerateZones().SelectMany(zone => zone).Single(card => card.InstanceKey == key);

    /// <summary>
    /// 把候选卡牌移动到另一事务区域并保持对象身份。
    /// </summary>
    /// <param name="key">稳定卡牌实例键。</param>
    /// <param name="destination">目标事务牌区。</param>
    public void MoveCard(EnemyCardInstanceKey key, EnemyCardZone destination)
    {
        List<BaseEnemyCard> source = EnumerateZones().Single(zone => zone.Any(card => card.InstanceKey == key));
        BaseEnemyCard card = source.Single(item => item.InstanceKey == key);
        List<BaseEnemyCard> target = GetZone(destination);
        if (!ReferenceEquals(source, target))
        {
            source.Remove(card);
            target.Add(card);
            AssertUniqueCards();
        }
    }

    /// <summary>
    /// 创建具有预计运行时身份的候选生成牌并加入目标区域。
    /// </summary>
    /// <param name="cardId">已注册生成牌定义标识。</param>
    /// <param name="destination">即时当前区或下回合保留区。</param>
    /// <returns>已绑定预计实例键的候选对象。</returns>
    public BaseEnemyCard AddGeneratedCard(EnemyCardId cardId, EnemyCardZone destination)
    {
        BaseEnemyCard generated = CardIntentTestCardCatalog.CreateCard(cardId);
        generated.AssignRuntimeInstanceId(NextGeneratedCardSequence);
        checked
        {
            NextGeneratedCardSequence++;
        }

        GetZone(destination).Add(generated);
        AssertUniqueCards();
        return generated;
    }

    /// <summary>
    /// 增加候选现有实例的重放计数。
    /// </summary>
    /// <param name="key">稳定卡牌实例键。</param>
    public void IncreaseReplay(EnemyCardInstanceKey key)
    {
        _ = FindCard(key);
        _plannedReplayIncreases[key] = _plannedReplayIncreases.GetValueOrDefault(key) + 1;
    }

    /// <summary>
    /// 获取权威基础重放加上本候选已虚拟增加后的准备重放次数。
    /// </summary>
    /// <param name="key">稳定卡牌实例键。</param>
    /// <returns>候选事务当前可见的额外重放次数。</returns>
    public int GetReplayCount(EnemyCardInstanceKey key)
    {
        BaseEnemyCard card = FindCard(key);
        return checked(card.ReplayCount + _plannedReplayIncreases.GetValueOrDefault(key));
    }

    /// <summary>
    /// 按区域枚举候选事务的五个卡牌列表。
    /// </summary>
    /// <returns>固定规范顺序的列表序列。</returns>
    private IEnumerable<List<BaseEnemyCard>> EnumerateZones()
    {
        yield return DrawPile;
        yield return CurrentCards;
        yield return RetainedCards;
        yield return DiscardPile;
        yield return ExhaustPile;
    }

    /// <summary>
    /// 取得候选事务指定区域的可变列表。
    /// </summary>
    /// <param name="zone">目标牌区。</param>
    /// <returns>区域唯一后备列表。</returns>
    private List<BaseEnemyCard> GetZone(EnemyCardZone zone) => zone switch
    {
        EnemyCardZone.Draw => DrawPile,
        EnemyCardZone.Current => CurrentCards,
        EnemyCardZone.Retained => RetainedCards,
        EnemyCardZone.Discard => DiscardPile,
        EnemyCardZone.Exhaust => ExhaustPile,
        _ => throw new ArgumentOutOfRangeException(nameof(zone), zone, "未知准备事务牌区。")
    };

    /// <summary>
    /// 验证候选五区没有重复对象或重复实例键。
    /// </summary>
    private void AssertUniqueCards()
    {
        BaseEnemyCard[] cards = EnumerateZones().SelectMany(zone => zone).ToArray();
        if (cards.Any(card => card is null) ||
            cards.Select(card => card.InstanceKey).Distinct().Count() != cards.Length ||
            cards.Distinct(ReferenceEqualityComparer.Instance).Count() != cards.Length)
        {
            throw new InvalidOperationException("准备事务违反卡牌实例唯一所有权不变量。 ");
        }
    }
}

/// <summary>
/// 在候选事务副本上冻结素材、随机选择、生成身份和完整深度优先步骤。
/// </summary>
public sealed class EnemyPreparedResolutionPlanner
{
    private readonly EnemyCardMaterialResolver _materialResolver;

    /// <summary>
    /// 创建递归准备规划器。
    /// </summary>
    /// <param name="materialResolver">无副作用素材解析器。</param>
    public EnemyPreparedResolutionPlanner(EnemyCardMaterialResolver? materialResolver = null)
    {
        _materialResolver = materialResolver ?? new EnemyCardMaterialResolver();
    }

    /// <summary>
    /// 为一个公开来源冻结全部成功重放及首个正常素材截断边界。
    /// </summary>
    /// <param name="source">当前公开来源牌。</param>
    /// <param name="maximumAttempts">准备瞬间冻结的一加重放次数。</param>
    /// <param name="transaction">候选专属事务状态。</param>
    /// <param name="random">唯一战斗随机源。</param>
    /// <param name="stepLimit">递归计划允许的总步骤上限。</param>
    /// <returns>不再需要执行阶段选择的完整来源计划。</returns>
    public PreparedEnemyCardSource PlanSource(
        BaseEnemyCard source,
        int maximumAttempts,
        EnemyPreparedPlanningState transaction,
        IEnemyCardRandomSource random,
        int stepLimit,
        EnemyEffectiveCardLedger? effectiveCardLedger = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(random);
        if (maximumAttempts < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumAttempts));
        }

        if (stepLimit < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(stepLimit));
        }

        PlanningSession session = new(stepLimit, effectiveCardLedger ?? new EnemyEffectiveCardLedger());
        if (!transaction.IsInSourceZone(source.InstanceKey))
        {
            return new PreparedEnemyCardSource(source, maximumAttempts, [], truncationAttemptIndex: 0);
        }

        List<PreparedEnemyCardUnitPlan> units = [];
        int? truncation = null;
        for (int replayIndex = 0; replayIndex < maximumAttempts; replayIndex++)
        {
            if (!TryBuildUnit(
                    source,
                    source.InstanceKey,
                    replayIndex,
                    EnemyPreparedExecutionMode.Normal,
                    transaction,
                    random,
                    session,
                    out PreparedEnemyCardUnitPlan? unit,
                    out UnitPlanFailure failure))
            {
                truncation = replayIndex;
                break;
            }

            units.Add(unit!);
        }

        session.EffectiveCardLedger.Complete(source.InstanceKey, units.Count > 0);
        ApplySourceLifecycle(transaction, source, units.Count > 0, immediateFailure: false);
        return new PreparedEnemyCardSource(source, maximumAttempts, units, truncation);
    }

    /// <summary>
    /// 为一次实际卡牌重放构建素材支付、直接效果、生成与作词步骤。
    /// </summary>
    /// <param name="source">实际执行牌。</param>
    /// <param name="rootSourceKey">公开来源实例键。</param>
    /// <param name="replayIndex">本牌重放索引。</param>
    /// <param name="mode">完整或受控直接模式。</param>
    /// <param name="state">候选事务状态。</param>
    /// <param name="random">唯一战斗随机源。</param>
    /// <param name="session">递归与步骤预算。</param>
    /// <param name="unit">成功时返回冻结单元。</param>
    /// <param name="failure">失败时区分正常素材截断与出牌条件拒绝。</param>
    /// <returns>条件满足、素材足够且成功冻结时为真。</returns>
    private bool TryBuildUnit(
        BaseEnemyCard source,
        EnemyCardInstanceKey rootSourceKey,
        int replayIndex,
        EnemyPreparedExecutionMode mode,
        EnemyPreparedPlanningState state,
        IEnemyCardRandomSource random,
        PlanningSession session,
        out PreparedEnemyCardUnitPlan? unit,
        out UnitPlanFailure failure)
    {
        session.Enter(source.InstanceKey);
        try
        {
            if (!session.EffectiveCardLedger.States.ContainsKey(source.InstanceKey))
            {
                EnemyFrozenXAttackAllEffect[] frozenXEffects = source.Definition.Effects
                    .OfType<EnemyFrozenXAttackAllEffect>()
                    .ToArray();
                int[] multipliers = frozenXEffects
                    .Select(effect => effect.ResolveMultiplier(state))
                    .Distinct()
                    .ToArray();
                if (multipliers.Length > 1)
                {
                    throw new InvalidOperationException($"执行牌 {source.InstanceKey} 的 X 效果冻结倍率不一致。");
                }

                session.EffectiveCardLedger.Begin(
                    source.InstanceKey,
                    isX: frozenXEffects.Length > 0,
                    multiplier: multipliers.SingleOrDefault(1));
            }
            if (!source.Definition.PlayCondition.CanPlan(state, source))
            {
                unit = null;
                failure = UnitPlanFailure.ConditionRejected;
                return false;
            }

            List<EnemyMaterialReservation> reservations = [];
            List<PreparedEnemyResolutionStep> steps = [];
            foreach (EnemyCardProgramOperation operation in source.Definition.ResolutionProgram.Operations)
            {
                if (mode == EnemyPreparedExecutionMode.ControlledDirectOnly &&
                    operation.Kind != EnemyCardProgramOperationKind.DirectEffects)
                {
                    continue;
                }

                switch (operation.Kind)
                {
                    case EnemyCardProgramOperationKind.ConsumeMaterials:
                        if (!TryAppendConsumedMaterials())
                        {
                            unit = null;
                            failure = UnitPlanFailure.MaterialShortfall;
                            return false;
                        }

                        break;

                    case EnemyCardProgramOperationKind.ComposeResult:
                        EnemyCardId resultId = source.Definition.ComposeResultCardId ??
                            throw new InvalidOperationException("显式 ComposeResult 操作缺少结果定义。");
                        steps.Add(BuildComposeStep(source, resultId, rootSourceKey, state, random, session));
                        break;

                    case EnemyCardProgramOperationKind.DirectEffects:
                        session.Step();
                        steps.Add(new PreparedDirectEffectsStep(
                            source.Definition.Effects.Select(effect => effect.ProgramId)));
                        steps.AddRange(BuildGeneratedCollectionSteps(source, state, random, session));
                        break;

                    default:
                        throw new InvalidOperationException($"未知敌人卡牌显式程序操作 {operation.Kind}。");
                }
            }

            unit = new PreparedEnemyCardUnitPlan(
                rootSourceKey,
                source.InstanceKey,
                source.CardId,
                replayIndex,
                mode,
                reservations,
                steps,
                source.Definition.ResolutionProgram.Fingerprint,
                source.Definition.PlayCondition.ProgramId);
            failure = UnitPlanFailure.None;
            return true;

            bool TryAppendConsumedMaterials()
            {
                EnemyCollectionInventory reservationInventory =
                    state.CollectionInventory.CreateTransactionalClone();
                HashSet<EnemyCardInstanceKey> virtuallyConsumedCards = [];
                List<EnemyMaterialReservation> operationReservations = [];
                foreach (EnemyMaterialRequest request in source.Definition.MaterialRequests)
                {
                    session.Step();
                    IReadOnlyList<EnemyMaterialCandidate> hand = state.CurrentCards
                        .Where(card => !virtuallyConsumedCards.Contains(card.InstanceKey))
                        .Select(ToMaterialCandidate)
                        .ToArray();
                    if (!_materialResolver.TryReserve(
                            request,
                            new EnemyMaterialContext(hand, reservationInventory, source.InstanceKey),
                            out EnemyMaterialReservation reservation))
                    {
                        return false;
                    }

                    operationReservations.Add(reservation);
                    foreach (EnemyMaterialBinding binding in reservation.Bindings)
                    {
                        if (binding.CardInstanceKey is EnemyCardInstanceKey cardKey)
                        {
                            virtuallyConsumedCards.Add(cardKey);
                        }
                        else if (binding.CollectionInstanceId is string collectionId)
                        {
                            reservationInventory.Consume(collectionId);
                        }
                    }
                }

                reservations.AddRange(operationReservations);
                foreach (EnemyMaterialBinding binding in operationReservations.SelectMany(item => item.Bindings))
                {
                    steps.Add(BuildConsumedMaterialStep(
                        binding,
                        rootSourceKey,
                        state,
                        random,
                        session));
                }

                return true;
            }
        }
        finally
        {
            session.Exit(source.InstanceKey);
        }
    }

    /// <summary>
    /// 在事务中消费一个冻结素材并创建其 DFS 子步骤。
    /// </summary>
    /// <param name="binding">完整预留中的一个稳定绑定。</param>
    /// <param name="rootSourceKey">公开根来源实例键。</param>
    /// <param name="state">候选事务状态。</param>
    /// <param name="random">唯一战斗随机源。</param>
    /// <param name="session">递归与步骤预算。</param>
    /// <returns>卡牌或收藏品消费步骤。</returns>
    private PreparedEnemyResolutionStep BuildConsumedMaterialStep(
        EnemyMaterialBinding binding,
        EnemyCardInstanceKey rootSourceKey,
        EnemyPreparedPlanningState state,
        IEnemyCardRandomSource random,
        PlanningSession session)
    {
        session.Step();
        if (binding.CardInstanceKey is EnemyCardInstanceKey cardKey)
        {
            BaseEnemyCard material = state.FindCard(cardKey);
            state.MoveCard(cardKey, EnemyCardZone.Exhaust);
            PreparedEnemyCardUnitPlan? child = null;
            if (binding.IsInspiration)
            {
                if (!TryBuildUnit(
                        material,
                        rootSourceKey,
                        replayIndex: 0,
                        EnemyPreparedExecutionMode.ControlledDirectOnly,
                        state,
                        random,
                        session,
                        out child,
                        out UnitPlanFailure failure))
                {
                    throw new InvalidOperationException(
                        $"受控灵感子单元不应在准备时失败：{failure}。 ");
                }

                session.EffectiveCardLedger.Complete(cardKey, anyUnitSucceeded: true);
            }

            return new PreparedConsumedCardStep(cardKey, child);
        }

        string collectionInstanceId = binding.CollectionInstanceId ??
            throw new InvalidOperationException("冻结素材绑定没有卡牌或收藏品身份。 ");
        EnemyCollectionInstance collection = state.CollectionInventory.Consume(collectionInstanceId);
        IReadOnlyList<PreparedEnemyResolutionStep> children = BuildCollectionChildren(
            collection,
            rootSourceKey,
            state,
            random,
            session);
        return new PreparedConsumedCollectionStep(
            collection.CollectionInstanceId,
            collection.Definition.CollectionId,
            children);
    }

    /// <summary>
    /// 从共享收藏品程序冻结直接效果和特殊随机选择。
    /// </summary>
    /// <param name="collection">刚被移动到消耗区的收藏品。</param>
    /// <param name="rootSourceKey">公开根来源实例键。</param>
    /// <param name="state">候选事务状态。</param>
    /// <param name="random">唯一战斗随机源。</param>
    /// <param name="session">递归与步骤预算。</param>
    /// <returns>收藏品步骤内部的 DFS 子步骤。</returns>
    private IReadOnlyList<PreparedEnemyResolutionStep> BuildCollectionChildren(
        EnemyCollectionInstance collection,
        EnemyCardInstanceKey rootSourceKey,
        EnemyPreparedPlanningState state,
        IEnemyCardRandomSource random,
        PlanningSession session)
    {
        EnemyCollectionEffectProgram program = EnemyCollectionEffectResolver.GetRequired(collection.Definition);
        List<PreparedEnemyResolutionStep> children = [];
        session.Step();
        children.Add(new PreparedDirectEffectsStep(program.DirectEffects.Select(effect => effect.ProgramId)));
        switch (program.SpecialResolutionKind)
        {
            case EnemyCollectionSpecialResolutionKind.None:
                break;
            case EnemyCollectionSpecialResolutionKind.DrawAndExecuteImmediateCard:
            {
                PreparedImmediateCardStep? immediate = BuildImmediateDrawStep(
                    rootSourceKey,
                    state,
                    random,
                    session);
                if (immediate is not null)
                {
                    children.Add(immediate);
                }

                break;
            }
            case EnemyCollectionSpecialResolutionKind.RecoverConsumedEntry:
            {
                PreparedRecoveryStep? recovery = BuildRecoveryStep(
                    collection.CollectionInstanceId,
                    rootSourceKey,
                    state,
                    random,
                    session);
                if (recovery is not null)
                {
                    children.Add(recovery);
                }

                break;
            }
            default:
                throw new InvalidOperationException($"未知收藏品特殊解析种类 {program.SpecialResolutionKind}。 ");
        }

        return children.AsReadOnly();
    }

    /// <summary>
    /// 冻结即时抽牌选择并在事务中完整解析其首个单元。
    /// </summary>
    /// <param name="rootSourceKey">公开根来源实例键。</param>
    /// <param name="state">候选事务状态。</param>
    /// <param name="random">唯一战斗随机源。</param>
    /// <param name="session">递归与步骤预算。</param>
    /// <returns>有牌可抽时的即时步骤，否则为空。</returns>
    private PreparedImmediateCardStep? BuildImmediateDrawStep(
        EnemyCardInstanceKey rootSourceKey,
        EnemyPreparedPlanningState state,
        IEnemyCardRandomSource random,
        PlanningSession session)
    {
        if (state.DrawPile.Count == 0)
        {
            foreach (EnemyCardInstanceKey key in state.DiscardPile.Select(card => card.InstanceKey).ToArray())
            {
                state.MoveCard(key, EnemyCardZone.Draw);
            }
        }

        if (state.DrawPile.Count == 0)
        {
            return null;
        }

        session.Step();
        BaseEnemyCard selected = state.DrawPile[random.NextIndex(state.DrawPile.Count)];
        state.MoveCard(selected.InstanceKey, EnemyCardZone.Current);
        IReadOnlyList<PreparedEnemyCardUnitPlan> children = BuildRequiredImmediateChildren(
            selected, rootSourceKey, state, random, session);
        ApplySourceLifecycle(state, selected, successful: true, immediateFailure: true);
        return new PreparedImmediateCardStep(selected.InstanceKey, children[0], children.Skip(1));
    }

    /// <summary>
    /// 冻结统一消耗区的随机回收对象，并对卡牌创建即时子单元。
    /// </summary>
    /// <param name="sourceCollectionId">当前回收收藏品自身实例标识。</param>
    /// <param name="rootSourceKey">公开根来源实例键。</param>
    /// <param name="state">候选事务状态。</param>
    /// <param name="random">唯一战斗随机源。</param>
    /// <param name="session">递归与步骤预算。</param>
    /// <returns>有候选时的回收步骤，否则为空。</returns>
    private PreparedRecoveryStep? BuildRecoveryStep(
        string sourceCollectionId,
        EnemyCardInstanceKey rootSourceKey,
        EnemyPreparedPlanningState state,
        IEnemyCardRandomSource random,
        PlanningSession session)
    {
        object[] candidates = state.ExhaustPile.Cast<object>()
            .Concat(state.CollectionInventory.Consumed
                .Where(item => item.CollectionInstanceId != sourceCollectionId))
            .ToArray();
        if (candidates.Length == 0)
        {
            return null;
        }

        session.Step();
        object selected = candidates[random.NextIndex(candidates.Length)];
        if (selected is EnemyCollectionInstance collection)
        {
            state.CollectionInventory.Recover(collection);
            return new PreparedRecoveryStep(
                EnemyPreparedRecoveryKind.Collection,
                collection.CollectionInstanceId,
                immediateCardChild: null);
        }

        BaseEnemyCard card = (BaseEnemyCard)selected;
        state.MoveCard(card.InstanceKey, EnemyCardZone.Current);
        IReadOnlyList<PreparedEnemyCardUnitPlan> children = BuildRequiredImmediateChildren(
            card, rootSourceKey, state, random, session);
        ApplySourceLifecycle(state, card, successful: true, immediateFailure: true);
        return new PreparedRecoveryStep(
            EnemyPreparedRecoveryKind.Card,
            card.InstanceKey.Value,
            children[0],
            children.Skip(1));
    }

    /// <summary>
    /// 为即时或回收卡牌建立必然成功的首个冻结单元。
    /// </summary>
    /// <param name="card">已移动到当前区的实际卡牌。</param>
    /// <param name="rootSourceKey">公开根来源实例键。</param>
    /// <param name="state">候选事务状态。</param>
    /// <param name="random">唯一战斗随机源。</param>
    /// <param name="session">递归与步骤预算。</param>
    /// <returns>冻结的即时卡牌子单元。</returns>
    private IReadOnlyList<PreparedEnemyCardUnitPlan> BuildRequiredImmediateChildren(
        BaseEnemyCard card,
        EnemyCardInstanceKey rootSourceKey,
        EnemyPreparedPlanningState state,
        IEnemyCardRandomSource random,
        PlanningSession session)
    {
        List<PreparedEnemyCardUnitPlan> children = [];
        int maximumAttempts = checked(state.GetReplayCount(card.InstanceKey) + 1);
        for (int replayIndex = 0; replayIndex < maximumAttempts; replayIndex++)
        {
            if (!TryBuildUnit(
                    card,
                    rootSourceKey,
                    replayIndex,
                    EnemyPreparedExecutionMode.Normal,
                    state,
                    random,
                    session,
                    out PreparedEnemyCardUnitPlan? child,
                    out _))
            {
                break;
            }

            children.Add(child!);
        }

        if (children.Count == 0)
        {
            throw new InvalidOperationException($"即时牌 {card.InstanceKey} 在准备阶段首个单元素材不足，不能冻结半成品计划。 ");
        }

        session.EffectiveCardLedger.Complete(card.InstanceKey, anyUnitSucceeded: true);
        return children.AsReadOnly();
    }

    /// <summary>区分不会生成冻结单元的两种正常规划边界。</summary>
    private enum UnitPlanFailure
    {
        None,
        MaterialShortfall,
        ConditionRejected
    }

    /// <summary>
    /// 冻结来源牌本体生成收藏品的定义选择与预期序号。
    /// </summary>
    /// <param name="source">实际执行牌。</param>
    /// <param name="state">候选事务状态。</param>
    /// <param name="random">唯一战斗随机源。</param>
    /// <param name="session">递归与步骤预算。</param>
    /// <returns>零到多个收藏品生成步骤。</returns>
    private static IReadOnlyList<PreparedGeneratedCollectionStep> BuildGeneratedCollectionSteps(
        BaseEnemyCard source,
        EnemyPreparedPlanningState state,
        IEnemyCardRandomSource random,
        PlanningSession session)
    {
        if (!source.Definition.Tags.HasFlag(EnemyCardTag.CollectionGenerator))
        {
            return [];
        }

        List<EnemyCollectionDefinition> definitions = [];
        IEnemyFrozenCollectionGenerationEffect[] generators = source.Definition.Effects
            .OfType<IEnemyFrozenCollectionGenerationEffect>()
            .ToArray();
        if (generators.Length > 0)
        {
            foreach (IEnemyFrozenCollectionGenerationEffect generator in generators)
            {
                definitions.AddRange(generator.FreezeCollections(state, random));
            }
        }
        else if (source.CardModel is WhyPlayHaruhikage)
        {
            List<EnemyCollectionDefinition> pool = CardIntentTestCollectionCatalog.Catalog.Definitions.ToList();
            for (int index = 0; index < Math.Min(2, pool.Count); index++)
            {
                definitions.Add(pool[random.NextIndex(pool.Count)]);
                pool.Remove(definitions[^1]);
            }
        }
        else if (source.CardModel is HopeOnTheVoice)
        {
            definitions.Add(CardIntentTestCollectionCatalog.Catalog.GetRequired(
                CardIntentTestCollectionCatalog.MidnightCoffeeId));
        }
        else if (source.CardModel is Woodlouse)
        {
            definitions.Add(CardIntentTestCollectionCatalog.Catalog.GetRequired(
                CardIntentTestCollectionCatalog.BrokenNoteId));
        }

        List<PreparedGeneratedCollectionStep> steps = [];
        foreach (EnemyCollectionDefinition definition in definitions)
        {
            session.Step();
            long expected = state.CollectionInventory.NextSequence;
            state.CollectionInventory.Append(definition);
            steps.Add(new PreparedGeneratedCollectionStep(definition.CollectionId, expected));
        }

        return steps.AsReadOnly();
    }

    /// <summary>
    /// 冻结作词结果的现有增层或预计生成实例，并按时机递归规划。
    /// </summary>
    /// <param name="source">产生结果的来源牌。</param>
    /// <param name="resultId">作词结果定义标识。</param>
    /// <param name="rootSourceKey">公开根来源实例键。</param>
    /// <param name="state">候选事务状态。</param>
    /// <param name="random">唯一战斗随机源。</param>
    /// <param name="session">递归与步骤预算。</param>
    /// <returns>完整作词结果步骤。</returns>
    private PreparedComposeResultStep BuildComposeStep(
        BaseEnemyCard source,
        EnemyCardId resultId,
        EnemyCardInstanceKey rootSourceKey,
        EnemyPreparedPlanningState state,
        IEnemyCardRandomSource random,
        PlanningSession session)
    {
        session.Step();
        BaseEnemyCard? existing = state.CurrentCards
            .Concat(state.RetainedCards)
            .Concat(state.DrawPile)
            .Concat(state.DiscardPile)
            .FirstOrDefault(card => card.CardId == resultId);
        if (existing is not null)
        {
            state.IncreaseReplay(existing.InstanceKey);
            return new PreparedComposeResultStep(
                resultId,
                existing.InstanceKey,
                source.Definition.TokenTiming,
                increasesExistingReplay: true,
                immediateChild: null);
        }

        EnemyCardZone destination = source.Definition.TokenTiming == EnemyCardTokenTiming.Immediate
            ? EnemyCardZone.Current
            : EnemyCardZone.Retained;
        BaseEnemyCard generated = state.AddGeneratedCard(resultId, destination);
        PreparedEnemyCardUnitPlan? child = null;
        if (destination == EnemyCardZone.Current)
        {
            IReadOnlyList<PreparedEnemyCardUnitPlan> children = BuildRequiredImmediateChildren(
                generated, rootSourceKey, state, random, session);
            child = children[0];
            ApplySourceLifecycle(state, generated, successful: true, immediateFailure: true);
            return new PreparedComposeResultStep(
                resultId,
                generated.InstanceKey,
                source.Definition.TokenTiming,
                increasesExistingReplay: false,
                child,
                children.Skip(1));
        }

        return new PreparedComposeResultStep(
            resultId,
            generated.InstanceKey,
            source.Definition.TokenTiming,
            increasesExistingReplay: false,
            child);
    }

    /// <summary>
    /// 在候选事务中应用与执行引擎相同的最终来源生命周期。
    /// </summary>
    /// <param name="state">候选事务状态。</param>
    /// <param name="source">待移动来源牌。</param>
    /// <param name="successful">是否至少成功一个单元。</param>
    /// <param name="immediateFailure">即时牌失败时是否强制弃置。</param>
    private static void ApplySourceLifecycle(
        EnemyPreparedPlanningState state,
        BaseEnemyCard source,
        bool successful,
        bool immediateFailure)
    {
        if (!state.IsInSourceZone(source.InstanceKey))
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
    /// 把候选当前牌转换为素材解析只读视图。
    /// </summary>
    /// <param name="card">候选当前牌实例。</param>
    /// <returns>包含类型、灵感与灵光资格的素材候选。</returns>
    private static EnemyMaterialCandidate ToMaterialCandidate(BaseEnemyCard card) =>
        EnemyMaterialCandidate.FromHand(
            card,
            card.CardModel is BaseCardModel model && model.IsInspiration,
            card.CardModel.Keywords.Contains(CustomKeyWord.Epiphany));

    /// <summary>
    /// 约束单次候选规划的递归循环和总步骤数量。
    /// </summary>
    private sealed class PlanningSession
    {
        private readonly int _stepLimit;
        private readonly HashSet<EnemyCardInstanceKey> _activeCards = [];
        private int _stepCount;

        /// <summary>
        /// 创建具有正步骤上限的规划会话。
        /// </summary>
        /// <param name="stepLimit">总步骤上限。</param>
        public PlanningSession(int stepLimit, EnemyEffectiveCardLedger effectiveCardLedger)
        {
            _stepLimit = stepLimit;
            EffectiveCardLedger = effectiveCardLedger ?? throw new ArgumentNullException(nameof(effectiveCardLedger));
        }

        /// <summary>获取整个候选共享的逐实例有效牌账本。</summary>
        public EnemyEffectiveCardLedger EffectiveCardLedger { get; }

        /// <summary>
        /// 进入一个递归卡牌单元并拒绝活动路径循环。
        /// </summary>
        /// <param name="key">实际执行卡牌实例键。</param>
        public void Enter(EnemyCardInstanceKey key)
        {
            Step();
            if (!_activeCards.Add(key))
            {
                throw new InvalidOperationException($"准备计划检测到递归卡牌循环 {key}。 ");
            }
        }

        /// <summary>
        /// 离开一个已经构造完毕的递归卡牌单元。
        /// </summary>
        /// <param name="key">实际执行卡牌实例键。</param>
        public void Exit(EnemyCardInstanceKey key) => _activeCards.Remove(key);

        /// <summary>
        /// 消耗一个有限计划步骤并在越界前终止候选。
        /// </summary>
        public void Step()
        {
            _stepCount++;
            if (_stepCount > _stepLimit)
            {
                throw new InvalidOperationException($"准备递归计划超过步骤上限 {_stepLimit}。 ");
            }
        }
    }
}
