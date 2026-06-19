using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.ValueProps;
using STS2_Tomorin_Mod.Cards.EnemyCards;
using STS2_Tomorin_Mod.Powers;

namespace STS2_Tomorin_Mod.Enemy;

public class Oblivionis : CustomMonsterModel
{
    // Phase 1
    private const int Phase1NonCHealAmount = 18;
    private const int Phase1CHealAmount = 30;
    private const int Phase1LessDrawAmount = 1;
    private const int Phase1VulnerableAmount = 1;
    private const int Phase1MultiHitCount = 3;

    private static int Phase1HighDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 28, 25);

    private static int Phase1MediumDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 23, 20);

    private static int Phase1HighMultiDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 9, 8);

    private static int Phase1LowMultiDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 7, 6);

    // Phase 2
    private const int Phase2Atk1 = 14;   // 12-15
    private const int Phase2WeakVuln = 2;
    private const int Phase2Atk2 = 30;   // 28-32
    private const int Phase2Atk3Dmg = 10; // 10-11 per hit
    private const int Phase2Atk3Count = 3;
    private const int Phase2StrAmount = 1;

    private MoveState _phase1CNoDeadState1;
    private MoveState _phase1COneDeadState1;
    private MoveState _phase1CTwoDeadState1;
    private MoveState _phase1CThreeDeadState1;
    private int _phase1DeadAllyCount;
    private MoveState _nonCState;
    private MoveState _phase2State1;
    private MoveState _phase2State2;
    private MoveState _phase2State3;
    private MoveState _deadState;

    public MoveState CState => GetPhase1CEntryState(_phase1DeadAllyCount);

    public MoveState NonCState
    {
        get => _nonCState;
        private set { AssertMutable(); _nonCState = value; }
    }

    public MoveState Phase2State
    {
        get => _phase2State1;
        private set { AssertMutable(); _phase2State1 = value; }
    }

    public MoveState DeadState
    {
        get => _deadState;
        private set { AssertMutable(); _deadState = value; }
    }
    
    //等待复活；
    private MoveState _waitRelive;
    public MoveState WaitRelive
    {
        get => _waitRelive;
        private set { AssertMutable(); _waitRelive = value; }
    }

    private bool _isHiddenDeath;

    public override int MinInitialHp => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 640, 600);

    public override int MaxInitialHp => MinInitialHp;

    public override string? CustomVisualPath =>
        "res://STS2_Tomorin_Mod/scenes/creature_visuals/enemies/oblivionis.tscn";

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        await PowerCmd.Apply<CenterPositionManagerPower>(new ThrowingPlayerChoiceContext(), Creature, 1, base.Creature, null);
        await PowerCmd.Apply<OblivionisHiddenRevivalPower>(new ThrowingPlayerChoiceContext(), Creature, 1, base.Creature, null);
    }

    public void PrepareForHiddenDeath()
    {
        _isHiddenDeath = true;
    }

    public void SetPhase1CStateByDeadAllies(int deadAllyCount, bool forceTransition = true)
    {
        _phase1DeadAllyCount = Math.Clamp(deadAllyCount, 0, 3);
        SetMoveImmediate(GetPhase1CEntryState(_phase1DeadAllyCount), forceTransition);
    }

    private MoveState GetPhase1CEntryState(int deadAllyCount)
    {
        return Math.Clamp(deadAllyCount, 0, 3) switch
        {
            0 => _phase1CNoDeadState1,
            1 => _phase1COneDeadState1,
            2 => _phase1CTwoDeadState1,
            _ => _phase1CThreeDeadState1
        };
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var states = new List<MonsterState>();

        // Phase 1 C-position states
        var noDeadS1 = new MoveState("OBLIVIONIS_P1_C_0_S1", Phase1CNoDeadS1Move,
            new SingleAttackIntent(Phase1HighDamage), new DebuffIntent());
        var noDeadS2 = new MoveState("OBLIVIONIS_P1_C_0_S2", Phase1CNoDeadS2Move,
            new MultiAttackIntent(Phase1HighMultiDamage, Phase1MultiHitCount));
        var noDeadS3 = new MoveState("OBLIVIONIS_P1_C_0_S3", Phase1CNoDeadS3Move,
            new HealIntent(), new DebuffIntent());

        var oneDeadS1 = new MoveState("OBLIVIONIS_P1_C_1_S1", Phase1COneDeadS1Move,
            new SingleAttackIntent(Phase1HighDamage));
        var oneDeadS2 = new MoveState("OBLIVIONIS_P1_C_1_S2", Phase1CLowMultiMove,
            new MultiAttackIntent(Phase1LowMultiDamage, Phase1MultiHitCount));
        var oneDeadS3 = new MoveState("OBLIVIONIS_P1_C_1_S3", Phase1COneDeadS3Move,
            new HealIntent(), new DebuffIntent());

        var twoDeadS1 = new MoveState("OBLIVIONIS_P1_C_2_S1", Phase1CTwoOrThreeDeadS1Move,
            new SingleAttackIntent(Phase1MediumDamage));
        var twoDeadS2 = new MoveState("OBLIVIONIS_P1_C_2_S2", Phase1CLowMultiMove,
            new MultiAttackIntent(Phase1LowMultiDamage, Phase1MultiHitCount));
        var twoDeadS3 = new MoveState("OBLIVIONIS_P1_C_2_S3", Phase1CTwoDeadS3Move,
            new HealIntent());

        var threeDeadS1 = new MoveState("OBLIVIONIS_P1_C_3_S1", Phase1CTwoOrThreeDeadS1Move,
            new SingleAttackIntent(Phase1MediumDamage));
        var threeDeadS2 = new MoveState("OBLIVIONIS_P1_C_3_S2", Phase1CLowMultiMove,
            new MultiAttackIntent(Phase1LowMultiDamage, Phase1MultiHitCount));

        noDeadS1.FollowUpState = noDeadS2;
        noDeadS2.FollowUpState = noDeadS3;
        noDeadS3.FollowUpState = noDeadS1;

        oneDeadS1.FollowUpState = oneDeadS2;
        oneDeadS2.FollowUpState = oneDeadS3;
        oneDeadS3.FollowUpState = oneDeadS1;

        twoDeadS1.FollowUpState = twoDeadS2;
        twoDeadS2.FollowUpState = twoDeadS3;
        twoDeadS3.FollowUpState = twoDeadS1;

        threeDeadS1.FollowUpState = threeDeadS2;
        threeDeadS2.FollowUpState = threeDeadS1;

        _phase1CNoDeadState1 = noDeadS1;
        _phase1COneDeadState1 = oneDeadS1;
        _phase1CTwoDeadState1 = twoDeadS1;
        _phase1CThreeDeadState1 = threeDeadS1;

        // Phase 1 non-C position
        NonCState = new MoveState("OBLIVIONIS_NONC_STATE", Phase1NonCMove,
            new HealIntent());
        NonCState.FollowUpState = NonCState;

        // Phase 2
        WaitRelive = new MoveState("Relive", WaitReliveMove, new BuffIntent(), new HealIntent());
        Phase2State = new MoveState("OBLIVIONIS_P2_S1", Phase2S1Move,
            new SingleAttackIntent(Phase2Atk1), new DebuffIntent());
        _phase2State2 = new MoveState("OBLIVIONIS_P2_S2", Phase2S2Move,
            new SingleAttackIntent(Phase2Atk2), new DebuffIntent());
        _phase2State3 = new MoveState("OBLIVIONIS_P2_S3", Phase2S3Move,
            new MultiAttackIntent(Phase2Atk3Dmg, Phase2Atk3Count), new DefendIntent(), new BuffIntent());

        WaitRelive.FollowUpState = Phase2State;
        Phase2State.FollowUpState = _phase2State2;
        _phase2State2.FollowUpState = _phase2State3;
        _phase2State3.FollowUpState = Phase2State;

        // Dead state (placeholder for hidden boss transition)
        DeadState = new MoveState("OBLIVIONIS_DEAD", DeadMove);
        DeadState.MustPerformOnceBeforeTransitioning = true;

        states.Add(noDeadS1);
        states.Add(noDeadS2);
        states.Add(noDeadS3);
        states.Add(oneDeadS1);
        states.Add(oneDeadS2);
        states.Add(oneDeadS3);
        states.Add(twoDeadS1);
        states.Add(twoDeadS2);
        states.Add(twoDeadS3);
        states.Add(threeDeadS1);
        states.Add(threeDeadS2);
        states.Add(NonCState);
        states.Add(Phase2State);
        states.Add(_phase2State2);
        states.Add(_phase2State3);
        states.Add(DeadState);
        states.Add(WaitRelive);

        return new MonsterMoveStateMachine(states, NonCState);
    }
    
    //复活阶段
    private const int Phase2HpBonus = 500;
    private async Task WaitReliveMove(IReadOnlyList<Creature> targets)
    {
        //移除自身和复活buff
        // 计算二阶段HP: 当前最大HP + 500
        decimal newMaxHp = Creature.MaxHp + Phase2HpBonus;
        await CreatureCmd.SetMaxHp(Creature, newMaxHp);
        await CreatureCmd.Heal(Creature, newMaxHp);
        
        await PowerCmd.Remove<CenterPositionManagerPower>(Creature);
        await PowerCmd.Remove<OblivionisHiddenRevivalPower>(Creature);
        
        await PowerCmd.Apply<OblivionisPhase2Power>(new ThrowingPlayerChoiceContext(), Creature, 1, Creature, null, silent:true);
        
        await CreatureCmd.TriggerAnim(Creature, "Cast", 1.0f);
        
    }

    private async Task HealAliveEnemies(decimal amount)
    {
        foreach (var enemy in base.CombatState.Enemies)
        {
            if (enemy.IsAlive)
                await CreatureCmd.Heal(enemy, amount);
        }
    }

    private async Task Phase1SingleAttack(int damage)
    {
        await DamageCmd.Attack(damage).FromMonster(this)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(null);
    }

    private async Task Phase1MultiAttack(int damage)
    {
        await DamageCmd.Attack(damage).WithHitCount(Phase1MultiHitCount)
            .FromMonster(this)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(null);
    }

    // Phase 1 C位，0名队友死亡：打25/28 + 全体玩家下回合少抽1张
    private async Task Phase1CNoDeadS1Move(IReadOnlyList<Creature> targets)
    {
        await Phase1SingleAttack(Phase1HighDamage);
        await PowerCmd.Apply<LessDrawNextTurnPower>(new ThrowingPlayerChoiceContext(), targets,
            Phase1LessDrawAmount, base.Creature, null);
    }

    // Phase 1 C位，0名队友死亡：打8/9x3
    private async Task Phase1CNoDeadS2Move(IReadOnlyList<Creature> targets)
    {
        await Phase1MultiAttack(Phase1HighMultiDamage);
    }

    // Phase 1 C位，0名队友死亡：敌方全体回复30 + 全体玩家1脆弱
    private async Task Phase1CNoDeadS3Move(IReadOnlyList<Creature> targets)
    {
        await HealAliveEnemies(Phase1CHealAmount);
        await PowerCmd.Apply<VulnerablePower>(new ThrowingPlayerChoiceContext(), targets,
            Phase1VulnerableAmount, base.Creature, null);
    }

    // Phase 1 C位，1名队友死亡：打25/28
    private async Task Phase1COneDeadS1Move(IReadOnlyList<Creature> targets)
    {
        await Phase1SingleAttack(Phase1HighDamage);
    }

    // Phase 1 C位，1/2/3名队友死亡：打6/7x3
    private async Task Phase1CLowMultiMove(IReadOnlyList<Creature> targets)
    {
        await Phase1MultiAttack(Phase1LowMultiDamage);
    }

    // Phase 1 C位，1名队友死亡：敌方全体回复30 + 全体玩家1脆弱
    private async Task Phase1COneDeadS3Move(IReadOnlyList<Creature> targets)
    {
        await HealAliveEnemies(Phase1CHealAmount);
        await PowerCmd.Apply<VulnerablePower>(new ThrowingPlayerChoiceContext(), targets,
            Phase1VulnerableAmount, base.Creature, null);
    }

    // Phase 1 C位，2/3名队友死亡：打20/23
    private async Task Phase1CTwoOrThreeDeadS1Move(IReadOnlyList<Creature> targets)
    {
        await Phase1SingleAttack(Phase1MediumDamage);
    }

    // Phase 1 C位，2名队友死亡：敌方全体回复30
    private async Task Phase1CTwoDeadS3Move(IReadOnlyList<Creature> targets)
    {
        await HealAliveEnemies(Phase1CHealAmount);
    }

    // Phase 1 非C位: 所有敌人回复18HP
    private async Task Phase1NonCMove(IReadOnlyList<Creature> targets)
    {
        await HealAliveEnemies(Phase1NonCHealAmount);
    }

    // Phase 2 State 1: 12-15 + 所有玩家2虚弱2脆弱
    private async Task Phase2S1Move(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(Phase2Atk1).FromMonster(this)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(null);

        await PowerCmd.Apply<WeakPower>(new ThrowingPlayerChoiceContext(), targets, Phase2WeakVuln, base.Creature, null);
        await PowerCmd.Apply<VulnerablePower>(new ThrowingPlayerChoiceContext(), targets, Phase2WeakVuln, base.Creature, null);
    }

    // Phase 2 State 2: 28-32 + 所有玩家下回合少抽1张
    private async Task Phase2S2Move(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(Phase2Atk2).FromMonster(this)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(null);

        await PowerCmd.Apply<LessDrawNextTurnPower>(new ThrowingPlayerChoiceContext(), targets, 1, base.Creature, null);
    }

    // Phase 2 State 3: 10-11x3 + 消耗区卡数量格挡 + 1力量
    private async Task Phase2S3Move(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(Phase2Atk3Dmg).WithHitCount(Phase2Atk3Count)
            .FromMonster(this)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(null);

        // 获得消耗区卡片数量的格挡
        int exhaustCount = 0;
        foreach (var target in targets)
        {
            exhaustCount += target.Player?.PlayerCombatState.ExhaustPile.Cards.Count ?? 0;
        }
        if (exhaustCount > 0)
            await CreatureCmd.GainBlock(base.Creature, exhaustCount, ValueProp.Move, null);

        // +1力量
        await PowerCmd.Apply<StrengthPower>(new ThrowingPlayerChoiceContext(), base.Creature, Phase2StrAmount, base.Creature, null);
    }

    // Dead move (hidden boss path - doesn't execute, just holds position)
    private async Task DeadMove(IReadOnlyList<Creature> targets)
    {
        _isHiddenDeath = false;
    }
}
