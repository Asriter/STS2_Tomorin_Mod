using BaseLib.Extensions;
using BaseLib.Patches.Content;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.CardPools;
using STS2_Tomorin_Mod.Cards.Base;
using STS2_Tomorin_Mod.Localization.CustomEnums;
using STS2_Tomorin_Mod.Powers;

namespace STS2_Tomorin_Mod.Cards;

/// <summary>
/// 白祥 一阶段给的状态牌
/// </summary>

[Pool(typeof(TokenCardPool))]
public class CrychicPhantomState() : BaseCardModel(1, CardType.Status, CardRarity.Status, TargetType.Self)
{
    public override int MaxUpgradeLevel => 0;

    public override bool IsInspiration => true;

    public override IEnumerable<CardKeyword> CanonicalKeywords
    {
        get
        {
            var list = base.CanonicalKeywords.ToList();
            list.Add(CustomKeyWord.Epiphany);
            list.Add(CardKeyword.Retain);
            return list;
        }
    }

    protected override IEnumerable<IHoverTip> ExtraHoverTips {
        get
        {
            var list = base.ExtraHoverTips.ToList();
            list.Add(HoverTipFactory.FromPower<CrychicRememberPower>());
            return list;
        }
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (Owner.HasPower<CrychicRememberPower>())
        {
            var power = Owner.Creature.GetPower<CrychicRememberPower>();
            await PowerCmd.ModifyAmount(choiceContext, power, 1, Owner.Creature, this);
        }
    }
}