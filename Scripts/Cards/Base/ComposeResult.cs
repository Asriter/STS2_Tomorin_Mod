using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace STS2_Tomorin_Mod.Cards.Base;

/// <summary>
/// 保存一次作词结算的来源、执行者与实际生成或强化的歌词牌。
/// </summary>
public sealed record ComposeResult(Player Player, CardModel Source, CardModel ResultCard, bool ReusedExistingCard);
