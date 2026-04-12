using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using STS2_Tomorin_Mod.CardPools;
using STS2_Tomorin_Mod.Cards.Base;

namespace STS2_Tomorin_Mod.Cards;

/// <summary>
/// 让我们一起迷失吧
/// 联机卡 3费 金色 技能 我方全体获得一层无实体 消耗 强化后获得保留
/// </summary>
[Pool(typeof(TomorinCardPool))]
public class MygoTogether : BaseCardModel
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new PowerVar<IntangiblePower>(1m)];

    public MygoTogether() :
        base(3, CardType.Skill, CardRarity.Rare, TargetType.AllAllies)
    {
    }

    public override CardMultiplayerConstraint MultiplayerConstraint => CardMultiplayerConstraint.MultiplayerOnly;

    public override IEnumerable<CardKeyword> CanonicalKeywords
    {
        get
        {
            var list = base.CanonicalKeywords.ToList();
            list.Add(CardKeyword.Exhaust);
            return list;
        }
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 所有队友获得无实体
        var allPlayers = base.CombatState!.Players;
        foreach (var player in allPlayers)
        {
            await PowerCmd.Apply<IntangiblePower>(player.Creature, base.DynamicVars["IntangiblePower"].BaseValue, base.Owner.Creature, this);
        }
    }

    protected override void OnUpgrade()
    {
        // 升级后获得保留关键词
        AddKeyword(CardKeyword.Retain);
    }
}
