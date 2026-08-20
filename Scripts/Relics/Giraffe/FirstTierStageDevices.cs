using BaseLib.Utils;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Gold;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;
using STS2_Tomorin_Mod.CardPools;
using STS2_Tomorin_Mod.Cards;
using STS2_Tomorin_Mod.Cards.Base;
using STS2_Tomorin_Mod.Cards.Collections;
using STS2_Tomorin_Mod.Enchantments;
using STS2_Tomorin_Mod.Localization.CustomEnums;
using STS2_Tomorin_Mod.Powers;

namespace STS2_Tomorin_Mod.Relics;

/// <summary>
/// 燃烧的舞台装置：以最大生命为契约，强化每个玩家回合首次作词及其歌词牌。
/// </summary>
[Pool(typeof(EventRelicPool))]
public sealed class BurningStageDevice : GiraffeStageDeviceRelic
{
    private bool _hasComposedThisTurn;

    /// <summary>
    /// 取得遗物时将最大生命值向上取整减半，并限制当前生命值。
    /// </summary>
    public override async Task AfterObtained()
    {
        int nextMaxHp = (Owner.Creature.MaxHp + 1) / 2;
        await CreatureCmd.SetMaxHp(Owner.Creature, nextMaxHp);
        if (Owner.Creature.CurrentHp > nextMaxHp)
        {
            await CreatureCmd.SetCurrentHp(Owner.Creature, nextMaxHp);
        }
    }

    /// <summary>
    /// 每个玩家回合开始时重置首次作词与歌词牌追踪。
    /// </summary>
    public override Task AfterPlayerTurnStartEarly(PlayerChoiceContext choiceContext, Player player)
    {
        if (player == Owner)
        {
            _hasComposedThisTurn = false;
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 强化首次作词；若歌词牌复用了已有牌，则再追加一次重放。
    /// </summary>
    public override async Task AfterCompose(PlayerChoiceContext choiceContext, ComposeResult result)
    {
        if (result.Player != Owner)
        {
            return;
        }

        result.ResultCard.BaseReplayCount++;
        // if (result.ReusedExistingCard)
        // {
        //     result.ResultCard.BaseReplayCount++;
        // }

        Flash();

        if (_hasComposedThisTurn)
        {
            return;
        }

        _hasComposedThisTurn = true;
        await PlayerCmd.GainEnergy(1m, Owner);
        await CardPileCmd.Draw(choiceContext, 1m, Owner);
    }
}

/// <summary>
/// 皆杀的舞台装置：清空金币、切断战斗牌与金币奖励，并赋予现有牌组再演关键词。
/// </summary>
[Pool(typeof(EventRelicPool))]
public sealed class MassacreStageDevice : GiraffeStageDeviceRelic
{
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new[]
        { HoverTipFactory.FromKeyword(CustomKeyWord.Epiphany), HoverTipFactory.FromKeyword(CustomKeyWord.Inspiration) };

    /// <summary>
    /// 取得遗物时失去全部金币，并为当前永久牌组赋予灵感与灵光乍现。
    /// </summary>
    public override async Task AfterObtained()
    {
        if (Owner.Gold > 0)
        {
            await PlayerCmd.LoseGold(Owner.Gold, Owner, GoldLossType.Spent);
        }

        foreach (CardModel card in Owner.Deck.Cards)
        {
            StageDeviceEnchantment.ApplyReplacingExisting<MassacreStageDeviceEnchantment>(card);
        }
    }

    /// <summary>
    /// 从战斗奖励中移除金币和卡牌奖励，同时保留商店及其他奖励来源。
    /// </summary>
    public override bool TryModifyRewards(Player player, List<Reward> rewards, AbstractRoom? room)
    {
        if (player != Owner || room is not CombatRoom)
        {
            return false;
        }

        int removed = rewards.RemoveAll(reward => reward is GoldReward or CardReward);
        return removed > 0;
    }
}

/// <summary>
/// 狩猎的舞台装置：消耗初始抽牌堆中的牌，并奖励每回合早期的消耗行为。
/// </summary>
[Pool(typeof(EventRelicPool))]
public sealed class HuntingStageDevice : GiraffeStageDeviceRelic
{
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new[]
    {
        HoverTipFactory.FromPower<AtFieldPower>(),
        HoverTipFactory.FromKeyword(CustomKeyWord.Inspiration)
    };
    private const int OpeningExhaustCount = 2;
    private const int ExhaustTriggersPerTurn = 3;
    private int _exhaustTriggersThisTurn;

    /// <summary>
    /// 战斗开始前重置初始消耗与每回合计数。
    /// </summary>
    public override Task BeforeCombatStart()
    {
        _exhaustTriggersThisTurn = 0;
        return Task.CompletedTask;
    }

    /// <summary>
    /// 每个玩家回合重置消耗奖励，并在首回合抽牌后优先消耗收藏品。
    /// </summary>
    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner)
        {
            return;
        }

        _exhaustTriggersThisTurn = 0;
        if (Owner.PlayerCombatState == null)
        {
            return;
        }

        List<CardModel> remaining = Owner.PlayerCombatState.DrawPile.Cards.ToList();
        List<CardModel> selected = [];
        for (int index = 0; index < OpeningExhaustCount && remaining.Count > 0; index++)
        {
            List<CardModel> collections = remaining
                .Where(card => card.Pool == ModelDb.CardPool<CollectionsCardPool>())
                .ToList();
            CardModel picked = Owner.RunState.Rng.CombatCardSelection.NextItem(
                collections.Count > 0 ? collections : remaining);
            selected.Add(picked);
            remaining.Remove(picked);
        }

        foreach (CardModel card in selected)
        {
            card.AddKeyword(CustomKeyWord.SingleTurnInspiration);
            await CardCmd.Exhaust(choiceContext, card);
        }
    }

