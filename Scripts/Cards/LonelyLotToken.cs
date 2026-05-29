using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using STS2_Tomorin_Mod.CardPools;
using STS2_Tomorin_Mod.Cards.Base;
using STS2_Tomorin_Mod.Powers;

namespace STS2_Tomorin_Mod.Cards;

/// <summary>
/// 皲裂的心
/// 白卡 Token 能力 1费 每当获得心之壁时，额外获得1层心之壁，升级后0费并获得1层心之壁
/// </summary>
[Pool(typeof(TomorinCardPool))]
public class LonelyLotToken : BaseCardModel
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<LonelyLotTokenPower>(1m),
        new PowerVar<AtFieldPower>(0m)
    ];

    public LonelyLotToken() :
        base(1, CardType.Power, CardRarity.Token, TargetType.Self)
    {
    }

    protected override IEnumerable<IHoverTip> ExtraHoverTips
    {
        get
        {
            var list = base.ExtraHoverTips.ToList();
            list.Add(HoverTipFactory.FromPower<AtFieldPower>());
            return list;
        }
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (IsUpgraded)
        {
            await PowerCmd.Apply<AtFieldPower>(choiceContext, base.Owner.Creature,
                base.DynamicVars["AtFieldPower"].BaseValue, base.Owner.Creature, this);
        }
        await PowerCmd.Apply<LonelyLotTokenPower>(choiceContext, base.Owner.Creature,
            base.DynamicVars["LonelyLotTokenPower"].BaseValue, base.Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        base.DynamicVars["AtFieldPower"].UpgradeValueBy(1m);
        MockSetEnergyCost(new CardEnergyCost(this, 0, false));
    }
}
