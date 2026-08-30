using HarmonyLib;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using STS2_Tomorin_Mod.Stage;

namespace STS2_Tomorin_Mod.Patch;

/// <summary>
/// 在原版创建房间前将舞台节点解析为固定事件、精英和首领内容。
/// </summary>
[HarmonyPatch(typeof(RunManager), "CreateRoom")]
internal static class StageRoomResolverPatch
{
    /// <summary>
    /// 覆盖舞台节点的随机内容模型；商店与篝火保持原版房间实现。
    /// </summary>
    /// <param name="roomType">可被替换的房间类型。</param>
    /// <param name="mapPointType">当前地图点类型，仅用于保持原版调用签名。</param>
    /// <param name="model">可被替换的固定内容模型。</param>
    [HarmonyPrefix]
    private static void Prefix(ref RoomType roomType, MapPointType mapPointType, ref AbstractModel model)
    {
        var runState = RunManager.Instance?.DebugOnlyGetState();
        if (runState == null || !StageRoomResolver.TryResolveCurrentRoom(runState, out var resolvedRoomType, out var resolvedModel))
        {
            return;
        }

        roomType = resolvedRoomType;
        model = resolvedModel!;
    }
}
