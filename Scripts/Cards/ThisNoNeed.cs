using BaseLib.Utils;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.ValueProps;
using STS2_Tomorin_Mod.CardPools;
using STS2_Tomorin_Mod.Cards.Base;
using STS2_Tomorin_Mod.Cards.Collections;

namespace STS2_Tomorin_Mod.Cards;

/// <summary>
/// 这个，不需要了
/// 白卡 1费 技能 消耗手牌中一张状态牌，对一名敌人造成9->12点伤害，获得9->12点格挡，将一张冷掉的红茶加入牌组
/// </summary>
[Pool(typeof(TomorinCardPool))]
public class ThisNoNeed : BaseCardModel
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new List<DynamicVar>()
        {
            new DamageVar(5m, ValueProp.Move),
            new BlockVar(5m, ValueProp.Move),
        };

    public ThisNoNeed() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
    }

    // protected override bool IsPlayable =>
        // Owner.PlayerCombatState?.Hand?.Cards.Any(c => c.Type == CardType.Status) ?? false;

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

        await ExhaustCard(choiceContext, 1);

        await DamageCmd.Attack(base.DynamicVars.Damage.BaseValue)
            .FromCard(this)
            .Targeting(cardPlay.Target)
            .Execute(choiceContext);

        await CreatureCmd.GainBlock(base.Owner.Creature, base.DynamicVars.Block, cardPlay);

        //不生成token
        // var coldRedTea = base.CombatState!.CreateCard<ColdRedTea>(Owner);
        // await CardPileCmd.AddGeneratedCardToCombat(coldRedTea, PileType.Draw, addedByPlayer: true);
    }

    protected override void OnUpgrade()
    {
        base.DynamicVars.Damage.UpgradeValueBy(2m);
        base.DynamicVars.Block.UpgradeValueBy(2m);
    }
}
