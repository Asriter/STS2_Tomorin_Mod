using BaseLib.Abstracts;
using BaseLib.Extensions;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Runs;
using STS2_Tomorin_Mod.Characters;
using STS2_Tomorin_Mod.Relics;

namespace STS2_Tomorin_Mod.Events;

/// <summary>
/// 表示只为包含 Tomorin 的队伍开放、且仅属于 Stage 的“舞台的长颈鹿”先古之民事件。
/// </summary>
public sealed class GiraffeAncient : CustomAncientModel
{
    /// <summary>
    /// 获取复用的先古之民背景场景路径。
    /// </summary>
    public override string? CustomScenePath => "res://STS2_Tomorin_Mod/scenes/Ancients/Giraffe.tscn";

    /// <summary>
    /// 获取复用的地图节点图标路径。
    /// </summary>
    public override string? CustomMapIconPath => "res://STS2_Tomorin_Mod/images/boss_icon/Giraffe_Icon.png";

    /// <summary>
    /// 获取复用的地图节点描边图标路径。
    /// </summary>
    public override string? CustomMapIconOutlinePath =>
        "res://STS2_Tomorin_Mod/images/boss_icon/Giraffe_Icon.png";

    /// <summary>
    /// 获取复用的运行历史图标路径。
    /// </summary>
    public override string? CustomRunHistoryIconPath => "res://STS2_Tomorin_Mod/images/ancient_headIcon/Giraffe_Icon.png";

    /// <summary>
    /// 获取复用的运行历史描边图标路径。
    /// </summary>
    public override string? CustomRunHistoryIconOutlinePath => "res://STS2_Tomorin_Mod/images/ancient_headIcon/Giraffe_Icon.png";

    /// <summary>
    /// 获取完整的三个风险档位选项池，供事件枚举全部可能结果。
    /// </summary>
    protected override OptionPools MakeOptionPools => new(
        MakeHighRiskPool(),
        MakeMediumRiskPool(),
        MakeLowRiskPool());

    /// <summary>
    /// 生成三个独立随机的展示选项，并在当前档位无候选时逐级降档。
    /// </summary>
    /// <returns>高、中、低三个展示位的遗物选项。</returns>
    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        var highRiskPool = MakeHighRiskPool();
        var mediumRiskPool = MakeMediumRiskPool();
        var lowRiskPool = MakeLowRiskPool();

        return
        [
            RollRelicOption(FirstAvailablePool(highRiskPool, mediumRiskPool, lowRiskPool)),
            RollRelicOption(FirstAvailablePool(mediumRiskPool, lowRiskPool)),
            RollRelicOption(FirstAvailablePool(lowRiskPool)),
        ];
    }

    /// <summary>
    /// 判断事件是否可在指定章节出现；舞台的长颈鹿仅属于隐藏 Stage 章节。
    /// </summary>
    /// <param name="act">正在生成地图的章节模型。</param>
    /// <returns>当章节为 Stage 时返回 <see langword="true"/>。</returns>
    public override bool IsValidForAct(ActModel act)
    {
        return act is STS2_Tomorin_Mod.Acts.Stage;
    }

    /// <summary>
    /// 判断当前队伍是否允许进入事件。
    /// </summary>
    /// <param name="runState">当前局的运行状态。</param>
    /// <returns>当队伍非空且至少有一名玩家为 Tomorin 时返回 <see langword="true"/>。</returns>
    public override bool IsAllowed(IRunState runState)
    {
        return runState.Players.Count > 0 && runState.Players.Any(player => player.Character is Tomorin);
    }

    /// <summary>
    /// 创建高风险、高收益档位的候选池。
    /// </summary>
    /// <returns>包含四种高风险舞台装置的候选池。</returns>
    private static WeightedList<AncientOption> MakeHighRiskPool()
    {
        return MakePool(
            AncientOption<BurningStageDevice>(),
            AncientOption<MassacreStageDevice>(),
            AncientOption<HuntingStageDevice>(),
            AncientOption<FinaleStageDevice>());
    }

    /// <summary>
    /// 创建低风险、中收益档位的候选池。
    /// </summary>
    /// <returns>包含三种中档舞台装置的候选池。</returns>
    private static WeightedList<AncientOption> MakeMediumRiskPool()
    {
        return MakePool(
            AncientOption<ReproductionStageDevice>(),
            AncientOption<DesireStageDevice>(),
            AncientOption<CompetitionStageDevice>());
    }

    /// <summary>
    /// 创建零风险、低收益档位的候选池。
    /// </summary>
    /// <returns>包含四种低风险舞台装置的候选池。</returns>
    private static WeightedList<AncientOption> MakeLowRiskPool()
    {
        return MakePool(
            AncientOption<FarewellStageDevice>(),
            AncientOption<PrideStageDevice>(),
            AncientOption<InterludeStageDevice>(),
            AncientOption<StarPickingStageDevice>());
    }

    /// <summary>
    /// 按档位顺序返回第一个至少含有一个当前有效候选的独立选项池。
    /// </summary>
    /// <param name="rankedPools">按风险和收益由高到低排列的候选池。</param>
    /// <returns>首个可用档位中过滤后的候选池。</returns>
    /// <exception cref="InvalidOperationException">所有给定档位均不存在有效候选时抛出。</exception>
    private WeightedList<AncientOption> FirstAvailablePool(
        params WeightedList<AncientOption>[] rankedPools)
    {
        foreach (var pool in rankedPools)
        {
            var availableOptions = pool
                .Where(option => option.ModelForOption.RelicCanSpawnAtCustomAncient(this))
                .ToArray();

            if (availableOptions.Length > 0)
            {
                return MakePool(availableOptions);
            }
        }

        throw new InvalidOperationException("舞台的长颈鹿没有可供当前展示位使用的舞台装置。");
    }

    /// <summary>
    /// 从给定候选池独立随机一个遗物，并创建无需二次确认的先古选项。
    /// </summary>
    /// <param name="pool">已经按当前出现条件过滤的候选池。</param>
    /// <returns>直接通过获得遗物完成结算的事件选项。</returns>
    private EventOption RollRelicOption(WeightedList<AncientOption> pool)
    {
        var option = pool.GetRandom(Rng);
        return RelicOption(option.ModelForOption);
    }
}
