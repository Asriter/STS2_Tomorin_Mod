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

namespace STS2_Tomorin_Mod.Cards;

/// <summary>
/// 好想成为人类
/// 金卡 1费 技能 移除所有格挡，每移除1点格挡获得1->2点力量（本回合结束时失去）
/// </summary>
[Pool(typeof(TomorinCardPool))]
public class WantToBeingHuman : BaseCardModel
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new List<DynamicVar>()
        {
            new PowerVar<FlexPotionPower>(1m),
            new BlockVar(1m, ValueProp.Move),
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
        decimal currentBlock = Owner.Creature.Block;
        if (currentBlock <= 0m) return;

        await CreatureCmd.LoseBlock(Owner.Creature, currentBlock);

        decimal strengthToGain = (currentBlock * base.DynamicVars[nameof(FlexPotionPower)].BaseValue) / DynamicVars.Block.IntValue;
        if (strengthToGain > 1m)
        {
            await PowerCmd.Apply<FlexPotionPower>(Owner.Creature, strengthToGain, Owner.Creature, this);
        }
    }
    

    protected override void OnUpgrade()
    {
        base.DynamicVars["FlexPotionPower"].UpgradeValueBy(1m);
    }
}
