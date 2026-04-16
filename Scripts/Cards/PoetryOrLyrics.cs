using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Powers;
using STS2_Tomorin_Mod.CardPools;
using STS2_Tomorin_Mod.Cards.Base;
using STS2_Tomorin_Mod.Powers;

namespace STS2_Tomorin_Mod.Cards;

/// <summary>
/// 诗与歌
/// 金卡 1费 技能 消耗 消耗手牌/牌组/弃牌堆中所有收集品。消耗牌堆中有一张收集品则获得1->2层敏捷和1->2层心之壁
/// </summary>
[Pool(typeof(TomorinCardPool))]
public class PoetryOrLyrics : BaseCardModel
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new List<DynamicVar>()
        {
            new PowerVar<DexterityPower>(1m),
            new PowerVar<AtFieldPower>(1m),
        };

    protected override IEnumerable<IHoverTip> ExtraHoverTips
    {
        get
        {
            var list = base.ExtraHoverTips.ToList();
            list.Add(HoverTipFactory.FromPower<DexterityPower>());
            list.Add(HoverTipFactory.FromPower<AtFieldPower>());
            return list;
        }
    }

    public override IEnumerable<CardKeyword> CanonicalKeywords 
    {
        get
        {
            var list = base.CanonicalKeywords.ToList();
            list.Add(CardKeyword.Exhaust);
            return list;
        }
    }

    public PoetryOrLyrics() : base(1, CardType.Skill, CardRarity.Rare, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var combatState = Owner.PlayerCombatState;
        if (combatState == null) return;

        var collectibleTypes = new HashSet<Type>(
            ModelDb.CardPool<CollectionsCardPool>().AllCards.Select(c => c.GetType()));

        // 先收集所有收集品（快照），再逐一消耗，避免修改时迭代
        var toExhaust = new List<CardModel>();
        foreach (var pile in new[]
                 {
                     (IEnumerable<CardModel>)combatState.Hand.Cards, combatState.DrawPile.Cards,
                     combatState.DiscardPile.Cards
                 })
        {
            toExhaust.AddRange(pile.Where(c => collectibleTypes.Contains(c.GetType())));
        }

        foreach (var card in toExhaust)
        {
            await CardCmd.Exhaust(choiceContext, card);
        }
        
        //消耗牌堆中收集品数量
        var exhaustPile = Owner.PlayerCombatState?.ExhaustPile;
        if (exhaustPile == null || exhaustPile.Cards.Count == 0)
            return;

        var count = exhaustPile.Cards.Count(c => collectibleTypes.Contains(c.GetType()));

        await PowerCmd.Apply<DexterityPower>(Owner.Creature, base.DynamicVars["DexterityPower"].BaseValue * count,
            Owner.Creature, this);
        await PowerCmd.Apply<AtFieldPower>(Owner.Creature, base.DynamicVars["AtFieldPower"].BaseValue * count,
            Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        base.DynamicVars["DexterityPower"].UpgradeValueBy(1m);
        base.DynamicVars["AtFieldPower"].UpgradeValueBy(1m);
    }
}