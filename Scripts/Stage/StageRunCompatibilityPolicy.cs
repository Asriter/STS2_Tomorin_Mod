using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace STS2_Tomorin_Mod.Stage;

/// <summary>
/// 集中处理舞台 Run 状态与原版 Neow、章节存档格式之间的兼容边界。
/// </summary>
public static class StageRunCompatibilityPolicy
{
    /// <summary>
    /// 从 UI 可见的 Modifier 中排除仅用于舞台内部持久化的进度状态。
    /// </summary>
    /// <param name="modifiers">当前 Run 的完整 Modifier 集合。</param>
    /// <returns>保留全部游戏规则 Modifier、但不包含舞台进度状态的新只读集合。</returns>
    public static IReadOnlyList<ModifierModel> FilterUiModifiers(IEnumerable<ModifierModel> modifiers)
    {
        ArgumentNullException.ThrowIfNull(modifiers);
        return modifiers.Where(modifier => modifier is not StageRunProgressModifier).ToArray();
    }

    /// <summary>
    /// 从 Neow 可见的 Modifier 中排除仅用于舞台内部持久化的进度状态。
    /// </summary>
    /// <param name="modifiers">当前 Run 的完整 Modifier 集合。</param>
    /// <returns>保留全部游戏规则 Modifier、但不包含舞台进度状态的新只读集合。</returns>
    public static IReadOnlyList<ModifierModel> FilterNeowModifiers(IEnumerable<ModifierModel> modifiers)
    {
        return FilterUiModifiers(modifiers);
    }

    /// <summary>
    /// 恢复因空集合序列化策略而从 Stage JSON 存档中省略的房间标识列表。
    /// </summary>
    /// <param name="rooms">即将交给原版 <c>RoomSet.FromSave</c> 的房间存档。</param>
    public static void NormalizeRoomCollections(SerializableRoomSet rooms)
    {
        ArgumentNullException.ThrowIfNull(rooms);
        rooms.EventIds ??= [];
        rooms.NormalEncounterIds ??= [];
        rooms.EliteEncounterIds ??= [];
    }
}
