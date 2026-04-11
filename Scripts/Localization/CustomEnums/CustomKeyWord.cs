using BaseLib.Patches.Content;
using MegaCrit.Sts2.Core.Entities.Cards;

namespace STS2_Tomorin_Mod.Localization.CustomEnums;

public static class CustomKeyWord
{
    //定义字段：灵感
    [CustomEnum, KeywordProperties(AutoKeywordPosition.After)]
    public static CardKeyword Inspiration;
    
    //单回合灵感
    [CustomEnum, KeywordProperties(AutoKeywordPosition.After)]
    public static CardKeyword SingleTurnInspiration;
    
    //定义字段：灵光乍现
    [CustomEnum, KeywordProperties(AutoKeywordPosition.After)]
    public static CardKeyword Epiphany;
}