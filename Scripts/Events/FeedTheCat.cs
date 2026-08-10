using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Gold;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Runs;
using STS2_Tomorin_Mod.Relics;

namespace STS2_Tomorin_Mod.Events;

public class FeedTheCat : CustomEventModel
{
    private const string InitialPage = "INITIAL";
    private const string WaitingPage = "WAITING";
    private const string RewardPage = "REWARD";
    private const string PenaltyPage = "PENALTY";
    private const string CompletePage = "COMPLETE";
    private const decimal GoldCost = 200m;

    public override string CustomInitialPortraitPath => "res://STS2_Tomorin_Mod/images/events/FeedTheCat.png";

    private static bool _allowFixedSelectionCheck;

    private readonly FeedTheCatVoteCoordinator _voteCoordinator = FeedTheCatVoteCoordinator.Current;
    private bool _finalBranchApplied;

    public override bool IsShared => false;

    public override bool IsAllowed(IRunState runState)
    {
        return _allowFixedSelectionCheck && runState.CurrentActIndex == 1;
    }

    internal static void BeginFixedSelectionCheck()
    {
        _allowFixedSelectionCheck = true;
    }

    internal static void EndFixedSelectionCheck()
    {
        _allowFixedSelectionCheck = false;
    }

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        return
        [
            CreateOption(VoteReward, InitialPage, nameof(VoteReward)),
            CreateOption(VotePenalty, InitialPage, nameof(VotePenalty)),
        ];
    }

    private async Task VoteReward()
    {
        SetWaitingForVotes();
        await _voteCoordinator.VoteAndWaitForBranch(this, FeedTheCatBranch.Reward);
    }

    private async Task VotePenalty()
    {
        SetWaitingForVotes();
        await _voteCoordinator.VoteAndWaitForBranch(this, FeedTheCatBranch.Penalty);
    }

    private void SetWaitingForVotes()
    {
        SetEventState(PageDescription(WaitingPage), [LockedOption("Waiting", WaitingPage)]);
    }

    internal async Task ApplyFinalBranchFromCoordinator(FeedTheCatBranch branch)
    {
        if (_finalBranchApplied || Owner == null)
        {
            return;
        }

        _finalBranchApplied = true;

        if (branch == FeedTheCatBranch.Reward)
        {
            await RelicCmd.Obtain<MatchaParfait>(Owner);
            SetEventState(PageDescription(RewardPage), GenerateRewardOptions());
        }
        else
        {
            await RelicCmd.Obtain<EmptyParfait>(Owner);
            SetEventState(PageDescription(PenaltyPage), GeneratePenaltyOptions());
        }
    }

    private IReadOnlyList<EventOption> GenerateRewardOptions()
    {
        return
        [
            CreateOption(Heal, RewardPage, nameof(Heal)),
            HasRemovableCards()
                ? CreateOption(RemoveCard, RewardPage, nameof(RemoveCard))
                : LockedOption("RemoveCardLocked", RewardPage),
            HasUpgradableCards()
                ? CreateOption(UpgradeCard, RewardPage, nameof(UpgradeCard))
                : LockedOption("UpgradeCardLocked", RewardPage),
        ];
    }

    private IReadOnlyList<EventOption> GeneratePenaltyOptions()
    {
        return
        [
            Owner != null && Owner.Gold >= GoldCost
                ? CreateOption(LoseGold, PenaltyPage, nameof(LoseGold))
                : LockedOption("LoseGoldLocked", PenaltyPage),
            CreateOption(GainDebt, PenaltyPage, nameof(GainDebt)),
        ];
    }

    private EventOption CreateOption(Func<Task> onChosen, string pageKey, string optionKey)
    {
        return new EventOption(this, onChosen, $"{Id.Entry}.pages.{pageKey}.options.{optionKey}");
    }

    private async Task Heal()
    {
        if (Owner != null)
        {
            var healAmount = Math.Ceiling(Owner.Creature.MaxHp * 0.2m);
            await CreatureCmd.Heal(Owner.Creature, healAmount, true);
        }

        FinishComplete();
    }

    private async Task RemoveCard()
    {
        if (Owner != null)
        {
            var selectedCards = (await CardSelectCmd.FromDeckForRemoval(
                Owner,
                new CardSelectorPrefs(CardSelectorPrefs.RemoveSelectionPrompt, 1))).ToList();

            if (selectedCards.Count > 0)
            {
                await CardPileCmd.RemoveFromDeck(selectedCards);
            }
        }

        FinishComplete();
    }

    private async Task UpgradeCard()
    {
        if (Owner != null)
        {
            var selectedCard = (await CardSelectCmd.FromDeckForUpgrade(
                Owner,
                new CardSelectorPrefs(CardSelectorPrefs.UpgradeSelectionPrompt, 1))).FirstOrDefault();

            if (selectedCard != null)
            {
                CardCmd.Upgrade(selectedCard, CardPreviewStyle.EventLayout);
            }
        }

        FinishComplete();
    }

    private async Task LoseGold()
    {
        if (Owner != null)
        {
            await PlayerCmd.LoseGold(200m, Owner, GoldLossType.Spent);
        }

        FinishComplete();
    }

    private async Task GainDebt()
    {
        if (Owner != null)
        {
            await CardPileCmd.AddCurseToDeck<Debt>(Owner);
        }

        FinishComplete();
    }

    private void FinishComplete()
    {
        SetEventFinished(PageDescription(CompletePage));
    }

    private bool HasRemovableCards()
    {
        return Owner?.Deck.Cards.Any(card => card.IsRemovable) == true;
    }

    private bool HasUpgradableCards()
    {
        return Owner?.Deck.Cards.Any(card => card.IsUpgradable) == true;
    }

    protected override void OnEventFinished()
    {
        FeedTheCatVoteCoordinator.ResetIfCurrent(_voteCoordinator);
        base.OnEventFinished();
    }
}
