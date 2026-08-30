namespace STS2_Tomorin_Mod.Enemy.CardIntents;

/// <summary>
/// 保存一次冻结行动中某个实际卡牌实例的有效牌序号、X 次数和计数状态。
/// </summary>
public sealed record EnemyFrozenEffectiveCardState
{
    public EnemyFrozenEffectiveCardState(
        EnemyCardInstanceKey executingCardInstanceKey,
        int frozenN,
        int? frozenX,
        int multiplier,
        bool wasCounted)
    {
        ExecutingCardInstanceKey = executingCardInstanceKey ??
                                   throw new ArgumentNullException(nameof(executingCardInstanceKey));
        if (frozenN < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(frozenN));
        }

        if (multiplier < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(multiplier));
        }

        int expectedX = checked(Math.Max(0, 6 - frozenN) * multiplier);
        if (frozenX is < 0 || frozenX is int value && value != expectedX)
        {
            throw new ArgumentException("冻结 X 必须等于 max(0, 6 - FrozenN) 乘以倍率。", nameof(frozenX));
        }

        ExecutingCardInstanceKey = executingCardInstanceKey;
        FrozenN = frozenN;
        FrozenX = frozenX;
        Multiplier = multiplier;
        WasCounted = wasCounted;
    }

    /// <summary>获取真正执行效果的卡牌实例键。</summary>
    public EnemyCardInstanceKey ExecutingCardInstanceKey { get; init; }

    /// <summary>获取该实例首次开始时已完成的有效牌数量。</summary>
    public int FrozenN { get; init; }

    /// <summary>获取倍率结算后的冻结 X；非 X 牌为空。</summary>
    public int? FrozenX { get; init; }

    /// <summary>获取在首次执行前冻结的次数倍率。</summary>
    public int Multiplier { get; init; }

    /// <summary>获取该实例是否已使行动有效牌计数增加。</summary>
    public bool WasCounted { get; init; }
}

/// <summary>
/// 在一个完整候选中按 DFS 完成顺序冻结实际卡牌实例的 X 元数据。
/// </summary>
public sealed class EnemyEffectiveCardLedger
{
    private readonly Dictionary<EnemyCardInstanceKey, EnemyFrozenEffectiveCardState> _states = [];
    private readonly IReadOnlyDictionary<EnemyCardInstanceKey, EnemyFrozenEffectiveCardState> _view;

    public EnemyEffectiveCardLedger(int initialCount = 0)
    {
        if (initialCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(initialCount));
        }

        CompletedEffectiveCardCount = initialCount;
        _view = new System.Collections.ObjectModel.ReadOnlyDictionary<
            EnemyCardInstanceKey,
            EnemyFrozenEffectiveCardState>(_states);
    }

    /// <summary>获取已完成且至少成功一个执行单元的实际卡牌数。</summary>
    public int CompletedEffectiveCardCount { get; private set; }

    /// <summary>获取按实际执行实例键索引的实时只读冻结状态。</summary>
    public IReadOnlyDictionary<EnemyCardInstanceKey, EnemyFrozenEffectiveCardState> States => _view;

    /// <summary>
    /// 在实际卡牌首个单元之前冻结 N、X 与倍率；同实例后续 Replay 原样复用。
    /// </summary>
    public EnemyFrozenEffectiveCardState Begin(
        EnemyCardInstanceKey key,
        bool isX,
        int multiplier)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (_states.TryGetValue(key, out EnemyFrozenEffectiveCardState? existing))
        {
            return existing;
        }

        if (multiplier < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(multiplier));
        }

        int frozenN = CompletedEffectiveCardCount;
        int? frozenX = isX ? checked(Math.Max(0, 6 - frozenN) * multiplier) : null;
        EnemyFrozenEffectiveCardState created = new(key, frozenN, frozenX, multiplier, wasCounted: false);
        _states.Add(key, created);
        return created;
    }

    /// <summary>
    /// 在该实例的本体与全部 Replay 结束后恰好计数一次。
    /// </summary>
    public void Complete(EnemyCardInstanceKey key, bool anyUnitSucceeded)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (!_states.TryGetValue(key, out EnemyFrozenEffectiveCardState? current))
        {
            throw new InvalidOperationException($"有效牌实例 {key} 尚未开始，不能完成。");
        }

        if (!anyUnitSucceeded || current.WasCounted)
        {
            return;
        }

        _states[key] = current with { WasCounted = true };
        CompletedEffectiveCardCount = checked(CompletedEffectiveCardCount + 1);
    }
}
