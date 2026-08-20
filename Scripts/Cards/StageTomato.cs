using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.CardPools;
using STS2_Tomorin_Mod.Cards.Base;
using STS2_Tomorin_Mod.Localization.CustomEnums;

namespace STS2_Tomorin_Mod.Cards;

/// <summary>
/// 舞台番茄事件状态牌。被消耗时通过灵感触发回复生命并抽牌。
/// </summary>
[Pool(typeof(StatusCardPool))]
public class StageTomato() : BaseCardModel(-1, CardType.Status, CardRarity.Status, TargetType.None)
{
    /// <summary>
    /// 事件状态牌不可升级。
    /// </summary>
    public override int MaxUpgradeLevel => 0;

    /// <summary>
    /// 事件专属状态牌不会进入战斗内随机生成池。
    /// </summary>
    public override bool CanBeGeneratedInCombat => false;

    /// <summary>
    /// 舞台番茄始终具有灵感，以便在被消耗时执行卡牌效果。
    /// </summary>
    public override bool IsInspiration => true;

    /// <summary>
    /// 获取舞台番茄的固定关键词。
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords
    {
        get
        {
            List<CardKeyword> keywords = base.CanonicalKeywords.ToList();
            keywords.Add(CardKeyword.Unplayable);
            keywords.Add(CardKeyword.Retain);
            keywords.Add(CustomKeyWord.Epiphany);
            return keywords;
        }
    }

    /// <summary>
    /// 获取舞台番茄的回复量与抽牌量。
    /// </summary>
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("HealAmount", 4m),
        new CardsVar(1),
    ];

    /// <summary>
    /// 舞台番茄不可由玩家主动打出。
    /// </summary>
    protected override bool IsPlayable => false;

    /// <summary>
    /// 在舞台番茄被灵感自动执行时回复生命并抽一张牌。
    /// </summary>
    /// <param name="choiceContext">本次消耗使用的玩家选择上下文。</param>
    /// <param name="cardPlay">灵感自动创建的卡牌执行信息。</param>
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.Heal(Owner.Creature, DynamicVars["HealAmount"].BaseValue);
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.BaseValue, Owner);
    }
}
