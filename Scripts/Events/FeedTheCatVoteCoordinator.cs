using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Runs;

namespace STS2_Tomorin_Mod.Events;

internal enum FeedTheCatBranch
{
    Reward = 0,
    Penalty = 1,
}

internal sealed class FeedTheCatVoteCoordinator
{
    private static FeedTheCatVoteCoordinator? _current;

    private readonly Dictionary<ulong, FeedTheCatBranch> _votes = [];
    private readonly TaskCompletionSource<FeedTheCatBranch> _finalBranchSource = new();
    private IRunState? _runState;
    private uint? _finalChoiceId;
    private bool _remoteWaitStarted;
    private bool _hostResolved;
    private Task? _applyTask;

    public static FeedTheCatVoteCoordinator Current => _current ??= new FeedTheCatVoteCoordinator();

    public static void ResetIfCurrent(FeedTheCatVoteCoordinator coordinator)
    {
        if (ReferenceEquals(_current, coordinator))
        {
            _current = null;
        }
    }

    public async Task VoteAndWaitForBranch(FeedTheCat source, FeedTheCatBranch vote)
    {
        if (source.Owner == null)
        {
            return;
        }

        _votes.TryAdd(source.Owner.NetId, vote);
        _runState ??= source.Owner.RunState;
        EnsureFinalChoiceReserved(source.Owner.RunState);
        ResolveAsHostIfReady(source);
        EnsureRemoteWaitStarted(source.Owner.RunState);

        var branch = await _finalBranchSource.Task;
        await ApplyFinalBranchToAll(branch);
    }

    private void EnsureFinalChoiceReserved(IRunState runState)
    {
        if (_finalChoiceId.HasValue)
        {
            return;
        }

        var finalChoicePlayer = GetFinalChoicePlayer(runState);
        _finalChoiceId = RunManager.Instance.PlayerChoiceSynchronizer.ReserveChoiceId(finalChoicePlayer);
    }

    private void ResolveAsHostIfReady(FeedTheCat source)
    {
        if (_hostResolved || RunManager.Instance.NetService.Type == NetGameType.Client)
        {
            return;
        }

        var players = source.Owner!.RunState.Players;
        if (players.Count == 0 || players.Any(player => !_votes.ContainsKey(player.NetId)))
        {
            return;
        }

        _hostResolved = true;

        var rewardVotes = _votes.Values.Count(vote => vote == FeedTheCatBranch.Reward);
        var penaltyVotes = _votes.Values.Count(vote => vote == FeedTheCatBranch.Penalty);
        var finalBranch = rewardVotes == penaltyVotes
            ? (source.Rng.NextBool() ? FeedTheCatBranch.Reward : FeedTheCatBranch.Penalty)
            : rewardVotes > penaltyVotes ? FeedTheCatBranch.Reward : FeedTheCatBranch.Penalty;

        _finalBranchSource.TrySetResult(finalBranch);

        if (RunManager.Instance.NetService.Type == NetGameType.Host)
        {
            var finalChoicePlayer = GetFinalChoicePlayer(source.Owner!.RunState);
            RunManager.Instance.PlayerChoiceSynchronizer.SyncLocalChoice(
                finalChoicePlayer,
                _finalChoiceId!.Value,
                PlayerChoiceResult.FromIndex((int)finalBranch));
        }
    }

    private void EnsureRemoteWaitStarted(IRunState runState)
    {
        if (_remoteWaitStarted || RunManager.Instance.NetService.Type != NetGameType.Client)
        {
            return;
        }

        _remoteWaitStarted = true;
        _ = WaitForRemoteFinalBranch(runState);
    }

    private async Task WaitForRemoteFinalBranch(IRunState runState)
    {
        var finalChoicePlayer = GetFinalChoicePlayer(runState);
        var result = await RunManager.Instance.PlayerChoiceSynchronizer.WaitForRemoteChoice(finalChoicePlayer, _finalChoiceId!.Value);
        _finalBranchSource.TrySetResult((FeedTheCatBranch)result.AsIndex());
    }

    private async Task ApplyFinalBranchToAll(FeedTheCatBranch branch)
    {
        _applyTask ??= ApplyFinalBranchToAllInternal(branch);
        await _applyTask;
    }

    private async Task ApplyFinalBranchToAllInternal(FeedTheCatBranch branch)
    {
        var eventsByPlayer = GetFeedTheCatEvents()
            .Where(feedTheCat => feedTheCat.Owner != null)
            .ToDictionary(feedTheCat => feedTheCat.Owner!.NetId);

        var players = _runState?.Players ?? eventsByPlayer.Values
            .Select(feedTheCat => feedTheCat.Owner!)
            .ToList();

        foreach (var player in players)
        {
            if (eventsByPlayer.TryGetValue(player.NetId, out var feedTheCat))
            {
                await feedTheCat.ApplyFinalBranchFromCoordinator(branch);
            }
        }

        ResetIfCurrent(this);
    }

    private static IEnumerable<FeedTheCat> GetFeedTheCatEvents()
    {
        return RunManager.Instance.EventSynchronizer.Events.OfType<FeedTheCat>();
    }

    private static Player GetFinalChoicePlayer(IRunState runState)
    {
        return runState.Players.First();
    }
}
