using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2_Tomorin_Mod.CardPools;
using STS2_Tomorin_Mod.Cards.Base;
using STS2_Tomorin_Mod.Commands;
using STS2_Tomorin_Mod.Localization.DynamicVars;

namespace STS2_Tomorin_Mod.Cards;
[Pool(typeof(TomorinCardPool))]
public sealed class Hitoshizuku : BaseCardModel
{
    
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new List<DynamicVar>()
        {
            new ComposeVar(new Dictionary<CardType, int>() { { CardType.Attack , 1}}, ModelDb.Card<HitoshizukuToken>()),
            new DamageVar(6m, ValueProp.Move)
        };

    protected override IEnumerable<IHoverTip> ExtraHoverTips
    {
        get
        {
            var list = base.ExtraHoverTips.ToList();
            list.Add(HoverTipFactory.FromCard<HitoshizukuToken>(base.IsUpgraded));
            return list;
        }
    }
   
    //测试，增加固有保证上手
    // public override IEnumerable<CardKeyword> CanonicalKeywords
    // {
    //     get
    //     {
    //         var list = base.CanonicalKeywords.ToList();
    //         list.Add(CardKeyword.Innate);
    //         return list;
    //     }
    // }

    public Hitoshizuku() : 
        base(1, CardType.Attack, CardRarity.Basic, TargetType.AnyEnemy)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
        
        await ComposeCmd.Compose<HitoshizukuToken>(choiceContext, Owner, ComposeCost, this);
        
        await DamageCmd.Attack(base.DynamicVars.Damage.BaseValue).WithHitCount(1).FromCard(this)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        base.DynamicVars.Damage.UpgradeValueBy(3m);
    }
}