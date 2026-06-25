using System;
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
using STS2_Tomorin_Mod.Enemy;

namespace STS2_Tomorin_Mod.Powers;

/// <summary>
/// C位管理器：挂在Oblivionis上，管理C位机制、击杀记录、阶段转换。
/// - 非C位敌人获得Intangible（不可被有效伤害）
/// - 玩家通过打出PositionZero卡切换C位
/// - C位死亡时按顺序自动切换
/// - Boss死亡时判断进入二阶段或隐藏Boss
/// </summary>
public class CenterPositionManagerPower : BasePowerModel
{
    private class Data
    {
        public Creature? centerPositionCreature;
        public HashSet<Type> killRegistry = new();
        public int phaseState; // 0=Phase1, 1=Phase2, 2=Hidden
        public bool cPositionMechanismActive = true;
        public bool isInitialized;
    }
    
    private StringVar centerStringValue = new StringVar("Center", "Dorlis");
    protected override IEnumerable<DynamicVar> CanonicalVars => [centerStringValue];

    /// <summary>
    /// C位切换顺序
    /// </summary>
    private static readonly Type[] CPositionOrder =
    [
        typeof(Doloris),
        typeof(Mortis),
        typeof(Timoris),
        typeof(Amoris),
        typeof(Oblivionis)
    ];

    public decimal HpReductionPerKill = 150m;
    private const decimal Phase2HpBonus = 500m;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    protected override bool IsVisibleInternal => false;

    protected override object InitInternalData() => new Data();

    #region Death Prevention Hooks

    public override bool ShouldStopCombatFromEnding() => true;

    public override bool ShouldCreatureBeRemovedFromCombatAfterDeath(Creature creature)
    {
        var data = GetInternalData<Data>();
        if (creature != base.Owner || data.killRegistry.Count == 0) return true;
        return false; // Oblivionis stays for revival
    }

    public override bool ShouldPowerBeRemovedAfterOwnerDeath() => false;

    #endregion

    #region Per-Turn: Intangible Management

    public override async Task BeforeSideTurnStart(PlayerChoiceContext choiceContext, CombatSide side,
        IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        var data = GetInternalData<Data>();

        // 首次玩家回合：初始化C位、给非C位敌人施加Intangible、发放PositionZero
        if (side == CombatSide.Player)
        {
            if (!data.isInitialized)
            {
                data.isInitialized = true;

                // 设置默认C位（Doloris，按顺序第一个存活者）
                FindAndSetNextAliveCPosition(data, combatState);
                
                // 每个玩家手牌放入3张PositionZero
                // 使用 CreateCard<T> 而非 AddToCombatAndPreview<T>，后者走 MockCardPool 路径，不支持Mod卡牌
                const int kPositionZeroCount = 3;
                foreach (var player in combatState.Players)
                {
                    await CardPileCmd.AddToCombatAndPreview<Cards.EnemyCards.PositionZero>(player.Creature, PileType.Hand, kPositionZeroCount, null);
                }
            }
            

            // 非C位敌人在玩家回合开始时就获得Intangible
            foreach (var enemy in combatState.Enemies)
            {
                if (enemy != data.centerPositionCreature && !enemy.HasPower<IntangiblePower>())
                    await PowerCmd.Apply<IntangiblePower>(new ThrowingPlayerChoiceContext(), enemy, 1, base.Owner, null);
            }

            // 确保C位引用有效
            if (data.centerPositionCreature == null || !data.centerPositionCreature.IsAlive)
            {
                // 寻找第一个存活者并按顺序设C位
                FindAndSetNextAliveCPosition(data, combatState);
            }

            foreach (var enemy in combatState.Enemies)
            {
                if (enemy == data.centerPositionCreature)
                {
                    // C位：移除Intangible
                    if (enemy.HasPower<IntangiblePower>())
                        await PowerCmd.Remove<IntangiblePower>(enemy);
                }
                else
                {
                    // 非C位：施加1层Intangible
                    if (!enemy.HasPower<IntangiblePower>())
                        await PowerCmd.Apply<IntangiblePower>(new ThrowingPlayerChoiceContext(), enemy, 1, base.Owner, null);
                }
            }
        }

        // if (side != CombatSide.Enemy) return;
        // if (!data.cPositionMechanismActive) return;
    }

