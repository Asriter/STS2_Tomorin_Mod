using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace STS2_Tomorin_Mod.Cards.Base;

/// <summary>
/// 定义 Tomorin 模组内部的自定义战斗回调。
/// </summary>
public interface CustomHookInterface
{
    /// <summary>
    /// 在一次作词完整结算后触发，并提供该次作词实际影响的歌词牌。
    /// </summary>
    /// <param name="choiceContext">当前玩家选择上下文。</param>
    /// <param name="result">本次作词的完整结果。</param>
    public Task AfterCompose(PlayerChoiceContext choiceContext, ComposeResult result);
}
