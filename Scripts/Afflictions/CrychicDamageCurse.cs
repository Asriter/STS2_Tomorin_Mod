using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.ValueProps;
using STS2_Tomorin_Mod.Afflictions.Base;

namespace STS2_Tomorin_Mod.Afflictions;

/// <summary>
/// Crychic诅咒: 打出此卡时受到6点伤害
/// </summary>
public class CrychicDamageCurse : BaseAfflictionModel
{
    /// <summary>
    /// 打出时受到的伤害值
    /// </summary>
    private const int SelfDamage = 6;
	public override bool HasExtraCardText => true;

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Card != Card || Card?.Owner == null) return;

        await CreatureCmd.Damage(choiceContext, Card.Owner.Creature, SelfDamage,
            ValueProp.Unpowered, Card.Owner.Creature, Card, cardPlay);
    }
}
