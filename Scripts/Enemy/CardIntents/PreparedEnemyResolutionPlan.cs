namespace STS2_Tomorin_Mod.Enemy.CardIntents;

/// <summary>
/// 指定冻结单元采用完整结算还是只执行直接效果。
/// </summary>
public enum EnemyPreparedExecutionMode
{
    /// <summary>支付素材并允许递归结果的完整结算。</summary>
    Normal,

    /// <summary>灵感素材专用，只执行直接效果与收藏品生成。</summary>
    ControlledDirectOnly
}

/// <summary>
/// 指定回收步骤选择的是消耗牌还是已消耗收藏品。
/// </summary>
public enum EnemyPreparedRecoveryKind
{
    /// <summary>从消耗牌区回收并立即结算一张牌。</summary>
    Card,

    /// <summary>把一件已消耗收藏品恢复到可用队列。</summary>
    Collection
}

/// <summary>
/// 保存一次成功重放的完整不可变深度优先结算单元。
/// </summary>
public sealed record PreparedEnemyCardUnitPlan
{
    /// <summary>
    /// 创建经过身份、模式与步骤组合校验的冻结单元。
    /// </summary>
    /// <param name="rootSourceKey">公开卡列中的根来源实例键。</param>
    /// <param name="executingCardKey">本单元实际执行的实例键。</param>
    /// <param name="executingCardId">本单元实际执行的定义标识。</param>
    /// <param name="replayIndex">实际执行牌从零开始的重放索引。</param>
    /// <param name="mode">完整或受控直接执行模式。</param>
    /// <param name="materialReservations">本单元冻结的完整素材预留。</param>
    /// <param name="orderedSteps">严格按深度优先执行顺序排列的步骤。</param>
    public PreparedEnemyCardUnitPlan(
        EnemyCardInstanceKey rootSourceKey,
        EnemyCardInstanceKey executingCardKey,
        EnemyCardId executingCardId,
        int replayIndex,
        EnemyPreparedExecutionMode mode,
        IEnumerable<EnemyMaterialReservation> materialReservations,
        IEnumerable<PreparedEnemyResolutionStep> orderedSteps)
    {
        RootSourceKey = rootSourceKey ?? throw new ArgumentNullException(nameof(rootSourceKey));
        ExecutingCardKey = executingCardKey ?? throw new ArgumentNullException(nameof(executingCardKey));
        if (!executingCardId.IsValid)
        {
            throw new ArgumentException("冻结单元必须携带有效执行牌定义标识。", nameof(executingCardId));
        }

        if (replayIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(replayIndex));
        }

        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode));
        }

        ArgumentNullException.ThrowIfNull(materialReservations);
        ArgumentNullException.ThrowIfNull(orderedSteps);
        EnemyMaterialReservation[] reservations = materialReservations.ToArray();
        PreparedEnemyResolutionStep[] steps = orderedSteps.ToArray();
        if (reservations.Any(item => item is null || !item.IsComplete))
        {
            throw new ArgumentException("冻结单元只能包含非空且完整的素材预留。", nameof(materialReservations));
        }

        if (steps.Any(item => item is null))
        {
            throw new ArgumentException("冻结单元不能包含空步骤。", nameof(orderedSteps));
        }

        if (mode == EnemyPreparedExecutionMode.ControlledDirectOnly &&
            (reservations.Length != 0 || steps.Any(IsRecursivePaymentOrComposeStep)))
        {
            throw new ArgumentException("受控直接执行单元不能携带素材预留、素材支付或作词结果步骤。", nameof(orderedSteps));
        }

        ExecutingCardId = executingCardId;
        ReplayIndex = replayIndex;
        Mode = mode;
        MaterialReservations = Array.AsReadOnly(reservations);
        OrderedSteps = Array.AsReadOnly(steps);
    }

    /// <summary>获取公开卡列中的根来源实例键。</summary>
    public EnemyCardInstanceKey RootSourceKey { get; }

    /// <summary>获取本单元实际执行的卡牌实例键。</summary>
    public EnemyCardInstanceKey ExecutingCardKey { get; }

    /// <summary>获取本单元实际执行的卡牌定义标识。</summary>
    public EnemyCardId ExecutingCardId { get; }

    /// <summary>获取实际执行牌从零开始的重放索引。</summary>
    public int ReplayIndex { get; }

    /// <summary>获取完整或受控直接执行模式。</summary>
    public EnemyPreparedExecutionMode Mode { get; }

    /// <summary>获取本单元不可修改的完整素材预留。</summary>
    public IReadOnlyList<EnemyMaterialReservation> MaterialReservations { get; }

    /// <summary>获取严格保持构造顺序的不可修改步骤。</summary>
    public IReadOnlyList<PreparedEnemyResolutionStep> OrderedSteps { get; }

    /// <summary>
    /// 判断步骤是否属于受控直接执行禁止携带的递归支付或作词类型。
    /// </summary>
    /// <param name="step">待检查步骤。</param>
    /// <returns>步骤会触发素材或作词递归时为真。</returns>
    private static bool IsRecursivePaymentOrComposeStep(PreparedEnemyResolutionStep step) =>
        step is PreparedConsumedCardStep or PreparedConsumedCollectionStep or PreparedComposeResultStep;
}

