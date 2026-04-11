using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using STS2_Tomorin_Mod.CardPools;
using STS2_Tomorin_Mod.RelicPools;

namespace STS2_Tomorin_Mod.Relics;

/// <summary>
/// 爱音的吉他
/// Common 遗物：每场战斗开始时，将一张随机收集品加入手牌。
/// </summary>
[Pool(typeof(TomorinRelicPool))]
public class AnonGuitar : BaseRelicModel
{
    public override RelicRarity Rarity => RelicRarity.Common;

    /// <summary>
    /// 战斗第一回合开始时，随机将一张收集品加入手牌
    /// </summary>
    public override async Task AfterSideTurnStart(CombatSide side, CombatState combatState)
    {
        if (side == Owner.Creature.Side && combatState.RoundNumber == 1)
        {
            var collectionsCards = ModelDb.CardPool<CollectionsCardPool>().AllCards.ToList();
            if (collectionsCards.Count > 0)
            {
                var randomCard = collectionsCards.TakeRandom(1, Owner.RunState.Rng.CombatCardGeneration).First();
                var newCard = combatState.CreateCard(randomCard, Owner);
                Flash();
                await CardPileCmd.AddGeneratedCardToCombat(newCard, PileType.Hand, addedByPlayer: true);
            }
        }
    }
}
