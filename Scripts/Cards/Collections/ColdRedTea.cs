using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using STS2_Tomorin_Mod.CardPools;
using STS2_Tomorin_Mod.Cards.Base;
using STS2_Tomorin_Mod.Powers;

namespace STS2_Tomorin_Mod.Cards.Collections;

/// <summary>
/// 收藏品
/// 长崎素世 冰冷的红茶
/// 打出效果，敌方全体两层虚弱和缠绕
/// </summary>

[Pool(typeof(CollectionsCardPool))]
public class ColdRedTea : BaseCardModel
{
	public override int MaxUpgradeLevel => 0;
    
    public ColdRedTea() : base(-1, CardType.Status, CardRarity.Status, TargetType.AllEnemies, true)
    {
        
    }

    protected override IEnumerable<IHoverTip> ExtraHoverTips
    {
	    get
	    {
		    var list = base.ExtraHoverTips.ToList();
		    list.Add(HoverTipFactory.FromPower<WeakPower>());
		    list.Add(HoverTipFactory.FromPower<CustomConstrictPower>());
		    return list;
	    }
    }

    protected override IEnumerable<DynamicVar> CanonicalVars
    {
	    get
	    {
		    var list = base.CanonicalVars.ToList();
		    list.Add(new PowerVar<WeakPower>(2m));
		    list.Add(new PowerVar<CustomConstrictPower>(2m));
		    return list;
	    }
    }

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

    public override bool IsInspiration => true;

    //打出效果，敌方全体两层虚弱和缠绕
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
		await CreatureCmd.TriggerAnim(base.Owner.Creature, "Cast", base.Owner.Character.AttackAnimDelay);
		await PowerCmd.Apply<CustomConstrictPower>(base.CombatState.HittableEnemies, base.DynamicVars["CustomConstrictPower"].BaseValue, base.Owner.Creature, this);
		await PowerCmd.Apply<WeakPower>(base.CombatState.HittableEnemies, base.DynamicVars.Weak.BaseValue, base.Owner.Creature, this);
    }
}