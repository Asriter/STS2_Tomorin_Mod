namespace STS2_Tomorin_Mod.Enemy.CardIntents;

/// <summary>影灯三阶段的加权指标、槽位硬资格与 Compose 结构限制。</summary>
public static class ShadowTomorinRules
{
    private static readonly IReadOnlyDictionary<EnemyCardPhase, EnemyCardPlanningRules> Rules =
        new Dictionary<EnemyCardPhase, EnemyCardPlanningRules>
        {
            [EnemyCardPhase.Phase1] = CreatePhase1(),
            [EnemyCardPhase.Phase2] = CreatePhase2(),
            [EnemyCardPhase.Phase3] = CreatePhase3()
        };

    public static EnemyCardPlanningRules ForPhase(EnemyCardPhase phase) =>
        Rules.TryGetValue(phase, out EnemyCardPlanningRules? rules)
            ? rules
            : throw new ArgumentOutOfRangeException(nameof(phase));

    private static EnemyCardPlanningRules CreatePhase1()
    {
        EnemyCandidateConstraints constraints = new(0, 0, 0);
        IReadOnlySet<EnemyCardId> abilityOrGain = Ids(
            "SORROWFUL_RAIN", "ADAYUME", "HEART_BEAT", "DUCK_AND_COVER", "NAME_OF_TEAR", "BUILD_AT_FIELD");
        IReadOnlySet<EnemyCardId> nonAttack = Ids(
            "SORROWFUL_RAIN", "ADAYUME", "HEART_BEAT", "DUCK_AND_COVER", "NAME_OF_TEAR",
            "BUILD_AT_FIELD", "DEFEND");
        IReadOnlySet<EnemyCardId> attackOrGain = Ids("STRIKE", "TOMORIN_PUNCH", "BUILD_AT_FIELD");
        return RulesFor(
            EnemyCardPhase.Phase1,
            new(
                Recipe(EnemyActionMetric.Gain, constraints,
                    Tag(EnemyCardTag.Ability),
                    Tag(EnemyCardTag.Ability, abilityOrGain),
                    Tag(EnemyCardTag.Defense)),
                55),
            new(
                Recipe(EnemyActionMetric.Fortify, constraints,
                    Tag(EnemyCardTag.Defense),
                    Tag(EnemyCardTag.Gain),
                    Id(nonAttack)),
                25),
            new(
                Recipe(EnemyActionMetric.Pressure, constraints,
                    Tag(EnemyCardTag.Attack),
                    Tag(EnemyCardTag.Attack, attackOrGain),
                    Tag(EnemyCardTag.Defense)),
                20));
    }

    private static EnemyCardPlanningRules CreatePhase2()
    {
        EnemyCandidateConstraints constraints = new(1, 1, 1);
        IReadOnlySet<EnemyCardId> nonComposeDefense = Ids(
            "AT_FIELD", "WOODLOUSE", "THIS_NO_NEED", "TOMORIN_PUNCH");
        IReadOnlySet<EnemyCardId> nonComposeGain = Ids(
            "AT_FIELD", "CANNOT_BEING_HUMAN", "POETRY_OR_LYRICS", "TOMORIN_PUNCH");
        IReadOnlySet<EnemyCardId> defenseOrGain = Ids(
            "AT_FIELD", "CANNOT_BEING_HUMAN", "WOODLOUSE", "POETRY_OR_LYRICS",
            "THIS_NO_NEED", "TOMORIN_PUNCH");
        IReadOnlySet<EnemyCardId> generatorOrConsumer = Ids(
            "AT_FIELD", "WOODLOUSE", "UNWANTED_SIXTH", "POETRY_OR_LYRICS",
            "THIS_NO_NEED", "HOPE_ON_THE_VOICE");
        IReadOnlySet<EnemyCardId> attackOrControl = Ids(
            "THIS_NO_NEED", "HOPE_ON_THE_VOICE", "TOMORIN_PUNCH");
        return RulesFor(
            EnemyCardPhase.Phase2,
            new(
                Recipe(EnemyActionMetric.Fortify, constraints,
                    Tag(EnemyCardTag.Defense, nonComposeDefense),
                    Tag(EnemyCardTag.Gain, nonComposeGain),
                    Id(generatorOrConsumer)),
                40),
            new(
                Recipe(EnemyActionMetric.Compose, constraints,
                    Tag(EnemyCardTag.Compose),
                    Material(),
                    Tag(EnemyCardTag.Defense, defenseOrGain)),
                35),
            new(
                Recipe(EnemyActionMetric.Pressure, constraints,
                    Tag(EnemyCardTag.Attack, attackOrControl),
                    Id(attackOrControl),
                    Tag(EnemyCardTag.Defense, defenseOrGain)),
                25));
    }

