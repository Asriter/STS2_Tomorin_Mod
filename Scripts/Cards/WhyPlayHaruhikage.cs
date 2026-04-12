using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.ValueProps;
using STS2_Tomorin_Mod.CardPools;
using STS2_Tomorin_Mod.Cards.Base;

namespace STS2_Tomorin_Mod.Cards;

/// <summary>
/// 为什么演奏春日影
/// 蓝卡 2费 攻击 灵感 对所有敌人造成16→20点伤害；将2张随机收集品加入手牌
/// </summary>
[Pool(typeof(TomorinCardPool))]
public class WhyPlayHaruhikage : BaseCardModel
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DamageVar(16m, ValueProp.Move)];

    public override bool IsInspiration => true;

    public WhyPlayHaruhikage() : base(2, CardType.Attack, CardRarity.Uncommon, TargetType.AllEnemies)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await DamageCmd.Attack(base.DynamicVars.Damage.BaseValue)
            .FromCard(this)
            .TargetingAllOpponents(CombatState)
            .Execute(choiceContext);

        var allCollectionCards = ModelDb.CardPool<CollectionsCardPool>().AllCards.ToList();
        var randomCards = allCollectionCards.TakeRandom(2, base.Owner.RunState.Rng.CombatCardGeneration).ToList();

        foreach (var card in randomCards)
        {
            var newCard = base.CombatState!.CreateCard(card, base.Owner);
            await CardPileCmd.AddGeneratedCardToCombat(newCard, PileType.Hand, addedByPlayer: true);
        }
    }

    protected override void OnUpgrade()
    {
        base.DynamicVars.Damage.UpgradeValueBy(4m);
    }
}