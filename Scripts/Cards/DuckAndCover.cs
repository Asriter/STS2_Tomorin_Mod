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
/// 抱头蹲防
/// 金卡 1费 能力 每回合开始时获得等于心之壁层数的格挡（升级后额外立即获得3层心之壁）
/// </summary>
[Pool(typeof(TomorinCardPool))]
public class DuckAndCover : BaseCardModel
{
    protected override IEnumerable<IHoverTip> ExtraHoverTips
    {
        get
        {
            var list = base.ExtraHoverTips.ToList();
            list.Add(HoverTipFactory.FromPower<AtFieldPower>());
            return list;
        }
    }

    protected override IEnumerable<DynamicVar> CanonicalVars
    {
        get
        {
            var list = base.CanonicalVars.ToList();
            list.Add(new IntVar("ATFieldAmount", 3));
            return list;
        }
    }

    public DuckAndCover() : base(1, CardType.Power, CardRarity.Rare, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (IsUpgraded)
        {
            await PowerCmd.Apply<AtFieldPower>(base.Owner.Creature, DynamicVars["ATFieldAmount"].IntValue, base.Owner.Creature, this);
        }

        await PowerCmd.Apply<DuckAndCoverPower>(base.Owner.Creature, 1m, base.Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        // 升级效果通过 IsUpgraded 在 OnPlay 中处理
    }
}
