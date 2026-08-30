using System.Globalization;

namespace STS2_Tomorin_Mod.Enemy.CardIntents;

/// <summary>指定敌人卡牌显式结算程序中唯一允许的顶层操作。</summary>
public enum EnemyCardProgramOperationKind
{
    /// <summary>冻结并消费全部素材请求。</summary>
    ConsumeMaterials,

    /// <summary>创建或增加作词结果。</summary>
    ComposeResult,

    /// <summary>执行定义的直接效果及其冻结派生步骤。</summary>
    DirectEffects
}

/// <summary>保存一个经过枚举边界验证的敌人卡牌程序操作。</summary>
public sealed record EnemyCardProgramOperation
{
    /// <summary>创建一个显式程序操作。</summary>
    public EnemyCardProgramOperation(EnemyCardProgramOperationKind kind)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "未知敌人卡牌程序操作。");
        }

        Kind = kind;
    }

    /// <summary>获取操作种类。</summary>
    public EnemyCardProgramOperationKind Kind { get; }
}

/// <summary>保存定义级唯一、不可变且无重复的显式结算顺序。</summary>
public sealed class EnemyCardResolutionProgram
{
    /// <summary>复制并验证显式操作顺序。</summary>
    public EnemyCardResolutionProgram(IEnumerable<EnemyCardProgramOperation> operations)
    {
        ArgumentNullException.ThrowIfNull(operations);
        EnemyCardProgramOperation[] copied = operations.ToArray();
        if (copied.Any(operation => operation is null))
        {
            throw new ArgumentException("敌人卡牌结算程序不能包含空操作。", nameof(operations));
        }

        EnemyCardProgramOperationKind[] duplicateKinds = copied
            .GroupBy(operation => operation.Kind)
            .Where(group => group.Count() != 1)
            .Select(group => group.Key)
            .ToArray();
        if (duplicateKinds.Length != 0)
        {
            throw new ArgumentException(
                $"敌人卡牌结算程序不能重复操作：{string.Join(", ", duplicateKinds)}。",
                nameof(operations));
        }

        Operations = Array.AsReadOnly(copied);
        Fingerprint = string.Join(
            ",",
            copied.Select(operation => ((int)operation.Kind).ToString(CultureInfo.InvariantCulture)));
    }

    /// <summary>获取唯一权威的有序操作。</summary>
    public IReadOnlyList<EnemyCardProgramOperation> Operations { get; }

    /// <summary>获取只依赖显式操作顺序的稳定指纹。</summary>
    public string Fingerprint { get; }

    /// <summary>按兼容 timing 在构造边界生成唯一显式程序，并省略不适用操作。</summary>
    internal static EnemyCardResolutionProgram FromCompatibility(
        EnemyCardCustomExecutionTiming timing,
        bool needsMaterials,
        bool needsCompose,
        bool needsDirectEffects)
    {
        if (!Enum.IsDefined(timing))
        {
            throw new ArgumentOutOfRangeException(nameof(timing), timing, "未知兼容执行时机。");
        }

        EnemyCardProgramOperationKind[] ordered = timing == EnemyCardCustomExecutionTiming.BeforeBaseEffects
            ?
            [
                EnemyCardProgramOperationKind.ConsumeMaterials,
                EnemyCardProgramOperationKind.ComposeResult,
                EnemyCardProgramOperationKind.DirectEffects
            ]
            :
            [
                EnemyCardProgramOperationKind.ConsumeMaterials,
                EnemyCardProgramOperationKind.DirectEffects,
                EnemyCardProgramOperationKind.ComposeResult
            ];
        return new EnemyCardResolutionProgram(ordered
            .Where(kind => kind switch
            {
                EnemyCardProgramOperationKind.ConsumeMaterials => needsMaterials,
                EnemyCardProgramOperationKind.ComposeResult => needsCompose,
                EnemyCardProgramOperationKind.DirectEffects => needsDirectEffects,
                _ => false
            })
            .Select(kind => new EnemyCardProgramOperation(kind)));
    }

    /// <summary>验证定义需要的操作恰好出现一次，且不允许多余操作。</summary>
    internal void ValidateDefinitionShape(
        bool needsMaterials,
        bool needsCompose,
        bool needsDirectEffects,
        string parameterName)
    {
        Validate(EnemyCardProgramOperationKind.ConsumeMaterials, needsMaterials);
        Validate(EnemyCardProgramOperationKind.ComposeResult, needsCompose);
        Validate(EnemyCardProgramOperationKind.DirectEffects, needsDirectEffects);
        return;

        void Validate(EnemyCardProgramOperationKind kind, bool required)
        {
            int count = Operations.Count(operation => operation.Kind == kind);
            if (count != (required ? 1 : 0))
            {
                throw new ArgumentException(
                    required
                        ? $"敌人卡牌定义需要且只能包含一个 {kind} 操作。"
                        : $"敌人卡牌定义不需要 {kind} 操作。",
                    parameterName);
            }
        }
    }
}

/// <summary>定义准备、纯模拟和真实执行共同重验的稳定出牌条件。</summary>
public interface IEnemyCardPlayCondition
{
    /// <summary>获取参与定义指纹与冻结计划校验的稳定标识。</summary>
    string ProgramId { get; }

    /// <summary>在任何素材预留或牌区移动前判断能否创建冻结单元。</summary>
    bool CanPlan(EnemyPreparedPlanningState state, BaseEnemyCard card);

    /// <summary>重验冻结条件是否仍满足纯模拟前提。</summary>
    bool CanSimulate(EnemyCardSimulationContext context);

    /// <summary>重验冻结条件是否仍满足真实执行前提。</summary>
    bool CanExecute(EnemyCardExecutionContext context);
}

/// <summary>没有额外条件的定义共用的稳定恒真程序。</summary>
public sealed class EnemyCardAlwaysPlayCondition : IEnemyCardPlayCondition
{
    private EnemyCardAlwaysPlayCondition()
    {
    }

    /// <summary>获取无条件定义共享实例。</summary>
    public static EnemyCardAlwaysPlayCondition Instance { get; } = new();

    /// <inheritdoc />
    public string ProgramId => "ENEMY_CARD:PLAY_CONDITION:ALWAYS";

    /// <inheritdoc />
    public bool CanPlan(EnemyPreparedPlanningState state, BaseEnemyCard card) => true;

    /// <inheritdoc />
    public bool CanSimulate(EnemyCardSimulationContext context) => true;

    /// <inheritdoc />
    public bool CanExecute(EnemyCardExecutionContext context) => true;
}
