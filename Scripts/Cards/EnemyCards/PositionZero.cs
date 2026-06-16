using BaseLib.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models.CardPools;
using STS2_Tomorin_Mod.Cards.Base;

namespace STS2_Tomorin_Mod.Cards.EnemyCards;

/// <summary>
/// Position Zero - 状态卡，1费，保留，选择目标敌人切换C位
/// 打出时无直接效果；CenterPositionManagerPower通过AfterCardPlayed监听切换C位
/// </summary>
[Pool(typeof(TokenCardPool))]
public class PositionZero() : BaseCardModel(
    canonicalEnergyCost: 1,
    type: CardType.Skill,
    rarity: CardRarity.Token,
    targetType: TargetType.AnyEnemy)
{
    public override int MaxUpgradeLevel => 0;

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Retain,
        CardKeyword.Exhaust
    ];
}
