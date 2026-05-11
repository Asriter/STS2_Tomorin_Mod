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
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;
using STS2_Tomorin_Mod.Cards;
using STS2_Tomorin_Mod.Cards.EnemyCards;
using STS2_Tomorin_Mod.Powers;
using STS2_Tomorin_Mod.Relics;

namespace STS2_Tomorin_Mod.Enemy.Ememies;

/// <summary>
/// boss 立希
/// </summary>
public class Taki : CustomMonsterModel
{
    public interface IChoosable
    {
        Task OnChosen();
    }

    private enum Phase
    {
        One = 0,
        Two = 1,
        Three = 2,
    }

    #region 路径相关

    public override string? CustomVisualPath => "res://STS2_Tomorin_Mod/scenes/creature_visuals/enemies/taki_boss.tscn";

    #endregion

    private VfxColor _takiColor = VfxColor.Purple;

    public override int MinInitialHp => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 560, 500);
    public override int MaxInitialHp => MinInitialHp;

    //各阶段伤害
    //第一阶段
    private int _phaseOneBuffCount = 1;

    private int PhaseOneStateAtk => AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 18, 16);

    //3*5
    private int PhaseOneNormalAtk => AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 4, 3);
    private int PhaseOneNormalAtkCount => 5;

    //12*2
    private int PhaseOneBigAtk => AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 13, 12);
    private int PhaseOneBigAtkCount => 2;

    //第二阶段
    //5*3 命中时给一张伤口到弃牌堆
    private int PhaseTwoCardAtk => AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 6, 5);
    private int PhaseTwoCardAtkCount => 5;
    private int PhaseTwoCardCount => 1;
    private int PhaseBlock => AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 28, 23);
    private decimal _phasePower = 1;

    //第三阶段
    //10*3
    private int PhaseThreeAtk => 10;
    private int PhaseThreeAtkCount => 5;

    //各阶段血量
    private int PhaseOneHp => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 235, 210);
    private int PhaseTwoHp => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 225, 200);


    #region 文本相关

    private static readonly LocString _initSpeak = new LocString("monsters", "STS2_TOMORIN_MOD-TAKI.moves.initSpeak");
    private static readonly LocString _wait = new LocString("monsters", "STS2_TOMORIN_MOD-TAKI.moves.wait");
    private static readonly LocString _phaseOne = new LocString("monsters", "STS2_TOMORIN_MOD-TAKI.moves.phaseOne");
    private static readonly LocString _phaseTwo = new LocString("monsters", "STS2_TOMORIN_MOD-TAKI.moves.phaseTwo");

    private static readonly LocString _phaseTwoDie =
        new LocString("monsters", "STS2_TOMORIN_MOD-TAKI.moves.phaseTwoDie");

    private static readonly LocString _phaseThree = new LocString("monsters", "STS2_TOMORIN_MOD-TAKI.moves.phaseThree");
    private static readonly LocString _run = new LocString("monsters", "STS2_TOMORIN_MOD-TAKI.moves.run");
    private static readonly LocString _die = new LocString("monsters", "STS2_TOMORIN_MOD-TAKI.moves.die");

    #endregion

    //buff相关
    private static List<List<IChoosable>> _takiBuffs = new List<List<IChoosable>>()
    {
        new List<IChoosable>()
        {
            ModelDb.Card<TakiSelectCard>(),
            ModelDb.Card<TakiRandomCard>(),
        },
        new List<IChoosable>()
        {
            ModelDb.Card<TakiAddEnergy>(),
            ModelDb.Card<TakiDrawCard>(),
            ModelDb.Card<TakiGetBlock>(),
        },
        new List<IChoosable>()
        {
            ModelDb.Card<TakiAddDamage>(),
            ModelDb.Card<TakiAtField>(),
            ModelDb.Card<TakiInspiration>(),
        }
    };

    private Phase _currentPhase = Phase.One;

    //转阶段时的state缓存
    private MoveState _changePhaseTwo;

    public MoveState ChangePhaseTwo
    {
        get => _changePhaseTwo;
        private set
        {
            AssertMutable();
            _changePhaseTwo = value;
        }
    }

    private MoveState _changePhaseThree;

    public MoveState ChangePhaseThree
    {
        get => _changePhaseThree;
        private set
        {
            AssertMutable();
            _changePhaseThree = value;
        }
    }

    //初始化
    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        var power = await PowerCmd.Apply<EnemyMaxDamageReceivedPower>(new ThrowingPlayerChoiceContext(),base.Creature, PhaseOneHp, base.Creature, null);
        power.DamageCallBack = PhaseOneClearCallBack;
        _currentPhase = Phase.One;

        //添加死亡回调
        base.Creature.Died += PhaseThreeClearCallBack;

        TalkCmd.Play(_initSpeak, base.Creature, _takiColor);
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        List<MonsterState> list = new List<MonsterState>();

        var initState = new MoveState("INIT_STATE", InitState, [new UnknownIntent()]);
        var changePhaseOneState = new MoveState("CHANGE_PHASE_ONE_STATE", ChangePhaseOneState, [new BuffIntent()]);
        var phaseOneFirstState = new MoveState("PHASE_ONE_FIRST_STATE", PhaseOneFirstState,
            [new SingleAttackIntent(PhaseOneStateAtk), new DebuffIntent()]);
        var phaseOneSecondState = new MoveState("PHASE_ONE_SECOND_STATE", PhaseOneSecondState,
            [new MultiAttackIntent(PhaseOneNormalAtk, PhaseOneNormalAtkCount)]);
        var phaseOneThirdState = new MoveState("PHASE_ONE_THIRD_STATE", PhaseOneThirdState,
            new MultiAttackIntent(PhaseOneBigAtk, PhaseOneBigAtkCount));
        ChangePhaseTwo = new MoveState("CHANGE_PHASE_TWO_STATE", ChangePhaseTwoState, [new BuffIntent()]);
        var phaseTwoAtk = new MoveState("PHASE_TWO_ATK_STATE", PhaseTwoAttackState,
            [new MultiAttackIntent(PhaseTwoCardAtk, PhaseTwoCardAtkCount), new StatusIntent(PhaseTwoCardCount)]);
        var phaseTwoBuff = new MoveState("PHASE_TWO_BUFF_STATE", PhaseTwoBuffState,
            [new DefendIntent(), new BuffIntent()]);
        ChangePhaseThree = new MoveState("CHANGE_PHASE_THREE_STATE", ChangePhaseThreeState, [new BuffIntent()]);
        var phaseThree = new MoveState("PHASE_THREE_STATE", PhaseThreeState,
            new MultiAttackIntent(PhaseThreeAtk, PhaseThreeAtkCount));

        initState.FollowUpState = changePhaseOneState;
        changePhaseOneState.FollowUpState = phaseOneFirstState;
        phaseOneFirstState.FollowUpState = phaseOneSecondState;
        phaseOneSecondState.FollowUpState = phaseOneThirdState;
        phaseOneThirdState.FollowUpState = phaseOneFirstState;

        //二阶段
        ChangePhaseTwo.FollowUpState = phaseTwoAtk;
        phaseTwoAtk.FollowUpState = phaseTwoBuff;
        phaseTwoBuff.FollowUpState = phaseTwoAtk;

        //三阶段
        ChangePhaseThree.FollowUpState = phaseThree;
        phaseThree.FollowUpState = phaseThree;

        list.Add(initState);
        list.Add(changePhaseOneState);
        list.Add(phaseOneFirstState);
        list.Add(phaseOneSecondState);
        list.Add(phaseOneThirdState);
        list.Add(phaseTwoAtk);
        list.Add(phaseTwoBuff);
        list.Add(phaseThree);
        list.Add(ChangePhaseTwo);
        list.Add(ChangePhaseThree);

        return new MonsterMoveStateMachine(list, initState);
    }

    #region 状态机

    private async Task InitState(IReadOnlyList<Creature> targets)
    {
        await PowerCmd.Apply<VulnerablePower>(new ThrowingPlayerChoiceContext(),base.Creature, 2, base.Creature, null);
        //说话
        TalkCmd.Play(_wait, base.Creature, _takiColor);
    }

    private async Task ChangePhaseOneState(IReadOnlyList<Creature> targets)
    {
        List<Task> list = new List<Task>();
        foreach (Creature target in targets)
        {
            list.Add(ChooseBuff(target));
        }

        await Task.WhenAll(list);

        TalkCmd.Play(_phaseOne, base.Creature, _takiColor);
    }

    private async Task PhaseOneFirstState(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(PhaseOneStateAtk).FromMonster(this) //.WithAttackerAnim("Attack", 0.3f)
            // .WithAttackerFx(null, "event:/sfx/enemy/enemy_attacks/waterfall_giant/waterfall_giant_attack_stomp")
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(null);

        //给buff
        await PowerCmd.Apply<WeakPower>(new ThrowingPlayerChoiceContext(),targets, _phaseOneBuffCount, this.Creature, null);
    }

    private async Task PhaseOneSecondState(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(PhaseOneNormalAtk).WithHitCount(PhaseOneNormalAtkCount)
            .FromMonster(this) //.WithAttackerAnim("Attack", 0.3f)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(null);
    }

    private async Task PhaseOneThirdState(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(PhaseOneBigAtk).WithHitCount(PhaseOneBigAtkCount)
            .FromMonster(this) //.WithAttackerAnim("Attack", 0.3f)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(null);
    }

    private async Task ChangePhaseTwoState(IReadOnlyList<Creature> targets)
    {
        _currentPhase = Phase.Two;

        var power = await PowerCmd.Apply<EnemyMaxDamageReceivedPower>(new ThrowingPlayerChoiceContext(),base.Creature, PhaseTwoHp, base.Creature, null);
        power.DamageCallBack = PhaseTwoClearCallBack;

        List<Task> list = new List<Task>();
        foreach (Creature target in targets)
        {
            list.Add(ChooseBuff(target));
        }

        await Task.WhenAll(list);

        TalkCmd.Play(_phaseTwo, base.Creature, _takiColor);
    }

    private async Task PhaseTwoAttackState(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(PhaseTwoCardAtk).WithHitCount(PhaseTwoCardAtkCount)
            .FromMonster(this) //.WithAttackerAnim("Attack", 0.3f)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(null);

        //塞伤口
        await CardPileCmd.AddToCombatAndPreview<Wound>(targets, PileType.Draw, PhaseTwoCardCount, null, CardPilePosition.Random);
    }

    private async Task PhaseTwoBuffState(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.GainBlock(base.Creature, PhaseBlock, ValueProp.Move, null);

        //获得力量
        await PowerCmd.Apply<StrengthPower>(new ThrowingPlayerChoiceContext(),Creature, _phasePower, Creature, null);
    }

    private async Task ChangePhaseThreeState(IReadOnlyList<Creature> targets)
    {
        _currentPhase = Phase.Three;

        //上buff
        await PowerCmd.Apply<TakiLockHpPower>(new ThrowingPlayerChoiceContext(),targets, 1, this.Creature, null);

        List<Task> list = new List<Task>();
        foreach (Creature target in targets)
        {
            list.Add(ChooseBuff(target));
        }

        await Task.WhenAll(list);

        TalkCmd.Play(_phaseThree, base.Creature, _takiColor);
    }

    private async Task PhaseThreeState(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(PhaseThreeAtk).WithHitCount(PhaseThreeAtkCount)
            .FromMonster(this) //.WithAttackerAnim("Attack", 0.3f)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(null);

        //检测是否要跑路
        var room = (CombatRoom)CombatState.RunState.CurrentRoom;
        if (ShouldRun(room))
        {
            await RunCallBack(room);
        }
    }

    #endregion

    #region CallBack

    private void PhaseOneClearCallBack()
    {
        SetMoveImmediate(ChangePhaseTwo, forceTransition: true);
    }

    private void PhaseTwoClearCallBack()
    {
        SetMoveImmediate(ChangePhaseThree, forceTransition: true);
        TalkCmd.Play(_phaseTwoDie, base.Creature, _takiColor);
    }

    private void PhaseThreeClearCallBack(Creature creature)
    {
        TalkCmd.Play(_die, base.Creature, _takiColor);

        //添加额外奖励
        var room = (CombatRoom)creature.CombatState.RunState.CurrentRoom;
        var players = room.CombatState.Players;
        foreach (var player in players)
        {
            var relic = ModelDb.Relic<TakiDrum>().ToMutable();
            room.AddExtraReward(player, new RelicReward(relic, player));
        }

        Creature.Died -= PhaseThreeClearCallBack;
    }

    private async Task RunCallBack(CombatRoom room)
    {
        //TODO 移除金币奖励

        TalkCmd.Play(_run, base.Creature, _takiColor);
        await Cmd.Wait(1f);

        await CreatureCmd.Escape(base.Creature);
        room.OnCombatEnded();
    }

    #endregion

    /// <summary>
    /// 仅三阶段适用，是否逃跑
    /// 当所有玩家都死了，或者都有手下留情buff且只剩一滴血则逃跑
    /// </summary>
    /// <param name="room"></param>
    /// <returns></returns>
    private bool ShouldRun(CombatRoom room)
    {
        if (_currentPhase == Phase.Three)
        {
            var players = room.CombatState.Players;
            foreach (var player in players)
            {
                var creature = player.Creature;
                if (!creature.IsDead && (!creature.HasPower<TakiLockHpPower>() || creature.CurrentHp > 1))
                {
                    return false;
                }
            }

            return true;
        }

        return false;
    }

    private async Task ChooseBuff(Creature target)
    {
        if (target.IsDead)
        {
            return;
        }

        List<CardModel> cards = _takiBuffs[(int)_currentPhase].Select(delegate(IChoosable c)
        {
            CardModel cardModel = base.CombatState.CreateCard((CardModel)c, target.Player);
            return cardModel;
        }).ToList();
        await ((IChoosable)(await CardSelectCmd.FromChooseACardScreen(new BlockingPlayerChoiceContext(), cards,
            target.Player))).OnChosen();
    }
}