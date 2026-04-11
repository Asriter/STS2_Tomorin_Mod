using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using STS2_Tomorin_Mod.CardPools;
using STS2_Tomorin_Mod.Cards.Base;

namespace STS2_Tomorin_Mod.Cards;

/// <summary>
/// 组一辈子乐队
/// 蓝卡 1→0费 技能 将一张随机的能力牌加入手牌，本回合费用为0 消耗
/// </summary>
[Pool(typeof(TomorinCardPool))]
public class BandForever : BaseCardModel
{
    protected override int CanonicalEnergyCost => IsUpgraded ? 0 : 1;

    public BandForever() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }

    public override IEnumerable<CardKeyword> CanonicalKeywords
    {
        get
        {
            var list = base.CanonicalKeywords.ToList();
            list.Add(CardKeyword.Exhaust);
            return list;
        }
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var powerCards = ModelDb.CardPool<TomorinCardPool>().AllCards
            .Where(c => c.Type == CardType.Power
                && c.Rarity != CardRarity.Token
                && c.Rarity != CardRarity.Status)
            .ToList();

        if (powerCards.Count == 0) return;

        var randomModel = powerCards
            .TakeRandom(1, base.Owner.RunState.Rng.CombatCardGeneration)
            .FirstOrDefault();
        if (randomModel == null) return;

        var newCard = base.CombatState!.CreateCard(randomModel, base.Owner);
        newCard.EnergyCost.SetThisTurn(0);
        await CardPileCmd.AddGeneratedCardToCombat(newCard, PileType.Hand, addedByPlayer: true);
    }
    
    protected override void OnUpgrade()
    {
        MockSetEnergyCost(new CardEnergyCost(this, 0, false));
    }
}
