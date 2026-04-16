using System.Diagnostics;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2_Tomorin_Mod.CardPools;
using STS2_Tomorin_Mod.Cards.Base;

namespace STS2_Tomorin_Mod.Cards;

/// <summary>
/// 企鹅飞踢 PenguinKick
/// 蓝卡（稀有度：Uncommon）5费 攻击
/// 对一名敌人造成27->35点伤害
/// 每次作词费用减少一点
/// </summary>
[Pool(typeof(TomorinCardPool))]
public class PenguinKick() : BaseCardModel(5, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new List<DynamicVar>()
        {
            new DamageVar(27m, ValueProp.Move)
        };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
        await DamageCmd.Attack(base.DynamicVars.Damage.BaseValue).FromCard(this)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        base.DynamicVars.Damage.UpgradeValueBy(8m);
    }

    /// <summary>
    /// 每次作词后，费用减少1点，最低为0
    /// </summary>
    public override Task AfterCompose(PlayerChoiceContext choiceContext, Player player, CardModel source)
    {
        if (player != base.Owner)
            return Task.CompletedTask;
        base.EnergyCost.AddThisCombat(-1);
        return Task.CompletedTask;
    }
}