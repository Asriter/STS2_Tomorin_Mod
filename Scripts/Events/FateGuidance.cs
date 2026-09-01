using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using STS2_Tomorin_Mod.Encounters;
using STS2_Tomorin_Mod.Services;

namespace STS2_Tomorin_Mod.Events;

/// <summary>
/// 表示 Stage 固定路线中的共享命运选择事件；当前版本仅开放 Crychic 选项。
/// </summary>
public sealed class FateGuidance : CustomEventModel
{
    private const string InitialPage = "INITIAL";
    private const string NormalPage = "NORMAL";
    private const string OblivionisPage = "OBLIVIONIS";
    private const string TakiPage = "TAKI";

    /// <summary>复用已确认的长颈鹿事件图片作为当前占位肖像。</summary>
    public override string CustomInitialPortraitPath =>
        "res://STS2_Tomorin_Mod/images/events/Giraffe.png";

    /// <summary>由游戏本体 EventSynchronizer 收集投票并同步最终选项。</summary>
    public override bool IsShared => true;

    /// <summary>
    /// 判断事件是否允许用于当前运行状态；固定事件只属于自定义 Stage 章节。
    /// </summary>
    /// <param name="runState">准备进入事件的运行状态。</param>
    /// <returns>当前章节为 Stage 时返回真。</returns>
    public override bool IsAllowed(IRunState runState) => runState.Act is STS2_Tomorin_Mod.Acts.Stage;

    /// <summary>
    /// 按稳定业务顺序创建一个可选项和两个游戏原生锁定选项。
    /// </summary>
    /// <returns>Crychic、Oblivionis、Taki 的有序事件选项。</returns>
    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
    [
        CreateOption(ChooseCrychic, InitialPage, nameof(ChooseCrychic)),
        LockedOption(nameof(ChooseOblivionis), InitialPage),
        LockedOption(nameof(ChooseTaki), InitialPage),
    ];

    /// <summary>
    /// 创建使用显式本地化键的普通事件选项，确保未来解锁分支与锁定分支共用同一键空间。
    /// </summary>
    /// <param name="onChosen">选项被原生事件系统裁决后执行的处理器。</param>
    /// <param name="pageKey">选项所属页面。</param>
    /// <param name="optionKey">选项本地化名称。</param>
    /// <returns>可被玩家选择的原生事件选项。</returns>
    private EventOption CreateOption(Func<Task> onChosen, string pageKey, string optionKey)
    {
        return new EventOption(this, onChosen, $"{Id.Entry}.pages.{pageKey}.options.{optionKey}");
    }

    /// <summary>
    /// 将 Crychic 设为第一 Boss，并进入 Crychic 独立结算页。
    /// </summary>
    /// <returns>同步状态写入已经完成的任务。</returns>
    private Task ChooseCrychic()
    {
        BossMapRouteService.ChangePrimaryBoss(
            RequireOwnerRunState(),
            ModelDb.Encounter<ShadowTomorinBoss>());
        SetEventFinished(PageDescription(NormalPage));
        return Task.CompletedTask;
    }

    /// <summary>
    /// 将 Oblivionis 设为第一 Boss，并进入 Oblivionis 独立结算页；当前版本选项保持锁定。
    /// </summary>
    /// <returns>同步状态写入已经完成的任务。</returns>
    private Task ChooseOblivionis()
    {
        BossMapRouteService.ChangePrimaryBoss(
            RequireOwnerRunState(),
            ModelDb.Encounter<OblivionisBoss>());
        SetEventFinished(PageDescription(OblivionisPage));
        return Task.CompletedTask;
    }

    /// <summary>
    /// 将 Taki 设为第一 Boss，并进入 Taki 独立结算页；当前版本选项保持锁定。
    /// </summary>
    /// <returns>同步状态写入已经完成的任务。</returns>
    private Task ChooseTaki()
    {
        BossMapRouteService.ChangePrimaryBoss(
            RequireOwnerRunState(),
            ModelDb.Encounter<TakiBoss>());
        SetEventFinished(PageDescription(TakiPage));
        return Task.CompletedTask;
    }

    /// <summary>
    /// 获取事件拥有者的运行状态；运行时事件缺少拥有者属于状态损坏并快速失败。
    /// </summary>
    /// <returns>事件拥有者所属的权威运行状态。</returns>
    private IRunState RequireOwnerRunState()
    {
        return Owner?.RunState ?? throw new InvalidOperationException("FateGuidance 事件缺少 Owner。");
    }
}
