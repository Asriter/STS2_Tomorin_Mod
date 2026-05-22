using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.ValueProps;
using STS2_Tomorin_Mod.CardPools;
using STS2_Tomorin_Mod.Cards.Base;
using STS2_Tomorin_Mod.Cards.Collections;
using STS2_Tomorin_Mod.Powers;

namespace STS2_Tomorin_Mod.Cards;

/// <summary>
/// 西瓜虫
/// 白卡 1费 技能 获得8->10点格挡，将一张笔记本加入手牌
/// </summary>
[Pool(typeof(TomorinCardPool))]
public class Woodlouse : BaseCardModel
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new List<DynamicVar>()
        {
            new BlockVar(8m, ValueProp.Move),
        };

    protected override IEnumerable<IHoverTip> ExtraHoverTips
    {
        get
        {
            var list = base.ExtraHoverTips.ToList();
            list.Add(HoverTipFactory.FromCard<BrokenNote>(base.IsUpgraded));
            return list;
        }
    }

    public Woodlouse() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(base.Owner.Creature, base.DynamicVars.Block, cardPlay);
        // await PowerCmd.Apply<AtFieldPower>(choiceContext, base.Owner.Creature, base.DynamicVars["AtFieldPower"].BaseValue, base.Owner.Creature, this);

        var brokenNote = base.CombatState!.CreateCard<BrokenNote>(Owner);
        await CardPileCmd.AddGeneratedCardToCombat(brokenNote, PileType.Hand, Owner);
    }

    protected override void OnUpgrade()
    {
        base.DynamicVars.Block.UpgradeValueBy(3m);
    }
}
