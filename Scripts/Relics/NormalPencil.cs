using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using STS2_Tomorin_Mod.RelicPools;

namespace STS2_Tomorin_Mod.Relics;

[Pool(typeof(TomorinRelicPool))]
public class NormalPencil : BaseRelicModel
{
    public override RelicRarity Rarity => RelicRarity.Starter;

    protected override IEnumerable<DynamicVar> CanonicalVars => new List<DynamicVar>()
    {
        new CardsVar(2),
    };

    private bool _canActice = false;

    /// <summary>
    /// 战斗开始时初始化状态，效果一场战斗只能生效一次
    /// </summary>
    /// <param name="side"></param>
    /// <param name="combatState"></param>
    public override async Task AfterSideTurnStart(CombatSide side, CombatState combatState)
    {
        if (side == Owner.Creature.Side && combatState.RoundNumber == 1)
        {
            _canActice = true;
        }
    }

    public override async Task AfterCompose(PlayerChoiceContext choiceContext, Player player, CardModel source)
    {
        if (player == Owner && _canActice)
        {
            _canActice = false;
            Flash(); // 触发遗物图标闪烁
            await CardPileCmd.Draw(choiceContext, base.DynamicVars.Cards.BaseValue, base.Owner);
        }
    }

   
}