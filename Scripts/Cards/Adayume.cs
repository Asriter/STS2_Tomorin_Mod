using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using STS2_Tomorin_Mod.CardPools;
using STS2_Tomorin_Mod.Cards.Base;
using STS2_Tomorin_Mod.Powers;

namespace STS2_Tomorin_Mod.Cards;

/// <summary>
/// 过堕幻
/// 能力 先古 1 强化固有 每次打出牌获得一点格挡，获得一点心之壁
/// </summary>

[Pool(typeof(TomorinCardPool))]
public class Adayume() : BaseCardModel(1, CardType.Power, CardRarity.Ancient, TargetType.Self)
{
    protected override IEnumerable<DynamicVar> CanonicalVars
    {
        get
        {
            var list = base.CanonicalVars.ToList();
            list.Add(new PowerVar<AdayumePower>(1m));
            list.Add(new BlockVar(1m, ValueProp.Unpowered));
            return list;
        }
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
        await PowerCmd.Apply<AdayumePower>(base.Owner.Creature, base.DynamicVars["AdayumePower"].BaseValue,
            base.Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        AddKeyword(CardKeyword.Innate);
    }
}