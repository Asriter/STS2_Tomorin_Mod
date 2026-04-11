using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using STS2_Tomorin_Mod.CardPools;
using STS2_Tomorin_Mod.Cards.Base;
using STS2_Tomorin_Mod.Localization.DynamicVars;

namespace STS2_Tomorin_Mod.Powers;

/// <summary>
/// 思如泉涌效果
/// 每回合开始时，将一张随机的作词牌加入手牌
/// </summary>

public class MayIdeaPower : BasePowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    //抽卡前随机加手，避免额外回合处理
    public override async Task BeforeHandDraw(Player player, PlayerChoiceContext choiceContext, CombatState combatState)
    {
        if (player == base.Owner.Player && base.AmountOnTurnStart >= 1)
        {
            Flash();
            IEnumerable<CardModel> distinctForCombat = CardFactory.GetDistinctForCombat(base.Owner.Player, from c in base.Owner.Player.Character.CardPool.GetUnlockedCards(base.Owner.Player.UnlockState, base.Owner.Player.RunState.CardMultiplayerConstraint)
                where c is BaseCardModel { IsCompose: true } && c.Rarity != CardRarity.Token && c.Rarity != CardRarity.Ancient
                select c, base.AmountOnTurnStart, base.Owner.Player.RunState.Rng.CombatCardGeneration);
            await CardPileCmd.AddGeneratedCardsToCombat(distinctForCombat, PileType.Hand, addedByPlayer: true);
        }
    }
}
