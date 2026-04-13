using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using STS2_Tomorin_Mod.Localization.DynamicVars;

namespace STS2_Tomorin_Mod.Powers;

/// <summary>
/// 过堕幻 能力
/// </summary>

public class AdayumePower : BasePowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new BlockVar(1m, ValueProp.Unpowered),
        new PowerVar<AtFieldPower>(1m),
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.FromPower<AtFieldPower>()];

    public override async Task AfterCardPlayedLate(PlayerChoiceContext context, CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner.Creature == Owner)
        {
            Flash();
            await PowerCmd.Apply<AtFieldPower>(Owner, DynamicVars["AtFieldPower"].BaseValue, Owner, null);
            await CreatureCmd.GainBlock(base.Owner, base.DynamicVars.Block, null, true);
        }
    }
}