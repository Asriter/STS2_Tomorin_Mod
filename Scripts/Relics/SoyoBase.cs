using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using STS2_Tomorin_Mod.Powers;
using STS2_Tomorin_Mod.RelicPools;

namespace STS2_Tomorin_Mod.Relics;

/// <summary>
/// 素世的贝斯
/// Uncommon 遗物：每打出三张技能卡，获得1层心之壁（ATField）。
/// 整场战斗累计计数，可多次触发。
/// </summary>
[Pool(typeof(TomorinRelicPool))]
public class SoyoBase : BaseRelicModel
{
    public override RelicRarity Rarity => RelicRarity.Uncommon;

    protected override IEnumerable<DynamicVar> CanonicalVars => new List<DynamicVar>()
    {
        new PowerVar<AtFieldPower>(1m),
    };

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<AtFieldPower>()
    ];

    private int _skillCount = 0;

    /// <summary>
    /// 战斗开始时重置技能计数器
    /// </summary>
    public override Task AfterSideTurnStart(CombatSide side, CombatState combatState)
    {
        if (side == Owner.Creature.Side && combatState.RoundNumber == 1)
        {
            _skillCount = 0;
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// 每打出一张技能卡时计数+1，累计3张时获得1层心之壁
    /// </summary>
    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner == Owner && cardPlay.Card.Type == CardType.Skill && !cardPlay.IsAutoPlay)
        {
            _skillCount++;
            if (_skillCount >= 3)
            {
                _skillCount = 0;
                Flash();
                await PowerCmd.Apply<AtFieldPower>(Owner.Creature, DynamicVars["AtFieldPower"].BaseValue,
                    Owner.Creature, null);
            }
        }
    }
}
