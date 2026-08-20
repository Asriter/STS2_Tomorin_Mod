using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Models;
using STS2_Tomorin_Mod.Localization.CustomEnums;

namespace STS2_Tomorin_Mod.Enchantments;

/// <summary>
/// 舞台装置附魔的公共基类。附魔由游戏原生牌组序列化负责保存和恢复。
/// </summary>
public abstract class StageDeviceEnchantment : CustomEnchantmentModel
{
    /// <summary>
    /// 舞台装置允许覆盖任意牌，包括状态、诅咒及不可打出的牌。
    /// </summary>
    public override bool CanEnchant(CardModel card) => true;

    /// <summary>
    /// 清除目标牌已有的附魔，然后应用指定的舞台装置附魔。
    /// </summary>
    public static T ApplyReplacingExisting<T>(CardModel card) where T : StageDeviceEnchantment
    {
        if (card.Enchantment != null)
        {
            CardCmd.ClearEnchantment(card);
        }

        return CardCmd.Enchant<T>(card, 1m)
               ?? throw new InvalidOperationException($"Failed to apply {typeof(T).Name} to {card.Id}.");
    }
}

/// <summary>
/// 皆杀的舞台装置附魔：永久赋予灵感与灵光乍现。
/// </summary>
public sealed class MassacreStageDeviceEnchantment : StageDeviceEnchantment
{
    protected override string CustomIconPath =>
        "res://STS2_Tomorin_Mod/images/relics/MassacreStageDevice.png";

    protected override void OnEnchant()
    {
        Card.AddKeyword(CustomKeyWord.Inspiration);
        Card.AddKeyword(CustomKeyWord.Epiphany);
    }
}

/// <summary>
/// 再生产的舞台装置附魔：永久赋予灵光乍现。
/// </summary>
public sealed class ReproductionStageDeviceEnchantment : StageDeviceEnchantment
{
    protected override string CustomIconPath =>
        "res://STS2_Tomorin_Mod/images/relics/ReproductionStageDevice.png";

    protected override void OnEnchant()
    {
        Card.AddKeyword(CustomKeyWord.Epiphany);
    }
}

/// <summary>
/// 竞演的舞台装置附魔：永久赋予灵光乍现、基础费用增加 1，并增加一次重放。
/// </summary>
public sealed class CompetitionStageDeviceEnchantment : StageDeviceEnchantment
{
    protected override string CustomIconPath =>
        "res://STS2_Tomorin_Mod/images/relics/CompetitionStageDevice.png";

    protected override void OnEnchant()
    {
        Card.AddKeyword(CustomKeyWord.Epiphany);
        Card.EnergyCost.UpgradeBy(1);
    }

    public override int EnchantPlayCount(int playCount) => playCount + 1;
}
