using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using STS2_Tomorin_Mod.Cards.Base;

namespace STS2_Tomorin_Mod.Powers;

/// <summary>
/// 为 Tomorin 能力提供统一资源路径与作词回调入口。
/// </summary>
public abstract class BasePowerModel : CustomPowerModel, CustomHookInterface
{
    public override string CustomPackedIconPath => $"res://STS2_Tomorin_Mod/images/powers/{this.GetType().Name}.png";
    public override string? CustomBigIconPath => $"res://STS2_Tomorin_Mod/images/powers/big/{this.GetType().Name}.png";

    /// <summary>
    /// 在作词完成后接收作词结果，默认不执行额外逻辑。
    /// </summary>
    public virtual Task AfterCompose(PlayerChoiceContext choiceContext, ComposeResult result)
    {
        return Task.CompletedTask;
    }
    
    
}
