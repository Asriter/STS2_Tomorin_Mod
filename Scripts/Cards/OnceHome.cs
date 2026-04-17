using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using STS2_Tomorin_Mod.CardPools;
using STS2_Tomorin_Mod.Cards.Base;
using STS2_Tomorin_Mod.Cards.Collections;
using STS2_Tomorin_Mod.Powers;

namespace STS2_Tomorin_Mod.Cards;

/// <summary>
/// 曾经的归宿
/// 金卡 1费 技能 消耗 回合开始时心之壁不会自动减少（升级后先额外获得2层）；将一张笔记本加入弃牌堆
/// </summary>
[Pool(typeof(TomorinCardPool))]
public class OnceHome : BaseCardModel
{
    // public override IEnumerable<CardKeyword> CanonicalKeywords =>
        // [CardKeyword.Exhaust];

    protected override IEnumerable<IHoverTip> ExtraHoverTips
    {
        get
        {
            var list = base.ExtraHoverTips.ToList();
            list.Add(HoverTipFactory.FromPower<AtFieldPower>());
            list.Add(HoverTipFactory.FromCard<BrokenNote>(base.IsUpgraded));
            return list;
        }
    }

    public OnceHome() : base(1, CardType.Power, CardRarity.Rare, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (IsUpgraded)
        {
            await PowerCmd.Apply<AtFieldPower>(base.Owner.Creature, 2m, base.Owner.Creature, this);
        }

        // if (Owner.Creature.HasPower<AtFieldPower>())
        // {
        //     var atFieldPower = Owner.Creature.GetPower<AtFieldPower>();
        //     decimal currentAmount = atFieldPower.Amount;
        //     if (currentAmount > 0m)
        //     {
        //         await PowerCmd.ModifyAmount(atFieldPower, currentAmount, Owner.Creature, this);
        //     }
        // }
        
        //挂buff
        await PowerCmd.Apply<OnceHomePower>(Owner.Creature, 1m, Owner.Creature, this);

        var brokenNote = base.CombatState!.CreateCard<BrokenNote>(Owner);
        await CardPileCmd.AddGeneratedCardToCombat(brokenNote, PileType.Discard, addedByPlayer: true);
    }

    protected override void OnUpgrade()
    {
        // 升级效果通过 IsUpgraded 在 OnPlay 中处理
    }
}
