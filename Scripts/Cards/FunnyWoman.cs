using BaseLib.Utils;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using STS2_Tomorin_Mod.CardPools;
using STS2_Tomorin_Mod.Cards.Base;
using STS2_Tomorin_Mod.Localization.CustomEnums;

namespace STS2_Tomorin_Mod.Cards;

/// <summary>
/// 有趣的女人
/// 1费 白卡 技能 消耗一张手牌 从卡组中随机将一张卡打出 升级获得灵感
/// </summary>
[Pool(typeof(TomorinCardPool))]
public class FunnyWoman : BaseCardModel
{
    public FunnyWoman() :
        base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
    }

    public override bool IsInspiration => IsUpgraded;

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        //消耗一张手牌
        await ExhaustCard(choiceContext, 1);
        
        // 从抽牌堆随机打出一张牌
        await CardPileCmd.AutoPlayFromDrawPile(choiceContext, base.Owner, 1, CardPilePosition.Random, false);
    }

    protected override void OnUpgrade()
    {
        // 升级后获得灵感关键词
        AddKeyword(CustomKeyWord.Inspiration);
    }
}
