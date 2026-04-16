using BaseLib.Utils;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using STS2_Tomorin_Mod.CardPools;
using STS2_Tomorin_Mod.Cards.Base;

namespace STS2_Tomorin_Mod.Cards;

/// <summary>
/// 巨石之力 GiantStoneForce
/// 白卡（稀有度：Common）1费 技能
/// 选择卡组中的两张卡变化为巨石"GIANT_ROCK"
/// 升级后则将卡变化为升级后的巨石
/// </summary>
[Pool(typeof(TomorinCardPool))]
public class GiantStoneForce : BaseCardModel
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new List<DynamicVar>();

    public GiantStoneForce() :
        base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var drawPile = base.Owner.PlayerCombatState?.DrawPile;
        if (drawPile == null || drawPile.Cards.Count == 0)
            return;

        int selectCount = Math.Min(2, drawPile.Cards.Count);

        var selected = await CommonActions.SelectCards(
            this,
            CardSelectorPrefs.TransformSelectionPrompt,
            choiceContext,
            PileType.Draw,
            0, selectCount);

        foreach (var card in selected)
        {
            CardPileAddResult? cardPileAddResult = await CardCmd.TransformTo<GiantRock>(card);
            if (base.IsUpgraded && cardPileAddResult.HasValue)
            {
                CardCmd.Upgrade(cardPileAddResult.Value.cardAdded);
            }
        }
    }

    protected override void OnUpgrade()
    {
    }
}