/// <summary>
/// 表示冻结单元中的一个显式深度优先结算步骤。
/// </summary>
public abstract record PreparedEnemyResolutionStep;

/// <summary>
/// 保存一次卡牌或收藏品的有序直接效果程序。
/// </summary>
public sealed record PreparedDirectEffectsStep : PreparedEnemyResolutionStep
{
    /// <summary>
    /// 创建直接效果步骤并复制程序标识集合。
    /// </summary>
    /// <param name="effectProgramIds">与定义效果顺序一致的非空程序标识。</param>
    public PreparedDirectEffectsStep(IEnumerable<string> effectProgramIds)
    {
        ArgumentNullException.ThrowIfNull(effectProgramIds);
        string[] ids = effectProgramIds.ToArray();
        if (ids.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("直接效果程序标识不能为空。", nameof(effectProgramIds));
        }

        EffectProgramIds = Array.AsReadOnly(ids);
    }

    /// <summary>获取按定义顺序冻结的直接效果程序标识。</summary>
    public IReadOnlyList<string> EffectProgramIds { get; }
}

/// <summary>
/// 保存被消耗卡牌及其可选灵感受控子单元。
/// </summary>
public sealed record PreparedConsumedCardStep : PreparedEnemyResolutionStep
{
    /// <summary>
    /// 创建卡牌素材消费步骤。
    /// </summary>
    /// <param name="materialKey">预期位于当前牌区的素材实例键。</param>
    /// <param name="controlledChild">素材具有灵感时的受控直接子单元。</param>
    public PreparedConsumedCardStep(
        EnemyCardInstanceKey materialKey,
        PreparedEnemyCardUnitPlan? controlledChild)
    {
        MaterialKey = materialKey ?? throw new ArgumentNullException(nameof(materialKey));
        if (controlledChild is not null &&
            (controlledChild.ExecutingCardKey != materialKey ||
             controlledChild.Mode != EnemyPreparedExecutionMode.ControlledDirectOnly))
        {
            throw new ArgumentException("灵感子单元必须以被消费素材为执行牌并采用受控直接模式。", nameof(controlledChild));
        }

        ControlledChild = controlledChild;
    }

    /// <summary>获取被消费素材的稳定实例键。</summary>
    public EnemyCardInstanceKey MaterialKey { get; }

    /// <summary>获取灵感素材的可选受控直接子单元。</summary>
    public PreparedEnemyCardUnitPlan? ControlledChild { get; }
}

/// <summary>
/// 保存被消费收藏品及其冻结效果子步骤。
/// </summary>
public sealed record PreparedConsumedCollectionStep : PreparedEnemyResolutionStep
{
    /// <summary>
    /// 创建收藏品素材消费步骤并复制效果子步骤。
    /// </summary>
    /// <param name="collectionInstanceId">预期位于可用队列的收藏品实例标识。</param>
    /// <param name="collectionId">收藏品定义标识。</param>
    /// <param name="children">收藏品效果的有序冻结子步骤。</param>
    public PreparedConsumedCollectionStep(
        string collectionInstanceId,
        string collectionId,
        IEnumerable<PreparedEnemyResolutionStep> children)
    {
        if (string.IsNullOrWhiteSpace(collectionInstanceId))
        {
            throw new ArgumentException("收藏品实例标识不能为空。", nameof(collectionInstanceId));
        }

        if (string.IsNullOrWhiteSpace(collectionId))
        {
            throw new ArgumentException("收藏品定义标识不能为空。", nameof(collectionId));
        }

        ArgumentNullException.ThrowIfNull(children);
        PreparedEnemyResolutionStep[] copied = children.ToArray();
        if (copied.Any(item => item is null))
        {
            throw new ArgumentException("收藏品效果不能包含空子步骤。", nameof(children));
        }

        CollectionInstanceId = collectionInstanceId;
        CollectionId = collectionId;
        Children = Array.AsReadOnly(copied);
    }

