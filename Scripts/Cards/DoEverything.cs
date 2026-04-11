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
/// 我什么都会做的
/// 4费 金卡 技能 获得99->168块钱，立刻结束你的回合
/// </summary>
[Pool(typeof(TomorinCardPool))]
public class DoEverything : BaseCardModel
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<DoEverythingPower>(99m)
    ];

    public DoEverything() :
        base(4, CardType.Power, CardRarity.Rare, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 获得金币（通过Power在战斗结束后获得）
        await PowerCmd.Apply<DoEverythingPower>(base.Owner.Creature, base.DynamicVars["DoEverythingPower"].BaseValue, base.Owner.Creature, this);

        // 立刻结束回合
        PlayerCmd.EndTurn(base.Owner, canBackOut: false);
    }

    protected override void OnUpgrade()
    {
        base.DynamicVars["DoEverythingPower"].UpgradeValueBy(69m);
    }
}
