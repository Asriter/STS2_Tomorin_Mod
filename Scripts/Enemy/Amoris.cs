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
using STS2_Tomorin_Mod.Powers;

namespace STS2_Tomorin_Mod.Enemy;

public class Amoris : CustomMonsterModel
{
    private const int MultiAtkDamage = 6;
    private const int MultiAtkCount = 3;
    private const int StrengthAmount = 1;

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
        "res://STS2_Tomorin_Mod/scenes/creature_visuals/enemies/amoris.tscn";

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        await PowerCmd.Apply<AmorisPassivePower>(new ThrowingPlayerChoiceContext(), Creature, 1, base.Creature, null);
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var states = new List<MonsterState>();

        CState = new MoveState("AMORIS_C_STATE", CMove, new MultiAttackIntent(MultiAtkDamage, MultiAtkCount));
        NonCState = new MoveState("AMORIS_NONC_STATE", NonCMove, new BuffIntent());

        CState.FollowUpState = CState;
        NonCState.FollowUpState = NonCState;

        states.Add(CState);
        states.Add(NonCState);

        return new MonsterMoveStateMachine(states, NonCState);
    }

    private async Task CMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(MultiAtkDamage).WithHitCount(MultiAtkCount)
            .FromMonster(this)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(null);
        
        //自成长
        await PowerCmd.Apply<StrengthPower>(new ThrowingPlayerChoiceContext(), Creature, StrengthAmount, base.Creature, null);
    }

    private async Task NonCMove(IReadOnlyList<Creature> targets)
    {
        var cPosition = base.CombatState.Enemies.FirstOrDefault(e => e != base.Creature && !e.HasPower<IntangiblePower>());
        if (cPosition != null)
        {
            await PowerCmd.Apply<StrengthPower>(new ThrowingPlayerChoiceContext(), cPosition, StrengthAmount, base.Creature, null);
        }
    }
}
