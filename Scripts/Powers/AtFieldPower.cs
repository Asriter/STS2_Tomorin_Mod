using BaseLib.Abstracts;
using BaseLib.Extensions;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using STS2_Tomorin_Mod.Cards.Base;

namespace STS2_Tomorin_Mod.Powers;

/// <summary>
/// 心之壁
/// 你的回合结束时，你可以保留当前心之壁层数的格挡；被攻击时，反击造成伤害；受到伤害时，心之壁减少那个数值的层数。
/// </summary>
public class AtFieldPower : BasePowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override bool AllowNegative => false;

    public static bool ShouldDeduceAtFieldPower = true;

    protected override List<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<AtFieldPower>()
    ];

    /// <summary>
    /// 不重置格挡
    /// </summary>
    /// <param name="creature"></param>
    /// <returns></returns>
    public override bool ShouldClearBlock(Creature creature)
    {
        if (base.Owner != creature)
        {
            return true;
        }

        return false;
    }

    
    /// <summary>
    /// 回合开始时重置状态：受伤时是否减少层数
    /// </summary>
    /// <param name="choiceContext"></param>
    /// <param name="side"></param>
    /// <param name="combatState"></param>
    /// <returns></returns>
    public override Task BeforeSideTurnStart(PlayerChoiceContext choiceContext, CombatSide side,
        CombatState combatState)
    {
        if (side == base.Owner.Side)
        {
            ShouldDeduceAtFieldPower = true;
        }
        

        return Task.CompletedTask;
    }

    /// <summary>
    /// 保留buff层数的格挡
    /// </summary>
    /// <param name="preventer"></param>
    /// <param name="creature"></param>
    public override async Task AfterPreventingBlockClear(AbstractModel preventer, Creature creature)
    {
        if (this != preventer || creature != base.Owner)
        {
            return;
        }

        int block = creature.Block;
        if (block != 0)
        {
            if (block > Amount)
            {
                await CreatureCmd.LoseBlock(creature, block - Amount);
            }

            Flash();
        }
    }

    /// <summary>
    /// 当前心之壁层数减半
    /// </summary>
    /// <param name="side"></param>
    /// <param name="combatState"></param>
    /// <returns></returns>
    public override async Task AfterSideTurnStart(CombatSide side, CombatState combatState)
    {
        if (side == Owner.Side)
        {
            int amount = Amount / 2;
            await PowerCmd.ModifyAmount(this, amount - Amount, null, null);
        }
    }

    /// <summary>
    /// 被攻击前反弹伤害
    /// </summary>
    /// <param name="choiceContext"></param>
    /// <param name="target"></param>
    /// <param name="amount"></param>
    /// <param name="props"></param>
    /// <param name="dealer"></param>
    /// <param name="cardSource"></param>
    public override async Task BeforeDamageReceived(PlayerChoiceContext choiceContext, Creature target, decimal amount,
        ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target == base.Owner && dealer != null && (props.IsPoweredAttack_() || cardSource is Omnislice))
        {
            //是否伤害翻倍
            var damage = Amount * (Owner.HasPower<NameOfTearPower>() ? 2 : 1);
            Flash();
            await CreatureCmd.Damage(choiceContext, dealer, damage, ValueProp.Unpowered | ValueProp.SkipHurtAnim,
                base.Owner, null);
        }
    }


    /// <summary>
    /// 被攻击后层数减少
    /// </summary>
    /// <param name="choiceContext"></param>
    /// <param name="target"></param>
    /// <param name="result"></param>
    /// <param name="props"></param>
    /// <param name="dealer"></param>
    /// <param name="cardSource"></param>
    /// <returns></returns>
    public override async Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target,
        DamageResult result, ValueProp props,
        Creature? dealer, CardModel? cardSource)
    {
        if (target == Owner && result.UnblockedDamage > 0 && ShouldDeduceAtFieldPower)
        {
            await PowerCmd.ModifyAmount(this, -result.UnblockedDamage, null, null);
        }
    }

    public static IHoverTip GetATFieldHoverTips()
    {
        var text = $"{MainFile.ModId}-AT_FIELD_POWER";
        LocString title = new LocString("static_hover_tips", text + ".title");
        LocString desc = new LocString("static_hover_tips", text + ".description");

        return new HoverTip(title, desc);
    }
}