    /// <summary>
    /// 每个玩家回合的前几次消耗卡牌时获得心之壁并抽牌。
    /// </summary>
    public override async Task AfterCardExhausted(PlayerChoiceContext choiceContext, CardModel card,
        bool causedByEthereal)
    {
        if (card.Owner != Owner || _exhaustTriggersThisTurn >= ExhaustTriggersPerTurn)
        {
            return;
        }

        _exhaustTriggersThisTurn++;
        Flash();
        await PowerCmd.Apply<AtFieldPower>(choiceContext, Owner.Creature, 3m, Owner.Creature, null);
        await CardPileCmd.Draw(choiceContext, 1m, Owner);
    }
}

/// <summary>
/// 终幕的舞台装置：提供高额开场资源与每回合首牌免费效果，并设置正常回合时限。
/// </summary>
[Pool(typeof(EventRelicPool))]
public sealed class FinaleStageDevice : GiraffeStageDeviceRelic
{
    private const int NormalTurnLimit = 6;
    private const decimal FinaleDamage = 999m;
    private bool _openingGranted;
    private bool _freeCardConsumed;
    private int _remainingNormalTurns;
    private int _lastCountedRound;

    public override bool ShowCounter => true;
    public override int DisplayAmount => _remainingNormalTurns;

    protected override IEnumerable<IHoverTip> ExtraHoverTips => new[]
    {
        HoverTipFactory.FromCard<StarStone>()
    };

    /// <summary>
    /// 战斗开始前初始化资源发放、免费牌资格与正常回合倒计时。
    /// </summary>
    public override Task BeforeCombatStart()
    {
        _openingGranted = false;
        _freeCardConsumed = false;
        _remainingNormalTurns = NormalTurnLimit;
        _lastCountedRound = 0;
        InvokeDisplayAmountChanged();
        return Task.CompletedTask;
    }

    /// <summary>
    /// 每个玩家回合刷新首张非 X 费牌的免费资格，并在首回合发放开场资源。
    /// </summary>
    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (side != Owner.Creature.Side)
        {
            return;
        }

        _freeCardConsumed = false;
        if (_openingGranted || Owner.PlayerCombatState == null)
        {
            return;
        }

        _openingGranted = true;
        await PlayerCmd.GainEnergy(2m, Owner);
        await CardPileCmd.Draw(new ThrowingPlayerChoiceContext(), 2m, Owner);
        // CardModel starStone = ModelDb.Card<StarStone>().CreateCloneForPlayer(Owner);
        CardModel starStone = combatState.CreateCard<StarStone>(base.Owner);
        await CardPileCmd.AddGeneratedCardToCombat(starStone, PileType.Hand, Owner);
    }

    /// <summary>
    /// 在免费资格未消耗时将所有非 X 费牌的战斗费用视为零。
    /// </summary>
    public override bool TryModifyEnergyCostInCombat(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        if (_freeCardConsumed || card.Owner != Owner || card.EnergyCost.CostsX)
        {
            return false;
        }

        modifiedCost = 0m;
        return true;
    }

    /// <summary>
    /// 第一张真正打出的非 X 费牌消耗本回合免费资格。
    /// </summary>
    public override Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (!_freeCardConsumed && cardPlay.Card.Owner == Owner && !cardPlay.Card.EnergyCost.CostsX)
        {
            _freeCardConsumed = true;
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 每个正常玩家回合结束时推进倒计时；相同 RoundNumber 的额外回合不会重复推进。
    /// </summary>
    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side != Owner.Creature.Side || Owner.Creature.CombatState is not CombatState combatState ||
            combatState.RoundNumber == _lastCountedRound)
        {
            return;
        }

        //额外回合判断
        if (CombatManager.Instance.PlayersTakingExtraTurn.Contains(Owner))
        {
            return;
        }

        _lastCountedRound = combatState.RoundNumber;
        _remainingNormalTurns--;
        InvokeDisplayAmountChanged();
        if (_remainingNormalTurns <= 0 && Owner.Creature.IsAlive)
        {
            Flash();
            await CreatureCmd.Damage(choiceContext, Owner.Creature, FinaleDamage, ValueProp.Unblockable,
                null, null, null);
        }
    }
}
