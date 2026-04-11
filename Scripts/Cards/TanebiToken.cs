using BaseLib.Utils;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using STS2_Tomorin_Mod.CardPools;
using STS2_Tomorin_Mod.Cards.Base;
using STS2_Tomorin_Mod.Commands;
using STS2_Tomorin_Mod.Localization.DynamicVars;

namespace STS2_Tomorin_Mod.Cards;

/// <summary>
/// 歌声化作灯火
/// 0费 金卡 技能卡 选择1->2张被消耗的卡加入手牌
/// </summary>
[Pool(typeof(TomorinCardPool))]
public class TanebiToken : BaseCardModel
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DisExhaustVar(1m)];

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [CardKeyword.Exhaust];

    public TanebiToken() :
        base(0, CardType.Skill, CardRarity.Token, TargetType.Self)
    {
    }


    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await DisExhaustCmd.DisExhaust(choiceContext, Owner, DynamicVars[DisExhaustVar.DefauleName].IntValue, this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars[DisExhaustVar.DefauleName].UpgradeValueBy(1m);
    }
}