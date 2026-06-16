using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace STS2_Tomorin_Mod.Powers;

/// <summary>
/// 隐藏Boss继承被动：每回合开始随机继承一个子Boss的被动机制
/// </summary>
public class OblivionisHiddenInheritPower : BasePowerModel
{
    protected override IEnumerable<IHoverTip> ExtraHoverTips => [
        HoverTipFactory.FromPower<DolorisPassivePower>(),
        HoverTipFactory.FromPower<MortisPassivePower>(),
        HoverTipFactory.FromPower<TimorisPassivePower>(),
        HoverTipFactory.FromPower<AmorisPassivePower>(),
    ];
    
    private enum InheritedPassive
    {
        Mortis,   // Attack x0.5, other x2
        Timoris,  // Hit cap per turn
        Amoris,   // Max 15 damage per hit
        Doloris   // Draw-based reduction
    }

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    private PowerModel? _curPower = null;

    public override async Task BeforeSideTurnStart(PlayerChoiceContext choiceContext, CombatSide side,
        IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (side == base.Owner.Side) return;

        await SetNewPower();
    }

    public async Task SetNewPower()
    {
        var rng = Owner.Monster.Rng;
        int buffId = rng.NextInt(0, 3);
        
        //移除所有老buff
        if (_curPower != null)
            await PowerCmd.Remove(_curPower);

        var context = new ThrowingPlayerChoiceContext();
        //根据buffId上buff
        switch (buffId)
        {
            case (int)InheritedPassive.Amoris:
                _curPower = await PowerCmd.Apply<AmorisPassivePower>(context, Owner, 1, Owner, null);
                break;
            case (int)InheritedPassive.Timoris:
                _curPower = await PowerCmd.Apply<TimorisPassivePower>(context, Owner, 1, Owner, null);
                break;
            case (int)InheritedPassive.Doloris:
                _curPower = await PowerCmd.Apply<DolorisPassivePower>(context, Owner, 1, Owner, null);
                break;
            case (int)InheritedPassive.Mortis:
                _curPower = await PowerCmd.Apply<MortisPassivePower>(context, Owner, 1, Owner, null);
                break;
            default:
                Log.Error("未找到对应Buff！当前随机出来的buffId：" + buffId);
            break;
        }
    }
}
