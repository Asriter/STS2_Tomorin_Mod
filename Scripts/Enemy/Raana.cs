using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Audio;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.ValueProps;
using STS2_Tomorin_Mod.Audio;
using STS2_Tomorin_Mod.Cards.Collections;
using STS2_Tomorin_Mod.Powers;
using STS2_Tomorin_Mod.Relics;

namespace STS2_Tomorin_Mod.Enemy;

public class Raana : CustomMonsterModel
{
    private const int WeakenedEntryBlock = 18;
    private const int UnwellStacks = 5;
    private const int S2HitCount = 4;
    private const int S2Block = 25;
    private const int S3HealPerDebuffType = 8;
    private const int BuffetsPerPlayer = 2;
    private const int S4HighHitCount = 3;

    private int S1Attack => AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 21, 18);
    private int S2Attack => AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 6, 5);
    private int S4Attack => AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 32, 28);
    private int S4HighAttack => AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 11, 10);

    public override int MinInitialHp => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 425, 395);
    public override int MaxInitialHp => MinInitialHp;
    public override string? CustomVisualPath => "res://STS2_Tomorin_Mod/scenes/creature_visuals/enemies/raana_boss.tscn";

    private MoveState _sleepState = null!;
    private MoveState _s1State = null!;
    private MoveState _s2State = null!;
    private MoveState _s3State = null!;
    private MoveState _s4LowState = null!;
    private MoveState _s4MidState = null!;
    private MoveState _s4HighState = null!;

    private bool _applyUnwellOnNextRaanaTurnStart;
    private bool _isResolvingS4;

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();

        var context = new ThrowingPlayerChoiceContext();
        await PowerCmd.Apply<RaanaInterestPower>(context, Creature, 1, Creature, null);

        if (CombatState.Players.Any(HasRelic<EmptyParfait>))
        {
            await ApplyEmpoweredRoute(context);
            return;
        }

        if (CombatState.Players.Any(HasRelic<MatchaParfait>))
        {
            await ApplyWeakenedRoute(context);
            return;
        }

        await ApplyDefaultEmpoweredRoute(context);
    }

    private async Task ApplyEmpoweredRoute(PlayerChoiceContext context)
    {
        await PowerCmd.Apply<RaanaRisingMoodPower>(context, Creature, 1, Creature, null);
        SetMoveImmediate(_s1State, forceTransition: true);
    }

    private async Task ApplyDefaultEmpoweredRoute(PlayerChoiceContext context)
    {
        await PowerCmd.Apply<RaanaRisingMoodPower>(context, Creature, 1, Creature, null);
        SetMoveImmediate(_s1State, forceTransition: true);
    }

    private async Task ApplyWeakenedRoute(PlayerChoiceContext context)
    {
        // await CreatureCmd.GainBlock(Creature, WeakenedEntryBlock, ValueProp.Unpowered, null);
        await PowerCmd.Apply<RaanaSleepBlockPower>(context, Creature, 1, Creature, null);
        SetMoveImmediate(_sleepState, forceTransition: true);
    }

    private static bool HasRelic<TRelic>(MegaCrit.Sts2.Core.Entities.Players.Player player)
    {
        return player.Relics.Any(relic => relic is TRelic);
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var states = new List<MonsterState>();

        _sleepState = new MoveState("RAANA_SLEEP", SleepMove, new SleepIntent())
        {
            MustPerformOnceBeforeTransitioning = true
        };
        _s1State = new MoveState("RAANA_S1_ATTACK", S1Move, new SingleAttackIntent(S1Attack));
        _s2State = new MoveState("RAANA_S2_MULTI_BLOCK", S2Move,
            new MultiAttackIntent(S2Attack, S2HitCount), new DefendIntent());
        _s3State = new MoveState("RAANA_S3_CLEANSE_PARFAIT", S3Move, new HealIntent(), new StatusIntent(BuffetsPerPlayer));
        _s4LowState = new MoveState("RAANA_S4_LOW_INTEREST", S4LowMove,
            new SingleAttackIntent(S4Attack), new DebuffIntent());
        _s4MidState = new MoveState("RAANA_S4_MID_INTEREST", S4MidMove,
            new SingleAttackIntent(S4Attack), new DebuffIntent());
        _s4HighState = new MoveState("RAANA_S4_HIGH_INTEREST", S4HighMove,
            new MultiAttackIntent(S4HighAttack, S4HighHitCount), new BuffIntent());

        _sleepState.FollowUpState = _s1State;
        _s1State.FollowUpState = _s2State;
        _s2State.FollowUpState = _s3State;
        _s3State.FollowUpState = _s4LowState;
        _s4LowState.FollowUpState = _s1State;
        _s4MidState.FollowUpState = _s1State;
        _s4HighState.FollowUpState = _s1State;

        states.Add(_sleepState);
        states.Add(_s1State);
        states.Add(_s2State);
        states.Add(_s3State);
        states.Add(_s4LowState);
        states.Add(_s4MidState);
        states.Add(_s4HighState);

        return new MonsterMoveStateMachine(states, _s1State);
    }

    public MoveState ResolveInterestMoveState()
    {
        var interestPower = Creature.GetPower<RaanaInterestPower>();
        if (interestPower == null)
        {
            return _s4LowState;
        }

        if (interestPower.Amount < interestPower.LowThreshold)
        {
            return _s4LowState;
        }

        if (interestPower.Amount < interestPower.HighThreshold)
        {
            return _s4MidState;
        }

        CustomAudioController.PlaySfx("raana-FunnyWoman");
        return _s4HighState;
    }

    public void RefreshInterestMoveStateIfNeeded()
    {
        if (_isResolvingS4 || !IsCurrentInterestPreviewState()) return;

        SetMoveImmediate(ResolveInterestMoveState(), forceTransition: true);
    }

    public bool IsCurrentInterestPreviewState()
    {
        return MoveStateMachine?.StateLog.LastOrDefault() is { } currentState &&
               (currentState == _s4LowState || currentState == _s4MidState || currentState == _s4HighState);
    }

    private async Task SleepMove(IReadOnlyList<Creature> targets)
    {
        await ApplyPendingUnwell();
    }

    private async Task S1Move(IReadOnlyList<Creature> targets)
    {
        // await ApplyPendingUnwell();
        await DamageCmd.Attack(S1Attack)
            .FromMonster(this)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(null);
    }

    private async Task ApplyPendingUnwell()
    {
        await PowerCmd.Apply<RaanaUnwellPower>(new ThrowingPlayerChoiceContext(), Creature, UnwellStacks, Creature, null);
    }

    private async Task S2Move(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(S2Attack)
            .WithHitCount(S2HitCount)
            .FromMonster(this)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(null);
        await CreatureCmd.GainBlock(Creature, S2Block, ValueProp.Move, null);
    }

    private async Task S3Move(IReadOnlyList<Creature> targets)
    {
        var debuffs = Creature.Powers
            .Where(power => power.Type == PowerType.Debuff && power is not RaanaUnwellPower)
            .ToList();
        var debuffTypeCount = debuffs.DistinctBy(power => power.GetType()).Count();

        foreach (var power in debuffs)
        {
            await PowerCmd.Remove(power);
        }

        if (debuffTypeCount > 0)
        {
            await CreatureCmd.Heal(Creature, debuffTypeCount * S3HealPerDebuffType);
        }

        foreach (var target in targets.Where(target => target.IsAlive))
        {
            var player = target.Player ?? target.PetOwner;
            if (player == null) continue;

            for (var i = 0; i < BuffetsPerPlayer; i++)
            {
                var card = CombatState.CreateCard<LeftoverBuffet>(player);
                await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, player);
            }
        }

        _s3State.FollowUpState = ResolveInterestMoveState();
    }

    private async Task S4LowMove(IReadOnlyList<Creature> targets)
    {
        _isResolvingS4 = true;
        await DamageCmd.Attack(S4Attack)
            .FromMonster(this)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(null);
        await PowerCmd.Apply<WeakPower>(new ThrowingPlayerChoiceContext(), targets, 2, Creature, null);
        await ClearInterest(new ThrowingPlayerChoiceContext());
        _isResolvingS4 = false;
    }

    private async Task S4MidMove(IReadOnlyList<Creature> targets)
    {
        _isResolvingS4 = true;
        await DamageCmd.Attack(S4Attack)
            .FromMonster(this)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(null);
        await PowerCmd.Apply<VulnerablePower>(new ThrowingPlayerChoiceContext(), targets, 2, Creature, null);
        await ClearInterest(new ThrowingPlayerChoiceContext());
        _isResolvingS4 = false;
    }

    private async Task S4HighMove(IReadOnlyList<Creature> targets)
    {
        _isResolvingS4 = true;
        await DamageCmd.Attack(S4HighAttack)
            .WithHitCount(S4HighHitCount)
            .FromMonster(this)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(null);
        await PowerCmd.Apply<StrengthPower>(new ThrowingPlayerChoiceContext(), Creature, 1, Creature, null);
        await ClearInterest(new ThrowingPlayerChoiceContext());
        _isResolvingS4 = false;
    }

    private async Task ClearInterest(PlayerChoiceContext choiceContext)
    {
        var interestPower = Creature.GetPower<RaanaInterestPower>();
        if (interestPower != null)
        {
            await interestPower.ClearInterest(choiceContext);
        }
    }
}
