using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using STS2_Tomorin_Mod.CardPools;
using STS2_Tomorin_Mod.Cards.Base;
using STS2_Tomorin_Mod.Powers;

namespace STS2_Tomorin_Mod.Cards;

/// <summary>
/// 结束的Crychic
/// 白卡 2费 技能 舍弃所有手牌，每舍弃一张手牌，获得4->5层格挡，获得1层心之壁
/// </summary>
[Pool(typeof(TomorinCardPool))]
public class CrychicIsOver : BaseCardModel
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(4m, ValueProp.Move),
        new PowerVar<AtFieldPower>(1m)
    ];

    public CrychicIsOver() :
        base(2, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 获取手牌列表（排除自己）
        var handCards = base.Owner.PlayerCombatState.Hand.Cards
            .Where(c => c != this)
            .ToList();

        int discardedCount = handCards.Count;


        // 舍弃所有手牌
        await CardCmd.Discard(choiceContext, handCards);

        // 每舍弃一张获得格挡
        if (discardedCount > 0)
        {
            decimal totalBlock = base.DynamicVars.Block.BaseValue * discardedCount;
            await CreatureCmd.GainBlock(base.Owner.Creature, totalBlock, ValueProp.Move, cardPlay);

            // 获得心之壁
            await PowerCmd.Apply<AtFieldPower>(base.Owner.Creature, base.DynamicVars["AtFieldPower"].BaseValue * discardedCount,
                base.Owner.Creature, this);
        }
    }

    protected override void OnUpgrade()
    {
        base.DynamicVars.Block.UpgradeValueBy(1m);
    }
}