using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using STS2_Tomorin_Mod.Cards;
using STS2_Tomorin_Mod.Powers;

namespace STS2_Tomorin_Mod.Enemy.CardIntents;

/// <summary>影灯三阶段来源牌与全 Carry 作词链的正式唯一目录。</summary>
public static class ShadowTomorinCardCatalog
{
    private const string IdPrefix = "STS2_TOMORIN_MOD:SHADOW_TOMORIN_";
    private static readonly IReadOnlyDictionary<EnemyCardId, EnemyCardDefinition> DefinitionsById;
    private static readonly IReadOnlyDictionary<EnemyCardPhase, IReadOnlyList<EnemyCardDefinition>> PhaseDefinitions;
    private static readonly IReadOnlyList<EnemyCardDefinition> Carries;

    static ShadowTomorinCardCatalog()
    {
        Dictionary<string, EnemyCardDefinition> definitions = new(StringComparer.Ordinal);

        Add("SORROWFUL_RAIN", Define(
            "SORROWFUL_RAIN",
            ModelDb.Card<SorrowfulRain>(),
            EnemyCardTag.Ability,
            new EnemyCardScoreProfile(buffPowerStacks: 1m, abilityHint: 3m),
            effects: [SelfPower<CardIntentSorrowfulRainPower>("SORROWFUL_RAIN:POWER", 1m)],
            lifecycle: EnemyCardLifecycle.Exhaust));
        Add("ADAYUME", Define(
            "ADAYUME",
            ModelDb.Card<Adayume>(),
            EnemyCardTag.Ability,
            new EnemyCardScoreProfile(buffPowerStacks: 1m, abilityHint: 2m),
            effects: [SelfPower<CardIntentAdayumePower>("ADAYUME:POWER", 1m)],
            lifecycle: EnemyCardLifecycle.Exhaust));
        Add("HEART_BEAT", Define(
            "HEART_BEAT",
            ModelDb.Card<HeartBeat>(),
            EnemyCardTag.Ability,
            new EnemyCardScoreProfile(buffPowerStacks: 1m, abilityHint: 2m),
            effects: [SelfPower<CardIntentHeartBeatPower>("HEART_BEAT:POWER", 1m)],
            lifecycle: EnemyCardLifecycle.Exhaust));
        Add("DUCK_AND_COVER", Define(
            "DUCK_AND_COVER",
            ModelDb.Card<DuckAndCover>(),
            EnemyCardTag.Ability,
            new EnemyCardScoreProfile(buffPowerStacks: 1m, abilityHint: 3m),
            effects: [SelfPower<DuckAndCoverPower>("DUCK_AND_COVER:POWER", 1m)],
            lifecycle: EnemyCardLifecycle.Exhaust));
        Add("NAME_OF_TEAR", Define(
            "NAME_OF_TEAR",
            ModelDb.Card<NameOfTear>(),
            EnemyCardTag.Ability,
            new EnemyCardScoreProfile(buffPowerStacks: 1m, abilityHint: 1.5m),
            effects: [SelfPower<NameOfTearPower>("NAME_OF_TEAR:POWER", 1m)],
            lifecycle: EnemyCardLifecycle.Exhaust));
        Add("BUILD_AT_FIELD", Define(
            "BUILD_AT_FIELD",
            ModelDb.Card<BuildAtField>(),
            EnemyCardTag.Gain,
            new EnemyCardScoreProfile(atField: 2m),
            effects: [SelfPower<AtFieldPower>("BUILD_AT_FIELD:HEART_WALL", 2m)]));
        Add("DEFEND", Define(
            "DEFEND",
            ModelDb.Card<DefendTomorin>(),
            EnemyCardTag.Defense,
            new EnemyCardScoreProfile(block: 5m),
            effects: [Block("DEFEND:BLOCK", 5m)]));
        Add("STRIKE", Define(
            "STRIKE",
            ModelDb.Card<StrikeTomorin>(),
            EnemyCardTag.Attack,
            new EnemyCardScoreProfile(attack: 6m),
            effects: [Attack("STRIKE:ATTACK", 6m)]));
        Add("TOMORIN_PUNCH", Define(
            "TOMORIN_PUNCH",
            ModelDb.Card<TomorinPunch>(),
            EnemyCardTag.Attack | EnemyCardTag.Defense | EnemyCardTag.Gain,
            new EnemyCardScoreProfile(attack: 8m, block: 8m, atField: 2m),
            effects:
            [
                Attack("TOMORIN_PUNCH:ATTACK", 8m),
                Block("TOMORIN_PUNCH:BLOCK", 8m),
                SelfPower<AtFieldPower>("TOMORIN_PUNCH:HEART_WALL", 2m)
            ]));

        Add("AT_FIELD", Define(
            "AT_FIELD",
            ModelDb.Card<AtField>(),
            EnemyCardTag.Defense | EnemyCardTag.Gain,
            new EnemyCardScoreProfile(block: 13m, atField: 5m),
            materials: [EnemyMaterialRequest.NonCompose(CardType.Status, 1)],
            effects:
            [
                Block("AT_FIELD:BLOCK", 13m),
                SelfPower<AtFieldPower>("AT_FIELD:HEART_WALL", 5m)
            ],
            effectClasses: EnemyCardEffectClass.CollectionConsumer));
        Add("CANNOT_BEING_HUMAN", Define(
            "CANNOT_BEING_HUMAN",
            ModelDb.Card<CannotBeingHuman>(),
            EnemyCardTag.Gain,
            new EnemyCardScoreProfile(dexterity: 1m, atField: 4m),
            effects:
            [
                SelfPower<DexterityPower>("CANNOT_BEING_HUMAN:DEXTERITY", 1m),
                SelfPower<AtFieldPower>("CANNOT_BEING_HUMAN:HEART_WALL", 4m)
            ]));
        Add("WOODLOUSE", Define(
            "WOODLOUSE",
            ModelDb.Card<Woodlouse>(),
            EnemyCardTag.Defense | EnemyCardTag.CollectionGenerator,
            new EnemyCardScoreProfile(block: 8m, normalCollection: 1m),
            effects:
            [
                Block("WOODLOUSE:BLOCK", 8m),
                FixedCollection("WOODLOUSE:BROKEN_NOTE", ShadowTomorinCollectionCatalog.BrokenNoteId)
            ]));
        Add("UNWANTED_SIXTH", Define(
            "UNWANTED_SIXTH",
            ModelDb.Card<UnwantedSixth>(),
            EnemyCardTag.Ability | EnemyCardTag.CollectionGenerator,
            new EnemyCardScoreProfile(normalCollection: 1m, abilityHint: 1m),
            effects:
            [
                ShadowTomorinEffects.ActivateUnwantedSixth("SHADOW:UNWANTED_SIXTH:ACTIVATE"),
                FixedCollection("UNWANTED_SIXTH:CRUMPLED_PAPER", ShadowTomorinCollectionCatalog.CrumpledPaperId)
            ],
            lifecycle: EnemyCardLifecycle.Exhaust));
        Add("POETRY_OR_LYRICS", Define(
            "POETRY_OR_LYRICS",
            ModelDb.Card<PoetryOrLyrics>(),
            EnemyCardTag.Gain,
            new EnemyCardScoreProfile(dexterity: 3m, atField: 3m),
            effects: [ShadowTomorinEffects.ConsumeAvailableCollections("SHADOW:POETRY_OR_LYRICS:CONSUME")],
            lifecycle: EnemyCardLifecycle.Exhaust,
            effectClasses: EnemyCardEffectClass.CollectionConsumer));
        Add("THIS_NO_NEED", Define(
            "THIS_NO_NEED",
            ModelDb.Card<ThisNoNeed>(),
            EnemyCardTag.Attack | EnemyCardTag.Defense,
            new EnemyCardScoreProfile(attack: 5m, block: 5m),
            effects:
            [
                ShadowTomorinEffects.ConsumeNonComposeSource("SHADOW:THIS_NO_NEED:CONSUME_SOURCE"),
                Attack("THIS_NO_NEED:ATTACK", 5m),
                Block("THIS_NO_NEED:BLOCK", 5m)
            ],
            playCondition: ShadowTomorinEffects.RequireNonComposeSource("SHADOW:THIS_NO_NEED:REQUIRE_SOURCE"),
            effectClasses: EnemyCardEffectClass.Control));
        Add("HOPE_ON_THE_VOICE", Define(
            "HOPE_ON_THE_VOICE",
            ModelDb.Card<HopeOnTheVoice>(),
            EnemyCardTag.Buff | EnemyCardTag.CollectionGenerator,
            new EnemyCardScoreProfile(vulnerable: 1m, otherDebuff: 1m, normalCollection: 1m),
            effects:
            [
                AllPlayersPower<WeakPower>("HOPE_ON_THE_VOICE:WEAK", 1m),
                AllPlayersPower<VulnerablePower>("HOPE_ON_THE_VOICE:VULNERABLE", 1m),
                FixedCollection("HOPE_ON_THE_VOICE:MIDNIGHT_COFFEE", ShadowTomorinCollectionCatalog.MidnightCoffeeId)
            ],
            lifecycle: EnemyCardLifecycle.Exhaust,
            effectClasses: EnemyCardEffectClass.Control));
        Add("HITOSHIZUKU", DefineCompose(
            "HITOSHIZUKU",
            ModelDb.Card<Hitoshizuku>(),
            EnemyCardTag.Attack | EnemyCardTag.Compose,
            new EnemyCardScoreProfile(attack: 6m),
            CardType.Attack,
            1,
            "HITOSHIZUKU_TOKEN",
            EnemyCardTokenTiming.Immediate,
            effects: [Attack("HITOSHIZUKU:ATTACK", 6m)],
            customExecutionTiming: EnemyCardCustomExecutionTiming.BeforeBaseEffects,
            effectClasses: EnemyCardEffectClass.ImmediateAttackProducer));
        Add("WANT_BE_YOUR_GOD", DefineCompose(
            "WANT_BE_YOUR_GOD",
            ModelDb.Card<WantBeYourGod>(),
            EnemyCardTag.Compose | EnemyCardTag.Gain,
            new EnemyCardScoreProfile(atField: 5m, deferredTokenHint: 1m),
            CardType.Skill,
            1,
            "WANT_BE_YOUR_GOD_TOKEN",
            EnemyCardTokenTiming.RetainedNextTurn,
            effects: [SelfPower<AtFieldPower>("WANT_BE_YOUR_GOD:HEART_WALL", 5m)],
            effectClasses: EnemyCardEffectClass.DelayedTokenProducer));

        Add("NAMELESS_PAPER", DefineCompose(
            "NAMELESS_PAPER",
            ModelDb.Card<NamelessPaper>(),
            EnemyCardTag.Attack | EnemyCardTag.Buff | EnemyCardTag.Compose,
            new EnemyCardScoreProfile(attack: 9m, vulnerable: 1m, deferredTokenHint: 1m),
            CardType.Attack,
            1,
            "SONG_OF_BE_HUMAN",
            EnemyCardTokenTiming.RetainedNextTurn,
            effects:
            [
                Attack("NAMELESS_PAPER:ATTACK", 9m),
                AllPlayersPower<VulnerablePower>("NAMELESS_PAPER:VULNERABLE", 1m)
            ],
            effectClasses: EnemyCardEffectClass.DelayedTokenProducer));
        Add("MAYOIUTA", DefineCompose(
            "MAYOIUTA",
            ModelDb.Card<Mayoiuta>(),
            EnemyCardTag.Attack | EnemyCardTag.Compose,
            new EnemyCardScoreProfile(attack: 6m, vulnerable: 2m),
            CardType.Attack,
            1,
            "MAYOIUTA_TOKEN",
            EnemyCardTokenTiming.Immediate,
            effects:
            [
                Attack("MAYOIUTA:ATTACK", 6m),
                AllPlayersPower<VulnerablePower>("MAYOIUTA:VULNERABLE", 2m)
            ],
            customExecutionTiming: EnemyCardCustomExecutionTiming.BeforeBaseEffects,
            effectClasses: EnemyCardEffectClass.ImmediateAttackProducer));
        Add("SENZAIHYOUMEI", DefineCompose(
            "SENZAIHYOUMEI",
            ModelDb.Card<Senzaihyoumei>(),
            EnemyCardTag.Compose | EnemyCardTag.Gain,
            new EnemyCardScoreProfile(deferredTokenHint: 1m),
            CardType.Status,
            1,
            "SENZAIHYOUMEI_TOKEN",
            EnemyCardTokenTiming.RetainedNextTurn,
            effectClasses: EnemyCardEffectClass.DelayedTokenProducer));
        Add("SING_FULL_POWER", Define(
            "SING_FULL_POWER",
            ModelDb.Card<SingFullPower>(),
            EnemyCardTag.Attack,
            new EnemyCardScoreProfile(attack: 9m),
            effects: [ShadowTomorinEffects.DynamicHeartWallAttackAll("SHADOW:SING_FULL_POWER:ATTACK")],
            effectClasses: EnemyCardEffectClass.Finisher));
        Add("WHY_PLAY_HARUHIKAGE", Define(
            "WHY_PLAY_HARUHIKAGE",
            ModelDb.Card<WhyPlayHaruhikage>(),
            EnemyCardTag.Attack | EnemyCardTag.CollectionGenerator,
            new EnemyCardScoreProfile(attack: 16m, normalCollection: 2m),
            effects:
            [
                Attack("WHY_PLAY_HARUHIKAGE:ATTACK", 16m),
                ShadowTomorinEffects.GenerateFrozenCollections(
                    "SHADOW:WHY_PLAY_HARUHIKAGE:COLLECTIONS",
                    ShadowTomorinCollectionCatalog.WeightedDefinitions,
                    count: 2)
            ]));
        Add("WANT_TO_BEING_HUMAN", Define(
            "WANT_TO_BEING_HUMAN",
            ModelDb.Card<WantToBeingHuman>(),
            EnemyCardTag.Gain,
            new EnemyCardScoreProfile(strength: 1m),
            effects: [ShadowTomorinEffects.ConsumeHeartWallGainStrength("SHADOW:WANT_TO_BEING_HUMAN:CONVERT")],
            failureDisposition: EnemyCardFailureDisposition.Retain,
            playCondition: ShadowTomorinEffects.RequireHeartWall("SHADOW:WANT_TO_BEING_HUMAN:REQUIRE_HEART_WALL"),
            effectClasses: EnemyCardEffectClass.HeartWallConsumer | EnemyCardEffectClass.Finisher));

        Add("HITOSHIZUKU_TOKEN", Define(
            "HITOSHIZUKU_TOKEN",
            ModelDb.Card<HitoshizukuToken>(),
            EnemyCardTag.Attack,
            new EnemyCardScoreProfile(attack: 18m),
            effects: [Attack("HITOSHIZUKU_TOKEN:ATTACK", 9m, 2)],
            carryAcrossPhase: true));
        Add("WANT_BE_YOUR_GOD_TOKEN", Define(
            "WANT_BE_YOUR_GOD_TOKEN",
            ModelDb.Card<WantBeYourGodToken>(),
            EnemyCardTag.Defense | EnemyCardTag.Gain,
            new EnemyCardScoreProfile(block: 9m, atField: 1m),
            effects:
            [
                Block("WANT_BE_YOUR_GOD_TOKEN:BLOCK", 9m),
                SelfPower<AtFieldPower>("WANT_BE_YOUR_GOD_TOKEN:HEART_WALL", 1m)
            ],
            lifecycle: EnemyCardLifecycle.Exhaust,
            carryAcrossPhase: true));
        Add("MAYOIUTA_TOKEN", Define(
            "MAYOIUTA_TOKEN",
            ModelDb.Card<MayoiutaToken>(),
            EnemyCardTag.Attack,
            new EnemyCardScoreProfile(attack: 25m),
            effects: [Attack("MAYOIUTA_TOKEN:ATTACK", 5m, 5)],
            carryAcrossPhase: true));
        Add("SENZAIHYOUMEI_TOKEN", Define(
            "SENZAIHYOUMEI_TOKEN",
            ModelDb.Card<SenzaihyoumeiToken>(),
            EnemyCardTag.Attack,
            new EnemyCardScoreProfile(attack: 8m),
            effects:
            [
                ShadowTomorinEffects.FrozenXAttackAll(
                    "SHADOW:SENZAIHYOUMEI_TOKEN:X_ATTACK",
                    damage: 8m,
                    doubleAtDistinctExhaustDefinitionCount: ShadowTomorinBalance.XMultiplierDefinitionThreshold)
            ],
            carryAcrossPhase: true));
        Add("SONG_OF_BE_HUMAN", DefineCompose(
            "SONG_OF_BE_HUMAN",
            ModelDb.Card<SongOfBeHuman>(),
            EnemyCardTag.Compose | EnemyCardTag.Defense | EnemyCardTag.Gain,
            new EnemyCardScoreProfile(block: 20m, dexterity: 5m, deferredTokenHint: 1m),
            CardType.Skill,
            2,
            "HARUHIKAGE",
            EnemyCardTokenTiming.RetainedNextTurn,
            effects:
            [
                SelfPower<DexterityPower>("SONG_OF_BE_HUMAN:DEXTERITY", 5m),
                Block("SONG_OF_BE_HUMAN:BLOCK", 20m)
            ],
            failureDisposition: EnemyCardFailureDisposition.Retain,
            carryAcrossPhase: true,
            effectClasses: EnemyCardEffectClass.DelayedTokenProducer));
        Add("HARUHIKAGE", DefineCompose(
            "HARUHIKAGE",
            ModelDb.Card<Haruhikage>(),
            EnemyCardTag.Compose | EnemyCardTag.Gain,
            new EnemyCardScoreProfile(atField: 20m, deferredTokenHint: 1m),
            CardType.Status,
            2,
            "PRIDE_MAN_SAKI",
            EnemyCardTokenTiming.RetainedNextTurn,
            effects: [SelfPower<AtFieldPower>("HARUHIKAGE:HEART_WALL", 20m)],
            failureDisposition: EnemyCardFailureDisposition.Retain,
            carryAcrossPhase: true,
            effectClasses: EnemyCardEffectClass.DelayedTokenProducer));
        Add("PRIDE_MAN_SAKI", Define(
            "PRIDE_MAN_SAKI",
            ModelDb.Card<PrideManSaki>(),
            EnemyCardTag.Attack,
            new EnemyCardScoreProfile(attack: 50m),
            effects: [Attack("PRIDE_MAN_SAKI:ATTACK", 5m, 10)],
            lifecycle: EnemyCardLifecycle.Exhaust,
            carryAcrossPhase: true));

        DefinitionsById = definitions.Values.ToDictionary(definition => definition.CardId);
        PhaseDefinitions = new Dictionary<EnemyCardPhase, IReadOnlyList<EnemyCardDefinition>>
        {
            [EnemyCardPhase.Phase1] = Copies(
                ("SORROWFUL_RAIN", 1), ("ADAYUME", 1), ("HEART_BEAT", 1),
                ("DUCK_AND_COVER", 1), ("NAME_OF_TEAR", 1), ("BUILD_AT_FIELD", 2),
                ("DEFEND", 2), ("STRIKE", 2), ("TOMORIN_PUNCH", 1)),
            [EnemyCardPhase.Phase2] = Copies(
                ("AT_FIELD", 2), ("CANNOT_BEING_HUMAN", 1), ("WOODLOUSE", 1),
                ("UNWANTED_SIXTH", 1), ("POETRY_OR_LYRICS", 1), ("THIS_NO_NEED", 1),
                ("HOPE_ON_THE_VOICE", 1), ("HITOSHIZUKU", 1), ("WANT_BE_YOUR_GOD", 1),
                ("TOMORIN_PUNCH", 1)),
            [EnemyCardPhase.Phase3] = Copies(
                ("NAMELESS_PAPER", 2), ("MAYOIUTA", 1), ("HITOSHIZUKU", 1),
                ("SENZAIHYOUMEI", 1), ("SING_FULL_POWER", 1), ("WHY_PLAY_HARUHIKAGE", 1),
                ("TOMORIN_PUNCH", 1), ("WANT_TO_BEING_HUMAN", 1))
        };
        Carries = Array.AsReadOnly(definitions.Values.Where(definition => definition.CarryAcrossPhase).ToArray());
        return;

        void Add(string suffix, EnemyCardDefinition definition) => definitions.Add(suffix, definition);
        IReadOnlyList<EnemyCardDefinition> Copies(params (string Suffix, int Count)[] entries) =>
            Array.AsReadOnly(entries.SelectMany(entry => Enumerable.Repeat(definitions[entry.Suffix], entry.Count)).ToArray());
    }

