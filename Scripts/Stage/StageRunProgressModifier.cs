using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace STS2_Tomorin_Mod.Stage;

/// <summary>
/// 保存本局舞台解锁进度与当前 FPO 首领战奖励资格的同步状态。
/// </summary>
public sealed class StageRunProgressModifier : ModifierModel
{
    private bool _hasDefeatedFullPowerOblivionis;
    private string? _eligibleBossEncounterId;
    private int _eligibleBossActIndex = -1;
    private int[] _eligibleBossMapCoord = [];
    private StageBossRewardState _bossRewardState;

    /// <summary>本局是否已经真实击败过 FullPowerOblivionis。</summary>
    [SavedProperty]
    public bool HasDefeatedFullPowerOblivionis
    {
        get => _hasDefeatedFullPowerOblivionis;
        set
        {
            AssertMutable();
            _hasDefeatedFullPowerOblivionis = value;
        }
    }

    /// <summary>当前或最近一次符合条件的首领战稳定 Encounter 标识。</summary>
    [SavedProperty]
    public string? EligibleBossEncounterId
    {
        get => _eligibleBossEncounterId;
        set
        {
            AssertMutable();
            _eligibleBossEncounterId = value;
        }
    }

    /// <summary>当前首领战资格绑定的章节索引；负值表示未绑定。</summary>
    [SavedProperty]
    public int EligibleBossActIndex
    {
        get => _eligibleBossActIndex;
        set
        {
            AssertMutable();
            _eligibleBossActIndex = value;
        }
    }

    /// <summary>当前首领战资格绑定的地图坐标；空数组表示未绑定。</summary>
    [SavedProperty]
    public int[] EligibleBossMapCoord
    {
        get => _eligibleBossMapCoord;
        set
        {
            AssertMutable();
            _eligibleBossMapCoord = value ?? [];
        }
    }

    /// <summary>当前首领战原版奖励资格的生命周期状态。</summary>
    [SavedProperty]
    public StageBossRewardState BossRewardState
    {
        get => _bossRewardState;
        set
        {
            AssertMutable();
            _bossRewardState = value;
        }
    }

    /// <summary>
    /// 将 FPO 真实死亡记录为本局进度；重复回调不会产生额外副作用。
    /// </summary>
    /// <returns>仅在首次从未解锁转为已解锁时返回 <see langword="true"/>。</returns>
    public bool MarkFullPowerOblivionisDefeated()
    {
        if (HasDefeatedFullPowerOblivionis)
        {
            return false;
        }

        HasDefeatedFullPowerOblivionis = true;
        return true;
    }

    /// <summary>
    /// 将当前首领战标记为可走原版首领奖励流程。
    /// </summary>
    /// <param name="encounterId">当前战斗的稳定 Encounter 标识。</param>
    /// <param name="actIndex">当前章节索引。</param>
    /// <param name="mapCoord">当前地图坐标。</param>
    public void MarkBossRewardEligible(ModelId encounterId, int actIndex, MapCoord? mapCoord)
    {
        if (BossRewardState == StageBossRewardState.Generated &&
            MatchesBossRewardBattle(encounterId, actIndex, mapCoord))
        {
            return;
        }

        EligibleBossEncounterId = encounterId.Entry;
        EligibleBossActIndex = actIndex;
        EligibleBossMapCoord = ToSavedMapCoord(mapCoord);
        BossRewardState = StageBossRewardState.Eligible;
    }

    /// <summary>
    /// 标记同一场符合条件的首领战已由原版流程生成奖励，阻止重入重复发奖。
    /// </summary>
    /// <param name="encounterId">当前战斗的稳定 Encounter 标识。</param>
    /// <param name="actIndex">当前章节索引。</param>
    /// <param name="mapCoord">当前地图坐标。</param>
    /// <returns>首次进入已生成状态时返回 <see langword="true"/>。</returns>
    public bool MarkBossRewardsGenerated(ModelId encounterId, int actIndex, MapCoord? mapCoord)
    {
        if (!MatchesBossRewardBattle(encounterId, actIndex, mapCoord) ||
            BossRewardState != StageBossRewardState.Eligible)
        {
            return false;
        }

        BossRewardState = StageBossRewardState.Generated;
        return true;
    }

    /// <summary>
    /// 清除不属于当前战斗的未消费首领奖励资格，防止资格泄漏到后续战斗。
    /// </summary>
    /// <param name="encounterId">即将校验的当前 Encounter 标识。</param>
    /// <param name="actIndex">当前章节索引。</param>
    /// <param name="mapCoord">当前地图坐标。</param>
    public void ClearStaleBossRewardEligibility(ModelId encounterId, int actIndex, MapCoord? mapCoord)
    {
        if (!MatchesBossRewardBattle(encounterId, actIndex, mapCoord) &&
            BossRewardState != StageBossRewardState.Generated)
        {
            EligibleBossEncounterId = null;
            EligibleBossActIndex = -1;
            EligibleBossMapCoord = [];
            BossRewardState = StageBossRewardState.None;
        }
    }

    /// <summary>
    /// 使用 Encounter、章节索引和地图坐标判断奖励状态是否属于同一场可跨存档恢复的战斗。
    /// </summary>
    /// <param name="encounterId">待比较的 Encounter 稳定标识。</param>
    /// <param name="actIndex">待比较的章节索引。</param>
    /// <param name="mapCoord">待比较的地图坐标。</param>
    /// <returns>全部身份片段一致时返回 <see langword="true"/>。</returns>
    public bool MatchesBossRewardBattle(ModelId encounterId, int actIndex, MapCoord? mapCoord)
    {
        return EligibleBossEncounterId == encounterId.Entry &&
               EligibleBossActIndex == actIndex &&
               EligibleBossMapCoord.SequenceEqual(ToSavedMapCoord(mapCoord));
    }

    /// <summary>
    /// 将可空地图坐标转换为 SavedProperty 支持的整数数组。
    /// </summary>
    /// <param name="mapCoord">当前地图坐标。</param>
    /// <returns>存在坐标时返回列与行；否则返回空数组。</returns>
    private static int[] ToSavedMapCoord(MapCoord? mapCoord)
    {
        return mapCoord is { } coord ? [coord.col, coord.row] : [];
    }

    /// <summary>
    /// 从 Run 的同步 Modifier 列表中取得舞台进度状态。
    /// </summary>
    /// <param name="runState">当前局状态。</param>
    /// <returns>找到的舞台进度状态；当前局未注册舞台时为 <see langword="null"/>。</returns>
    public static StageRunProgressModifier? Find(IRunState runState)
    {
        return runState.Modifiers.OfType<StageRunProgressModifier>().SingleOrDefault();
    }
}

/// <summary>
/// 表示当前首领战原版奖励资格的有限状态。
/// </summary>
public enum StageBossRewardState
{
    /// <summary>当前战斗没有 FPO 首领奖励资格。</summary>
    None,
    /// <summary>当前首领战已真实击败 FPO，允许原版奖励流程生成奖励。</summary>
    Eligible,
    /// <summary>原版奖励集合已生成，重复入口不得再次生成。</summary>
    Generated,
}
