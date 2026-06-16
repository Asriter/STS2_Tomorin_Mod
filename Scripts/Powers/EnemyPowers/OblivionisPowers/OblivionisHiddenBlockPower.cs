using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace STS2_Tomorin_Mod.Powers;

/// <summary>
/// 隐藏Boss格挡被动：同一玩家连续打出两张相同类型牌时Boss获得5格挡
/// 每个玩家独立跟踪，不同玩家之间不互相影响。
/// </summary>
public class OblivionisHiddenBlockPower : BasePowerModel
{
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Single;

    private readonly Dictionary<Player, CardType?> _lastPlayedCardTypePerPlayer = new();

    public override async Task BeforeSideTurnStart(PlayerChoiceContext choiceContext, CombatSide side,
        IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (side == base.Owner.Side)
            _lastPlayedCardTypePerPlayer.Clear();
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner?.Creature?.Side == base.Owner.Side) return;

        var player = cardPlay.Card.Owner;
        if (player == null) return;

        var cardType = cardPlay.Card.Type;
        if (_lastPlayedCardTypePerPlayer.TryGetValue(player, out var lastType) && lastType == cardType)
        {
            Flash();
            await CreatureCmd.GainBlock(base.Owner, 5m, ValueProp.Move, null);
        }

        _lastPlayedCardTypePerPlayer[player] = cardType;
    }
}