    #endregion

    #region Card Play: PositionZero Detection

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var data = GetInternalData<Data>();
        // if (!data.cPositionMechanismActive) return;

        if (cardPlay.Card is Cards.EnemyCards.PositionZero)
        {
            var target = cardPlay.Target;
            if (target != null && target != data.centerPositionCreature)
            {
                await SwitchCenterPosition(data, target);
            }
        }
    }

    #endregion

    #region Death Handling

    public override async Task AfterDeath(PlayerChoiceContext choiceContext, Creature creature,
        bool wasRemovalPrevented, float deathAnimLength)
    {
        var data = GetInternalData<Data>();

        if (IsSubBoss(creature))
        {
            // 子Boss死亡
            data.killRegistry.Add(creature.Monster!.GetType());
            await ReduceOblivionisMaxHp(HpReductionPerKill);
            await ApplyKillBuffToAllPlayers(creature);

            // if (data.killRegistry.Count >= 4)
            // {
            //     // 所有子Boss死亡 → 二阶段
            //     await TransitionToPhase2(data);
            // }
            // else if (data.centerPositionCreature?.Monster is Oblivionis oblivionis)
            if (data.centerPositionCreature?.Monster is Oblivionis oblivionis)
            {
                // Oblivionis为C位时，队友死亡会切到对应死亡数量状态机的第1招
                oblivionis.SetPhase1CStateByDeadAllies(data.killRegistry.Count);
            }
            
            // 如果C位死亡，自动切换
            if (creature == data.centerPositionCreature && data.cPositionMechanismActive)
            {
                FindAndSetNextAliveCPosition(data, base.CombatState);
            }
        }
        else if (creature == base.Owner)
        {
            // Oblivionis死亡
            if (wasRemovalPrevented) return;

            if (data.killRegistry.Count == 0)
            {
                // 0子Boss死亡 → 隐藏Boss（由OblivionisHiddenRevivalPower处理）
                data.phaseState = 2;
            }
            else
            {
                // 有子Boss死亡 → 二阶段
                await TransitionToPhase2(data);
            }
        }
    }

    #endregion

    #region Private Helpers

    private static bool IsSubBoss(Creature creature)
    {
        return creature.Monster is Doloris or Mortis or Timoris or Amoris;
    }

    private async Task ReduceOblivionisMaxHp(decimal amount)
    {
        await CreatureCmd.LoseMaxHp(
            new ThrowingPlayerChoiceContext(), base.Owner, amount, false);
    }

    private async Task ApplyKillBuffToAllPlayers(Creature deadSubBoss)
    {
        switch (deadSubBoss.Monster)
        {
            case Doloris:
                foreach (var p in base.CombatState.Players)
                    await PowerCmd.Apply<DolorisKillBuffPower>(new ThrowingPlayerChoiceContext(), p.Creature, 1, base.Owner, null);
                break;
            case Mortis:
                foreach (var p in base.CombatState.Players)
                {
                    await PowerCmd.Apply<MortisKillBuffPower>(new ThrowingPlayerChoiceContext(), p.Creature, 1, base.Owner, null);
                    await PowerCmd.Apply<ThornsPower>(new ThrowingPlayerChoiceContext(), p.Creature, 5, base.Owner, null);
                }
                break;
            case Timoris:
                foreach (var p in base.CombatState.Players)
                    await PowerCmd.Apply<TimorisKillBuffPower>(new ThrowingPlayerChoiceContext(), p.Creature, 1, base.Owner, null);
                break;
            case Amoris:
                foreach (var p in base.CombatState.Players)
                    await PowerCmd.Apply<AmorisKillBuffPower>(new ThrowingPlayerChoiceContext(), p.Creature, 1, base.Owner, null);
                break;
        }
    }

    private async Task SwitchCenterPosition(Data data, Creature newTarget)
    {
        // 旧C位切换到非C位行为
        if (data.centerPositionCreature != null && data.centerPositionCreature.IsAlive)
        {
            SwitchToNonCState(data.centerPositionCreature.Monster);
        }

        // 新C位切换到C位行为
        SwitchToCState(newTarget.Monster, data.killRegistry.Count);

        data.centerPositionCreature = newTarget;
       

        // Intangible 在下一次 BeforeSideTurnStart 中统一更新
    }

    private static void SwitchToNonCState(MonsterModel? monster)
    {
        switch (monster)
        {
            case Oblivionis o: o.SetMoveImmediate(o.NonCState); break;
            case Doloris d: d.SetMoveImmediate(d.NonCState); break;
            case Mortis m: m.SetMoveImmediate(m.NonCState); break;
            case Timoris t: t.SetMoveImmediate(t.NonCState); break;
            case Amoris a: a.SetMoveImmediate(a.NonCState); break;
        }
        //添加一层无实体
        PowerCmd.Apply<IntangiblePower>(new ThrowingPlayerChoiceContext(), monster.Creature, 1, null, null);
        PowerCmd.Remove<PositionZeroShowPower>(monster.Creature);
    }

    private static void SwitchToCState(MonsterModel? monster, int deadAllyCount)
    {
        switch (monster)
        {
            case Oblivionis o: o.SetPhase1CStateByDeadAllies(deadAllyCount); break;
            case Doloris d: d.SetMoveImmediate(d.CState); break;
            case Mortis m: m.SetMoveImmediate(m.CState); break;
            case Timoris t: t.SetMoveImmediate(t.CState); break;
            case Amoris a: a.SetMoveImmediate(a.CState); break;
        }
        
        //移除一层无实体
        PowerCmd.Remove<IntangiblePower>(monster.Creature);
        PowerCmd.Apply<PositionZeroShowPower>(new ThrowingPlayerChoiceContext(), monster.Creature, 1, null, null);
    }

    private void FindAndSetNextAliveCPosition(Data data, ICombatState combatState)
    {
        foreach (var type in CPositionOrder)
        {
            var enemy = combatState.Enemies.FirstOrDefault(e => e.Monster?.GetType() == type && e.IsAlive);
            if (enemy != null)
            {
                if (data.centerPositionCreature != enemy)
                {
                    // 切换状态
                    if (data.centerPositionCreature != null && data.centerPositionCreature.IsAlive)
                        SwitchToNonCState(data.centerPositionCreature.Monster);

                    SwitchToCState(enemy.Monster, data.killRegistry.Count);
                    data.centerPositionCreature = enemy;
                }
                return;
            }
        }
    }

    private bool _isReliving = false;

    private async Task TransitionToPhase2(Data data)
    {
        data.cPositionMechanismActive = false;
        data.phaseState = 1;

        // 普通二阶段转场：隐藏Boss路径由OblivionisHiddenRevivalPower单独处理。
        var subBosses = base.CombatState.Enemies
            .Where(e => IsSubBoss(e) && e.IsAlive)
            .ToList();
        foreach (var subBoss in subBosses)
        {
            await CreatureCmd.Escape(subBoss);
        }

        // 移除所有敌人的Intangible
        foreach (var enemy in base.CombatState.Enemies)
        {
            if (enemy.HasPower<IntangiblePower>())
                await PowerCmd.Remove<IntangiblePower>(enemy);
        }
        
        // 应用二阶段被动
        await PowerCmd.Apply<OblivionisPhase2Power>(new ThrowingPlayerChoiceContext(), base.Owner, 1, base.Owner, null);

        // 切换到二阶段状态机
        if (base.Owner.Monster is Oblivionis oblivionis)
        {
            base.Owner.Monster.SetMoveImmediate(oblivionis.WaitRelive, forceTransition: true);
        }

        _isReliving = true;
        // await CreatureCmd.TriggerAnim(Owner, "Skill_2_Loop_B", 3.0f);

        // //移除自身和复活buff
        // await PowerCmd.Remove<CenterPositionManagerPower>(Owner);
        // await PowerCmd.Remove<OblivionisHiddenRevivalPower>(Owner);
    }
    
    public override bool ShouldAllowHitting(Creature creature)
    {
        if (creature != base.Owner) return true;
        return !_isReliving;
    }

    #endregion
}
