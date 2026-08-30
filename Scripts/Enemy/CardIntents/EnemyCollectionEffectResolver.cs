using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using STS2_Tomorin_Mod.Cards.Collections;
using STS2_Tomorin_Mod.Enemy.CardIntents.Test;
using STS2_Tomorin_Mod.Powers;

namespace STS2_Tomorin_Mod.Enemy.CardIntents;

/// <summary>
/// 指定收藏品效果程序在直接效果以外需要冻结的特殊解析步骤。
/// </summary>
public enum EnemyCollectionSpecialResolutionKind
{
    /// <summary>收藏品只包含直接效果或不产生额外步骤。</summary>
    None,

    /// <summary>从敌人抽牌区选择并立即执行一张卡牌。</summary>
    DrawAndExecuteImmediateCard,

    /// <summary>从普通消耗牌与已消耗收藏品的统一候选中恢复一个对象。</summary>
    RecoverConsumedEntry
}

/// <summary>
/// 保存一项已注册收藏品效果的共享直接效果与特殊解析语义。
/// </summary>
public sealed class EnemyCollectionEffectProgram
{
    /// <summary>
    /// 创建不可变收藏品效果程序。
    /// </summary>
    /// <param name="programId">跨准备、执行和投影稳定的程序标识。</param>
    /// <param name="directEffects">可由执行和纯模拟共享的有序直接效果。</param>
    /// <param name="specialResolutionKind">需要准备阶段冻结的额外解析种类。</param>
    public EnemyCollectionEffectProgram(
        string programId,
        IEnumerable<IEnemyCardEffectNode>? directEffects = null,
        EnemyCollectionSpecialResolutionKind specialResolutionKind = EnemyCollectionSpecialResolutionKind.None)
    {
        if (string.IsNullOrWhiteSpace(programId))
        {
            throw new ArgumentException("收藏品效果程序标识不能为空。", nameof(programId));
        }

        IEnemyCardEffectNode[] copiedEffects = (directEffects ?? []).ToArray();
        if (copiedEffects.Any(effect => effect is null || string.IsNullOrWhiteSpace(effect.ProgramId)))
        {
            throw new ArgumentException("收藏品直接效果必须完整且具有稳定标识。", nameof(directEffects));
        }

        ProgramId = programId;
        DirectEffects = Array.AsReadOnly(copiedEffects);
        SpecialResolutionKind = specialResolutionKind;
    }

    /// <summary>获取稳定收藏品效果程序标识。</summary>
    public string ProgramId { get; }

    /// <summary>获取真实执行与纯模拟共享的有序直接效果。</summary>
    public IReadOnlyList<IEnemyCardEffectNode> DirectEffects { get; }

    /// <summary>获取需要冻结稳定选择的特殊解析种类。</summary>
    public EnemyCollectionSpecialResolutionKind SpecialResolutionKind { get; }
}

/// <summary>
/// 把显式收藏品目录解析为准备、执行和投影共同消费的效果程序。
/// </summary>
public static class EnemyCollectionEffectResolver
{
    private static readonly IReadOnlyDictionary<string, EnemyCollectionEffectProgram> Programs =
        BuildPrograms();

    /// <summary>
    /// 解析已注册收藏品定义，并拒绝目录尚未适配的第三方程序。
    /// </summary>
    /// <param name="definition">待解析的不可变收藏品定义。</param>
    /// <returns>与定义程序标识完全匹配的共享效果程序。</returns>
    public static EnemyCollectionEffectProgram GetRequired(EnemyCollectionDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (!Programs.TryGetValue(definition.EffectProgramId, out EnemyCollectionEffectProgram? program))
        {
            throw new KeyNotFoundException(
                $"未知收藏品效果程序 {definition.EffectProgramId}（{definition.CollectionId}）。");
        }

        return program;
    }

    /// <summary>
    /// 构造当前测试收藏品目录的唯一共享效果映射。
    /// </summary>
    /// <returns>按稳定程序标识索引的只读映射。</returns>
    private static IReadOnlyDictionary<string, EnemyCollectionEffectProgram> BuildPrograms()
    {
        BrokenNote brokenNote = ModelDb.Card<BrokenNote>();
        ColdRedTea coldRedTea = ModelDb.Card<ColdRedTea>();
        Dictionary<string, EnemyCollectionEffectProgram> programs = new(StringComparer.Ordinal)
        {
            ["COLLECTION:BROKEN_NOTE"] = new(
                "COLLECTION:BROKEN_NOTE",
                [
                    new EnemyBlockEffect(
                        "COLLECTION:BROKEN_NOTE:BLOCK",
                        brokenNote.DynamicVars.Block.BaseValue),
                    new EnemySelfPowerEffect<BrokenNotePower>(
                        "COLLECTION:BROKEN_NOTE:POWER",
                        decimal.One)
                ]),
            ["COLLECTION:COLD_RED_TEA"] = new(
                "COLLECTION:COLD_RED_TEA",
                [
                    new EnemyAllPlayersPowerEffect<WeakPower>(
                        "COLLECTION:COLD_RED_TEA:WEAK",
                        coldRedTea.DynamicVars["WeakPower"].BaseValue),
                    new EnemyAllPlayersPowerEffect<CustomConstrictPower>(
                        "COLLECTION:COLD_RED_TEA:CONSTRICT",
                        coldRedTea.DynamicVars["CustomConstrictPower"].BaseValue)
                ]),
            ["COLLECTION:CRUMPLED_PAPER"] = new(
                "COLLECTION:CRUMPLED_PAPER",
                specialResolutionKind: EnemyCollectionSpecialResolutionKind.DrawAndExecuteImmediateCard),
            ["COLLECTION:LEFTOVER_BUFFET"] = new(
                "COLLECTION:LEFTOVER_BUFFET",
                specialResolutionKind: EnemyCollectionSpecialResolutionKind.RecoverConsumedEntry),
            ["COLLECTION:MIDNIGHT_COFFEE"] = new(
                "COLLECTION:MIDNIGHT_COFFEE",
                specialResolutionKind: EnemyCollectionSpecialResolutionKind.DrawAndExecuteImmediateCard),
            ["COLLECTION:STAR_STONE"] = new("COLLECTION:STAR_STONE")
        };
        return new Dictionary<string, EnemyCollectionEffectProgram>(programs, StringComparer.Ordinal);
    }
}
