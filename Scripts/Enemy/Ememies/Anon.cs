using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes.Audio;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;
using STS2_Tomorin_Mod.Cards;
using STS2_Tomorin_Mod.Powers;
using STS2_Tomorin_Mod.Relics;

namespace STS2_Tomorin_Mod.Enemy.Ememies;

/// <summary>
/// 
/// </summary>
public class Anon : CustomMonsterModel
{
    public override int MinInitialHp => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 221, 210);
    public override int MaxInitialHp => MinInitialHp;

    //生成状态牌数量
    private int _stateCount = 1;
    private int _secondPhaseStateCount = 5;

    //攻击部分
    //一阶段
    private int NormalSingleAtk => AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 21, 18);
    private int NormalMultiAtk => AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 6, 5);
    private int NormalMultiCount = 3;
    private int NormalBlockNum => AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 23, 20);

    //是否触发语音
    private bool _isSpeak = false;
    private bool _isSecondPhase = false;
    private bool _isAddReward = false;

    //逃跑buff层数
    private int _escapeBuffCount = 3;

    // 死了复活开二阶段
    private MoveState _deadState;

    public MoveState DeadState
    {
        get { return _deadState; }
        private set
        {
            AssertMutable();
            _deadState = value;
        }
    }

    private VfxColor _anonColor = VfxColor.Orange;

    #region 文本相关

    private static readonly LocString _initSpeak = new LocString("monsters", "STS2_TOMORIN_MOD-ANON.moves.initSpeak");
    private static readonly LocString _speak1 = new LocString("monsters", "STS2_TOMORIN_MOD-ANON.moves.speakLine1");
    private static readonly LocString _dead = new LocString("monsters", "STS2_TOMORIN_MOD-ANON.moves.dead");
    private static readonly LocString _speakRun1 = new LocString("monsters", "STS2_TOMORIN_MOD-ANON.moves.runLine1");
    private static readonly LocString _speakRun2 = new LocString("monsters", "STS2_TOMORIN_MOD-ANON.moves.runLine2");
    private static readonly LocString _run = new LocString("monsters", "STS2_TOMORIN_MOD-ANON.moves.run");
    private static readonly LocString _die = new LocString("monsters", "STS2_TOMORIN_MOD-ANON.moves.die");

    #endregion

    #region 路径相关

    public override string? CustomVisualPath => "res://STS2_Tomorin_Mod/scenes/creature_visuals/enemies/anon_boss.tscn";

    #endregion

    /// <summary>
    /// 初始化
    /// </summary>
    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        base.Creature.Died += AfterDeath;
        CombatManager.Instance.CombatEnded += OnAfterCombatEnd;
        _isSpeak = false;
        _isSecondPhase = false;
        _isAddReward = false;
    }

    /// <summary>
    /// 打完后事件，可能在这里处理奖励？
    /// TODO 音效相关
    /// </summary>
    /// <param name="creature"></param>
    private void AfterDeath(Creature creature)
    {
        if (_isSecondPhase)
        {
            _isAddReward = true;
            AddReward((CombatRoom)creature.CombatState.RunState.CurrentRoom);

            TalkCmd.Play(_die, Creature, _anonColor);
        }
        else
        {
            //切换语音
            _isSecondPhase = true;
        }
    }

    public void OnAfterCombatEnd(CombatRoom room)
    {
        CombatManager.Instance.CombatEnded -= OnAfterCombatEnd;
        base.Creature.Died -= AfterDeath;
    }


    public void AddReward(CombatRoom room)
    {
        if (_isAddReward)
        {
            var players = room.CombatState.Players;
            foreach (var player in players)
            {
                var relic = ModelDb.Relic<AnonGuitar>().ToMutable();
                room.AddExtraReward(player, new RelicReward(relic, player));
            }
        }
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        List<MonsterState> list = new List<MonsterState>();

        var initState = new MoveState("INIT_STATE", InitState, [new BuffIntent(), new StatusIntent(_stateCount)]);
        var normalMultiState = new MoveState("NORMAL_MULTI_STATE", NormalMultiState,
            new MultiAttackIntent(NormalMultiAtk, NormalMultiCount));
        var normalSingleState = new MoveState("NORMAL_SINGLE_STATE", NormalSingleState,
            new SingleAttackIntent(NormalSingleAtk));
        var normalBuffState = new MoveState("NORMAL_BUFF_STATE", NormalBuffState,
            [new DefendIntent(), new StatusIntent(_stateCount)]);
        //死亡事件,往玩家卡组里塞牌，给自己上counter buff
        DeadState = new MoveState("DEAD_STATE", WaitingRevoceryState, [new StatusIntent(3), new BuffIntent()])
        {
            MustPerformOnceBeforeTransitioning = true
        };
        //逃跑事件
        var runPhaseOneState = new MoveState("RUN_PHASE_ONE_STATE", RunPhaseOneState, new StatusIntent(5));
        var runPhaseTwoState = new MoveState("RUN_PHASE_TWO_STATE", RunPhaseTwoState, new DebuffIntent());
        var runState = new MoveState("RUN_STATE", RunState, new EscapeIntent());

        //状态机
        initState.FollowUpState = normalMultiState;
        normalMultiState.FollowUpState = normalSingleState;
        normalSingleState.FollowUpState = normalBuffState;
        normalBuffState.FollowUpState = normalMultiState;
        DeadState.FollowUpState = runPhaseOneState;
        runPhaseOneState.FollowUpState = runPhaseTwoState;
        runPhaseTwoState.FollowUpState = runState;

        list.Add(initState);
        list.Add(normalMultiState);
        list.Add(normalSingleState);
        list.Add(normalBuffState);
        list.Add(runState);
        list.Add(runPhaseOneState);
        list.Add(runPhaseTwoState);
        list.Add(DeadState);


        return new MonsterMoveStateMachine(list, initState);
    }

    #region 状态机相关

    /// <summary>
    /// 开始阶段，说话
    /// “哼哼，谁也不能阻止我成立AnonTokyo！”
    /// 上buff，往卡组里塞卡
    /// </summary>
    /// <param name="targets"></param>
    private async Task InitState(IReadOnlyList<Creature> targets)
    {
        //TODO 音效
        await CreatureCmd.TriggerAnim(base.Creature, "Heal", 0.8f);
        await PowerCmd.Apply<AnonEscapePower>(base.Creature, 1, base.Creature, null);
        await CardPileCmd.AddToCombatAndPreview<AnonReject>(targets, PileType.Draw, _stateCount, addedByPlayer: false);

        //说话
        TalkCmd.Play(_initSpeak, base.Creature, _anonColor);

        _isSpeak = false;
    }

    /// <summary>
    /// 循环第一阶段，多段伤害
    /// 如果没说过话则输出语音
    /// “只要能摘下TopStar，我也能成为受人瞩目的大明星了！”
    /// </summary>
    /// <param name="targets"></param>
    private async Task NormalMultiState(IReadOnlyList<Creature> targets)
    {
        if (!_isSpeak)
        {
            TalkCmd.Play(_speak1, base.Creature, _anonColor);
        }

        //TODO 动画，特效
        await DamageCmd.Attack(NormalMultiAtk).WithHitCount(NormalMultiCount)
            .FromMonster(this) //.WithAttackerAnim("Attack", 0.3f)
            // .WithAttackerFx(null, "event:/sfx/enemy/enemy_attacks/waterfall_giant/waterfall_giant_attack_stomp")
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(null);

        _isSpeak = true;
    }

    /// <summary>
    /// 循环二阶段 单段伤害
    /// </summary>
    /// <param name="targets"></param>
    private async Task NormalSingleState(IReadOnlyList<Creature> targets)
    {
        //TODO 动画，特效
        await DamageCmd.Attack(NormalSingleAtk).FromMonster(this) //.WithAttackerAnim("Attack", 0.3f)
            // .WithAttackerFx(null, "event:/sfx/enemy/enemy_attacks/waterfall_giant/waterfall_giant_attack_stomp")
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(null);
    }

    /// <summary>
    /// 循环三阶段，上盾，喂屎
    /// </summary>
    /// <param name="targets"></param>
    private async Task NormalBuffState(IReadOnlyList<Creature> targets)
    {
        //TODO 动画，特效
        await CreatureCmd.TriggerAnim(base.Creature, "Plow", 0f);
        await CreatureCmd.GainBlock(base.Creature, NormalBlockNum, ValueProp.Move, null);
        await CardPileCmd.AddToCombatAndPreview<AnonReject>(targets, PileType.Draw, _stateCount, addedByPlayer: false);
    }

    /// <summary>
    /// 等待复活
    /// </summary>
    /// <param name="targets"></param>
    private async Task WaitingRevoceryState(IReadOnlyList<Creature> targets)
    {
        //移除buff
        await PowerCmd.Remove<AnonEscapePower>(Creature);

        //TODO 动画，特效
        await CreatureCmd.TriggerAnim(base.Creature, "Heal", 0.8f);

        //塞牌
        await CardPileCmd.AddToCombatAndPreview<AnonNeedYou>(targets, PileType.Hand, 1, addedByPlayer: false);
        await CardPileCmd.AddToCombatAndPreview<AnonPlayGuitar>(targets, PileType.Discard, 1, addedByPlayer: false);
        await CardPileCmd.AddToCombatAndPreview<AnonLiveTogether>(targets, PileType.Draw, 1, addedByPlayer: false);

        //上buff：
        await PowerCmd.Apply<AnonEscapeCountPower>(this.Creature, targets.Count * _escapeBuffCount, this.Creature,
            null);

        TalkCmd.Play(_speak1, base.Creature, _anonColor);
    }

    /// <summary>
    /// 复活第一阶段
    /// 喂屎
    /// </summary>
    /// <param name="targets"></param>
    private async Task RunPhaseOneState(IReadOnlyList<Creature> targets)
    {
        //TODO 动画，特效
        await CardPileCmd.AddToCombatAndPreview<Dazed>(targets, PileType.Draw, _secondPhaseStateCount,
            false, CardPilePosition.Random);

        TalkCmd.Play(_speakRun1, base.Creature, _anonColor);
    }

    /// <summary>
    /// 复活二阶段
    /// 减少抽排数量
    /// </summary>
    /// <param name="targets"></param>
    private async Task RunPhaseTwoState(IReadOnlyList<Creature> targets)
    {
        //TODO 动画，特效
        await PowerCmd.Apply<LessDrawNextTurnPower>(targets, 2, this.Creature, null);

        TalkCmd.Play(_speakRun2, base.Creature, _anonColor);
    }

    /// <summary>
    /// 复活第三阶段 逃跑
    /// 说台词：
    /// </summary>
    /// <param name="targets"></param>
    private async Task RunState(IReadOnlyList<Creature> targets)
    {
        TalkCmd.Play(_run, base.Creature, _anonColor);
        //TODO 动画，特效
        await Cmd.Wait(0.5f);
        await CreatureCmd.Escape(base.Creature);
    }

    #endregion

    #region Trigger相关

    public async Task TriggerAnonRun()
    {
        //TODO 切换bgm
        // NRunMusicController.Instance?.UpdateMusicParameter("waterfall_giant_progress", 2f);
        await CreatureCmd.SetMaxAndCurrentHp(base.Creature, 999999999m);
        base.Creature.ShowsInfiniteHp = true;
        SetMoveImmediate(DeadState, forceTransition: true);
        // SfxCmd.SetParam("event:/sfx/enemy/enemy_attacks/waterfall_giant/waterfall_giant_ambient", "waterfall_giant_sfx", 3f);
        //TODO 切换立绘

        //播放文字
        TalkCmd.Play(_dead, base.Creature, _anonColor);
    }

    public async Task TriggerAnonDie()
    {
        //TODO 播放文字语音，然后自杀
        TalkCmd.Play(_die, base.Creature, _anonColor);

        await Cmd.Wait(0.5f);
        await CreatureCmd.Kill(Creature);
    }

    #endregion
}