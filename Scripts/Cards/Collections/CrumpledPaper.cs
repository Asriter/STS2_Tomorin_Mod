using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using STS2_Tomorin_Mod.CardPools;
using STS2_Tomorin_Mod.Cards.Base;
using STS2_Tomorin_Mod.Commands;
using STS2_Tomorin_Mod.Localization.DynamicVars;

namespace STS2_Tomorin_Mod.Cards.Collections;

/// <summary>
/// 压皱的残页
/// 搜集品 anon
/// 抽两张卡
/// </summary>

[Pool(typeof(CollectionsCardPool))]
public class CrumpledPaper() : 
    BaseCardModel(-1, CardType.Status, CardRarity.Status, TargetType.None, true)
{
	public override int MaxUpgradeLevel => 0;
    
    public override IEnumerable<CardKeyword> CanonicalKeywords
    {
        get
        {
            var list = base.CanonicalKeywords.ToList();
            list.Add(CardKeyword.Unplayable);
            list.Add(CardKeyword.Retain);
            return list;
        }
    }

    protected override IEnumerable<DynamicVar> CanonicalVars
    {
        get
        {
            var list = base.CanonicalVars.ToList();
            list.Add( new CardsVar(2));
            return list;
        }
    }

    protected override bool IsPlayable => false;

    public override bool IsInspiration => true;

    //打出效果，抽二
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
		await CardPileCmd.Draw(choiceContext, base.DynamicVars.Cards.BaseValue, base.Owner);
    }
}