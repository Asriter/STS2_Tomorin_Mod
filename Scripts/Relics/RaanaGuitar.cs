using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.RelicPools;
using STS2_Tomorin_Mod.Cards.Collections;

namespace STS2_Tomorin_Mod.Relics;

/// <summary>
/// 乐奈的吉他
/// 事件遗物：吃剩的巴菲（LeftoverBuffet）被消耗时，回复1点生命值。
/// </summary>
[Pool(typeof(EventRelicPool))]
public class RaanaGuitar : BaseRelicModel
{
    public override RelicRarity Rarity => RelicRarity.Event;

    protected override IEnumerable<DynamicVar> CanonicalVars => new List<DynamicVar>()
    {
        new IntVar("HealAmount", 1m),
    };

    /// <summary>
    /// 当卡牌被消耗时，若为吃剩的巴菲则回复1点生命值
    /// </summary>
    public override async Task AfterCardExhausted(PlayerChoiceContext choiceContext, CardModel card,
        bool causedByEthereal)
    {
        if (card is LeftoverBuffet && card.Owner == Owner)
        {
            Flash();
            await CreatureCmd.Heal(Owner.Creature, DynamicVars["HealAmount"].BaseValue);
        }
    }
}
