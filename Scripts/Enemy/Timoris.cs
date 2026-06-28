using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.ValueProps;
using STS2_Tomorin_Mod.Powers;

namespace STS2_Tomorin_Mod.Enemy;

public class Timoris : CustomMonsterModel
{
    private int AttackDamage => AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 25, 22);
    private const int BlockAmount = 20;

    private MoveState _cState;
    private MoveState _nonCState;

    public MoveState CState
    {
        get => _cState;
        private set { AssertMutable(); _cState = value; }
    }

    public MoveState NonCState
    {
        get => _nonCState;
        private set { AssertMutable(); _nonCState = value; }
    }

    public override int MinInitialHp => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 200, 180);
    public override int MaxInitialHp => MinInitialHp;

    public override string? CustomVisualPath =>
        "res://STS2_Tomorin_Mod/scenes/creature_visuals/enemies/timoris.tscn";

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        await PowerCmd.Apply<TimorisPassivePower>(new ThrowingPlayerChoiceContext(), Creature, 1, base.Creature, null);
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var states = new List<MonsterState>();

        CState = new MoveState("TIMORIS_C_STATE", CMove, new SingleAttackIntent(AttackDamage));
        NonCState = new MoveState("TIMORIS_NONC_STATE", NonCMove, new DefendIntent());

        CState.FollowUpState = CState;
        NonCState.FollowUpState = NonCState;

        states.Add(CState);
        states.Add(NonCState);

        return new MonsterMoveStateMachine(states, NonCState);
    }

    private async Task CMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(AttackDamage).FromMonster(this)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(null);
    }

    private async Task NonCMove(IReadOnlyList<Creature> targets)
    {
        var cPosition = base.CombatState.Enemies.FirstOrDefault(e => e != base.Creature && !e.HasPower<IntangiblePower>());
        if (cPosition != null)
        {
            await CreatureCmd.GainBlock(cPosition, BlockAmount, ValueProp.Move, null);
        }
    }
}
