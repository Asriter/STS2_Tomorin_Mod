using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using STS2_Tomorin_Mod.Cards.Base;

namespace STS2_Tomorin_Mod.Powers;

/// <summary>
/// =未来永劫 - Power
/// 每当你作词时，将那张作词牌放入弃牌堆（而非消耗）
/// </summary>

public class SilhouetteDanceTokenPower : BasePowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    /// <summary>
    /// 在所属玩家作词后将来源作词牌的复制加入弃牌堆。
    /// </summary>
    public override async Task AfterCompose(PlayerChoiceContext choiceContext, ComposeResult result)
    {
        if (result.Player != base.Owner.Player) return;
        
        CardModel card = result.Source.CreateClone();
        await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Discard, Owner.Player);
    }
}
