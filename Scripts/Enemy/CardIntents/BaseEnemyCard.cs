using System.Threading;
using MegaCrit.Sts2.Core.Models;

namespace STS2_Tomorin_Mod.Enemy.CardIntents;

/// <summary>
/// 指定敌人卡牌自定义效果相对于统一基础攻防的执行时机。
/// </summary>
public enum EnemyCardCustomExecutionTiming
{
    /// <summary>先执行子类自定义效果，再执行基础攻击与防御。</summary>
    BeforeBaseEffects,

    /// <summary>先执行基础攻击与防御，再执行子类自定义效果。</summary>
    AfterBaseEffects
}

/// <summary>
/// 为可显示且可执行的敌人卡牌提供稳定身份、牌面原型和不可绕过的执行模板。
/// </summary>
public abstract class BaseEnemyCard
{
    private int _executionGate;
    private EnemyCardInstanceKey? _instanceKey;
    private EnemyCardPhase? _sourcePhase;

    /// <summary>
    /// 创建一张无内部计数状态的敌人卡牌实例。
    /// </summary>
    /// <param name="cardId">卡牌定义的稳定标识。</param>
    /// <param name="cardModel">仅用于本地化和原版 NCard 渲染的只读原型。</param>
    /// <param name="atk">本牌的一次基础攻击贡献。</param>
    /// <param name="def">本牌对怪物自身的基础防御贡献。</param>
    /// <param name="customExecutionTiming">子类自定义效果相对基础攻防的时机。</param>
    protected BaseEnemyCard(
        EnemyCardId cardId,
        CardModel cardModel,
        decimal atk = 0m,
        decimal def = 0m,
        EnemyCardCustomExecutionTiming customExecutionTiming = EnemyCardCustomExecutionTiming.AfterBaseEffects)
        : this(new EnemyCardDefinition(
            cardId,
            cardModel,
            (atk > 0m ? EnemyCardTag.Attack : EnemyCardTag.None) |
            (def > 0m ? EnemyCardTag.Defense : EnemyCardTag.None),
            new EnemyCardScoreProfile(attack: atk, block: def),
            customExecutionTiming: customExecutionTiming))
    {
    }

