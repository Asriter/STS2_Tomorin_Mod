using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.ValueProps;
using STS2_Tomorin_Mod.CardPools;
using STS2_Tomorin_Mod.Cards.Base;

namespace STS2_Tomorin_Mod.Cards;

/// <summary>
/// 主唱太拼命了
/// 3->2费 消耗 修改对方意图 改成1*3
/// </summary>
[Pool(typeof(TomorinCardPool))]
public class GoingAllOut() : BaseCardModel(3, CardType.Skill, CardRarity.Rare, TargetType.AnyEnemy)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override IEnumerable<DynamicVar> CanonicalVars
    {
        get
        {
            var list = base.CanonicalVars.ToList();
            list.Add(new DamageVar(1m, ValueProp.Move));
            return list;
        }
    }

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
		ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
        
        var creature = cardPlay.Target;
        if (creature == null || creature.Monster == null)
        {
            throw new InvalidOperationException("Can't stun a player.");
        }
        
        if (CombatState != null && !creature.IsDead)
        {
            List<MonsterState> stateLog = creature.Monster.MoveStateMachine.StateLog;
            var nextMoveId = stateLog.Last().Id;
            
            MoveState state = new MoveState("MULTI_ATTACK", MultiAttack, new MultiAttackIntent(1, 3))
            {
                FollowUpStateId = nextMoveId,
                MustPerformOnceBeforeTransitioning = true
            };
            creature.Monster.SetMoveImmediate(state);
        }
        
        async Task MultiAttack(IReadOnlyList<Creature> c)
        {
            await DamageCmd.Attack(1).WithHitCount(3).FromMonster(creature.Monster)
                .WithAttackerAnim("Attack", 0.35f)
                .WithHitFx("vfx/vfx_attack_slash")
                .OnlyPlayAnimOnce()
                .Execute(null);
        }
        
        return Task.CompletedTask;
    }

    protected override void OnUpgrade()
    {
        MockSetEnergyCost(new CardEnergyCost(this, 2, false));
    }
}