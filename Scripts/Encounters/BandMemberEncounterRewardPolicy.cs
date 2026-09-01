using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using STS2_Tomorin_Mod.Relics;

namespace STS2_Tomorin_Mod.Encounters;

/// <summary>
/// 负责把已确认的乐队成员奖励资格转换为逐玩家去重后的遗物奖励。
/// </summary>
internal static class BandMemberEncounterRewardPolicy
{
    /// <summary>
    /// 为战斗中的每名玩家添加其尚未持有、也尚未进入待领奖列表的已赚取遗物。
    /// </summary>
    public static void AddEarnedRewards(
        CombatRoom room,
        BandMemberSelection selection,
        bool leftRewardEarned,
        bool rightRewardEarned)
    {
        ArgumentNullException.ThrowIfNull(room);
        ArgumentNullException.ThrowIfNull(selection);
        if (room.CombatState.Encounter is not BandMemberEncounter)
        {
            throw new InvalidOperationException(
                $"{nameof(BandMemberEncounterRewardPolicy)} 只能为 {nameof(BandMemberEncounter)} 结算奖励。");
        }

        foreach (Player player in room.CombatState.Players)
        {
            IEnumerable<ModelId> ownedRelicIds = player.Relics.Select(relic => relic.Id);
            IEnumerable<ModelId> pendingRelicIds = room.ExtraRewards.TryGetValue(player, out var rewards)
                ? rewards.OfType<RelicReward>()
                    .Select(reward => reward.Relic)
                    .OfType<RelicModel>()
                    .Select(relic => relic.Id)
                : [];

            IReadOnlyList<ModelId> missingRelicIds = DetermineMissingEarnedRewards(
                selection,
                leftRewardEarned,
                rightRewardEarned,
                ownedRelicIds,
                pendingRelicIds,
                member => ResolveRelic(member).Id);

            foreach (ModelId relicId in missingRelicIds)
            {
                RelicModel relic = ResolveSelectedRelic(selection, relicId).ToMutable();
                room.AddExtraReward(player, new RelicReward(relic, player));
            }
        }
    }

    /// <summary>
    /// 按左右顺序返回已赚取且不在持有/待领取集合中的身份；同时阻止同一身份重复进入结果。
    /// </summary>
    internal static IReadOnlyList<T> DetermineMissingEarnedRewards<T>(
        BandMemberSelection selection,
        bool leftRewardEarned,
        bool rightRewardEarned,
        IEnumerable<T> owned,
        IEnumerable<T> pending,
        Func<BandMemberKind, T> resolve)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(owned);
        ArgumentNullException.ThrowIfNull(pending);
        ArgumentNullException.ThrowIfNull(resolve);

        HashSet<T> excluded = new(owned);
        excluded.UnionWith(pending);
        List<T> result = [];

        AddIfMissing(selection.Left, leftRewardEarned);
        AddIfMissing(selection.Right, rightRewardEarned);
        return result;

        void AddIfMissing(BandMemberKind member, bool earned)
        {
            if (!earned)
            {
                return;
            }

            T identity = resolve(member);
            if (excluded.Add(identity))
            {
                result.Add(identity);
            }
        }
    }

    private static RelicModel ResolveSelectedRelic(BandMemberSelection selection, ModelId relicId)
    {
        RelicModel left = ResolveRelic(selection.Left);
        if (left.Id == relicId)
        {
            return left;
        }

        RelicModel right = ResolveRelic(selection.Right);
        if (right.Id == relicId)
        {
            return right;
        }

        throw new InvalidOperationException($"奖励遗物 {relicId} 不属于当前左右成员选择。");
    }

    private static RelicModel ResolveRelic(BandMemberKind member)
    {
        return member switch
        {
            BandMemberKind.Anon => ModelDb.Relic<AnonGuitar>(),
            BandMemberKind.Taki => ModelDb.Relic<TakiDrum>(),
            BandMemberKind.Soyo => ModelDb.Relic<SoyoBase>(),
            BandMemberKind.Raana => ModelDb.Relic<RaanaGuitar>(),
            _ => throw new ArgumentOutOfRangeException(nameof(member), member, "未知乐队成员身份。")
        };
    }
}