    public static IReadOnlyDictionary<EnemyCardId, EnemyCardDefinition> AllDefinitions => DefinitionsById;
    public static IReadOnlyList<EnemyCardDefinition> CarryDefinitions => Carries;

    public static EnemyCardDefinition Get(string suffix) =>
        DefinitionsById.TryGetValue(CardId(suffix), out EnemyCardDefinition? definition)
            ? definition
            : throw new KeyNotFoundException($"影灯目录未注册卡牌 {suffix}。");

    public static IReadOnlyList<EnemyCardDefinition> GetPhaseDefinitions(EnemyCardPhase phase) =>
        PhaseDefinitions.TryGetValue(phase, out IReadOnlyList<EnemyCardDefinition>? definitions)
            ? definitions
            : throw new KeyNotFoundException($"影灯目录未注册阶段 {phase}。");

    public static BaseEnemyCard Create(EnemyCardId id) =>
        new CatalogEnemyCard(AllDefinitions.TryGetValue(id, out EnemyCardDefinition? definition)
            ? definition
            : throw new KeyNotFoundException($"影灯目录未注册卡牌 {id}。"));

    public static EnemyCardId CardId(string suffix) => new($"{IdPrefix}{suffix}");

    private static ShadowFrozenCollectionGenerationEffect FixedCollection(string id, string collectionId) =>
        AssertFrozenGenerator(ShadowTomorinEffects.GenerateFrozenCollections(
            $"SHADOW:{id}",
            [(ShadowTomorinCollectionCatalog.Catalog.GetRequired(collectionId), 1)],
            count: 1));

