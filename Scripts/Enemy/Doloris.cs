using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using STS2_Tomorin_Mod.Powers;

namespace STS2_Tomorin_Mod.Enemy;

public class Doloris : CustomMonsterModel
{
    private int HealAmount => AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 30, 20);
    private int AttackDamage => AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 18, 15);
    

    private const int WoundCount = 2;
    private MoveState _cState;
    private MoveState _nonCState;

    public MoveState CState
    {
        get => _cState;
        private set
        {
            AssertMutable();
            _cState = value;
        }
    }

    public MoveState NonCState
    {
        get => _nonCState;
        private set
        {
            AssertMutable();
            _nonCState = value;
        }
    }

    public override int MinInitialHp => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 200, 180);

    public override int MaxInitialHp => MinInitialHp;

    public override string? CustomVisualPath =>
        "res://STS2_Tomorin_Mod/scenes/creature_visuals/enemies/doloris.tscn";

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        // await PowerCmd.Apply<DolorisPassivePower>(new ThrowingPlayerChoiceContext(), Creature, 1, base.Creature, null);
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var states = new List<MonsterState>();

        CState = new MoveState("DOLORIS_C_STATE", CMove,
            new SingleAttackIntent(AttackDamage), new DebuffIntent(), new StatusIntent(WoundCount));
        NonCState = new MoveState("DOLORIS_NONC_STATE", NonCMove,
            new HealIntent());

        // 自循环
        CState.FollowUpState = CState;
        NonCState.FollowUpState = NonCState;

        states.Add(CState);
        states.Add(NonCState);

        // 初始状态: CState（默认C位）
        return new MonsterMoveStateMachine(states, CState);
    }

    private async Task CMove(IReadOnlyList<Creature> targets)
    {
        // 打15
        await DamageCmd.Attack(AttackDamage).FromMonster(this)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(null);
        
        // 给2张伤口
        await CardPileCmd.AddToCombatAndPreview<Wound>(targets, PileType.Discard, WoundCount, null, CardPilePosition.Random);

        // 下回合抽卡-1
        await PowerCmd.Apply<LessDrawNextTurnPower>(new ThrowingPlayerChoiceContext(), targets, 1, base.Creature, null);
    }

    private async Task NonCMove(IReadOnlyList<Creature> targets)
    {
        // 给C位敌人+30HP
        foreach (var enemy in base.CombatState.Enemies)
        {
            if (enemy != base.Creature && !enemy.HasPower<IntangiblePower>())
            {
                await CreatureCmd.Heal(enemy, HealAmount);
                break;
            }
        }
    }
}