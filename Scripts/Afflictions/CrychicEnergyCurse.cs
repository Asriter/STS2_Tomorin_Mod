using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using STS2_Tomorin_Mod.Afflictions.Base;

namespace STS2_Tomorin_Mod.Afflictions;

/// <summary>
/// Crychic诅咒: 打出此卡时失去1点能量
/// 对应需求 3.3.1
/// </summary>
public class CrychicEnergyCurse : BaseAfflictionModel
{
    /// <summary>
    /// 失去的能量数量
    /// </summary>
    private const decimal EnergyLoss = 1m;

	public override bool HasExtraCardText => true;

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Card != Card || Card?.Owner == null) return;
        
        await PlayerCmd.LoseEnergy(EnergyLoss, Card.Owner);
    }
}