    /// <summary>
    /// 从不可变语义定义创建尚未绑定战斗身份的敌人卡牌实例。
    /// </summary>
    /// <param name="definition">跨实例共享的完整语义定义。</param>
    protected BaseEnemyCard(EnemyCardDefinition definition)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        Atk = definition.ScoreProfile.Attack;
        Def = definition.ScoreProfile.Block;
    }

    /// <summary>获取跨实例共享的不可变语义定义。</summary>
    public EnemyCardDefinition Definition { get; }

    /// <summary>获取用于注册、牌堆快照和显式恢复的稳定卡牌标识。</summary>
    public EnemyCardId CardId => Definition.CardId;

    /// <summary>获取只用于牌面及本地化显示的原版卡牌模型原型。</summary>
    public CardModel CardModel => Definition.CardModel;

    /// <summary>获取只用于敌人 Intent 卡面的可信富文本描述覆写。</summary>
    public string DescriptionOverride => Definition.DescriptionOverride;

    /// <summary>获取本牌的一次基础攻击贡献。</summary>
    public decimal Atk { get; }

    /// <summary>获取本牌对怪物自身的基础防御贡献。</summary>
    public decimal Def { get; }

    /// <summary>获取子类自定义效果相对统一基础攻防的执行时机。</summary>
    public EnemyCardCustomExecutionTiming CustomExecutionTiming => Definition.CustomExecutionTiming;

    /// <summary>获取定义级跨阶段保留契约；该值不复制进实例可变状态。</summary>
    public bool CarryAcrossPhase => Definition.CarryAcrossPhase;

    /// <summary>获取定义级效果分类；该值不扩展规划 Tag。</summary>
    public EnemyCardEffectClass EffectClasses => Definition.EffectClasses;

    /// <summary>获取本实例创建时的活跃内容阶段。</summary>
    public EnemyCardPhase SourcePhase => _sourcePhase ?? throw new InvalidOperationException(
        $"敌人卡牌 {CardId} 尚未绑定创建阶段。");

    /// <summary>获取初始模板槽位；战斗生成牌返回空。</summary>
    public int? TemplateSlot { get; private set; }

    /// <summary>获取战斗生成实例的单调序号；初始牌返回空。</summary>
    public long? RuntimeInstanceId { get; private set; }

    /// <summary>获取跨五牌区、计划和重连保持不变的唯一实例键。</summary>
    public EnemyCardInstanceKey InstanceKey => _instanceKey ?? throw new InvalidOperationException(
        $"敌人卡牌 {CardId} 尚未绑定模板槽位或运行时实例身份。");

    /// <summary>获取本牌额外重放次数；总最大尝试次数为一加该值。</summary>
    public int ReplayCount { get; private set; }

    /// <summary>
    /// 永久增加一次本实例的重放标记。
    /// </summary>
    public void IncreaseReplayCount()
    {
        if (ReplayCount == int.MaxValue)
        {
            throw new InvalidOperationException($"敌人卡牌 {InstanceKey} 的重放计数已达到上限。");
        }

        ReplayCount++;
    }

    /// <summary>
    /// 在当前版本重连 DTO 已通过全量校验后恢复重放次数，不触发任何执行或随机行为。
    /// </summary>
    /// <param name="replayCount">必须为非负数的权威重放次数。</param>
    internal void RestoreReplayCount(int replayCount)
    {
        if (replayCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(replayCount), "恢复的重放次数不能为负数。");
        }

        ReplayCount = replayCount;
    }

    /// <summary>
    /// 由注册表为初始牌绑定唯一模板槽位，禁止重复绑定或改绑。
    /// </summary>
    /// <param name="templateSlot">从零开始的模板槽位。</param>
    internal void AssignTemplateSlot(int templateSlot)
    {
        EnsureIdentityUnbound();
        TemplateSlot = templateSlot;
        _instanceKey = EnemyCardInstanceKey.FromTemplateSlot(templateSlot);
    }

    /// <summary>
    /// 由战斗状态为生成牌绑定唯一运行时序号，禁止重复绑定或改绑。
    /// </summary>
    /// <param name="runtimeInstanceId">战斗内单调递增序号。</param>
    internal void AssignRuntimeInstanceId(long runtimeInstanceId)
    {
        EnsureIdentityUnbound();
        RuntimeInstanceId = runtimeInstanceId;
        _instanceKey = EnemyCardInstanceKey.FromRuntimeInstanceId(runtimeInstanceId);
    }

    /// <summary>
    /// 在初始阶段来源或战斗生成实例创建时一次性绑定来源阶段。
    /// </summary>
    internal void AssignSourcePhase(EnemyCardPhase phase)
    {
        if (!Enum.IsDefined(phase))
        {
            throw new ArgumentOutOfRangeException(nameof(phase), phase, "未知敌人卡牌来源阶段。");
        }

        if (_sourcePhase.HasValue)
        {
            throw new InvalidOperationException(
                $"敌人卡牌 {CardId} 已绑定来源阶段 {_sourcePhase.Value}，禁止改绑。");
        }

        _sourcePhase = phase;
    }

    /// <summary>
    /// 确保当前实例尚未归属于任何模板槽位或运行时序号。
    /// </summary>
    private void EnsureIdentityUnbound()
    {
        if (_instanceKey is not null || TemplateSlot is not null || RuntimeInstanceId is not null)
        {
            throw new InvalidOperationException($"敌人卡牌 {CardId} 已经绑定实例身份，禁止重复归属。");
        }
    }

    /// <summary>
    /// 按固定模板执行自定义前置、基础攻击、基础防御和自定义后置效果。
    /// </summary>
    /// <param name="context">与当前状态和怪物绑定的执行上下文。</param>
    /// <returns>整张敌人卡牌效果完成任务。</returns>
    public async Task ExecuteAsync(EnemyCardExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!ReferenceEquals(context.Owner, context.State.Owner) || !context.State.IsExecuting)
        {
            throw new InvalidOperationException("敌人卡牌只能由拥有它的正在执行中的 CardIntentMoveState 调用。");
        }

        if (Interlocked.CompareExchange(ref _executionGate, 1, 0) != 0)
        {
            throw new InvalidOperationException($"敌人卡牌 {CardId} 正在执行，禁止重入。");
        }

        try
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            if (context.ShouldStop)
            {
                return;
            }

            if (CustomExecutionTiming == EnemyCardCustomExecutionTiming.BeforeBaseEffects)
            {
                await CustomExecuteAsync(context);
            }

            if (context.ShouldStop)
            {
                return;
            }

            if (Atk > 0m)
            {
                await context.ExecuteAttackAsync(Atk);
            }

            if (context.ShouldStop)
            {
                return;
            }

            if (Def > 0m)
            {
                await context.ExecuteDefendAsync(Def);
            }

            if (context.ShouldStop)
            {
                return;
            }

            if (CustomExecutionTiming == EnemyCardCustomExecutionTiming.AfterBaseEffects)
            {
                await CustomExecuteAsync(context);
            }
        }
        finally
        {
            Volatile.Write(ref _executionGate, 0);
        }
    }

    /// <summary>
    /// 执行子类额外效果；默认不产生效果，且不得直接修改状态的三类牌堆。
    /// </summary>
    /// <param name="context">当前卡牌执行上下文。</param>
    /// <returns>自定义效果完成任务。</returns>
    protected virtual Task CustomExecuteAsync(EnemyCardExecutionContext context) => Task.CompletedTask;
}
