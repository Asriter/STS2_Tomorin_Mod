using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;

namespace STS2_Tomorin_Mod.Enemy.CardIntents.Test;

/// <summary>
/// 只承载 Tomorin 固定敌人牌组、初始收藏品和一个自循环行动状态的显式开发测试敌人。
/// </summary>
public sealed class CardIntentTestMonster : BaseCardIntentMonsterModel
{
    private const string TestStateId = "CARD_INTENT_TEST_LOOP";

    /// <summary>
    /// 获取仅供开发验证使用的固定初始生命下界。
    /// </summary>
    public override int MinInitialHp => 50;

    /// <summary>
    /// 获取与下界相同的固定初始生命上界。
    /// </summary>
    public override int MaxInitialHp => MinInitialHp;

    /// <summary>
    /// 复用仓库现有 Crychic 幻影敌人视觉，不引入新的 Godot 资源。
    /// </summary>
    public override string? CustomVisualPath =>
        "res://STS2_Tomorin_Mod/scenes/creature_visuals/enemies/ShadowTomorin.tscn";

    /// <summary>
    /// 显式注册测试牌组，并生成唯一且循环到自身的卡牌行动状态。
    /// </summary>
    /// <returns>以测试状态为初始状态的怪物状态机。</returns>
    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        CardIntentTestDeck.EnsureRegistered();
        CardIntentMoveState testState = RegisterCardIntentState(
            CardIntentMoveState.Create(
                TestStateId,
                this,
                CardIntentTestDeck.DeckId,
                CardIntentTestDeck.HandCapacity,
                rules: CardIntentTestRules.Default));
        testState.FollowUpState = testState;

        return new MonsterMoveStateMachine([testState], testState);
    }
}
