using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using STS2_Tomorin_Mod.Cards;
using STS2_Tomorin_Mod.Enemy;

namespace STS2_Tomorin_Mod.Powers;

/// <summary>
/// 逃跑计数状态
/// 计数归零立刻死亡
/// </summary>
public class AnonEscapeCountPower : BasePowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override bool ShouldScaleInMultiplayer => false;

    /// <summary>
    /// 打出卡牌后，若果是对应的状态牌，则减少buff层数
    /// </summary>
    /// <param name="context"></param>
    /// <param name="cardPlay"></param>
    /// <returns></returns>
    public override async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
    {
        if (cardPlay.Card is AnonPlayGuitar || cardPlay.Card is AnonLiveTogether || cardPlay.Card is AnonNeedYou)
        {
                await PowerCmd.ModifyAmount(this, -1, cardPlay.Card.Owner.Creature, cardPlay.Card);
        }
    }

    /// <summary>
    /// 卡牌被消耗掉也有效；特定“一起演出”，
    /// </summary>
    /// <param name="choiceContext"></param>
    /// <param name="card"></param>
    /// <param name="causedByEthereal"></param>
    /// <returns></returns>
    public override async Task AfterCardExhausted(PlayerChoiceContext choiceContext, CardModel card, bool causedByEthereal)
    {
        if (card is AnonLiveTogether)
        {
            await PowerCmd.ModifyAmount(this, -1, card.Owner.Creature, card);
        }
    }

    public override async Task AfterRemoved(Creature oldOwner)
    {
        await ((Anon)Owner.Monster).TriggerAnonDie();
    }
}