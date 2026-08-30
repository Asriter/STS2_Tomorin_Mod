namespace STS2_Tomorin_Mod.Enemy.CardIntents;

/// <summary>
/// 描述一个玩家目标在无副作用模拟中的稳定身份与已知数值修正。
/// </summary>
public sealed record EnemySimulationTarget(
    string TargetId,
    decimal DamageMultiplier,
    decimal DebuffMultiplier);

/// <summary>
/// 为效果节点保存纯内存顺序模拟状态，不调用战斗命令、随机源或未知 Hook。
/// </summary>
public sealed class EnemyCardSimulationContext
{
    private readonly IReadOnlyList<EnemySimulationTarget> _targets;
    private readonly IReadOnlyDictionary<EnemyCardInstanceKey, EnemyFrozenEffectiveCardState> _effectiveCardStates;
    private readonly List<UnitAccumulator> _units = [];
    private readonly Stack<UnitAccumulator> _parentUnits = new();
    private readonly List<string> _diagnostics = [];
    private UnitAccumulator? _current;
    private bool _stepLimitReached;

    /// <summary>
    /// 创建模拟上下文。
    /// </summary>
    /// <param name="targets">全部存活且有效的玩家目标及已知修正。</param>
    /// <param name="stepLimit">本次模拟允许提交的最大原子步骤数。</param>
    public EnemyCardSimulationContext(
        IEnumerable<EnemySimulationTarget> targets,
        int stepLimit,
        IReadOnlyDictionary<EnemyCardInstanceKey, EnemyFrozenEffectiveCardState>? effectiveCardStates = null)
    {
        _targets = Array.AsReadOnly((targets ?? throw new ArgumentNullException(nameof(targets))).ToArray());
        if (_targets.Any(target => string.IsNullOrWhiteSpace(target.TargetId)))
        {
            throw new ArgumentException("模拟目标必须具有非空稳定标识。", nameof(targets));
        }

        if (_targets.Select(target => target.TargetId).Distinct(StringComparer.Ordinal).Count() != _targets.Count)
        {
            throw new ArgumentException("模拟目标稳定标识不能重复。", nameof(targets));
        }

        if (stepLimit < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(stepLimit), "模拟步骤上限必须大于零。");
        }

