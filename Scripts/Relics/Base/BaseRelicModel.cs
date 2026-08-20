using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using STS2_Tomorin_Mod.Cards.Base;

namespace STS2_Tomorin_Mod.Relics;

/// <summary>
/// 为 Tomorin 遗物提供统一资源路径与作词回调入口。
/// </summary>
public abstract class BaseRelicModel : CustomRelicModel, CustomHookInterface
{
    protected override string BigIconPath =>  $"res://STS2_Tomorin_Mod/images/relics/big/{this.GetType().Name}.png";
    public override string PackedIconPath => $"res://STS2_Tomorin_Mod/images/relics/{this.GetType().Name}.png";
    protected override string PackedIconOutlinePath => $"res://STS2_Tomorin_Mod/images/relics/{this.GetType().Name}.png";
    
    /// <summary>
    /// 在作词完成后接收作词结果，默认不执行额外逻辑。
    /// </summary>
    public virtual Task AfterCompose(PlayerChoiceContext choiceContext, ComposeResult result)
    {
        return Task.CompletedTask;
    }
    
    
}
