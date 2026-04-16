using BaseLib.Utils;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using STS2_Tomorin_Mod.CardPools;
using STS2_Tomorin_Mod.Cards.Base;

namespace STS2_Tomorin_Mod.Cards;

/// <summary>
/// 再生产
/// 蓝卡 0费 技能 消耗-强化移除 可打出条件：手牌中有状态牌；消耗1张状态牌，获得2→3费
/// </summary>
[Pool(typeof(TomorinCardPool))]
public class Reproduction : BaseCardModel
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new EnergyVar(2)];

    public override IEnumerable<CardKeyword> CanonicalKeywords
    {
        get
        {
            var list = base.CanonicalKeywords.ToList();
            list.Add(CardKeyword.Exhaust);
            return list;
        }
    }

    protected override bool IsPlayable =>
        base.Owner.PlayerCombatState?.Hand?.Cards.Any(c => c.Type == CardType.Status) ?? false;

    public Reproduction() : base(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var selected = await CardSelectCmd.FromHand(
            choiceContext,
            base.Owner,
            new CardSelectorPrefs(CardSelectorPrefs.ExhaustSelectionPrompt, 1),
            c => c.Type == CardType.Status,
            this);

        foreach (var card in selected)
        {
            await CardCmd.Exhaust(choiceContext, card);
        }

        await PlayerCmd.GainEnergy(base.DynamicVars.Energy.BaseValue, base.Owner);
    }

    protected override void OnUpgrade()
    {
        // base.DynamicVars.Energy.UpgradeValueBy(1m);
        RemoveKeyword(CardKeyword.Exhaust);
    }
}
