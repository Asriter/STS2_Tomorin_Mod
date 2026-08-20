using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using STS2_Tomorin_Mod.CardPools;
using STS2_Tomorin_Mod.Cards.Base;
using STS2_Tomorin_Mod.Commands;
using STS2_Tomorin_Mod.Localization.DynamicVars;

namespace STS2_Tomorin_Mod.Cards;

[Pool(typeof(TomorinCardPool))]
public class UtakotobaToken : BaseCardModel
{
    public UtakotobaToken() : base(3, CardType.Attack, CardRarity.Token, TargetType.AllEnemies)
    {
    }

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new List<DynamicVar>()
        {
            new DamageVar(5m, ValueProp.Move),
            new IntVar("ComposeNum", 0m)
        };

    public override IEnumerable<CardKeyword> CanonicalKeywords
    {
        get
        {
            var list = base.CanonicalKeywords.ToList();
            list.Add(CardKeyword.Retain);
            return list;
        }
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (!ComposeCmd.ComposeCostCardDict.TryGetValue(Owner, out var value))
            return;
        
        int count = value + (IsUpgraded ? 5 : 0);
        if (count == 0)
            return;

        await DamageCmd.Attack(base.DynamicVars.Damage.BaseValue).WithHitCount(count).FromCard(this, cardPlay)
            .TargetingAllOpponents(CombatState)
            .WithHitFx("vfx/vfx_giant_horizontal_slash", null, "slash_attack.mp3")
            .Execute(choiceContext);

        //抽卡
        await CardPileCmd.Draw(choiceContext, ComposeCmd.ComposeCostCardDict[Owner], base.Owner);
    }

    protected override void OnUpgrade()
    {
        // base.DynamicVars.Damage.UpgradeValueBy(3m);
    }

    /// <summary>
    /// 在作词后刷新累计作词消耗数量的显示。
    /// </summary>
    public override async Task AfterCompose(PlayerChoiceContext choiceContext, ComposeResult result)
    {
        await base.AfterCompose(choiceContext, result);
        //更新融合次数，用于表现
        UpdateComposeNum();
    }

    public override Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
    {
        //更新融合次数，用于表现
        UpdateComposeNum();
        
        //如果被打出的是自己
        if (cardPlay.Card == this)
        {
            //减费
            base.EnergyCost.AddThisCombat(-1);
        }
        return Task.CompletedTask;
    }

    private void UpdateComposeNum()
    {
        base.DynamicVars["ComposeNum"].BaseValue = ComposeCmd.ComposeCostCardDict.ContainsKey(Owner) ? ComposeCmd.ComposeCostCardDict[Owner] : 0;
    }
}
