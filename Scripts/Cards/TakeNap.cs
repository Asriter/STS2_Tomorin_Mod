using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using STS2_Tomorin_Mod.CardPools;
using STS2_Tomorin_Mod.Cards.Base;
using STS2_Tomorin_Mod.Powers;

namespace STS2_Tomorin_Mod.Cards;

/// <summary>
/// 小睡一会
/// 白卡 技能 1费 获得4->5点格挡，下回合获得4->5点格挡
/// </summary>
[Pool(typeof(TomorinCardPool))]
public class TakeNap() : BaseCardModel(
    canonicalEnergyCost: 1,
    type: CardType.Skill,
    rarity: CardRarity.Common,
    targetType: TargetType.Self)
{
    public override bool GainsBlock => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new BlockVar(4m, ValueProp.Move), new BlockVar("BlockNextTurn", 4M, ValueProp.Move)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(base.Owner.Creature, base.DynamicVars.Block, cardPlay);
       
        var cardSource = this;
        await CreatureCmd.TriggerAnim(cardSource.Owner.Creature, "Cast", cardSource.Owner.Character.CastAnimDelay);
        BlockVar dynamicVar = (BlockVar) cardSource.DynamicVars["BlockNextTurn"];
        Decimal blockNextTurnAmount = Hook.ModifyBlock(cardSource.CombatState, cardSource.Owner.Creature, dynamicVar.BaseValue, dynamicVar.Props, (CardModel) cardSource, cardPlay, out IEnumerable<AbstractModel> _);
        BlockNextTurnPower blockNextTurnPower = await PowerCmd.Apply<BlockNextTurnPower>(choiceContext, cardSource.Owner.Creature, blockNextTurnAmount, cardSource.Owner.Creature, (CardModel) cardSource);
    }

    protected override void OnUpgrade()
    {
        base.DynamicVars.Block.UpgradeValueBy(2m);
        this.DynamicVars["BlockNextTurn"].UpgradeValueBy(1M);
    }
}
