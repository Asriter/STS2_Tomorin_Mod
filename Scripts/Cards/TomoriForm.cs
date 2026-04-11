using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;
using STS2_Tomorin_Mod.CardPools;
using STS2_Tomorin_Mod.Cards.Base;
using STS2_Tomorin_Mod.Powers;

namespace STS2_Tomorin_Mod.Cards;

/// <summary>
/// 光武形态
/// 金卡 3费 能力 增加1点最大费用；若未升级则获得无形；应用TomoriFormPower
/// </summary>
[Pool(typeof(TomorinCardPool))]
public class TomoriForm : BaseCardModel
{
    public TomoriForm() : base(3, CardType.Power, CardRarity.Rare, TargetType.Self) { }

    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Ethereal];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<TomoriFormPower>(base.Owner.Creature, 1m, base.Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        // 升级后不再获得无形，效果通过 IsUpgraded 在 OnPlay 中处理
        RemoveKeyword(CardKeyword.Ethereal);
    }
}
