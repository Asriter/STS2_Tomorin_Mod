using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace STS2_Tomorin_Mod.Stage;

/// <summary>
/// 保存本局舞台解锁进度的同步状态。
/// </summary>
public sealed class StageRunProgressModifier : ModifierModel
{
    private bool _hasDefeatedFullPowerOblivionis;

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
    /// 从 Run 的同步 Modifier 列表中取得舞台进度状态。
    /// </summary>
    /// <param name="runState">当前局状态。</param>
    /// <returns>找到的舞台进度状态；当前局未注册舞台时为 <see langword="null"/>。</returns>
    public static StageRunProgressModifier? Find(IRunState runState)
    {
        return runState.Modifiers.OfType<StageRunProgressModifier>().SingleOrDefault();
    }
}
