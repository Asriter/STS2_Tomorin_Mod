using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2_Tomorin_Mod.Stage;

/// <summary>
/// 在原版 UI 构建期间临时隐藏仅用于舞台持久化的内部 Modifier。
/// </summary>
internal static class StageRunModifierVisibilityScope
{
    private static readonly PropertyInfo ModifiersProperty =
        AccessTools.Property(typeof(RunState), nameof(RunState.Modifiers)) ??
        throw new MissingMemberException(typeof(RunState).FullName, nameof(RunState.Modifiers));

    /// <summary>
    /// 用 UI 可见列表临时替换具体 RunState 的 Modifier，并返回待恢复的原列表。
    /// </summary>
    internal static IReadOnlyList<ModifierModel>? Hide(IRunState? runState)
    {
        if (runState is not RunState mutableRunState)
        {
            return null;
        }

        IReadOnlyList<ModifierModel> original = mutableRunState.Modifiers;
        IReadOnlyList<ModifierModel> filtered = StageRunCompatibilityPolicy.FilterUiModifiers(original);
        if (filtered.Count == original.Count)
        {
            return null;
        }

        ModifiersProperty.SetValue(mutableRunState, filtered);
        return original;
    }

    /// <summary>
    /// 恢复 UI 构建前的完整 Modifier 列表，保留舞台条件状态与存档数据。
    /// </summary>
    internal static void Restore(IRunState? runState, IReadOnlyList<ModifierModel>? original)
    {
        if (runState is RunState mutableRunState && original != null)
        {
            ModifiersProperty.SetValue(mutableRunState, original);
        }
    }
}