    private static EnemyCardPlanningRules CreatePhase3()
    {
        EnemyCandidateConstraints constraints = new(2, 1, 1);
        IReadOnlySet<EnemyCardId> nonComposeAttacks = Ids(
            "SING_FULL_POWER", "WHY_PLAY_HARUHIKAGE", "TOMORIN_PUNCH");
        IReadOnlySet<EnemyCardId> gainOrDefense = Ids("TOMORIN_PUNCH", "WANT_TO_BEING_HUMAN");
        IReadOnlySet<EnemyCardId> growth = Ids("SING_FULL_POWER", "WANT_TO_BEING_HUMAN");
        IReadOnlySet<EnemyCardId> nonComposeAny = Ids(
            "SING_FULL_POWER", "WHY_PLAY_HARUHIKAGE", "TOMORIN_PUNCH", "WANT_TO_BEING_HUMAN");
        return RulesFor(
            EnemyCardPhase.Phase3,
            new(
                Recipe(EnemyActionMetric.Burst, constraints,
                    Tag(EnemyCardTag.Attack, nonComposeAttacks),
                    Tag(EnemyCardTag.Attack, nonComposeAttacks),
                    Id(nonComposeAny),
                    Id(nonComposeAny)),
                45),
            new(
                Recipe(EnemyActionMetric.Compose, constraints,
                    Tag(EnemyCardTag.Compose),
                    Material(),
                    Tag(EnemyCardTag.Attack, nonComposeAttacks),
                    Tag(EnemyCardTag.Gain, gainOrDefense)),
                40),
            new(
                Recipe(EnemyActionMetric.Growth, constraints,
                    Id(growth),
                    Tag(EnemyCardTag.Attack, nonComposeAttacks),
                    Id(nonComposeAny)),
                15));
    }

    private static EnemyCardPlanningRules RulesFor(
        EnemyCardPhase phase,
        params EnemyWeightedActionRecipe[] recipes) =>
        new(
            ShadowTomorinBalance.StaticLocks(phase),
            ShadowTomorinBalance.FullLocks(phase),
            ShadowTomorinBalance.MaxCandidateAttempts,
            ShadowTomorinBalance.ProjectionStepLimit,
            recipes);

    private static EnemyActionRecipe Recipe(
        EnemyActionMetric metric,
        EnemyCandidateConstraints constraints,
        params EnemyActionSlotRule[] slots) =>
        new(metric, slots, constraints);

    private static EnemyActionSlotRule Tag(
        EnemyCardTag tag,
        IReadOnlySet<EnemyCardId>? allowed = null) =>
        new(tag, allowed);

    private static EnemyActionSlotRule Id(IReadOnlySet<EnemyCardId> allowed) =>
        new(null, allowed);

    private static EnemyActionSlotRule Material() =>
        new(null, MustMatchSelectedComposeMaterial: true);

    private static IReadOnlySet<EnemyCardId> Ids(params string[] suffixes) =>
        suffixes.Select(ShadowTomorinCardCatalog.CardId).ToHashSet();
}
