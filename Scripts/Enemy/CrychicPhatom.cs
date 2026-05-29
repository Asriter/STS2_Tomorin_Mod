using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.ValueProps;
using STS2_Tomorin_Mod.Afflictions;
using STS2_Tomorin_Mod.Cards;
using STS2_Tomorin_Mod.Powers;

namespace STS2_Tomorin_Mod.Enemy;

/// <summary>
/// boss Crychic亡灵
/// 一阶段: 3步循环（单体重击 → 多段攻击 → 回复+护盾）
/// 一血死亡后复活进入二阶段: 双方99易伤 → 单体重击 → 多段攻击 → 攻击+护盾
/// 复活时根据所有玩家CrychicRemember总层数增加最大HP
/// 复活时将墓地+消耗非状态卡回收并分配Affliction
/// </summary>
public class CrychicPhatom : CustomMonsterModel
{
    // ===== 可配置参数 =====

    /// <summary>
    /// 每层CrychicRemember在复活时转换的HP量
    /// 最终HP增量 = 所有玩家CrychicRemember层数之和 × 此值 × 玩家数量(多人缩放)
    /// </summary>
    private const int HpPerRememberStack = 20;

    // -- 一阶段参数 --

    /// <summary>
    /// 一阶段单体重击伤害 (2.1)
    /// 基础25, 高进阶28
    /// </summary>
    private int PhaseOneBigAtk => AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 18, 15);

    /// <summary>
    /// 一阶段多段攻击每段伤害 (2.2)
    /// 基础10, 高进阶12
    /// </summary>
    private int PhaseOneMultiAtk => AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 10, 8);

    /// <summary>
    /// 一阶段多段攻击段数 (2.2)
    /// </summary>
    private const int PhaseOneMultiCount = 3;

    /// <summary>
    /// 一阶段回复生命值 (2.3)
    /// </summary>
    private const int PhaseOneHeal = 50;

    /// <summary>
    /// 一阶段获得的护盾值 (2.3)
    /// </summary>
    private const int PhaseOneBlock = 30;
    
    //一阶段分别给予的状态卡数量
    private const int PhaseOneState = 1;
    private const int PhaseOneStates = 2;
    private const int PhaseOneBuff = 1;
    private const int PhaseOneBuffs = 2;
    
    

    // -- 二阶段参数 --

    /// <summary>
    /// 二阶段单体重击伤害 (4.2)
    /// 基础35, 高进阶38
    /// </summary>
    private int PhaseTwoBigAtk => AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 27, 24);

    /// <summary>
    /// 二阶段多段攻击每段伤害 (4.3)
    /// 基础3, 高进阶4
    /// </summary>
    private int PhaseTwoMultiAtk => AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 10, 8);

    /// <summary>
    /// 二阶段多段攻击段数 (4.3)
    /// </summary>
    private const int PhaseTwoMultiCount = 4;

    /// <summary>
    /// 二阶段混合攻击伤害 (4.4)
    /// </summary>
    private const int PhaseTwoAtk = 20;

    /// <summary>
    /// 二阶段获得的护盾值 (4.4)
    /// </summary>
    private const int PhaseTwoBlock = 55;

    /// <summary>
    /// 二阶段初始化时双方获得的易伤层数 (4.1)
    /// </summary>
    private const int PhaseTwoVulnCount = 99;

    // -- 复活参数 --

    /// <summary>
    /// 第二条命的固定基础HP
    /// </summary>
    private const int SecondPhaseBaseHp = 500;

    // ===== 内部状态 =====

    /// <summary>
    /// 是否已进入二阶段
    /// </summary>
    private bool _isSecondPhase;

    /// <summary>
    /// 死亡/复活状态缓存（参照 TestSubjectOne.DeadState）
    /// </summary>
    private MoveState _respawnState;

    public MoveState RespawnState
    {
        get => _respawnState;
        private set
        {
            AssertMutable();
            _respawnState = value;
        }
    }

    public override int MinInitialHp => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 550, 500);
    public override int MaxInitialHp => MinInitialHp;

    public override string? CustomVisualPath =>
        "res://STS2_Tomorin_Mod/scenes/creature_visuals/enemies/crychic_phatom.tscn";

    #region Lifecycle

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        // base.Creature.Died += AfterDeath;
        _isSecondPhase = false;
        
        //添加复活buff
        await PowerCmd.Apply<CrychicPhantomPower>( new ThrowingPlayerChoiceContext(), Creature, 1, base.Creature, null);
    }

    /// <summary>
    /// 死亡回调: 一血死亡 → 触发复活状态; 二阶段死亡 → 正常结算
    /// </summary>
    // private void AfterDeath(Creature creature)
    // {
    //     if (!_isSecondPhase)
    //     {
    //         SetMoveImmediate(RespawnState, forceTransition: true);
    //     }
    //     else
    //     {
    //         base.Creature.Died -= AfterDeath;
    //     }
    // }
    public Task TriggerDeadState()
    {
        SetMoveImmediate(RespawnState, forceTransition: true);
        return Task.CompletedTask;
    }

    #endregion

    #region State Machine

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var states = new List<MonsterState>();

        // -- 一阶段 --
        var initState = new MoveState("INIT_STATE", InitMove, [new BuffIntent()]);
        var phase1Atk1 = new MoveState("PHASE1_ATK1_STATE", Phase1Atk1Move,
            new SingleAttackIntent(PhaseOneBigAtk));
        var phase1Atk2 = new MoveState("PHASE1_ATK2_STATE", Phase1Atk2Move,
            new MultiAttackIntent(PhaseOneMultiAtk, PhaseOneMultiCount), new StatusIntent(PhaseOneState));
        var phase1Heal = new MoveState("PHASE1_HEAL_STATE", Phase1HealMove,
            [new HealIntent(), new DefendIntent(), new StatusIntent(PhaseOneStates)]);

        // -- 复活 --
        RespawnState = new MoveState("RESPAWN_STATE", RespawnMove,
            [new HealIntent(), new BuffIntent()])
        {
            MustPerformOnceBeforeTransitioning = true
        };

        // -- 二阶段 --
        var phase2Init = new MoveState("PHASE2_INIT_STATE", Phase2InitMove,
            [new DebuffIntent(), new DebuffIntent()])
        {
            MustPerformOnceBeforeTransitioning = true
        };
        var phase2Atk1 = new MoveState("PHASE2_ATK1_STATE", Phase2Atk1Move,
            new SingleAttackIntent(PhaseTwoBigAtk));
        var phase2Atk2 = new MoveState("PHASE2_ATK2_STATE", Phase2Atk2Move,
            new MultiAttackIntent(PhaseTwoMultiAtk, PhaseTwoMultiCount));
        var phase2Block = new MoveState("PHASE2_BLOCK_STATE", Phase2BlockMove,
            [new SingleAttackIntent(PhaseTwoAtk), new DefendIntent()]);

        // -- 连线 --
        initState.FollowUpState = phase1Atk1;
        phase1Atk1.FollowUpState = phase1Atk2;
        phase1Atk2.FollowUpState = phase1Heal;
        phase1Heal.FollowUpState = phase1Atk1;

        RespawnState.FollowUpState = phase2Init;
        phase2Init.FollowUpState = phase2Atk1;
        phase2Atk1.FollowUpState = phase2Atk2;
        phase2Atk2.FollowUpState = phase2Block;
        phase2Block.FollowUpState = phase2Atk1;

        states.Add(initState);
        states.Add(phase1Atk1);
        states.Add(phase1Atk2);
        states.Add(phase1Heal);
        states.Add(RespawnState);
        states.Add(phase2Init);
        states.Add(phase2Atk1);
        states.Add(phase2Atk2);
        states.Add(phase2Block);

        return new MonsterMoveStateMachine(states, initState);
    }

    #endregion

    #region Phase 1 Moves

    /// <summary>
    /// 战斗初始化: 给所有玩家挂 CrychicRememberPower(1层)
    /// </summary>
    private async Task InitMove(IReadOnlyList<Creature> targets)
    {
        await PowerCmd.Apply<CrychicRememberPower>(
            new MegaCrit.Sts2.Core.GameActions.Multiplayer.ThrowingPlayerChoiceContext(),
            targets, 1, base.Creature, null);
        
        await PowerCmd.Apply<CrychicPhantomCounterPower>( new ThrowingPlayerChoiceContext(), targets, 1, base.Creature, null);
        
        
        // await CardPileCmd.AddToCombatAndPreview<CrychicPhantomState>(targets, PileType.Hand, PhaseOneState, null);
        
    }

    /// <summary>
    /// 一阶段 2.1: 单体重击25
    /// </summary>
    private async Task Phase1Atk1Move(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(PhaseOneBigAtk).FromMonster(this)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(null);
        
        // for (int i = 0; i < targets.Count; i++)
        // {
        //     var target = targets[i];
        //     if (target.HasPower<CrychicRememberPower>())
        //     {
        //         var power = target.GetPower<CrychicRememberPower>();
        //         await PowerCmd.ModifyAmount(new BlockingPlayerChoiceContext(), power, PhaseOneBuff, Creature, null);
        //     }
        // }
        
        // await CardPileCmd.AddToCombatAndPreview<CrychicPhantomState>(targets, PileType.Hand, PhaseOneState, null, CardPilePosition.Random);
    }

    /// <summary>
    /// 一阶段 2.2: 多段攻击10×3
    /// </summary>
    private async Task Phase1Atk2Move(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(PhaseOneMultiAtk).WithHitCount(PhaseOneMultiCount)
            .FromMonster(this)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(null);

        // for (int i = 0; i < targets.Count; i++)
        // {
        //     var target = targets[i];
        //     if (target.HasPower<CrychicRememberPower>())
        //     {
        //         var power = target.GetPower<CrychicRememberPower>();
        //         await PowerCmd.ModifyAmount(new BlockingPlayerChoiceContext(), power, PhaseOneBuffs, Creature, null);
        //     }
        // }
        await CardPileCmd.AddToCombatAndPreview<CrychicPhantomState>(targets, PileType.Hand, PhaseOneState, null, CardPilePosition.Random);
    }

    /// <summary>
    /// 一阶段 2.3: 回复50HP + 获得30护盾
    /// </summary>
    private async Task Phase1HealMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.Heal(base.Creature, PhaseOneHeal);
        await CreatureCmd.GainBlock(base.Creature, PhaseOneBlock, ValueProp.Move, null);
        
        await CardPileCmd.AddToCombatAndPreview<CrychicPhantomState>(targets, PileType.Hand, PhaseOneStates, null, CardPilePosition.Random);
    }

    #endregion

    #region Respawn

    /// <summary>
    /// 复活流程:
    /// 1. 收集所有玩家CrychicRemember层数总和
    /// 2. 计算新最大HP = 基础HP + 总层数 × HpPerRememberStack × 玩家数量
    /// 3. 设置新最大HP并回满
    /// 4. 移除所有玩家的CrychicRememberPower
    /// 5. 执行卡牌回收 + Affliction分配
    /// 6. 进入二阶段状态机
    /// </summary>
    private async Task RespawnMove(IReadOnlyList<Creature> targets)
    {
        // 1. 收集所有玩家CrychicRemember总层数
        decimal totalStacks = 0;
        foreach (var creature in targets)
        {
            if (creature.HasPower<CrychicRememberPower>())
            {
                totalStacks += creature.GetPower<CrychicRememberPower>().Amount;
            }
        }

        // 2. 计算新HP（含多人缩放）
        int playerCount = base.CombatState.Players.Count;
        int scaledHp = SecondPhaseBaseHp + (int)totalStacks * HpPerRememberStack * playerCount;

        // 3. 复活：设置最大HP并回满
        await CreatureCmd.SetMaxHp(base.Creature, scaledHp);
        await CreatureCmd.Heal(base.Creature, scaledHp);

        // 4. 移除所有玩家的CrychicRememberPower
        foreach (var creature in targets)
        {
            if (creature.HasPower<CrychicRememberPower>())
            {
                await PowerCmd.Remove<CrychicRememberPower>(creature);
            }
            //移除counter buff
            if (creature.HasPower<CrychicPhantomCounterPower>())
            {
                await PowerCmd.Remove<CrychicPhantomCounterPower>(creature);
            }
        }
        
        //赋予再演buff
        // await PowerCmd.Apply<CrychicRelivePower>(new ThrowingPlayerChoiceContext(), Creature, 1, base.Creature, null);

        // 5. 卡牌回收 + Affliction分配
        await RecycleCardsAndAfflict(targets);

        //移除buff
        await PowerCmd.Remove<CrychicPhantomPower>(base.Creature);

        _isSecondPhase = true;
    }

    /// <summary>
    /// 卡牌回收与Affliction分配:
    /// - 墓地卡牌 → 回抽牌堆
    /// - 消耗堆非状态卡 → 回抽牌堆
    /// - 所有回卡组的卡按顺序循环分配Affliction
    ///
    /// Affliction循环: EnergyCurse → ExhaustCurse → DiscardCurse → DrawLessCurse → SelfDamageCurse
    ///
    /// 注: 除外区(PileType.Exile)在StS2中不存在，故跳过除外区处理
    /// </summary>
    private async Task RecycleCardsAndAfflict(IReadOnlyList<Creature> targets)
    {
        // 定义5种Affliction类型的循环顺序
        var afflictionFactories = new Func<AfflictionModel>[]
        {
            () => ModelDb.Affliction<CrychicEnergyCurse>().ToMutable(),
            () => ModelDb.Affliction<CrychicExhaustCurse>().ToMutable(),
            () => ModelDb.Affliction<CrychicDiscardCurse>().ToMutable(),
            () => ModelDb.Affliction<CrychicDrawLessCurse>().ToMutable(),
            () => ModelDb.Affliction<CrychicDamageCurse>().ToMutable()
        };

        foreach (var creature in targets)
        {
            var player = creature.Player;
            if (player == null) continue;

            var cardsToReturn = new List<CardModel>();

            // 墓地卡牌（弃牌堆）
            var discardPile = player.PlayerCombatState.DiscardPile;
            foreach (var card in discardPile.Cards.ToList())
            {
                cardsToReturn.Add(card);
            }

            // 消耗堆：非状态卡
            var exhaustPile = player.PlayerCombatState.ExhaustPile;
            foreach (var card in exhaustPile.Cards.ToList())
            {
                if (card.Type != CardType.Status)
                {
                    cardsToReturn.Add(card);
                }
            }

            // 将回收的卡牌移回抽牌堆并按序分配Affliction
            for (int i = 0; i < cardsToReturn.Count; i++)
            {
                var card = cardsToReturn[i];
                await CardPileCmd.Add(card, PileType.Draw, CardPilePosition.Random, this);
            
                // 按顺序循环分配Affliction
                int afflictionIndex = i % afflictionFactories.Length;
                var affliction = afflictionFactories[afflictionIndex]();
                
                // 为该Affliction类型做特殊处理
                await CardCmd.Afflict(affliction, card, 1);
            }
        }
    }

    #endregion

    #region Phase 2 Moves

    /// <summary>
    /// 二阶段初始化 4.1: 敌人自身和所有玩家各获得99层易伤（仅执行一次）
    /// </summary>
    private async Task Phase2InitMove(IReadOnlyList<Creature> targets)
    {
        // 敌人自身
        await PowerCmd.Apply<VulnerablePower>(
            new MegaCrit.Sts2.Core.GameActions.Multiplayer.ThrowingPlayerChoiceContext(),
            base.Creature, PhaseTwoVulnCount, base.Creature, null);

        // 所有玩家
        await PowerCmd.Apply<VulnerablePower>(
            new MegaCrit.Sts2.Core.GameActions.Multiplayer.ThrowingPlayerChoiceContext(),
            targets, PhaseTwoVulnCount, base.Creature, null);
    }

    /// <summary>
    /// 二阶段 4.2: 单体攻击35
    /// </summary>
    private async Task Phase2Atk1Move(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(PhaseTwoBigAtk).FromMonster(this)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(null);
    }

    /// <summary>
    /// 二阶段 4.3: 多段攻击3×10
    /// </summary>
    private async Task Phase2Atk2Move(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(PhaseTwoMultiAtk).WithHitCount(PhaseTwoMultiCount)
            .FromMonster(this)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(null);
    }

    /// <summary>
    /// 二阶段 4.4: 攻击20 + 获得55护盾
    /// </summary>
    private async Task Phase2BlockMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(PhaseTwoAtk).FromMonster(this)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(null);
        await CreatureCmd.GainBlock(base.Creature, PhaseTwoBlock, ValueProp.Move, null);
    }

    #endregion
}