    /// <summary>获取被消费收藏品实例标识。</summary>
    public string CollectionInstanceId { get; }

    /// <summary>获取被消费收藏品定义标识。</summary>
    public string CollectionId { get; }

    /// <summary>获取收藏品效果的不可修改子步骤。</summary>
    public IReadOnlyList<PreparedEnemyResolutionStep> Children { get; }
}

/// <summary>
/// 保存一次生成收藏品的定义与预期单调序号。
/// </summary>
public sealed record PreparedGeneratedCollectionStep : PreparedEnemyResolutionStep
{
    /// <summary>
    /// 创建收藏品生成步骤。
    /// </summary>
    /// <param name="collectionId">待生成收藏品定义标识。</param>
    /// <param name="expectedSequence">执行时必须匹配的下一收藏品序号。</param>
    public PreparedGeneratedCollectionStep(string collectionId, long expectedSequence)
    {
        if (string.IsNullOrWhiteSpace(collectionId))
        {
            throw new ArgumentException("生成收藏品定义标识不能为空。", nameof(collectionId));
        }

        if (expectedSequence < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(expectedSequence));
        }

        CollectionId = collectionId;
        ExpectedSequence = expectedSequence;
    }

    /// <summary>获取待生成收藏品定义标识。</summary>
    public string CollectionId { get; }

    /// <summary>获取执行时必须匹配的下一收藏品序号。</summary>
    public long ExpectedSequence { get; }
}

/// <summary>
/// 保存作词结果的稳定实例身份、加入时机与可选即时子单元。
/// </summary>
public sealed record PreparedComposeResultStep : PreparedEnemyResolutionStep
{
    /// <summary>
    /// 创建经过生成或增层组合校验的作词结果步骤。
    /// </summary>
    /// <param name="resultCardId">作词结果定义标识。</param>
    /// <param name="resultInstanceKey">现有或预计生成实例键。</param>
    /// <param name="timing">即时或下回合保留时机。</param>
    /// <param name="increasesExistingReplay">是否复用现有实例并仅增加重放。</param>
    /// <param name="immediateChild">新生成即时结果的递归子单元。</param>
    public PreparedComposeResultStep(
        EnemyCardId resultCardId,
        EnemyCardInstanceKey resultInstanceKey,
        EnemyCardTokenTiming timing,
        bool increasesExistingReplay,
        PreparedEnemyCardUnitPlan? immediateChild,
        IEnumerable<PreparedEnemyCardUnitPlan>? additionalReplayUnits = null)
    {
        if (!resultCardId.IsValid)
        {
            throw new ArgumentException("作词结果必须携带有效卡牌定义标识。", nameof(resultCardId));
        }

        ResultInstanceKey = resultInstanceKey ?? throw new ArgumentNullException(nameof(resultInstanceKey));
        if (timing == EnemyCardTokenTiming.None || !Enum.IsDefined(timing))
        {
            throw new ArgumentOutOfRangeException(nameof(timing));
        }

        PreparedEnemyCardUnitPlan[] additional = (additionalReplayUnits ?? []).ToArray();
        if (additional.Any(unit => unit is null))
        {
            throw new ArgumentException("作词结果不能包含空的附加重放单元。", nameof(additionalReplayUnits));
        }

        if (increasesExistingReplay && (immediateChild is not null || additional.Length != 0))
        {
            throw new ArgumentException("现有作词结果增层时不能同时执行即时子单元。", nameof(immediateChild));
        }

        if (timing == EnemyCardTokenTiming.RetainedNextTurn && immediateChild is not null)
        {
            throw new ArgumentException("下回合保留结果不能携带即时子单元。", nameof(immediateChild));
        }

        if (!increasesExistingReplay && timing == EnemyCardTokenTiming.Immediate && immediateChild is null)
        {
            throw new ArgumentException("新生成的即时作词结果必须携带递归子单元。", nameof(immediateChild));
        }

        if (immediateChild is not null &&
            (immediateChild.ExecutingCardKey != resultInstanceKey || immediateChild.ExecutingCardId != resultCardId))
        {
            throw new ArgumentException("即时作词子单元必须匹配结果实例与定义标识。", nameof(immediateChild));
        }

        ValidateAdditionalReplayUnits(immediateChild, additional, nameof(additionalReplayUnits));

        ResultCardId = resultCardId;
        Timing = timing;
        IncreasesExistingReplay = increasesExistingReplay;
        ImmediateChild = immediateChild;
        AdditionalReplayUnits = Array.AsReadOnly(additional);
    }

