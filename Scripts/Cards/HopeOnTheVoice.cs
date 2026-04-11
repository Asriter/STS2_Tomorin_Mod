using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Powers;
using STS2_Tomorin_Mod.CardPools;
using STS2_Tomorin_Mod.Cards.Base;
using STS2_Tomorin_Mod.Cards.Collections;

namespace STS2_Tomorin_Mod.Cards;

/// <summary>
/// 立足音色的希望
/// 0费 白卡 技能 消耗（升级后去除）给予一名敌人一层虚弱，一层易伤。将一张深夜的罐装咖啡加入手牌。
/// </summary>
[Pool(typeof(TomorinCardPool))]
public class HopeOnTheVoice : BaseCardModel
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new List<DynamicVar>()
        {
            new PowerVar<WeakPower>(1m),
            new PowerVar<VulnerablePower>(1m),
        };

    public override IEnumerable<CardKeyword> CanonicalKeywords
    {
        get
        {
            var list = base.CanonicalKeywords.ToList();
            list.Add(CardKeyword.Exhaust);
            return list;
        }
    }

    protected override IEnumerable<IHoverTip> ExtraHoverTips
    {
        get
        {
            var list = base.ExtraHoverTips.ToList();
            list.Add(HoverTipFactory.FromPower<WeakPower>());
            list.Add(HoverTipFactory.FromPower<VulnerablePower>());
            list.Add(HoverTipFactory.FromCard<MidnightCoffee>(base.IsUpgraded));
            return list;
        }
    }

    public HopeOnTheVoice() : base(0, CardType.Skill, CardRarity.Common, TargetType.AnyEnemy)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

        // 给予目标一层虚弱和一层易伤
        await PowerCmd.Apply<WeakPower>(cardPlay.Target, base.DynamicVars["WeakPower"].BaseValue, Owner.Creature, this);
        await PowerCmd.Apply<VulnerablePower>(cardPlay.Target, base.DynamicVars["VulnerablePower"].BaseValue, Owner.Creature, this);

        // 将一张深夜的罐装咖啡加入手牌
        var midnightCoffee = base.CombatState!.CreateCard<MidnightCoffee>(Owner);
        await CardPileCmd.AddGeneratedCardToCombat(midnightCoffee, PileType.Hand, addedByPlayer: true);
    }

    protected override void OnUpgrade()
    {
        RemoveKeyword(CardKeyword.Exhaust);
    }
}
