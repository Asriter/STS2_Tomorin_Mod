using BaseLib.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.CardPools;
using STS2_Tomorin_Mod.Cards.Base;
using STS2_Tomorin_Mod.Powers;

namespace STS2_Tomorin_Mod.Cards;


[Pool(typeof(StatusCardPool))]
public class AnonLiveTogether() : BaseCardModel(2, CardType.Status, CardRarity.Status, TargetType.None, false)
{
    public override bool IsInspiration => true;

    protected override IEnumerable<IHoverTip> ExtraHoverTips
    {
        get
        {
            var list = base.ExtraHoverTips.ToList();
            list.Add(HoverTipFactory.FromPower<AnonEscapeCountPower>());
            return list;
        }
    }
}