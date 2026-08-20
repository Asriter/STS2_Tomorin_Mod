using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.ValueProps;
using STS2_Tomorin_Mod.Cards.Base;
using STS2_Tomorin_Mod.Powers;

namespace STS2_Tomorin_Mod.Cards;

/// <summary>
/// 被夺走的闪耀事件诅咒牌。每次被消耗时依次侵蚀格挡、心之壁并造成普通伤害。
/// </summary>
[Pool(typeof(CurseCardPool))]
public class StolenShine() : BaseCardModel(-1, CardType.Curse, CardRarity.Curse, TargetType.None)
{
    /// <summary>
    /// 事件诅咒牌不可升级。
    /// </summary>
    public override int MaxUpgradeLevel => 0;

    /// <summary>
    /// 事件专属诅咒牌不会进入战斗内随机生成池。
    /// </summary>
    public override bool CanBeGeneratedInCombat => false;

    /// <summary>
    /// 获取被夺走的闪耀的固定关键词。
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Unplayable,
        CardKeyword.Ethereal,
    ];

    /// <summary>
    /// 获取诅咒消耗时的格挡损失、心之壁损失与伤害值。
    /// </summary>
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("BlockLoss", 10m),
        new PowerVar<AtFieldPower>(5m),
        new DamageVar(5m, ValueProp.Unpowered),
    ];

    /// <summary>
    /// 被夺走的闪耀不可由玩家主动打出。
    /// </summary>
    protected override bool IsPlayable => false;

    //TODO 暂用资源：正式卡图完成前复用拒绝卡图。
    /// <summary>
    /// 获取暂用的小卡图路径。
    /// </summary>
    public override string PortraitPath => "res://STS2_Tomorin_Mod/images/card_portraits/AnonReject.png";

    /// <summary>
    /// 获取暂用的大卡图路径。
    /// </summary>
    public override string CustomPortraitPath => "res://STS2_Tomorin_Mod/images/card_portraits/big/AnonReject.png";

    /// <summary>
    /// 获取暂用的测试卡图路径。
    /// </summary>
    public override string BetaPortraitPath => "res://STS2_Tomorin_Mod/images/card_portraits/AnonReject.png";

    /// <summary>
    /// 在这张诅咒被消耗时按固定顺序结算一次防御资源侵蚀与伤害。
    /// </summary>
    /// <param name="choiceContext">本次消耗使用的玩家选择上下文。</param>
    /// <param name="card">本次被消耗的卡牌。</param>
    /// <param name="causedByEthereal">是否由消失关键词触发消耗。</param>
    public override async Task AfterCardExhausted(PlayerChoiceContext choiceContext, CardModel card,
        bool causedByEthereal)
    {
        await base.AfterCardExhausted(choiceContext, card, causedByEthereal);
        if (card != this)
        {
            return;
        }

        int blockLoss = Math.Min(Owner.Creature.Block, DynamicVars["BlockLoss"].IntValue);
        if (blockLoss > 0)
        {
            await CreatureCmd.LoseBlock(choiceContext, Owner.Creature, blockLoss, null);
        }

        AtFieldPower? atFieldPower = Owner.Creature.GetPower<AtFieldPower>();
        if (atFieldPower != null)
        {
            int atFieldLoss = Math.Min(atFieldPower.Amount, DynamicVars[AtFieldPower.DefaultName].IntValue);
            if (atFieldLoss > 0)
            {
                await PowerCmd.ModifyAmount(choiceContext, atFieldPower, -atFieldLoss, Owner.Creature, this);
            }
        }

        await CreatureCmd.Damage(choiceContext, Owner.Creature, DynamicVars.Damage.BaseValue,
            ValueProp.Unpowered, null, this, null);
    }
}
