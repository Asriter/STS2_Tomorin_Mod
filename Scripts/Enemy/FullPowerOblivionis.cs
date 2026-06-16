using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.ValueProps;
using STS2_Tomorin_Mod.Audio;
using STS2_Tomorin_Mod.Cards.EnemyCards;
using STS2_Tomorin_Mod.Patch;
using STS2_Tomorin_Mod.Powers;

namespace STS2_Tomorin_Mod.Enemy;

public class FullPowerOblivionis : CustomMonsterModel
{
    private const int State1Atk = 20;    // 18-22
    private const int CurseCount = 3;
    private const int State2Atk = 15;
    private const int State2Count = 3;
    private const int State3Atk = 50;
    private const int State3Str = 3;

    public override int MinInitialHp => 1000;
    public override int MaxInitialHp => MinInitialHp;

    public override string? CustomVisualPath =>
        "res://STS2_Tomorin_Mod/scenes/creature_visuals/enemies/full_power_oblivionis.tscn";

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        var power = await PowerCmd.Apply<OblivionisHiddenInheritPower>(new ThrowingPlayerChoiceContext(), Creature, 1, base.Creature, null);
        await power.SetNewPower();
        await PowerCmd.Apply<OblivionisHiddenBlockPower>(new ThrowingPlayerChoiceContext(), Creature, 1, base.Creature, null);
        
        //播放bgm
        CustomAudioController.PlayMusic("FullPowerOblivionisBgm");
    }

    public async Task AnimIn()
    {
        await CreatureCmd.TriggerAnim(base.Creature, "Start", 3.0f);
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var states = new List<MonsterState>();

        var state1 = new MoveState("FPO_S1", State1Move,
            new SingleAttackIntent(State1Atk), new StatusIntent(CurseCount));
        var state2 = new MoveState("FPO_S2", State2Move,
            new MultiAttackIntent(State2Atk, State2Count));
        var state3 = new MoveState("FPO_S3", State3Move,
            new SingleAttackIntent(State3Atk), new BuffIntent());

        state1.FollowUpState = state2;
        state2.FollowUpState = state3;
        state3.FollowUpState = state1;

        states.Add(state1);
        states.Add(state2);
        states.Add(state3);

        return new MonsterMoveStateMachine(states, state1);
    }

    private async Task State1Move(IReadOnlyList<Creature> targets)
    {
        // 打18-22
        await DamageCmd.Attack(State1Atk).FromMonster(this)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(null);
   
        // 往弃牌堆放3张重压
        foreach (Creature target in targets)
        {
			Player player = target.Player ?? target.PetOwner;
            List<CardPileAddResult> statusCards = new List<CardPileAddResult>();
            for (int i = 0; i < 3; i++)
            {
                CardModel card = base.CombatState.CreateCard<PressureCurse>(player);
                List<CardPileAddResult> list = statusCards;
                list.Add(await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Discard, null, CardPilePosition.Random));
            }
            if (LocalContext.IsMe(player))
            {
                CardCmd.PreviewCardPileAdd(statusCards);
                await Cmd.Wait(1f);
            }
        }
    }

    private async Task State2Move(IReadOnlyList<Creature> targets)
    {
        // 打15x3
        await DamageCmd.Attack(State2Atk).WithHitCount(State2Count)
            .FromMonster(this)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(null);
    }

    private async Task State3Move(IReadOnlyList<Creature> targets)
    {
        // 打50
        await DamageCmd.Attack(State3Atk).FromMonster(this)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(null);

        // +3力量
        await PowerCmd.Apply<StrengthPower>(new ThrowingPlayerChoiceContext(), base.Creature, State3Str, base.Creature, null);
    }
}
