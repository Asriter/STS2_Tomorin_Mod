using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace STS2_Tomorin_Mod.Powers;

/// <summary>
/// =未来永劫 - Power
/// 每当你作词时，将那张作词牌放入弃牌堆（而非消耗）
/// </summary>

public class SilhouetteDanceTokenPower : BasePowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    public override async Task AfterCompose(PlayerChoiceContext choiceContext, Player player, CardModel source)
    {
        if (player != base.Owner.Player) return;
        
        if (source != null)
        {
            CardModel card = source.CreateClone();
            await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Discard, addedByPlayer: true);
        }
    }
}
