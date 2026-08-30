using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;
using STS2_Tomorin_Mod.Stage;
using StageAct = STS2_Tomorin_Mod.Acts.Stage;

namespace STS2_Tomorin_Mod.Patch;

/// <summary>
/// 在原版章节恢复逻辑读取 Stage 空房间列表前补回被 JSON 省略的集合。
/// </summary>
[HarmonyPatch(typeof(ActModel), nameof(ActModel.FromSave))]
internal static class StageActSaveCompatibilityPatch
{
    /// <summary>
    /// 仅修复 Stage 存档，不改变原版或其他 Mod 章节的恢复语义。
    /// </summary>
    [HarmonyPrefix]
    private static void Prefix(SerializableActModel save)
    {
        if (save.Id != ModelDb.Act<StageAct>().Id)
        {
            return;
        }

        StageRunCompatibilityPolicy.NormalizeRoomCollections(save.SerializableRooms);
    }
}
