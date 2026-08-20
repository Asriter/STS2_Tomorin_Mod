using MegaCrit.Sts2.Core.Entities.Relics;

namespace STS2_Tomorin_Mod.Relics;

/// <summary>
/// 为长颈鹿先古之民的全部舞台装置提供统一事件稀有度与暂用资源。
/// </summary>
public abstract class GiraffeStageDeviceRelic : BaseRelicModel
{
    /// <summary>
    /// 舞台装置均属于事件专属遗物。
    /// </summary>
    public override RelicRarity Rarity => RelicRarity.Event;
}
