using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;

namespace STS2_Tomorin_Mod.Powers;

/// <summary>
/// 击杀Amoris后玩家获得的永久Buff：每回合打出第3张攻击牌后获得1点能量
/// </summary>
public class AmorisKillBuffPower : BasePowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    private int _attackCountThisTurn;

    public override async Task BeforeSideTurnStart(PlayerChoiceContext choiceContext, CombatSide side,
        IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (side == base.Owner.Side)
            _attackCountThisTurn = 0;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner?.Creature != base.Owner) return;
        if (cardPlay.Card.Type != CardType.Attack) return;

        _attackCountThisTurn++;
        if (_attackCountThisTurn >= 3)
        {
            Flash();
            await PlayerCmd.GainEnergy(1m, base.Owner.Player!);
            _attackCountThisTurn = 0;
        }
    }
}