    private static ShadowFrozenCollectionGenerationEffect AssertFrozenGenerator(IEnemyCardEffectNode effect) =>
        (ShadowFrozenCollectionGenerationEffect)effect;

    private static IEnemyCardEffectNode Attack(string id, decimal damage, int hits = 1) =>
        new EnemyAttackAllEffect($"SHADOW:{id}", damage, hits);

    private static IEnemyCardEffectNode Block(string id, decimal block) =>
        new EnemyBlockEffect($"SHADOW:{id}", block);

    private static IEnemyCardEffectNode SelfPower<TPower>(string id, decimal amount)
        where TPower : PowerModel, new() =>
        new EnemySelfPowerEffect<TPower>($"SHADOW:{id}", amount);

    private static IEnemyCardEffectNode AllPlayersPower<TPower>(string id, decimal amount)
        where TPower : PowerModel, new() =>
        new EnemyAllPlayersPowerEffect<TPower>($"SHADOW:{id}", amount);

    private static EnemyCardDefinition DefineCompose(
        string suffix,
        CardModel model,
        EnemyCardTag tags,
        EnemyCardScoreProfile score,
        CardType materialType,
        int materialCount,
        string resultSuffix,
        EnemyCardTokenTiming tokenTiming,
        IEnumerable<IEnemyCardEffectNode>? effects = null,
        EnemyCardCustomExecutionTiming customExecutionTiming = EnemyCardCustomExecutionTiming.AfterBaseEffects,
        EnemyCardFailureDisposition failureDisposition = EnemyCardFailureDisposition.Discard,
        bool carryAcrossPhase = false,
        EnemyCardEffectClass effectClasses = EnemyCardEffectClass.None) =>
        Define(
            suffix,
            model,
            tags,
            score,
            materials: [EnemyMaterialRequest.Compose(materialType, materialCount)],
            effects: effects,
            lifecycle: EnemyCardLifecycle.Exhaust,
            failureDisposition: failureDisposition,
            tokenTiming: tokenTiming,
            composeResultCardId: CardId(resultSuffix),
            customExecutionTiming: customExecutionTiming,
            carryAcrossPhase: carryAcrossPhase,
            effectClasses: effectClasses);

