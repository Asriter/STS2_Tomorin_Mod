using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;
using STS2_Tomorin_Mod.Audio;
using STS2_Tomorin_Mod.Cards;
using STS2_Tomorin_Mod.Enemy;

namespace STS2_Tomorin_Mod.Powers;

/// <summary>
/// Handles Soyo phase switching and once-per-player easter eggs.
/// </summary>
public class SoyoPhaseControllerPower : BasePowerModel
{
    private readonly HashSet<Player> _prideTriggeredPlayers = new();
    private readonly HashSet<Player> _doEverythingTriggeredPlayers = new();
    private readonly HashSet<Player> _utakotobaTriggeredPlayers = new();

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    protected override bool IsVisibleInternal => false;

    public override async Task BeforeSideTurnStart(PlayerChoiceContext choiceContext, CombatSide side,
        IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (side != CombatSide.Player || Owner.Monster is not Soyo soyo) return;

        soyo.RefreshPhaseByEstrangement();
        if (soyo.Phase == Soyo.SoyoPhase.True)
        {
            await SoyoTaskPower.RemoveCurrentTask(Owner);
        }
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var player = cardPlay.Card.Owner;
        if (player == null || Owner.Monster is not Soyo soyo) return;

        switch (cardPlay.Card)
        {
            case PrideManSaki when _prideTriggeredPlayers.Add(player):
                CustomAudioController.PlaySfx("soyo-WhyPlayHaru");
                soyo.WhyPlayHaru();
                await soyo.StunOneTurn();
                await SoyoEstrangementPower.Modify(choiceContext, Owner, 5, this);
                break;
            case DoEverything when _doEverythingTriggeredPlayers.Add(player):
                CustomAudioController.PlaySfx("soyo-DoEverything");
                soyo.DoEverything();
                await PowerCmd.Apply<WeakPower>(choiceContext, Owner, 2, player.Creature, cardPlay.Card);
                await PowerCmd.Apply<VulnerablePower>(choiceContext, Owner, 2, player.Creature, cardPlay.Card);
                break;
            case UtakotobaToken when _utakotobaTriggeredPlayers.Add(player):
                CustomAudioController.PlaySfx("soyo-ForEnding");
                soyo.ForEnding();
                await SoyoEstrangementPower.Modify(choiceContext, Owner, -5, this);
                break;
        }
    }
}
