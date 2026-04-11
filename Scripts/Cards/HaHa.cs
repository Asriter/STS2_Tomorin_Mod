using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2_Tomorin_Mod.CardPools;
using STS2_Tomorin_Mod.Cards.Base;

namespace STS2_Tomorin_Mod.Cards;

/// <summary>
/// 对哈
/// 白卡 1费 攻击 对所有敌人造成9->12点伤害，随机将一张[收藏品]加入手牌
/// </summary>
[Pool(typeof(TomorinCardPool))]
public class HaHa : BaseCardModel
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DamageVar(9m, ValueProp.Move)];

    public HaHa() :
        base(1, CardType.Attack, CardRarity.Common, TargetType.AllEnemies)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 对所有敌人造成伤害
        await DamageCmd.Attack(base.DynamicVars.Damage.BaseValue)
            .FromCard(this)
            .TargetingAllOpponents(CombatState)
            .Execute(choiceContext);

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
        base.DynamicVars.Damage.UpgradeValueBy(3m);
    }
}
