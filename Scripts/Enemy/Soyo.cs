using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;
using STS2_Tomorin_Mod.Encounters;
using STS2_Tomorin_Mod.Powers;
using STS2_Tomorin_Mod.Relics;

namespace STS2_Tomorin_Mod.Enemy;

/// <summary>
/// Boss 长崎素世；假面阶段提供共享任务，真实阶段根据疏远值发动攻击。
/// </summary>
public class Soyo : CustomMonsterModel
{
    public enum SoyoPhase
    {
        Mask,
        True
    }

    private const int TruePhaseThreshold = 6;
    private const int TrueAttackWoundCap = 4;
    private const int TruePhaseEstrangementLoss = 2;

    private int MaskBlock => AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 25, 20);
    protected virtual int MaskMultiAttack => AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 10, 9);
    private int MaskMultiAttackCount => 2;
    private int MaskHeal => AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 23, 18);
    protected virtual int TrueAttack => AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 27, 24);
    protected virtual int TrueMultiAttack => AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 4, 3);

    public override int MinInitialHp => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 430, 400);
    public override int MaxInitialHp => MinInitialHp;

    /// <summary>
    /// 是否发放素世首领战专属遗物奖励。
    /// </summary>
    protected virtual bool ShouldGrantBossReward => true;

    //记录是否第一次切换状态，用于显示文字
    private bool _isFirstChangeState = true;

    public SoyoPhase Phase { get; private set; } = SoyoPhase.Mask;

    private int _nextMaskIndex;
    private int _nextTrueIndex;

    private MoveState _maskBlockWeakState = null!;
    private MoveState _maskMultiAttackState = null!;
    private MoveState _maskHealState = null!;
    private MoveState _trueAttackWoundState = null!;
    private MoveState _trueMultiAttackState = null!;

    #region 路径相关

    public override string? CustomVisualPath => "res://STS2_Tomorin_Mod/scenes/creature_visuals/enemies/soyo_boss.tscn";

    #endregion

    #region 文本相关

    private VfxColor _soyoColor = VfxColor.Orange;

    private static readonly LocString _initSpeak = new LocString("monsters", "STS2_TOMORIN_MOD-SOYO.moves.enter");

    private static readonly LocString _changePhaseSpeak =
        new LocString("monsters", "STS2_TOMORIN_MOD-SOYO.moves.changePhase");

    private static readonly LocString _whyPlayHaruSpeak =
        new LocString("monsters", "STS2_TOMORIN_MOD-SOYO.moves.whyPlayHaru");

    private static readonly LocString _doEverythingSpeak =
        new LocString("monsters", "STS2_TOMORIN_MOD-SOYO.moves.doEverything");

    private static readonly LocString _forEndingSpeak =
        new LocString("monsters", "STS2_TOMORIN_MOD-SOYO.moves.forEnding");

    //外部调用接口
    public void WhyPlayHaru()
    {
        TalkCmd.Play(_whyPlayHaruSpeak, base.Creature, _soyoColor);
    }
    
    public void DoEverything()
    {
        TalkCmd.Play(_doEverythingSpeak, base.Creature, _soyoColor);
    }
    
    public void ForEnding()
    {
        TalkCmd.Play(_forEndingSpeak, base.Creature, _soyoColor);
    }
    #endregion

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();

        Creature.Died += OnDeath;
        CombatManager.Instance.CombatEnded += OnAfterCombatEnd;

        var context = new ThrowingPlayerChoiceContext();
        await PowerCmd.Apply<SoyoMaskedDamageReductionPower>(context, Creature, 1, Creature, null);
        await PowerCmd.Apply<SoyoMaskVisualPower>(context, Creature, 1, Creature, null);
        await PowerCmd.Apply<SoyoEstrangementPower>(context, Creature, 0, Creature, null);
        await PowerCmd.Apply<SoyoPhaseControllerPower>(context, Creature, 1, Creature, null);
        await SoyoTaskPower.ApplyRandomTask(context, Creature);

        TalkCmd.Play(_initSpeak, base.Creature, _soyoColor);
        _isFirstChangeState = true;
    }

    private void OnDeath(Creature creature)
    {
        if (creature == Creature)
        {
            BandMemberRelicRewardLifecycle.RecordEarnedAndGrantBossReward<SoyoBase>(
                creature,
                BandMemberKind.Soyo,
                ShouldGrantBossReward);
        }
    }

    private void OnAfterCombatEnd(CombatRoom room)
    {
        CombatManager.Instance.CombatEnded -= OnAfterCombatEnd;
        Creature.Died -= OnDeath;
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var states = new List<MonsterState>();

        _maskBlockWeakState = new MoveState("SOYO_MASK_BLOCK_WEAK", MaskBlockWeakMove,
            [new DefendIntent(), new DebuffIntent()]);
        _maskMultiAttackState = new MoveState("SOYO_MASK_MULTI_ATTACK", MaskMultiAttackMove,
            new MultiAttackIntent(MaskMultiAttack, MaskMultiAttackCount));
        _maskHealState = new MoveState("SOYO_MASK_HEAL", MaskHealMove,
            [new HealIntent(), new BuffIntent()]);

        _trueAttackWoundState = new MoveState("SOYO_TRUE_ATTACK_WOUND", TrueAttackWoundMove,
            [new SingleAttackIntent(TrueAttack), new StatusIntent(TrueAttackWoundCap)]);
        _trueMultiAttackState = new MoveState("SOYO_TRUE_MULTI_ATTACK", TrueMultiAttackMove,
            [new MultiAttackIntent(TrueMultiAttack, TruePhaseThreshold), new BuffIntent()]);

        _maskBlockWeakState.FollowUpState = _maskMultiAttackState;
        _maskMultiAttackState.FollowUpState = _maskHealState;
        _maskHealState.FollowUpState = _maskBlockWeakState;
        _trueAttackWoundState.FollowUpState = _trueMultiAttackState;
        _trueMultiAttackState.FollowUpState = _trueAttackWoundState;

        states.Add(_maskBlockWeakState);
        states.Add(_maskMultiAttackState);
        states.Add(_maskHealState);
        states.Add(_trueAttackWoundState);
        states.Add(_trueMultiAttackState);

        return new MonsterMoveStateMachine(states, _maskBlockWeakState);
    }

    public async Task EnterTruePhase()
    {
        if (Phase == SoyoPhase.True) return;

        //如果是第一次切换则播放语音；
        if (_isFirstChangeState)
        {
            _isFirstChangeState = false;
            TalkCmd.Play(_changePhaseSpeak, base.Creature, _soyoColor);
        }

        Phase = SoyoPhase.True;
        SetMoveImmediate(GetNextTrueState(), forceTransition: true);

        await PowerCmd.Remove<SoyoMaskVisualPower>(Creature);
        await PowerCmd.Apply<SoyoTruthVisualPower>(new ThrowingPlayerChoiceContext(), Creature, 1, Creature, null);

        foreach (var maskPower in Creature.Powers.OfType<SoyoMaskedDamageReductionPower>().ToList())
        {
            await PowerCmd.Remove(maskPower);
        }
    }

    public async Task EnterMaskPhase()
    {
        Phase = SoyoPhase.Mask;
        SetMoveImmediate(GetNextMaskState(), forceTransition: true);
        await PowerCmd.Remove<SoyoTruthVisualPower>(Creature);
        await PowerCmd.Apply<SoyoMaskVisualPower>(new ThrowingPlayerChoiceContext(), Creature, 1, Creature, null);
    }

    public async Task RefreshPhaseAfterCounterChanged()
    {
        int estrangement = SoyoEstrangementPower.GetAmount(Creature);
        if (Phase == SoyoPhase.Mask && estrangement > TruePhaseThreshold)
        {
            await EnterTruePhase();
        }
    }

    public async Task<SoyoPhase> RefreshPhaseForPlayerTurnStart()
    {
        int estrangement = SoyoEstrangementPower.GetAmount(Creature);
        if (Phase == SoyoPhase.True)
        {
            if (estrangement <= TruePhaseThreshold)
            {
                await EnterMaskPhase();
            }

            return Phase;
        }

        await SoyoEstrangementPower.Modify(new ThrowingPlayerChoiceContext(), Creature, 1, this);
        return Phase;
    }

    public Task StunOneTurn()
    {
        var stateLog = MoveStateMachine.StateLog;
        string followUpStateId = stateLog.Count > 0
            ? stateLog.Last().Id
            : GetCurrentPhaseNextState().Id;
        return CreatureCmd.Stun(Creature, followUpStateId);
    }

    private MoveState GetCurrentPhaseNextState() =>
        Phase == SoyoPhase.Mask ? GetNextMaskState() : GetNextTrueState();

    private MoveState GetNextMaskState() => _nextMaskIndex switch
    {
        0 => _maskBlockWeakState,
        1 => _maskMultiAttackState,
        _ => _maskHealState
    };

    private MoveState GetNextTrueState() => _nextTrueIndex switch
    {
        0 => _trueAttackWoundState,
        _ => _trueMultiAttackState
    };

    private async Task MaskBlockWeakMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.GainBlock(Creature, MaskBlock, ValueProp.Move, null);
        await PowerCmd.Apply<WeakPower>(new ThrowingPlayerChoiceContext(), targets, 1, Creature, null);
        await PowerCmd.Apply<CustomConstrictPower>(new ThrowingPlayerChoiceContext(), targets, 2, Creature, null);
        _nextMaskIndex = 1;
    }

    private async Task MaskMultiAttackMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(MaskMultiAttack).WithHitCount(MaskMultiAttackCount)
            .FromMonster(this)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(null);
        _nextMaskIndex = 2;
    }

    private async Task MaskHealMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.Heal(Creature, MaskHeal);
        await PowerCmd.Apply<StrengthPower>(new ThrowingPlayerChoiceContext(), Creature, 1, Creature, null);
        _nextMaskIndex = 0;
    }

    private async Task TrueAttackWoundMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(TrueAttack).FromMonster(this)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(null);

        int woundCount = GetTruePhaseWoundCount();
        if (woundCount > 0)
        {
            await CardPileCmd.AddToCombatAndPreview<Wound>(targets, PileType.Draw, woundCount, null,
                CardPilePosition.Random);
        }

        await SoyoEstrangementPower.Modify(new ThrowingPlayerChoiceContext(), Creature, -TruePhaseEstrangementLoss,
            this);
        _nextTrueIndex = 1;
    }

    private async Task TrueMultiAttackMove(IReadOnlyList<Creature> targets)
    {
        int hitCount = SoyoEstrangementPower.GetAmount(Creature);
        if (hitCount > 0)
        {
            await DamageCmd.Attack(TrueMultiAttack).WithHitCount(hitCount)
                .FromMonster(this)
                .WithHitFx("vfx/vfx_attack_blunt")
                .Execute(null);
        }

        await SoyoEstrangementPower.Modify(new ThrowingPlayerChoiceContext(), Creature, -TruePhaseEstrangementLoss,
            this);
        _nextTrueIndex = 0;
    }

    private int GetTruePhaseWoundCount()
    {
        int estrangement = SoyoEstrangementPower.GetAmount(Creature);
        return Math.Clamp(estrangement - TruePhaseThreshold, 0, TrueAttackWoundCap);
    }
}
