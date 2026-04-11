using BaseLib.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using STS2_Tomorin_Mod.CardPools;
using STS2_Tomorin_Mod.Cards.Base;
using STS2_Tomorin_Mod.Commands;
using STS2_Tomorin_Mod.Localization.DynamicVars;

namespace STS2_Tomorin_Mod.Cards.Collections;

[Pool(typeof(CollectionsCardPool))]
public class LeftoverBuffet() : 
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
            list.Add( new DisExhaustVar(1m));
            return list;
        }
    }

    protected override bool IsPlayable => false;

    public override bool IsInspiration => true;

    //打出效果，敌方全体两层虚弱和缠绕
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await DisExhaustCmd.RandomDisExhaust(choiceContext, Owner, DynamicVars[DisExhaustVar.DefauleName].IntValue, this);
    }
}