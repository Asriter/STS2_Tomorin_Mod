using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Logging;

namespace STS2_Tomorin_Mod.Powers;

/// <summary>
/// Oblivionis二阶段被动：玩家回合结束时每个玩家随机消耗1张手牌
/// </summary>
public class OblivionisPhase2Power : BasePowerModel
{
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Single;

    public override async Task BeforeSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side == base.Owner.Side) return;

        foreach (var player in base.CombatState.Players)
        {
            var handCards = player.PlayerCombatState.Hand.Cards.ToList();
            if (handCards.Count == 0)
            {
                Log.Warn("该随机删卡了，但是tmd没手牌？");
                continue;
            }

            var randomCard = handCards[Random.Shared.Next(handCards.Count)];
            Flash();
            await CardCmd.Exhaust(choiceContext, randomCard);
        }
    }
}
