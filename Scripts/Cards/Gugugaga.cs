using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using STS2_Tomorin_Mod.CardPools;
using STS2_Tomorin_Mod.Cards.Base;

namespace STS2_Tomorin_Mod.Cards;

/// <summary>
/// 咕咕嘎嘎
/// 2费 白卡 技能 回2->3点能量 灵感
/// </summary>
[Pool(typeof(TomorinCardPool))]
public class Gugugaga : BaseCardModel
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new EnergyVar(2)];

    public Gugugaga() :
        base(2, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
    }

    public override bool IsInspiration => true;

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 回复能量
        await PlayerCmd.GainEnergy(base.DynamicVars.Energy.BaseValue, base.Owner);
    }

    protected override void OnUpgrade()
    {
        base.DynamicVars.Energy.UpgradeValueBy(1m);
    }
}
