namespace STS2_Tomorin_Mod.Enemy.CardIntents;

/// <summary>影灯首领的生命、阶段、规划锁与权重唯一配置源。</summary>
public static class ShadowTomorinBalance
{
    public const int MaxHp = 1200;
    public const decimal Phase1DamageAllowance = 216m;
    public const decimal Phase2DamageAllowance = 432m;
    public const int MaxCandidateAttempts = 3;
    public const int MaxEffectiveCards = 6;
    public const int XMultiplierDefinitionThreshold = 5;
    public const int Phase1TemplateCount = 12;
    public const int Phase2TemplateCount = 11;
    public const int Phase3TemplateCount = 9;
    public const int ProjectionStepLimit = 256;

    public const int BrokenNoteWeight = 25;
    public const int CrumpledPaperWeight = 20;
    public const int MidnightCoffeeWeight = 15;
    public const int ColdRedTeaWeight = 15;
    public const int LeftoverBuffetWeight = 15;
    public const int StarStoneWeight = 10;

    public static EnemySoftLockLimits StaticLocks(EnemyCardPhase phase) => phase switch
    {
        EnemyCardPhase.Phase1 => new(38m, 72m),
        EnemyCardPhase.Phase2 => new(58m, 108m),
        EnemyCardPhase.Phase3 => new(77m, 152m),
        _ => throw new ArgumentOutOfRangeException(nameof(phase))
    };

    public static EnemySoftLockLimits FullLocks(EnemyCardPhase phase) => phase switch
    {
        EnemyCardPhase.Phase1 => new(48m, 90m),
        EnemyCardPhase.Phase2 => new(72m, 135m),
        EnemyCardPhase.Phase3 => new(96m, 190m),
        _ => throw new ArgumentOutOfRangeException(nameof(phase))
    };
}