    /// <summary>获取作词结果定义标识。</summary>
    public EnemyCardId ResultCardId { get; }

    /// <summary>获取现有或预计生成的稳定实例键。</summary>
    public EnemyCardInstanceKey ResultInstanceKey { get; }

    /// <summary>获取即时或下回合保留时机。</summary>
    public EnemyCardTokenTiming Timing { get; }

    /// <summary>获取是否只增加现有实例重放。</summary>
    public bool IncreasesExistingReplay { get; }

    /// <summary>获取新生成即时结果的可选递归子单元。</summary>
    public PreparedEnemyCardUnitPlan? ImmediateChild { get; }

    /// <summary>获取即时结果在首个单元后的连续附加重放单元。</summary>
    public IReadOnlyList<PreparedEnemyCardUnitPlan> AdditionalReplayUnits { get; }

    /// <summary>
    /// 验证附加单元与首单元身份一致且重放索引连续。
    /// </summary>
    /// <param name="first">可选首单元。</param>
    /// <param name="additional">附加重放单元。</param>
    /// <param name="parameterName">异常参数名。</param>
    private static void ValidateAdditionalReplayUnits(
        PreparedEnemyCardUnitPlan? first,
        IReadOnlyList<PreparedEnemyCardUnitPlan> additional,
        string parameterName)
    {
        if (additional.Count == 0)
        {
            return;
        }

        if (first is null || additional.Any(unit => unit is null) || additional.Select(unit => unit.ReplayIndex)
                .SequenceEqual(Enumerable.Range(1, additional.Count)) == false ||
            additional.Any(unit => unit.RootSourceKey != first.RootSourceKey ||
                                   unit.ExecutingCardKey != first.ExecutingCardKey ||
                                   unit.ExecutingCardId != first.ExecutingCardId ||
                                   unit.Mode != first.Mode))
        {
            throw new ArgumentException("附加重放单元必须与首单元身份一致且索引从一连续递增。", parameterName);
        }
    }
}

/// <summary>
/// 保存准备阶段选中的即时抽牌实例及其递归子单元。
/// </summary>
public sealed record PreparedImmediateCardStep : PreparedEnemyResolutionStep
{
    /// <summary>
    /// 创建即时抽牌步骤并校验子单元身份。
    /// </summary>
    /// <param name="selectedCardKey">准备阶段选中的稳定卡牌实例键。</param>
    /// <param name="child">被选卡牌的完整递归单元。</param>
    public PreparedImmediateCardStep(
        EnemyCardInstanceKey selectedCardKey,
        PreparedEnemyCardUnitPlan child,
        IEnumerable<PreparedEnemyCardUnitPlan>? additionalReplayUnits = null)
    {
        SelectedCardKey = selectedCardKey ?? throw new ArgumentNullException(nameof(selectedCardKey));
        Child = child ?? throw new ArgumentNullException(nameof(child));
        if (child.ExecutingCardKey != selectedCardKey)
        {
            throw new ArgumentException("即时抽牌子单元必须执行被选择的实例。", nameof(child));
        }


        PreparedEnemyCardUnitPlan[] additional = (additionalReplayUnits ?? []).ToArray();
        if (additional.Any(unit => unit is null) ||
            !additional.Select(unit => unit.ReplayIndex).SequenceEqual(Enumerable.Range(1, additional.Length)) ||
            additional.Any(unit =>
                                   unit.RootSourceKey != child.RootSourceKey ||
                                   unit.ExecutingCardKey != child.ExecutingCardKey ||
                                   unit.ExecutingCardId != child.ExecutingCardId ||
                                   unit.Mode != child.Mode))
        {
            throw new ArgumentException("即时牌附加重放单元必须与首单元身份一致且索引连续。", nameof(additionalReplayUnits));
        }

        AdditionalReplayUnits = Array.AsReadOnly(additional);
    }