        StepLimit = stepLimit;
        _effectiveCardStates = effectiveCardStates ??
                               new Dictionary<EnemyCardInstanceKey, EnemyFrozenEffectiveCardState>();
    }

    /// <summary>获取本次模拟的有限步骤上限。</summary>
    public int StepLimit { get; }

    /// <summary>获取已经提交的模拟原子步骤数。</summary>
    public int CommittedStepCount { get; private set; }

    /// <summary>获取模拟是否已经到达有限步骤边界。</summary>
    public bool IsStepLimitReached => _stepLimitReached;

    /// <summary>获取投影是否仍然完整。</summary>
    public bool IsComplete { get; private set; } = true;

    /// <summary>读取当前实际执行实例的冻结有效牌状态。</summary>
    public EnemyFrozenEffectiveCardState GetCurrentEffectiveCardState(bool requireFrozenX = false)
    {
        UnitAccumulator unit = RequireCurrentUnit();
        if (!_effectiveCardStates.TryGetValue(unit.ExecutingCardKey, out EnemyFrozenEffectiveCardState? state) ||
            state.ExecutingCardInstanceKey != unit.ExecutingCardKey ||
            requireFrozenX && state.FrozenX is null)
        {
            throw new InvalidOperationException(
                $"执行牌 {unit.ExecutingCardKey} 缺少完整的冻结有效牌元数据。");
        }

        return state;
    }

    /// <summary>
    /// 开始记录一张来源牌的一次重放。
    /// </summary>
    /// <param name="rootSourceKey">公开卡列中的根来源实例身份。</param>
    /// <param name="executingCardKey">当前真正执行效果的实例身份。</param>
    /// <param name="executingCardId">当前真正执行效果的卡牌定义身份。</param>
    /// <param name="replayIndex">从零开始的重放索引。</param>
    public void BeginUnit(
        EnemyCardInstanceKey rootSourceKey,
        EnemyCardInstanceKey executingCardKey,
        EnemyCardId executingCardId,
        int replayIndex)
    {
        if (replayIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(replayIndex));
        }

        ArgumentNullException.ThrowIfNull(rootSourceKey);
        ArgumentNullException.ThrowIfNull(executingCardKey);
        if (!executingCardId.IsValid)
        {
            throw new ArgumentException("投影单元必须具有完整的根来源与实际执行身份。");
        }

        if (_current is not null)
        {
            _parentUnits.Push(_current);
        }

        _current = new UnitAccumulator(
            rootSourceKey,
            executingCardKey,
            executingCardId,
            replayIndex,
            _targets);
        _units.Add(_current);
    }

    /// <summary>
    /// 向全部模拟目标追加相同基础伤害的若干独立命中。
    /// </summary>
    /// <param name="baseDamage">每次命中的已知基础伤害。</param>
    /// <param name="hitCount">独立命中次数。</param>
    public void AddDamageToAll(decimal baseDamage, int hitCount = 1)
    {
        UnitAccumulator unit = RequireCurrentUnit();
        if (baseDamage < decimal.Zero || hitCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(baseDamage), "伤害不能为负且命中次数不能为负。");
        }

        if (!TryCommitStep())
        {
            return;
        }

        foreach (TargetAccumulator target in unit.Targets.Values)
        {
            for (int index = 0; index < hitCount; index++)
            {
                target.DamageHits.Add(new EnemyDamageHitProjection(
                    baseDamage,
                    baseDamage * target.Input.DamageMultiplier));
            }
        }

    }

    /// <summary>
    /// 向敌人自身累计预计格挡。
    /// </summary>
    /// <param name="amount">预计获得的格挡量。</param>
    public void AddEnemyBlock(decimal amount)
    {
        if (!TryCommitStep())
        {
            return;
        }

        RequireCurrentUnit().EnemyBlock += amount;
    }

    /// <summary>
    /// 向敌人自身累计一个稳定 Power 标识的层数变化。
    /// </summary>
    /// <param name="powerId">Power 稳定标识。</param>
    /// <param name="amount">层数变化。</param>
    public void AddEnemyPower(string powerId, decimal amount)
    {
        if (!TryCommitStep())
        {
            return;
        }

        AddDelta(RequireCurrentUnit().EnemyPowers, powerId, amount);
    }

    /// <summary>
    /// 向全部模拟目标累计一个负面 Power 的层数变化。
    /// </summary>
    /// <param name="powerId">Power 稳定标识。</param>
    /// <param name="amount">未应用目标修正前的层数变化。</param>
    public void AddTargetPowerToAll(string powerId, decimal amount)
    {
        if (!TryCommitStep())
        {
            return;
        }

        foreach (TargetAccumulator target in RequireCurrentUnit().Targets.Values)
        {
            AddDelta(target.PowerDeltas, powerId, amount * target.Input.DebuffMultiplier);
        }
    }

    /// <summary>
    /// 记录收藏品实例在当前执行单元中的消费、生成或恢复。
    /// </summary>
    /// <param name="projection">只包含稳定身份的收藏品变化。</param>
    public void AddCollectionDelta(EnemyCollectionProjection projection)
    {
        ArgumentNullException.ThrowIfNull(projection);
        if (string.IsNullOrWhiteSpace(projection.CollectionInstanceId) ||
            string.IsNullOrWhiteSpace(projection.CollectionId))
        {
            throw new ArgumentException("收藏品投影必须具有完整稳定身份。", nameof(projection));
        }

        if (!TryCommitStep())
        {
            return;
        }

        RequireCurrentUnit().CollectionDeltas.Add(projection);
    }

    /// <summary>
    /// 记录当前执行单元产生或增层的作词结果牌。
    /// </summary>
    /// <param name="projection">包含稳定卡牌身份和生成语义的投影。</param>
    public void AddGeneratedCard(EnemyGeneratedCardProjection projection)
    {
        ArgumentNullException.ThrowIfNull(projection);
        if (projection.CardInstanceKey is null || !projection.CardId.IsValid)
        {
            throw new ArgumentException("生成牌投影必须具有完整稳定身份。", nameof(projection));
        }

        if (!TryCommitStep())
        {
            return;
        }

        RequireCurrentUnit().GeneratedCards.Add(projection);
    }

    /// <summary>
    /// 标记存在未知或可能有副作用的第三方修正器，因此仅提供诊断性结果。
    /// </summary>
    /// <param name="diagnostic">可供 UI 或日志解释不完整原因的文本。</param>
    public void MarkIncomplete(string diagnostic)
    {
        IsComplete = false;
        if (!string.IsNullOrWhiteSpace(diagnostic))
        {
            _diagnostics.Add(diagnostic);
        }
    }

    /// <summary>
    /// 提交当前重放单元，并保留其逐目标命中结构。
    /// </summary>
    public void CommitUnit()
    {
        RequireCurrentUnit();
        _current = _parentUnits.Count > 0 ? _parentUnits.Pop() : null;
    }

    /// <summary>
    /// 构建最终只读投影；存在未提交单元时拒绝生成半成品。
    /// </summary>
    /// <returns>本次顺序模拟结果。</returns>
    public LiveActionProjection BuildProjection()
    {
        if (_current is not null || _parentUnits.Count != 0)
        {
            throw new InvalidOperationException("存在未提交模拟单元，不能生成投影。 ");
        }

        return new LiveActionProjection(
            _units.Select(unit => unit.ToProjection()),
            IsComplete,
            _diagnostics,
            _effectiveCardStates.Values);
    }

    /// <summary>
    /// 获取当前模拟单元，防止效果节点脱离来源牌写入。
    /// </summary>
    /// <returns>当前可写单元。</returns>
    private UnitAccumulator RequireCurrentUnit() =>
        _current ?? throw new InvalidOperationException("必须先开始模拟单元。 ");

    /// <summary>
    /// 提交一个有限原子步骤，超过规则上限时终止模拟。
    /// </summary>
    private bool TryCommitStep()
    {
        if (_stepLimitReached)
        {
            return false;
        }

        if (CommittedStepCount >= StepLimit)
        {
            _stepLimitReached = true;
            MarkIncomplete("实时投影超过有限步骤上限，结果已截断。 ");
            return false;
        }

        CommittedStepCount++;
        return true;
    }

    /// <summary>
    /// 向字典累计一个非空稳定键的变化量。
    /// </summary>
    /// <param name="deltas">待更新字典。</param>
    /// <param name="key">稳定键。</param>
    /// <param name="amount">增量。</param>
    private static void AddDelta(IDictionary<string, decimal> deltas, string key, decimal amount)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Power 标识不能为空。", nameof(key));
        }

        deltas.TryGetValue(key, out decimal previous);
        deltas[key] = previous + amount;
    }

    /// <summary>保存一个尚未提交的来源牌重放投影。</summary>
    private sealed class UnitAccumulator
    {
        /// <summary>创建可写重放投影。</summary>
        public UnitAccumulator(
            EnemyCardInstanceKey rootSourceKey,
            EnemyCardInstanceKey executingCardKey,
            EnemyCardId executingCardId,
            int replayIndex,
            IReadOnlyList<EnemySimulationTarget> targets)
        {
            RootSourceKey = rootSourceKey;
            ExecutingCardKey = executingCardKey;
            ExecutingCardId = executingCardId;
            ReplayIndex = replayIndex;
            Targets = targets.ToDictionary(
                target => target.TargetId,
                target => new TargetAccumulator(target),
                StringComparer.Ordinal);
        }

        /// <summary>获取来源牌身份。</summary>
        public EnemyCardInstanceKey RootSourceKey { get; }

        /// <summary>获取真正执行当前效果的实例身份。</summary>
        public EnemyCardInstanceKey ExecutingCardKey { get; }

        /// <summary>获取真正执行当前效果的定义身份。</summary>
        public EnemyCardId ExecutingCardId { get; }

        /// <summary>获取重放索引。</summary>
        public int ReplayIndex { get; }

        /// <summary>获取逐目标累加器。</summary>
        public Dictionary<string, TargetAccumulator> Targets { get; }

        /// <summary>获取或设置敌人格挡变化。</summary>
        public decimal EnemyBlock { get; set; }

        /// <summary>获取敌人 Power 层数变化。</summary>
        public Dictionary<string, decimal> EnemyPowers { get; } = new(StringComparer.Ordinal);

        /// <summary>获取当前单元的收藏品结构变化。</summary>
        public List<EnemyCollectionProjection> CollectionDeltas { get; } = [];

        /// <summary>获取当前单元产生或增层的作词结果牌。</summary>
        public List<EnemyGeneratedCardProjection> GeneratedCards { get; } = [];

        /// <summary>转换为不可变重放投影。</summary>
        public EnemyCardReplayProjection ToProjection() =>
            new(
                RootSourceKey,
                ExecutingCardKey,
                ExecutingCardId,
                ReplayIndex,
                Targets.Values.Select(target => target.ToProjection()).ToArray(),
                EnemyBlock,
                new Dictionary<string, decimal>(EnemyPowers, StringComparer.Ordinal),
                Array.AsReadOnly(CollectionDeltas.ToArray()),
                Array.AsReadOnly(GeneratedCards.ToArray()));
    }

    /// <summary>保存一个玩家目标的可写模拟结果。</summary>
    private sealed class TargetAccumulator
    {
        /// <summary>创建目标累加器。</summary>
        public TargetAccumulator(EnemySimulationTarget input) => Input = input;

        /// <summary>获取目标输入。</summary>
        public EnemySimulationTarget Input { get; }

        /// <summary>获取逐次命中伤害。</summary>
        public List<EnemyDamageHitProjection> DamageHits { get; } = [];

        /// <summary>获取目标 Power 变化。</summary>
        public Dictionary<string, decimal> PowerDeltas { get; } = new(StringComparer.Ordinal);

        /// <summary>转换为不可变逐目标结果。</summary>
        public EnemyTargetProjection ToProjection() =>
            new(
                Input.TargetId,
                Array.AsReadOnly(DamageHits.ToArray()),
                new Dictionary<string, decimal>(PowerDeltas, StringComparer.Ordinal));
    }
}
