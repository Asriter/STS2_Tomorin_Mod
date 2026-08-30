using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using STS2_Tomorin_Mod.Powers;

namespace STS2_Tomorin_Mod.Enemy.CardIntents;

/// <summary>
/// 封装单张敌人卡牌执行时所需的战斗对象、目标和可替换测试 seam。
/// </summary>
public sealed class EnemyCardExecutionContext
{
    private readonly Stack<EnemyCardInstanceKey> _executingCardStack = new();
    private readonly Func<bool> _shouldStopExecution;
    private readonly Func<decimal, Task>? _attackExecutor;
    private readonly Func<decimal, Task>? _defendExecutor;
    private readonly Func<decimal, int, Task>? _attackAllExecutor;
    private readonly Func<Type, decimal, Task>? _enemyPowerExecutor;
    private readonly Func<Type, decimal, Task>? _targetPowerExecutor;
    private readonly Func<IReadOnlyList<string>, Task>? _collectionPowerExecutor;

    /// <summary>
    /// 创建敌人卡牌执行上下文。
    /// </summary>
    /// <param name="owner">正在执行牌的卡牌 Intent 怪物。</param>
    /// <param name="state">拥有当前冻结手牌的行动状态。</param>
    /// <param name="choiceContext">供原版命令使用的多人选择上下文。</param>
    /// <param name="targets">本次怪物行动收到的目标顺序。</param>
    /// <param name="combatState">当前战斗状态。</param>
    /// <param name="cancellationToken">由调用方提供的协作取消标记。</param>
    /// <param name="shouldStopExecution">额外终止查询；为空时只检查取消标记。</param>
    /// <param name="attackExecutor">测试可替换的攻击步骤；为空时执行原版怪物攻击命令。</param>
    /// <param name="defendExecutor">测试可替换的防御步骤；为空时执行原版获得格挡命令。</param>
    /// <param name="attackAllExecutor">测试可替换的全体多段攻击步骤。</param>
    /// <param name="enemyPowerExecutor">测试可替换的敌人自身 Power 步骤。</param>
    /// <param name="targetPowerExecutor">测试可替换的全体玩家 Power 步骤。</param>
    /// <param name="collectionPowerExecutor">测试可替换的收藏品队列 Power 同步步骤。</param>
    public EnemyCardExecutionContext(
        BaseCardIntentMonsterModel owner,
        CardIntentMoveState state,
        PlayerChoiceContext choiceContext,
        IReadOnlyList<Creature> targets,
        ICombatState combatState,
        CancellationToken cancellationToken = default,
        Func<bool>? shouldStopExecution = null,
        Func<decimal, Task>? attackExecutor = null,
        Func<decimal, Task>? defendExecutor = null,
        Func<decimal, int, Task>? attackAllExecutor = null,
        Func<Type, decimal, Task>? enemyPowerExecutor = null,
        Func<Type, decimal, Task>? targetPowerExecutor = null,
        Func<IReadOnlyList<string>, Task>? collectionPowerExecutor = null)
    {
        Owner = owner ?? throw new ArgumentNullException(nameof(owner));
        State = state ?? throw new ArgumentNullException(nameof(state));
        ChoiceContext = choiceContext ?? throw new ArgumentNullException(nameof(choiceContext));
        Targets = targets ?? throw new ArgumentNullException(nameof(targets));
        CombatState = combatState ?? throw new ArgumentNullException(nameof(combatState));
        CancellationToken = cancellationToken;
        _shouldStopExecution = shouldStopExecution ?? (() => false);
        _attackExecutor = attackExecutor;
        _defendExecutor = defendExecutor;
        _attackAllExecutor = attackAllExecutor;
        _enemyPowerExecutor = enemyPowerExecutor;
        _targetPowerExecutor = targetPowerExecutor;
        _collectionPowerExecutor = collectionPowerExecutor;
    }

    /// <summary>获取正在执行牌的怪物模型。</summary>
    public BaseCardIntentMonsterModel Owner { get; }

    /// <summary>获取拥有冻结行动、五牌区及收藏品数据的行动状态。</summary>
    public CardIntentMoveState State { get; }

    /// <summary>获取供原版异步命令使用的选择上下文。</summary>
    public PlayerChoiceContext ChoiceContext { get; }

    /// <summary>获取本次行动的只读目标集合。</summary>
    public IReadOnlyList<Creature> Targets { get; }

    /// <summary>获取本次执行所属的战斗状态。</summary>
    public ICombatState CombatState { get; }

    /// <summary>获取调用方提供的协作取消标记。</summary>
    public CancellationToken CancellationToken { get; }

    /// <summary>
    /// 获取战斗流程是否要求立即停止后续卡牌命令。
    /// </summary>
    public bool ShouldStop => CancellationToken.IsCancellationRequested || _shouldStopExecution();

    /// <summary>在真实执行单元进入时设置当前实际卡牌实例。</summary>
    internal void PushExecutingCard(EnemyCardInstanceKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        _executingCardStack.Push(key);
    }

    /// <summary>按严格 LIFO 顺序退出真实执行单元。</summary>
    internal void PopExecutingCard(EnemyCardInstanceKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (!_executingCardStack.TryPeek(out EnemyCardInstanceKey? current) || current != key)
        {
            throw new InvalidOperationException("真实执行卡牌上下文必须严格按 LIFO 退出。");
        }

        _executingCardStack.Pop();
    }

