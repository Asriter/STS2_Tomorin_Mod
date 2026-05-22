using BaseLib.Utils;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.ValueProps;
using STS2_Tomorin_Mod.CardPools;
using STS2_Tomorin_Mod.Cards.Base;
using STS2_Tomorin_Mod.Cards.Collections;
using STS2_Tomorin_Mod.Commands;
using STS2_Tomorin_Mod.Localization.CustomEnums;
using STS2_Tomorin_Mod.Powers;

namespace STS2_Tomorin_Mod.Cards;

/// <summary>
/// AT立场
/// 白卡 2费 技能 灵感 消耗一张状态牌 获得13->15点格挡，获得5-6点心之壁
/// </summary>
[Pool(typeof(TomorinCardPool))]
public class AtField : BaseCardModel
{
    public override bool IsInspiration => true;

    protected override bool IsPlayable =>
        Owner.PlayerCombatState.Hand.Cards.Count(card => card.Type == CardType.Status) > 0;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new List<DynamicVar>()
        {
            new BlockVar(13m, ValueProp.Move),
            new PowerVar<AtFieldPower>(5m),
        };

    protected override IEnumerable<IHoverTip> ExtraHoverTips
    {
        get
        {
            var list = base.ExtraHoverTips.ToList();
            list.Add(HoverTipFactory.FromCard<BrokenNote>(base.IsUpgraded));
            list.Add(HoverTipFactory.FromPower<AtFieldPower>());
            return list;
        }
    }

    public AtField() : base(2, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        //如果有状态牌，选择一张状态牌并 Exhaust
        if (IsPlayable)
        {
            var cardsToExhaust = await DisExhaustCmd.FromHandForExhaust(
                choiceContext,
                Owner,
                1,
                model => model.Type == CardType.Status,
                this);
            foreach (var card in cardsToExhaust)
            {
                await CardCmd.Exhaust(choiceContext, card);
            }
        }

        await CreatureCmd.GainBlock(base.Owner.Creature, base.DynamicVars.Block, cardPlay);
        await PowerCmd.Apply<AtFieldPower>(choiceContext, base.Owner.Creature, base.DynamicVars["AtFieldPower"].BaseValue,
            base.Owner.Creature, this);

        // var brokenNote = base.CombatState!.CreateCard<BrokenNote>(Owner);
        // await CardPileCmd.AddGeneratedCardToCombat(brokenNote, PileType.Hand, Owner);
    }

    protected override void OnUpgrade()
    {
        base.DynamicVars.Block.UpgradeValueBy(2m);
        base.DynamicVars["AtFieldPower"].UpgradeValueBy(1m);
    }
    
}