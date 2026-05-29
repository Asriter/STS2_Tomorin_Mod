using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using STS2_Tomorin_Mod.CardPools;
using STS2_Tomorin_Mod.Cards.Base;
using STS2_Tomorin_Mod.Powers;

namespace STS2_Tomorin_Mod.Cards;

/// <summary>
/// 被引导的心跳
/// 蓝卡 能力 1费 每当卡被消耗获得2->3点防御
/// </summary>
[Pool(typeof(TomorinCardPool))]
public class HeartBeat : BaseCardModel
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new PowerVar<HeartBeatPower>(2m)];

    public HeartBeat() :
        base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<HeartBeatPower>(choiceContext, base.Owner.Creature, base.DynamicVars["HeartBeatPower"].BaseValue, base.Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        base.DynamicVars["HeartBeatPower"].UpgradeValueBy(1m);
    }
}