    /// <summary>读取当前真正执行实例的冻结有效牌状态。</summary>
    public EnemyFrozenEffectiveCardState GetCurrentEffectiveCardState(bool requireFrozenX = false)
    {
        if (!_executingCardStack.TryPeek(out EnemyCardInstanceKey? key))
        {
            throw new InvalidOperationException("当前没有正在执行的敌人卡牌单元。");
        }

        PreparedEnemyCardAction action = State.CombatState.PreparedAction ??
                                         throw new InvalidOperationException("当前没有冻结行动。");
        if (!action.EffectiveCardStates.TryGetValue(key, out EnemyFrozenEffectiveCardState? frozen) ||
            frozen.ExecutingCardInstanceKey != key ||
            requireFrozenX && frozen.FrozenX is null)
        {
            throw new InvalidOperationException($"执行牌 {key} 缺少完整的冻结有效牌元数据。");
        }

        return frozen;
    }

    /// <summary>
    /// 执行一次具有怪物来源及 <see cref="ValueProp.Move"/> 语义的基础攻击。
    /// </summary>
    /// <param name="amount">单次基础伤害。</param>
    /// <returns>攻击命令完成任务。</returns>
    public Task ExecuteAttackAsync(decimal amount)
    {
        CancellationToken.ThrowIfCancellationRequested();
        if (_attackExecutor is not null)
        {
            return _attackExecutor(amount);
        }

        return DamageCmd.Attack(amount)
            .FromMonster(Owner)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(ChoiceContext);
    }

    /// <summary>
    /// 对本次行动中的全部存活有效玩家执行一次或多次独立命中。
    /// </summary>
    /// <param name="amount">每次命中的基础伤害。</param>
    /// <param name="hitCount">独立命中次数。</param>
    /// <returns>全部玩家目标结算完成任务。</returns>
    public async Task ExecuteAttackAllAsync(decimal amount, int hitCount = 1)
    {
        CancellationToken.ThrowIfCancellationRequested();
        if (amount < decimal.Zero || hitCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "伤害不能为负且命中次数必须大于零。");
        }

        if (_attackAllExecutor is not null)
        {
            await _attackAllExecutor(amount, hitCount);
            return;
        }

        if (ShouldStop || ValidPlayerTargets().Count == 0)
        {
            return;
        }

        await DamageCmd.Attack(amount)
            .WithHitCount(hitCount)
            .FromMonster(Owner)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(ChoiceContext);
    }

    /// <summary>
    /// 使怪物自身获得具有 <see cref="ValueProp.Move"/> 语义的基础格挡。
    /// </summary>
    /// <param name="amount">基础格挡量。</param>
    /// <returns>格挡命令完成任务。</returns>
    public async Task ExecuteDefendAsync(decimal amount)
    {
        CancellationToken.ThrowIfCancellationRequested();
        if (_defendExecutor is not null)
        {
            await _defendExecutor(amount);
            return;
        }

        await CreatureCmd.GainBlock(Owner.Creature, amount, ValueProp.Move, null);
    }

    /// <summary>
    /// 向敌人自身施加指定标准 Power。
    /// </summary>
    /// <typeparam name="TPower">标准 Power 模型类型。</typeparam>
    /// <param name="amount">施加层数。</param>
    /// <returns>Power 命令完成任务。</returns>
    public async Task ApplyEnemyPowerAsync<TPower>(decimal amount)
        where TPower : PowerModel, new()
    {
        CancellationToken.ThrowIfCancellationRequested();
        if (_enemyPowerExecutor is not null)
        {
            await _enemyPowerExecutor(typeof(TPower), amount);
            return;
        }

        await PowerCmd.Apply<TPower>(ChoiceContext, Owner.Creature, amount, Owner.Creature, null);
    }

    /// <summary>
    /// 向全部存活有效玩家施加指定标准负面 Power。
    /// </summary>
    /// <typeparam name="TPower">标准 Power 模型类型。</typeparam>
    /// <param name="amount">对每名目标施加的层数。</param>
    /// <returns>全部 Power 命令完成任务。</returns>
    public async Task ApplyPowerToAllPlayersAsync<TPower>(decimal amount)
        where TPower : PowerModel, new()
    {
        CancellationToken.ThrowIfCancellationRequested();
        if (_targetPowerExecutor is not null)
        {
            await _targetPowerExecutor(typeof(TPower), amount);
            return;
        }

        IReadOnlyList<Creature> targets = ValidPlayerTargets();
        if (targets.Count > 0)
        {
            await PowerCmd.Apply<TPower>(ChoiceContext, targets, amount, Owner.Creature, null);
        }
    }

    /// <summary>
    /// 将权威收藏品可用队列投影到敌人 Power；数量为零时仍保留 Power。
    /// </summary>
    /// <param name="queueEntries">按权威顺序排列的收藏品稳定定义标识。</param>
    /// <returns>Power 数量与描述刷新完成任务。</returns>
    public Task SynchronizeCollectionPowerAsync(IReadOnlyList<string> queueEntries)
    {
        ArgumentNullException.ThrowIfNull(queueEntries);
        if (_collectionPowerExecutor is not null)
        {
            return _collectionPowerExecutor(queueEntries);
        }

        return EnemyCollectionInventoryPower.SynchronizeAsync(
            ChoiceContext,
            Owner.Creature,
            queueEntries,
            Owner);
    }

    /// <summary>
    /// 从 MoveState 目标中筛选仍存活且仍属于当前战斗的玩家生物。
    /// </summary>
    /// <returns>保持原始行动目标顺序的有效玩家目标。</returns>
    private IReadOnlyList<Creature> ValidPlayerTargets() => Targets
        .Where(target => target.Player is not null && target.IsAlive && CombatState.ContainsCreature(target))
        .ToArray();
}
