using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using STS2_Tomorin_Mod.Enemy.CardIntents;
using STS2_Tomorin_Mod.Powers;

namespace STS2_Tomorin_Mod.Enemy;

/// <summary>使用一个自循环 CardIntent 状态完成三阶段迁移的影灯首领。</summary>
public sealed class ShadowTomorin : BaseCardIntentMonsterModel
{
    public const string StateId = "SHADOW_TOMORIN_CARD_LOOP";

    private CardIntentMoveState? _cardState;
    private ShadowTomorinDamageGatePower? _damageGate;

    public override int MinInitialHp => ShadowTomorinBalance.MaxHp;
    public override int MaxInitialHp => ShadowTomorinBalance.MaxHp;
    public override string? CustomVisualPath =>
        "res://STS2_Tomorin_Mod/scenes/creature_visuals/enemies/ShadowTomorin.tscn";

    /// <summary>获取本场战斗从入场到死亡始终复用的唯一卡牌状态。</summary>
    public CardIntentMoveState CardState => _cardState ?? throw new InvalidOperationException(
        "影灯状态机尚未生成。");

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        ShadowTomorinDeck.EnsureRegistered();
        _cardState = RegisterCardIntentState(CardIntentMoveState.Create(
            StateId,
            this,
            ShadowTomorinDeck.DeckId,
            ShadowTomorinBalance.MaxEffectiveCards,
            createPreparationCycle: CreatePreparationCycle,
            rules: ShadowTomorinRules.ForPhase(EnemyCardPhase.Phase1)));
        _cardState.FollowUpState = _cardState;
        return new MonsterMoveStateMachine([_cardState], _cardState);
    }

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        _damageGate = await ApplyDamageGateAsync(
            ShadowTomorinBalance.Phase1DamageAllowance,
            EnemyCardPhase.Phase2);
    }

    /// <summary>
    /// 伤害门回调只登记下一阶段，不重置战斗、不换 MoveState，也不接触当前公开行动。
    /// </summary>
    public void RequestNextPhase(EnemyCardPhase phase)
    {
        EnemyCardPhase expected = CardState.CombatState.ActivePhase switch
        {
            EnemyCardPhase.Phase1 => EnemyCardPhase.Phase2,
            EnemyCardPhase.Phase2 => EnemyCardPhase.Phase3,
            _ => EnemyCardPhase.None
        };
        if (phase != expected)
        {
            throw new InvalidOperationException(
                $"影灯当前阶段 {CardState.CombatState.ActivePhase} 只能请求 {expected}，不能请求 {phase}。");
        }

        CardState.RequestPhase(phase);
    }

    /// <summary>只在行动完全结算后的 Idle 安全点提交待处理阶段。</summary>
    protected internal override Task AfterCardIntentActionSettledAsync(
        CardIntentMoveState state,
        CancellationToken cancellationToken = default)
    {
        if (!ReferenceEquals(state, CardState))
        {
            throw new InvalidOperationException("影灯收到不属于自身唯一 CardIntent 状态的结算回调。");
        }

        return TransitionPendingPhaseAtIdleAsync(cancellationToken);
    }

    /// <summary>
    /// 先构造无副作用候选，再依阶段安装状态与伤害门，最后一次性替换权威牌区。
    /// </summary>
    public async Task<bool> TransitionPendingPhaseAtIdleAsync(
        CancellationToken cancellationToken = default)
    {
        EnemyCardCombatState state = CardState.CombatState;
        if (state.PendingPhase == EnemyCardPhase.None)
        {
            return false;
        }

        if (state.RuntimePhase != EnemyCardRuntimePhase.Idle ||
            state.PreparedAction is not null ||
            state.ImmediateResolutionStack.Count != 0)
        {
            return false;
        }

        cancellationToken.ThrowIfCancellationRequested();
        EnemyCardPhaseTransitionCandidate candidate = CardState.BuildPendingPhaseTransitionCandidate();
        ThrowingPlayerChoiceContext context = new();
        switch ((candidate.From, candidate.To))
        {
            case (EnemyCardPhase.Phase1, EnemyCardPhase.Phase2):
            {
                ShadowTomorinDamageGatePower oldGate = _damageGate ??
                    throw new InvalidOperationException("P1→P2 迁移缺少仍在生效的零额度伤害门。");
                ShadowTomorinDamageGatePower nextGate = await ApplyDamageGateAsync(
                    ShadowTomorinBalance.Phase2DamageAllowance,
                    EnemyCardPhase.Phase3);
                await PowerCmd.Apply<ShadowTomoriFormPower>(
                    context,
                    Creature,
                    1m,
                    Creature,
                    null);
                if (!ReferenceEquals(oldGate, nextGate))
                {
                    await PowerCmd.Remove(oldGate);
                }

                _damageGate = nextGate;
                break;
            }

            case (EnemyCardPhase.Phase2, EnemyCardPhase.Phase3):
                if (_damageGate is not null)
                {
                    await PowerCmd.Remove(_damageGate);
                    _damageGate = null;
                }

                break;

            default:
                throw new InvalidOperationException(
                    $"影灯不支持阶段迁移 {candidate.From}→{candidate.To}。");
        }

        CardState.ApplyPhaseTransition(candidate);
        return true;
    }

    /// <summary>以实际最大生命为唯一缩放源，并统一使用远离零点的中点舍入。</summary>
    public static decimal ScaleDamageAllowance(decimal baseAllowance, decimal actualMaxHp)
    {
        if (baseAllowance < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(baseAllowance));
        }

        if (actualMaxHp <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(actualMaxHp));
        }

        decimal scale = actualMaxHp / ShadowTomorinBalance.MaxHp;
        return decimal.Round(baseAllowance * scale, 0, MidpointRounding.AwayFromZero);
    }

    private async Task<ShadowTomorinDamageGatePower> ApplyDamageGateAsync(
        decimal baseAllowance,
        EnemyCardPhase nextPhase)
    {
        decimal allowance = ScaleDamageAllowance(baseAllowance, Creature.MaxHp);
        ShadowTomorinDamageGatePower power =
            await PowerCmd.Apply<ShadowTomorinDamageGatePower>(
                new ThrowingPlayerChoiceContext(),
                Creature,
                allowance,
                Creature,
                null) ?? throw new InvalidOperationException("影灯伤害门应用后未返回 Power 实例。");
        power.RemoveAtEnemyTurnStartWhenDepleted = false;
        power.DamageCallBack = () => RequestNextPhase(nextPhase);
        return power;
    }

    private EnemyPreparationCycle CreatePreparationCycle(
        EnemyCardCombatState state,
        IEnemyCardRandomSource randomSource)
    {
        if (state.ActivePhase == EnemyCardPhase.Phase1 ||
            !Creature.HasPower<ShadowTomoriFormPower>())
        {
            return new EnemyPreparationCycle(null, EnemyPreparedPreActionInventoryDelta.Empty);
        }

        ShadowTomoriFormPower form = Creature.GetPower<ShadowTomoriFormPower>() ??
            throw new InvalidOperationException("影灯形态标记存在但无法解析其实例。");
        return form.CreatePreparationCycle(
            state,
            randomSource,
            ShadowTomorinCollectionCatalog.WeightedDefinitions);
    }
}
