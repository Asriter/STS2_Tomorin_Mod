using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using STS2_Tomorin_Mod.CardPools;
using STS2_Tomorin_Mod.Cards.Base;
using STS2_Tomorin_Mod.Powers;

namespace STS2_Tomorin_Mod.Cards;

/// <summary>
/// 只想着自己呢
/// 1费 蓝色 技能 生命值减少2点 12->15防 3层心之壁  立即结束当前回合
/// </summary>
[Pool(typeof(TomorinCardPool))]
public class ThinkingYourself : BaseCardModel
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
		new HpLossVar(2m),
        new BlockVar(12m, ValueProp.Move),
        new PowerVar<AtFieldPower>(3m),
        new PowerVar<ThinkingYourselfPower>(5m)
    ];

    public ThinkingYourself() :
        base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        //扣血
        await CreatureCmd.Damage(choiceContext, base.Owner.Creature, base.DynamicVars.HpLoss.BaseValue, ValueProp.Unblockable | ValueProp.Unpowered | ValueProp.Move, this);

        
        // 获得格挡
        await CreatureCmd.GainBlock(base.Owner.Creature, base.DynamicVars.Block, cardPlay);

        // 获得心之壁
        await PowerCmd.Apply<AtFieldPower>(base.Owner.Creature, base.DynamicVars["AtFieldPower"].BaseValue, base.Owner.Creature, this);

        var value = base.DynamicVars["ThinkingYourselfPower"].BaseValue;
        // 降低力量
        // await PowerCmd.Apply<StrengthPower>(Owner.Creature, -value, Owner.Creature, null);
        // 降低敏捷
        // await PowerCmd.Apply<DexterityPower>(Owner.Creature, -value, Owner.Creature, null);
        
        // 获得临时debuff（回合结束前降低力量和敏捷）
        // await PowerCmd.Apply<ThinkingYourselfPower>(base.Owner.Creature, value, base.Owner.Creature, this);
        
        //立刻结束回合
        PlayerCmd.EndTurn(base.Owner, canBackOut: false);
    }

    protected override void OnUpgrade()
    {
        base.DynamicVars.Block.UpgradeValueBy(3m);
    }
}
