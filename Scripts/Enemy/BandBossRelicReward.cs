using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;

namespace STS2_Tomorin_Mod.Enemy;

/// <summary>
/// 为乐队成员 Boss 战向所有玩家发放指定的专属事件遗物。
/// </summary>
internal static class BandBossRelicReward
{
    /// <summary>
    /// 仅在 Boss 房中向每名玩家添加一份指定遗物奖励。
    /// </summary>
    public static void Add<TRelic>(CombatRoom room) where TRelic : RelicModel
    {
        if (room.RoomType != RoomType.Boss)
        {
            return;
        }

        foreach (var player in room.CombatState.Players)
        {
            var relic = ModelDb.Relic<TRelic>().ToMutable();
            room.AddExtraReward(player, new RelicReward(relic, player));
        }
    }
}