    private static EnemyCardDefinition Define(
        string suffix,
        CardModel model,
        EnemyCardTag tags,
        EnemyCardScoreProfile score,
        IEnumerable<EnemyMaterialRequest>? materials = null,
        IEnumerable<IEnemyCardEffectNode>? effects = null,
        EnemyCardLifecycle lifecycle = EnemyCardLifecycle.Discard,
        EnemyCardFailureDisposition failureDisposition = EnemyCardFailureDisposition.Discard,
        EnemyCardTokenTiming tokenTiming = EnemyCardTokenTiming.None,
        EnemyCardId? composeResultCardId = null,
        EnemyCardCustomExecutionTiming customExecutionTiming = EnemyCardCustomExecutionTiming.AfterBaseEffects,
        IEnemyCardPlayCondition? playCondition = null,
        bool carryAcrossPhase = false,
        EnemyCardEffectClass effectClasses = EnemyCardEffectClass.None) =>
        new(
            CardId(suffix),
            model,
            tags,
            score,
            materialRequests: materials,
            lifecycle: lifecycle,
            failureDisposition: failureDisposition,
            tokenTiming: tokenTiming,
            composeResultCardId: composeResultCardId,
            effects: effects,
            customExecutionTiming: customExecutionTiming,
            playCondition: playCondition,
            carryAcrossPhase: carryAcrossPhase,
            effectClasses: effectClasses);

    private sealed class CatalogEnemyCard(EnemyCardDefinition definition) : BaseEnemyCard(definition);
}
