using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;
using STS2_Tomorin_Mod.Encounters;
using STS2_Tomorin_Mod.Events;
using STS2_Tomorin_Mod.Stage;

namespace STS2_Tomorin_Mod.Acts;

/// <summary>
/// 表示位于荣耀章节之后、仅在满足解锁条件时才会进入的隐藏舞台章节。
/// </summary>
public sealed class Stage : CustomActModel
{
    /// <summary>
    /// 初始化不参与自然章节匹配的隐藏舞台章节，并将其注册为自定义内容。
    /// </summary>
    public Stage() : base(-1, true)
    {
    }

    /// <summary>复用 Glory 场景背景；Stage 资源 TODO：替换章节背景与环境视觉。</summary>
    protected override string CustomBackgroundScenePath => GloryAssets.BackgroundScenePath!;

    /// <summary>复用 Glory 地图顶部背景；Stage 资源 TODO：替换地图顶部背景。</summary>
    protected override string CustomMapTopBgPath => GloryAssets.MapTopBgPath!;

    /// <summary>复用 Glory 地图中部背景；Stage 资源 TODO：替换地图中部背景。</summary>
    protected override string CustomMapMidBgPath => GloryAssets.MapMidBgPath!;

    /// <summary>复用 Glory 地图底部背景；Stage 资源 TODO：替换地图底部背景。</summary>
    protected override string CustomMapBotBgPath => GloryAssets.MapBotBgPath!;

    /// <summary>复用 Glory 篝火背景；Stage 资源 TODO：替换篝火场景与角色休息展示。</summary>
    protected override string CustomRestSiteBackgroundPath => GloryAssets.RestSiteBackgroundPath!;

    /// <summary>复用 Glory 环境音乐；Stage 资源 TODO：替换环境音、背景音乐和音频切换。</summary>
    public override string[] BgMusicOptions => GloryAssets.BgMusicOptions;

    /// <summary>复用 Glory 音频银行；Stage 资源 TODO：替换舞台专属音频银行。</summary>
    public override string[] MusicBankPaths => GloryAssets.MusicBankPaths;

    /// <summary>复用 Glory 环境音；Stage 资源 TODO：替换环境音。</summary>
    public override string AmbientSfx => GloryAssets.AmbientSfx;

    /// <summary>复用 Glory 宝箱资源；Stage 资源 TODO：替换宝箱或公共房间资源。</summary>
    public override string ChestSpineResourcePath => GloryAssets.ChestSpineResourcePath;

    /// <summary>复用 Glory 宝箱普通皮肤；Stage 资源 TODO：替换宝箱皮肤。</summary>
    public override string ChestSpineSkinNameNormal => GloryAssets.ChestSpineSkinNameNormal;

    /// <summary>复用 Glory 宝箱描边皮肤；Stage 资源 TODO：替换宝箱描边皮肤。</summary>
    public override string ChestSpineSkinNameStroke => GloryAssets.ChestSpineSkinNameStroke;

    /// <summary>复用 Glory 宝箱开启音效；Stage 资源 TODO：替换宝箱与公共房间音效。</summary>
    public override string ChestOpenSfx => GloryAssets.ChestOpenSfx;

    /// <summary>
    /// 按唯一的 <see cref="StageRouteDefinition"/> 创建确定性的舞台地图。
    /// </summary>
    /// <param name="runState">正在进入舞台的本局状态。</param>
    /// <param name="replaceTreasureWithElites">引擎传入的替换标志；舞台无宝箱节点，因此不会改变路线。</param>
    /// <returns>只包含已确认单一路线的地图。</returns>
    protected override MegaCrit.Sts2.Core.Map.ActMap CustomCreateMap(RunState runState, bool replaceTreasureWithElites)
    {
        return new StageActMap();
    }

    /// <summary>
    /// 以影灯作为舞台默认第一 Boss，不让其他可选 Boss 参与初始发现顺序。
    /// </summary>
    public override IEnumerable<EncounterModel> BossDiscoveryOrder =>
        [ModelDb.Encounter<ShadowTomorinBoss>()];

