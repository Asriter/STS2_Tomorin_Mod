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
/// 白卡 1->0费 技能 获得心之壁层数的格挡
/// </summary>
[Pool(typeof(TomorinCardPool))]
public class PickStone : BaseCardModel
{
    protected override int CanonicalEnergyCost => IsUpgraded ? 0 : 1;

    public PickStone() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
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

    protected override IEnumerable<DynamicVar> CanonicalVars
    {
        get
        {
            var list = base.CanonicalVars.ToList();
            list.Add(new DynamicVar(AtFieldVar.DefaultName, 0));
            list.Add(new BlockVar(0m, ValueProp.Move));
            return list;
        }
    }
    
    private decimal AtFieldAmount => Owner.Creature.HasPower<AtFieldPower>()
        ? Owner.Creature.GetPower<AtFieldPower>().Amount
        : 0m;

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (AtFieldAmount > 0m)
        {
            await CreatureCmd.GainBlock(base.Owner.Creature, AtFieldAmount, ValueProp.Move, cardPlay);
        }
    }

    public override Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
    {
        //同步心之壁层数
        DynamicVars[AtFieldVar.DefaultName].BaseValue = AtFieldAmount;
        return Task.CompletedTask;
    }

    protected override void OnUpgrade()
    {
        MockSetEnergyCost(new CardEnergyCost(this, 0, false));
    }
}