using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace STS2_Tomorin_Mod.Powers;

/// <summary>
/// Doloris被动：受到的伤害减少100%，每额外抽5张卡减少10%减伤，直到0%。
/// 通过 ModifyDamageAdditive 实现动态减伤比例。
/// 使用 AfterCardDrawn 追踪每位玩家每回合的抽卡总数。
/// </summary>
public class DolorisPassivePower : BasePowerModel
{
    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.FromPower<DolorisKillBuffPower>()];
    private class Data
    {
        /// <summary>
        /// 所有玩家本回合累计抽卡数
        /// </summary>
        public int DrawCount = 0;
    }

    private const int _drawCount = 3;
    private const string _drawCountName = "DrawCount";
    private const string _damageReduceName = "DamegeReduce";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar(_drawCountName, _drawCount),
        new IntVar(_damageReduceName, 100)
    ];


    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override int DisplayAmount => GetInternalData<Data>().DrawCount < 5
        ? _drawCount
        : _drawCount - ((GetInternalData<Data>().DrawCount - 5) % _drawCount);

    protected override object InitInternalData() => new Data();

    public override Task BeforeSideTurnStart(PlayerChoiceContext choiceContext, CombatSide side,
        IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (side == base.Owner.Side)
        {
            ResetDrawCount();
        }

        return Task.CompletedTask;
    }

    public override Task AfterCardDrawn(PlayerChoiceContext choiceContext, CardModel card, bool fromHandDraw)
    {
        if (card.Owner == null) return Task.CompletedTask;

        ModifyDrawCount(1);

        return Task.CompletedTask;
    }

    public override decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props,
        Creature? dealer, CardModel? cardSource)
    {
        if (target != base.Owner) return 0m;
        if (dealer?.Player == null) return 0m;

        var data = GetInternalData<Data>();
        var totalDraws = data.DrawCount;

        // 前5张为基础抽卡，超出部分每5张额外抽卡减少10%减伤
        int extraDraws = Math.Max(0, totalDraws - 5);
        int extraDrawGroups = extraDraws / _drawCount; // 整数除法，每5张一组
        decimal reductionFactor = Math.Max(0m, 1m - extraDrawGroups * 0.1m);

        // reductionFactor: 1.0 = 100%减伤（免疫）, 0.0 = 0%减伤（全额受伤）
        // 返回 -amount * reductionFactor 作为加值 → 最终伤害 = amount - amount*reductionFactor = amount*(1-reductionFactor)
        return -amount * reductionFactor;
    }

    private void ModifyDrawCount(int count)
    {
        var data = GetInternalData<Data>();
        data.DrawCount += count;
        if (data.DrawCount > 5)
        {
            int extraDraws = data.DrawCount - 5;
            int extraDrawGroups = extraDraws / _drawCount; // 整数除法
            decimal reductionFactor = Math.Max(0m, 100m - extraDrawGroups * 10m);
            DynamicVars[_damageReduceName].BaseValue = reductionFactor;
            Log.Warn("该更新减伤了！当前减伤倍率："+reductionFactor);
        }

        InvokeDisplayAmountChanged();
    }

    private void ResetDrawCount()
    {
        var data = GetInternalData<Data>();
        ModifyDrawCount(-data.DrawCount);
    }
}