using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using STS2_Tomorin_Mod.CardPools;
using STS2_Tomorin_Mod.Cards.Base;

namespace STS2_Tomorin_Mod.Cards;

/// <summary>
/// 渺小的瞬间
/// 白卡 1费 技能 抽2->3 将一张随机收藏品加入手牌
/// </summary>
[Pool(typeof(TomorinCardPool))]
public class Melody : BaseCardModel
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new CardsVar(2)];

    public Melody() :
        base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 抽牌
        await CardPileCmd.Draw(choiceContext, base.DynamicVars.Cards.BaseValue, Owner);

        // 随机将一张收藏品加入手牌
        var collectionsCards = ModelDb.CardPool<CollectionsCardPool>().AllCards.ToList();
        if (collectionsCards.Count > 0)
        {
            var randomCard = collectionsCards.TakeRandom(1, base.Owner.RunState.Rng.CombatCardGeneration).First();
            var newCard = base.CombatState!.CreateCard(randomCard, base.Owner);
            await CardPileCmd.AddGeneratedCardToCombat(newCard, PileType.Hand, addedByPlayer: true);
        }
    }

    protected override void OnUpgrade()
    {
        base.DynamicVars.Cards.UpgradeValueBy(1m);
    }
}