    /// <summary>获取准备阶段选中的稳定卡牌实例键。</summary>
    public EnemyCardInstanceKey SelectedCardKey { get; }

    /// <summary>获取被选卡牌的完整递归单元。</summary>
    public PreparedEnemyCardUnitPlan Child { get; }

    /// <summary>获取首个即时单元后的连续附加重放单元。</summary>
    public IReadOnlyList<PreparedEnemyCardUnitPlan> AdditionalReplayUnits { get; }
}

/// <summary>
/// 保存准备阶段从消耗牌或已消耗收藏品中选中的回收结果。
/// </summary>
public sealed record PreparedRecoveryStep : PreparedEnemyResolutionStep
{
    /// <summary>
    /// 创建回收步骤并校验所选类型与子单元组合。
    /// </summary>
    /// <param name="kind">回收卡牌或收藏品。</param>
    /// <param name="selectedInstanceId">稳定卡牌实例键文本或收藏品实例标识。</param>
    /// <param name="immediateCardChild">回收卡牌时立即结算的递归子单元。</param>
    public PreparedRecoveryStep(
        EnemyPreparedRecoveryKind kind,
        string selectedInstanceId,
        PreparedEnemyCardUnitPlan? immediateCardChild,
        IEnumerable<PreparedEnemyCardUnitPlan>? additionalReplayUnits = null)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        if (string.IsNullOrWhiteSpace(selectedInstanceId))
        {
            throw new ArgumentException("回收实例标识不能为空。", nameof(selectedInstanceId));
        }

        if (kind == EnemyPreparedRecoveryKind.Card && immediateCardChild is null)
        {
            throw new ArgumentException("回收卡牌必须携带即时递归子单元。", nameof(immediateCardChild));
        }

        PreparedEnemyCardUnitPlan[] additional = (additionalReplayUnits ?? []).ToArray();
        if (kind == EnemyPreparedRecoveryKind.Collection &&
            (immediateCardChild is not null || additional.Length != 0))
        {
            throw new ArgumentException("回收收藏品不能携带卡牌子单元。", nameof(immediateCardChild));
        }

        if (immediateCardChild is not null &&
            !string.Equals(immediateCardChild.ExecutingCardKey.Value, selectedInstanceId, StringComparison.Ordinal))
        {
            throw new ArgumentException("回收卡牌子单元必须匹配所选实例键。", nameof(immediateCardChild));
        }

        if (additional.Any(unit => unit is null) ||
            !additional.Select(unit => unit.ReplayIndex).SequenceEqual(Enumerable.Range(1, additional.Length)) ||
            additional.Any(unit => immediateCardChild is null ||
                                   unit.RootSourceKey != immediateCardChild.RootSourceKey ||
                                   unit.ExecutingCardKey != immediateCardChild.ExecutingCardKey ||
                                   unit.ExecutingCardId != immediateCardChild.ExecutingCardId ||
                                   unit.Mode != immediateCardChild.Mode))
        {
            throw new ArgumentException("回收卡牌附加重放单元必须与首单元身份一致且索引连续。", nameof(additionalReplayUnits));
        }

        Kind = kind;
        SelectedInstanceId = selectedInstanceId;
        ImmediateCardChild = immediateCardChild;
        AdditionalReplayUnits = Array.AsReadOnly(additional);
    }

    /// <summary>获取回收对象类型。</summary>
    public EnemyPreparedRecoveryKind Kind { get; }

    /// <summary>获取稳定卡牌实例键文本或收藏品实例标识。</summary>
    public string SelectedInstanceId { get; }

    /// <summary>获取回收卡牌时立即结算的递归子单元。</summary>
    public PreparedEnemyCardUnitPlan? ImmediateCardChild { get; }

    /// <summary>获取回收卡牌首单元后的连续附加重放单元。</summary>
    public IReadOnlyList<PreparedEnemyCardUnitPlan> AdditionalReplayUnits { get; }
}
