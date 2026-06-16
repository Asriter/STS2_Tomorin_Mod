using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Patches.Content;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.ValueProps;
using STS2_Tomorin_Mod.Cards.Base;

namespace STS2_Tomorin_Mod.Cards.EnemyCards;

/// <summary>
/// 重压 - 诅咒卡，2费，灵感，打出后体力流失2点
/// </summary>
[Pool(typeof(StatusCardPool))]
public class PressureCurse() : BaseCardModel(
    canonicalEnergyCost: 2,
    type: CardType.Status,
    rarity: CardRarity.Status,
    targetType: TargetType.Self)
{
    public override int MaxUpgradeLevel => 0;

    public override bool IsInspiration => true;

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        // 体力流失2点：对自己造成2点不可阻挡伤害
        await CreatureCmd.Damage(ctx, Owner.Creature, 2m, ValueProp.Unblockable, null, this);
    }
}
