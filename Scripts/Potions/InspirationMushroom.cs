using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using STS2_Tomorin_Mod.Localization.CustomEnums;
using STS2_Tomorin_Mod.PotionPools;

namespace STS2_Tomorin_Mod.Potions;

[Pool(typeof(TomorinPotionPool))]
public class InspirationMushroom : BasePotionModel
{
    public override PotionRarity Rarity => PotionRarity.Common;
    public override PotionUsage Usage => PotionUsage.CombatOnly;
    public override TargetType TargetType => TargetType.Self;

    public override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.FromKeyword(CustomKeyWord.Inspiration)];

    protected override async Task OnUse(PlayerChoiceContext choiceContext, Creature? target)
    {
        //选择一张手牌
        var cards = await CardSelectCmd.FromHand(
            choiceContext,
            target.Player,
            new CardSelectorPrefs(SelectionScreenPrompt, 1),
            null,
            this);

        foreach (var card in cards)
        {
            card.AddKeyword(CustomKeyWord.SingleTurnInspiration);
        }
    }
}