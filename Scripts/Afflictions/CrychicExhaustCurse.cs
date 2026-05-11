using MegaCrit.Sts2.Core.Entities.Cards;
using STS2_Tomorin_Mod.Afflictions.Base;

namespace STS2_Tomorin_Mod.Afflictions;

/// <summary>
/// Crychic诅咒: 卡牌获得消耗和虚无关键词
/// </summary>
public class CrychicExhaustCurse : BaseAfflictionModel
{
	public override bool HasExtraCardText => true;
    
    public override void AfterApplied()
    {
        base.AfterApplied();
        Card?.AddKeyword(CardKeyword.Exhaust);
        Card?.AddKeyword(CardKeyword.Ethereal);
    }

    public override void BeforeRemoved()
    {
        Card?.RemoveKeyword(CardKeyword.Exhaust);
        Card?.RemoveKeyword(CardKeyword.Ethereal);
        base.BeforeRemoved();
    }
}