    /// <summary>
    /// 返回舞台路线与 FateGuidance 会引用的全部合法 Encounter，供模型校验和存档恢复。
    /// </summary>
    /// <returns>固定乐队成员遭遇以及三个 FateGuidance Boss。</returns>
    public override IEnumerable<EncounterModel> GenerateAllEncounters()
    {
        return
        [
            ModelDb.Encounter<BandMemberEncounter>(),
            ModelDb.Encounter<ShadowTomorinBoss>(),
            ModelDb.Encounter<OblivionisBoss>(),
            ModelDb.Encounter<TakiBoss>(),
        ];
    }

    /// <summary>
    /// 舞台事件由固定路线解析器直接指定，不加入普通随机事件池。
    /// </summary>
    /// <returns>空事件池。</returns>
    public override IEnumerable<EventModel> AllEvents => [];

    /// <summary>
    /// 返回舞台固定长颈鹿先古之民，避免索引为 -1 时触发默认章节先古之民选择逻辑。
    /// </summary>
    /// <returns>仅包含 GiraffeAncient 的固定先古之民集合。</returns>
    public override IEnumerable<AncientEventModel> AllAncients => [ModelDb.AncientEvent<GiraffeAncient>()];

    /// <summary>
    /// 返回固定路线的房间数量，供章节级统计使用。
    /// </summary>
    protected override int BaseNumberOfRooms => StageRouteDefinition.Nodes.Count;

    /// <summary>
    /// 返回与固定路线一致的空随机计数；舞台地图由 <see cref="CustomCreateMap"/> 完整提供。
    /// </summary>
    /// <param name="rng">引擎传入的随机源，舞台不使用该随机源。</param>
    /// <returns>不请求额外随机房间的地图点计数。</returns>
    public override MapPointTypeCounts GetMapPointTypes(Rng rng)
    {
        return new MapPointTypeCounts(0, 0);
    }

    /// <summary>
    /// 集中代理 Glory 的临时资源，避免在多个章节属性中复制资源路径。
    /// </summary>
    private static class GloryAssets
    {
        /// <summary>获取当前模型库中的 Glory 章节。</summary>
        private static Glory Glory => ModelDb.Act<Glory>();

        /// <summary>获取复用的章节背景场景。</summary>
        internal static string? BackgroundScenePath => Glory.BackgroundScenePath;
        /// <summary>获取复用的地图顶部背景。</summary>
        internal static string MapTopBgPath => Glory.MapTopBgPath;
        /// <summary>获取复用的地图中部背景。</summary>
        internal static string MapMidBgPath => Glory.MapMidBgPath;
        /// <summary>获取复用的地图底部背景。</summary>
        internal static string MapBotBgPath => Glory.MapBotBgPath;
        /// <summary>获取复用的篝火背景。</summary>
        internal static string RestSiteBackgroundPath => Glory.RestSiteBackgroundPath;
        /// <summary>获取复用的背景音乐选项。</summary>
        internal static string[] BgMusicOptions => Glory.BgMusicOptions;
        /// <summary>获取复用的音频银行。</summary>
        internal static string[] MusicBankPaths => Glory.MusicBankPaths;
        /// <summary>获取复用的环境音。</summary>
        internal static string AmbientSfx => Glory.AmbientSfx;
        /// <summary>获取复用的宝箱骨骼资源。</summary>
        internal static string ChestSpineResourcePath => Glory.ChestSpineResourcePath;
        /// <summary>获取复用的宝箱普通皮肤。</summary>
        internal static string ChestSpineSkinNameNormal => Glory.ChestSpineSkinNameNormal;
        /// <summary>获取复用的宝箱描边皮肤。</summary>
        internal static string ChestSpineSkinNameStroke => Glory.ChestSpineSkinNameStroke;
        /// <summary>获取复用的宝箱开启音效。</summary>
        internal static string ChestOpenSfx => Glory.ChestOpenSfx;
    }
}
