using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using STS2_Tomorin_Mod.Afflictions.Base;
using STS2_Tomorin_Mod.Powers;

namespace STS2_Tomorin_Mod.Afflictions;

/// <summary>
/// Crychic诅咒: 下回合抽牌数量-1
/// </summary>
public class CrychicDrawLessCurse : BaseAfflictionModel
{
	public override bool HasExtraCardText => true;

	public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		if (cardPlay.Card != Card || Card?.Owner == null) return;

		await PowerCmd.Apply<LessDrawNextTurnPower>(choiceContext, Card.Owner.Creature, 1, null, Card);
	}
}
