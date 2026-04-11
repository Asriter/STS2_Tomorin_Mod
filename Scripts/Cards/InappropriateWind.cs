using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using STS2_Tomorin_Mod.CardPools;
using STS2_Tomorin_Mod.Cards.Base;
using STS2_Tomorin_Mod.Cards.Collections;

namespace STS2_Tomorin_Mod.Cards;

/// <summary>
/// 不该吹拂的清风
/// 白卡 1费 攻击 打9->12 灵感
/// </summary>
[Pool(typeof(TomorinCardPool))]
public class InappropriateWind : BaseCardModel
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DamageVar(9m, ValueProp.Move)];

    public InappropriateWind() :
        base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
    }
    
    protected override IEnumerable<IHoverTip> ExtraHoverTips
    {
        get
        {
            var list = new List<IHoverTip>().ToList();
            list.Add(HoverTipFactory.FromCard<ColdRedTea>());
            return list;
        }
    }
    public override bool IsInspiration => true;

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
        await DamageCmd.Attack(base.DynamicVars.Damage.BaseValue)
            .FromCard(this)
            .Targeting(cardPlay.Target)
            .Execute(choiceContext);
        
        var coldRedTea = base.CombatState!.CreateCard<ColdRedTea>(Owner);
        await CardPileCmd.AddGeneratedCardToCombat(coldRedTea, PileType.Draw, addedByPlayer: true);
    }

    protected override void OnUpgrade()
    {
        base.DynamicVars.Damage.UpgradeValueBy(3m);
    }
}
