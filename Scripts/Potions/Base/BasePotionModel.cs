using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using STS2_Tomorin_Mod.Cards.Base;

namespace STS2_Tomorin_Mod.Potions;

/// <summary>
/// 为 Tomorin 药水提供统一资源路径与作词回调入口。
/// </summary>
public abstract class BasePotionModel : CustomPotionModel, CustomHookInterface
{
    public override string? CustomPackedImagePath => $"res://STS2_Tomorin_Mod/images/potions/{this.GetType().Name}.png";
    public override string? CustomPackedOutlinePath => $"res://STS2_Tomorin_Mod/images/potions/{this.GetType().Name}.png";

    /// <summary>
    /// 在作词完成后接收作词结果，默认不执行额外逻辑。
    /// </summary>
    public virtual Task AfterCompose(PlayerChoiceContext choiceContext, ComposeResult result)
    {
        return Task.CompletedTask;
    }
}
