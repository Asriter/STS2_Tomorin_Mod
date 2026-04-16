using BaseLib.Utils;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;
using STS2_Tomorin_Mod.CardPools;
using STS2_Tomorin_Mod.Cards.Base;
using STS2_Tomorin_Mod.Powers;

namespace STS2_Tomorin_Mod.Cards;

/// <summary>
/// 好想成为人类
/// 金卡 1费 技能 移除所有心之壁，每移除1点心之壁获得1->2点力量（本回合结束时失去）
/// </summary>
[Pool(typeof(TomorinCardPool))]
public class WantToBeingHuman : BaseCardModel
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new List<DynamicVar>()
        {
            new PowerVar<FlexPotionPower>(1m),
        };

    protected override IEnumerable<IHoverTip> ExtraHoverTips
    {
        get
        {
            var list = base.ExtraHoverTips.ToList();
            list.Add(HoverTipFactory.FromPower<FlexPotionPower>());
            return list;
        }
    }

    public WantToBeingHuman() : base(1, CardType.Skill, CardRarity.Rare, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (Owner.Creature.HasPower<AtFieldPower>())
        {
            var atFieldPower = Owner.Creature.GetPower<AtFieldPower>();
            decimal currentAmount = atFieldPower.Amount;
            if (currentAmount > 0m)
            {
                await PowerCmd.ModifyAmount(atFieldPower, -currentAmount, Owner.Creature, this);
                decimal strengthToGain = currentAmount * base.DynamicVars[nameof(FlexPotionPower)].BaseValue;
                await PowerCmd.Apply<FlexPotionPower>(Owner.Creature, strengthToGain, Owner.Creature, this);
            }
        }
    }


    protected override void OnUpgrade()
    {
        base.DynamicVars["FlexPotionPower"].UpgradeValueBy(1m);
    }
}