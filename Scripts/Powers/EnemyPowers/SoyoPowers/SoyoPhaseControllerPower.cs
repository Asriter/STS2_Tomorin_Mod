using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;
using STS2_Tomorin_Mod.Audio;
using STS2_Tomorin_Mod.Cards;
using STS2_Tomorin_Mod.Enemy;

namespace STS2_Tomorin_Mod.Powers;

/// <summary>
/// Handles Soyo phase switching and once-per-combat easter eggs.
/// </summary>
public class SoyoPhaseControllerPower : BasePowerModel
{
    private bool _prideTriggered;
    private bool _doEverythingTriggered;
    private bool _utakotobaTriggered;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    protected override bool IsVisibleInternal => false;

    public override async Task BeforeSideTurnStart(PlayerChoiceContext choiceContext, CombatSide side,
        IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (Owner.Monster is not Soyo soyo) return;

        if (side == CombatSide.Player)
        {
            var phase = await soyo.RefreshPhaseForPlayerTurnStart();
            if (phase == Soyo.SoyoPhase.True)
            {
                await SoyoTaskPower.RemoveCurrentTask(Owner);
                return;
            }

            await SoyoTaskPower.ApplyRandomTask(choiceContext, Owner);
            return;
        }

        if (side == CombatSide.Enemy && soyo.Phase == Soyo.SoyoPhase.Mask)
        {
            await PowerCmd.Apply<SoyoMaskedDamageReductionPower>(choiceContext, Owner, 1, Owner, null);
        }
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var player = cardPlay.Card.Owner;
        if (player == null || Owner.Monster is not Soyo soyo) return;

        switch (cardPlay.Card)
        {
            case PrideManSaki when !_prideTriggered:
                _prideTriggered = true;
                CustomAudioController.PlaySfx("soyo-WhyPlayHaru");
                soyo.WhyPlayHaru();
                await SoyoEstrangementPower.SetAmount(choiceContext, Owner, 7, this);
                await soyo.StunOneTurn();
                break;
            case DoEverything when !_doEverythingTriggered:
                _doEverythingTriggered = true;
                CustomAudioController.PlaySfx("soyo-DoEverything");
                soyo.DoEverything();
                await PowerCmd.Apply<WeakPower>(choiceContext, Owner, 2, player.Creature, cardPlay.Card);
                await PowerCmd.Apply<VulnerablePower>(choiceContext, Owner, 2, player.Creature, cardPlay.Card);
                await SoyoTaskPower.CompleteCurrentTask(choiceContext, Owner);
                break;
            case UtakotobaToken when !_utakotobaTriggered:
                _utakotobaTriggered = true;
                CustomAudioController.PlaySfx("soyo-ForEnding");
                soyo.ForEnding();
                await SoyoEstrangementPower.Clear(choiceContext, Owner, this);
                foreach (var maskPower in Owner.Powers.OfType<SoyoMaskedDamageReductionPower>().ToList())
                {
                    await PowerCmd.Remove(maskPower);
                }

                break;
        }
    }
}
