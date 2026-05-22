using BaseLib.Extensions;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.ValueProps;
using STS2_Tomorin_Mod.CardPools;
using STS2_Tomorin_Mod.Cards.Base;
using STS2_Tomorin_Mod.Localization.DynamicVars;
using STS2_Tomorin_Mod.Powers;

namespace STS2_Tomorin_Mod.Cards;

/// <summary>
/// 拾起石子
/// 白卡 1->0费 技能 获得5层心之壁
/// </summary>
[Pool(typeof(TomorinCardPool))]
public class PickStone : BaseCardModel
{
    protected override int CanonicalEnergyCost => IsUpgraded ? 0 : 1;

    public PickStone() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
    }
    
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new List<DynamicVar>()
        {
            new PowerVar<AtFieldPower>(5m),
        };

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
        await PowerCmd.Apply<AtFieldPower>(choiceContext, base.Owner.Creature, base.DynamicVars["AtFieldPower"].BaseValue,
            base.Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        MockSetEnergyCost(new CardEnergyCost(this, 0, false));
    }
}