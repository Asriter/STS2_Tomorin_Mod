using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using STS2_Tomorin_Mod.Afflictions;

namespace STS2_Tomorin_Mod.Powers;

/// <summary>
/// CrychicRemember 核心状态机 Buff
/// 挂在玩家身上，根据层数%7切换7种不同阶段效果
/// 初始1层（进入阶段1）
/// 1. 回合结束时收到10点伤害
/// 2. 所有卡牌获得消耗
/// 3. 受到的伤害减半，回合开始时自动进入下一阶段
/// 4. 卡牌打出时额外消耗一费
/// 5. 回合结束时所有敌人获得1点力量和5点敏捷
/// 6. 造成伤害增加50%，回合开始时自动进入下一阶段
/// 7. 进入当前阶段，或回合开始时，强制结束当前回合
/// </summary>
public class CrychicRememberPower : BasePowerModel
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new IntVar("CurStage", 1m)];

    // ===== 可配置参数 =====

    /// <summary>
    /// 阶段1: 回合结束时受到的基础伤害值
    /// </summary>
    private const decimal Stage1Damage = 10;

    /// <summary>
    /// 阶段5: 回合结束时所有敌人获得的力量层数
    /// </summary>
    private const decimal Stage5Strength = 1;

    /// <summary>
    /// 阶段5: 回合结束时所有敌人获得的敏捷层数
    /// </summary>
    private const decimal Stage5Dexterity = 5;

    /// <summary>
    /// 阶段6: 造成伤害的倍率（1.5 = +50%）
    /// </summary>
    private const decimal Stage6DamageMultiplier = 0.5m;

    /// <summary>
    /// 阶段4: 卡牌打出时额外消耗的能量
    /// </summary>
    private const int Stage4ExtraEnergyCost = 1;

    // ===== 内部追踪 =====

    /// <summary>
    /// 追踪被阶段2添加了Exhaust关键词的卡牌
    /// </summary>
    private HashSet<CardModel> _stage2TrackedCards = new();

    /// <summary>
    /// 上一次检测到的Amount值，用于判断阶段切换
    /// </summary>
    private int _lastCheckedAmount = 1;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override bool AllowNegative => false;

    #region Stage Transition Detection

    /// <summary>
    /// 检测阶段切换并执行清理/初始化
    /// 在所有阶段相关钩子开始时调用
    /// </summary>
    private async Task HandleStageTransition(PlayerChoiceContext choiceContext)
    {
        int currentAmount = (int)base.Amount;
        if (currentAmount == _lastCheckedAmount) return;

        int oldStage = _lastCheckedAmount % 7;
        int newStage = currentAmount % 7;

        // 清理旧阶段
        await HandleStageExit(oldStage);

        // 应用新阶段
        await HandleStageEnter(newStage, choiceContext);

        _lastCheckedAmount = currentAmount;

        //更新显示层数
        base.DynamicVars["CurStage"].BaseValue = (Amount - 1) % 7 + 1;
    }

    /// <summary>
    /// 离开阶段时的清理逻辑
    /// </summary>
    private async Task HandleStageExit(int stage)
    {
        switch (stage)
        {
            case 2:
                // 移除手牌中被添加的Exhaust关键词
                CleanupStage2Exhaust();
                break;
            // case 3:
            //     // 移除手牌中CrychicDamageReduceCurse Affliction
            //     await CleanupStage3Affliction();
            //     break;
        }
    }

    /// <summary>
    /// 进入阶段时的初始化逻辑
    /// </summary>
    private async Task HandleStageEnter(int stage, PlayerChoiceContext choiceContext)
    {
        switch (stage)
        {
            case 2:
                // 给当前手牌全部添加Exhaust
                ApplyStage2Exhaust();
                break;
            // case 3:
            //     // 给当前手牌全部添加减伤Affliction
            //     await ApplyStage3Affliction();
            //     break;
            case 0:
                // 直接结束当前回合，然后流转状态
                Flash();
                PlayerCmd.EndTurn(base.Owner.Player, canBackOut: false);
                // await PowerCmd.ModifyAmount(choiceContext, this, 1, base.Owner, null);
                break;
        }
    }

    #endregion

    #region Stage 2: Exhaust Keyword

    private void ApplyStage2Exhaust()
    {
        var player = base.Owner.Player;
        if (player == null) return;

        foreach (var card in player.PlayerCombatState.Hand.Cards)
        {
            if (!card.Keywords.Contains(CardKeyword.Exhaust))
            {
                card.AddKeyword(CardKeyword.Exhaust);
                _stage2TrackedCards.Add(card);
            }
        }
    }

    private void CleanupStage2Exhaust()
    {
        foreach (var card in _stage2TrackedCards)
        {
            card.RemoveKeyword(CardKeyword.Exhaust);
        }

        _stage2TrackedCards.Clear();
    }

    #endregion

    #region Combat Hooks

    /// <summary>
    /// 阶段1: 回合结束时受到10点不可阻挡伤害
    /// 阶段5: 回合结束时所有敌人获得力量和敏捷
    /// </summary>
    public override async Task BeforeSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side != base.Owner.Side) return;
        await HandleStageTransition(choiceContext);

        int stage = (int)base.Amount % 7;

        if (stage == 1)
        {
            // 阶段1: 受到10点伤害
            Flash();
            await CreatureCmd.Damage(choiceContext, base.Owner, Stage1Damage,
                ValueProp.Unpowered, base.Owner, null, null);
        }
        else if (stage == 5)
        {
            // 阶段5: 所有敌人获得力量和敏捷
            Flash();
            var enemies = base.CombatState.Enemies;
            foreach (var enemy in enemies)
            {
                await PowerCmd.Apply<StrengthPower>(choiceContext, enemy, Stage5Strength, base.Owner, null);
                await PowerCmd.Apply<DexterityPower>(choiceContext, enemy, Stage5Dexterity, base.Owner, null);
            }
        }
    }

    /// <summary>
    /// 阶段2: 卡牌进入手牌时添加Exhaust关键词
    /// </summary>
    public override async Task AfterCardDrawn(PlayerChoiceContext choiceContext, CardModel card, bool fromHandDraw)
    {
        if (card.Owner != base.Owner.Player) return;
        await HandleStageTransition(choiceContext);

        int stage = (int)base.Amount % 7;

        if (stage == 2 && !card.Keywords.Contains(CardKeyword.Exhaust))
        {
            card.AddKeyword(CardKeyword.Exhaust);
            _stage2TrackedCards.Add(card);
        }
    }

    /// <summary>
    /// 阶段4: 卡牌打出时额外消耗1点能量
    /// </summary>
    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner?.Creature != base.Owner) return;
        if ((int)base.Amount % 7 != 4) return;

        await PlayerCmd.LoseEnergy(Stage4ExtraEnergyCost, Owner.Player);
    }

    /// <summary>
    /// 阶段6: 造成的伤害提高50%
    /// 阶段3: 受到伤害减半
    /// </summary>
    public override decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource,
        CardPlay? cardPlay)
    {
        //6阶段
        if (dealer == base.Owner && (int)base.Amount % 7 == 6)
            return amount * Stage6DamageMultiplier;

        //3阶段
        if ((int)base.Amount % 7 == 3 && target == Owner)
        {
            return -amount * Stage6DamageMultiplier;
        }

        return 0;
    }

    /// <summary>
    /// 阶段3+6: 玩家回合开始时自动推进层数
    /// 阶段0: 玩家回合开始时强制结束回合
    /// </summary>
    public override async Task BeforeHandDraw(Player player, PlayerChoiceContext choiceContext,
        ICombatState combatState)
    {
        if (player != base.Owner.Player) return;
        await HandleStageTransition(choiceContext);

        int stage = (int)base.Amount % 7;

        //所有情况下都是回合开始时自动流转状态
        await PowerCmd.ModifyAmount(choiceContext, this, 1, base.Owner, null);

        // if (stage == 3 || stage == 6)
        // {
        //     // 自动推进到下一阶段
        //     Flash();
        //     await PowerCmd.ModifyAmount(choiceContext, this, 1, base.Owner, null);
        // }
        // else if (stage == 0 && !_stage0TriggeredThisTurn)
        // {
        //     // 强制结束当前玩家回合
        //     _stage0TriggeredThisTurn = true;
        //     Flash();
        //     PlayerCmd.EndTurn(base.Owner.Player, canBackOut: false);
        //     // 层数+1 → 进入阶段1
        //     await PowerCmd.ModifyAmount(choiceContext, this, 1, base.Owner, null);
        // }
    }

    /// <summary>
    /// 阶段0备用触发: 若在敌人回合进入阶段0，等待玩家回合开始时再触发强制结束
    /// </summary>
    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != base.Owner.Player) return;
        await HandleStageTransition(choiceContext);

        int stage = (int)base.Amount % 7;

        // if (stage == 0 && !_stage0TriggeredThisTurn)
        // {
        //     _stage0TriggeredThisTurn = true;
        //     Flash();
        //     PlayerCmd.EndTurn(base.Owner.Player, canBackOut: false);
        //     await PowerCmd.ModifyAmount(choiceContext, this, 1, base.Owner, null);
        // }
    }

    /// <summary>
    /// 回合开始时重置阶段0触发标记
    /// </summary>
    public override Task BeforeSideTurnStart(PlayerChoiceContext choiceContext, CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (side == base.Owner.Side)
        {
            _lastCheckedAmount = (int)base.Amount;
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 移除时清理所有追踪数据
    /// </summary>
    public override async Task AfterRemoved(Creature oldOwner)
    {
        // 如果当前是阶段2，清理Exhaust
        if ((int)base.Amount % 7 == 2)
        {
            CleanupStage2Exhaust();
        }

        await base.AfterRemoved(oldOwner);
    }

    public override async Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext, PowerModel power,
        decimal amount, Creature? applier,
        CardModel? cardSource)
    {
        await HandleStageTransition(choiceContext);
    }

    #endregion
}