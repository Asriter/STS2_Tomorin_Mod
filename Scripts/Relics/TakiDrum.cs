using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using STS2_Tomorin_Mod.RelicPools;

namespace STS2_Tomorin_Mod.Relics;

/// <summary>
/// 立希的鼓
/// Rare 遗物：每回合打出的第五张牌会被额外打出一次。
/// 只计算玩家手动打出的牌（不含 auto-play）。
/// 通过 ModifyCardPlayCount 实现额外打出。
/// </summary>
[Pool(typeof(TomorinRelicPool))]
public class TakiDrum : BaseRelicModel
{
    public override RelicRarity Rarity => RelicRarity.Rare;

    private int _cardPlayCount = 0;

    /// <summary>
    /// 每回合开始时重置计数器
    /// </summary>
    public override Task AfterSideTurnStart(CombatSide side, CombatState combatState)
    {
        if (side == Owner.Creature.Side)
        {
            _cardPlayCount = 0;
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// 每打出一张牌时计数+1（仅计手动打出）
    /// </summary>
    public override Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner == Owner && !cardPlay.IsAutoPlay)
        {
            _cardPlayCount++;
            if (_cardPlayCount == 5)
            {
                Flash();
            }
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// 当第5张牌打出时，令其额外打出一次
    /// 注意：ModifyCardPlayCount 在打出前被调用，此时 _cardPlayCount 还未+1（在 AfterCardPlayed 后才+1）
    /// 因此这里判断 _cardPlayCount == 4（即将打出第5张）
    /// </summary>
    public override int ModifyCardPlayCount(CardModel card, Creature? target, int playCount)
    {
        if (card.Owner == Owner && _cardPlayCount == 4)
        {
            return playCount + 1;
        }
        return playCount;
    }
}
