using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using STS2_Tomorin_Mod.CardPools;
using STS2_Tomorin_Mod.Cards.Base;
using STS2_Tomorin_Mod.Commands;
using STS2_Tomorin_Mod.Localization.CustomEnums;
using STS2_Tomorin_Mod.Localization.DynamicVars;

namespace STS2_Tomorin_Mod.Cards.Collections;

/// <summary>
/// 星石
/// 搜集品 
/// 可以代替任意一种作词的消耗
/// </summary>

[Pool(typeof(CollectionsCardPool))]
public class StarStone() : 
    BaseCardModel(-1, CardType.Status, CardRarity.Status, TargetType.None, true)
{
    public override int MaxUpgradeLevel => 0;
    
    public override IEnumerable<CardKeyword> CanonicalKeywords
    {
        get
        {
            var list = base.CanonicalKeywords.ToList();
            list.Add(CardKeyword.Unplayable);
            list.Add(CustomKeyWord.Epiphany);
            list.Add(CardKeyword.Retain);
            return list;
        }
    }
}