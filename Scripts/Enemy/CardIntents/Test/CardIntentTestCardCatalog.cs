using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using STS2_Tomorin_Mod.Cards;
using STS2_Tomorin_Mod.Powers;

namespace STS2_Tomorin_Mod.Enemy.CardIntents.Test;

/// <summary>
/// 显式声明测试敌人的十三种初始牌与四种作词衍生牌，不调用玩家出牌流程。
/// </summary>
public static class CardIntentTestCardCatalog
{
    private const string IdPrefix = "STS2_TOMORIN_MOD:CARD_INTENT_TOMORIN_";

    /// <summary>获取一滴卡牌规范中不依赖作词目标注册的基础伤害。</summary>
    private const decimal HitoshizukuDamage = 6m;

    /// <summary>获取无名文稿卡牌规范中不依赖作词目标注册的基础伤害。</summary>
    private const decimal NamelessPaperDamage = 9m;

    /// <summary>获取无名文稿卡牌规范中的易伤层数。</summary>
    private const decimal NamelessPaperVulnerable = 1m;
    private static readonly IReadOnlyDictionary<EnemyCardId, EnemyCardDefinition> DefinitionsById;
    private static readonly IReadOnlyList<EnemyCardDefinition> InitialDefinitions;

    /// <summary>
    /// 初始化所有不可变定义，并从玩家牌规范变量取得未升级数值。
    /// </summary>
    static CardIntentTestCardCatalog()
    {
        List<EnemyCardDefinition> initial = [];
        List<EnemyCardDefinition> all = [];

        SorrowfulRain rain = ModelDb.Card<SorrowfulRain>();
        AddInitial(Define(
            "RAIN",
            rain,
            EnemyCardTag.Ability,
            new EnemyCardScoreProfile(buffPowerStacks: Var(rain, "SorrowfulRainPower")),
            effects: [SelfPower<CardIntentSorrowfulRainPower>("RAIN_POWER", Var(rain, "SorrowfulRainPower"))],
            lifecycle: EnemyCardLifecycle.Exhaust,
            descriptionOverride: "[color=cyan]悲伤如雨落下。[/color]"));

        Adayume adayume = ModelDb.Card<Adayume>();
        AddInitial(Define(
            "ADAYUME",
            adayume,
            EnemyCardTag.Ability,
            new EnemyCardScoreProfile(buffPowerStacks: Var(adayume, "AdayumePower")),
            effects: [SelfPower<CardIntentAdayumePower>("ADAYUME_POWER", Var(adayume, "AdayumePower"))],
            lifecycle: EnemyCardLifecycle.Exhaust));

        NameOfTear nameOfTear = ModelDb.Card<NameOfTear>();
        AddInitial(Define(
            "NAME_OF_TEAR",
            nameOfTear,
            EnemyCardTag.Ability,
            new EnemyCardScoreProfile(buffPowerStacks: decimal.One),
            effects: [SelfPower<NameOfTearPower>("NAME_OF_TEAR_POWER", decimal.One)],
            lifecycle: EnemyCardLifecycle.Exhaust));

        StrikeTomorin strike = ModelDb.Card<StrikeTomorin>();
        AddInitial(AttackDefinition("ATTACK", strike, EnemyCardTag.Attack));

        WhyPlayHaruhikage whyPlay = ModelDb.Card<WhyPlayHaruhikage>();
        AddInitial(AttackDefinition(
            "WHY_PLAY",
            whyPlay,
            EnemyCardTag.Attack | EnemyCardTag.CollectionGenerator));

        ThisNoNeed noNeed = ModelDb.Card<ThisNoNeed>();
        AddInitial(Define(
            "THIS_NO_NEED",
            noNeed,
            EnemyCardTag.Attack | EnemyCardTag.Defense,
            new EnemyCardScoreProfile(
                attack: noNeed.DynamicVars.Damage.BaseValue,
                block: noNeed.DynamicVars.Block.BaseValue),
            materials: [EnemyMaterialRequest.NonComposeAny(1)],
            effects:
            [
                Attack("THIS_NO_NEED_ATTACK", noNeed.DynamicVars.Damage.BaseValue),
                Block("THIS_NO_NEED_BLOCK", noNeed.DynamicVars.Block.BaseValue)
            ]));

        DefendTomorin defend = ModelDb.Card<DefendTomorin>();
        AddInitial(BlockDefinition("DEFEND", defend, EnemyCardTag.Defense));

        AtField atField = ModelDb.Card<AtField>();
        AddInitial(Define(
            "AT_FIELD",
            atField,
            EnemyCardTag.Defense | EnemyCardTag.Gain,
            new EnemyCardScoreProfile(
                block: atField.DynamicVars.Block.BaseValue,
                atField: Var(atField, "AtFieldPower")),
            materials: [EnemyMaterialRequest.NonCompose(CardType.Status, 1)],
            effects:
            [
                Block("AT_FIELD_BLOCK", atField.DynamicVars.Block.BaseValue),
                SelfPower<AtFieldPower>("AT_FIELD_POWER", Var(atField, "AtFieldPower"))
            ]));

        HopeOnTheVoice hope = ModelDb.Card<HopeOnTheVoice>();
        AddInitial(Define(
            "HOPE",
            hope,
            EnemyCardTag.Buff | EnemyCardTag.CollectionGenerator,
            new EnemyCardScoreProfile(
                buffPowerStacks: Var(hope, "WeakPower") + Var(hope, "VulnerablePower")),
            effects:
            [
                AllPlayersPower<WeakPower>("HOPE_WEAK", Var(hope, "WeakPower")),
                AllPlayersPower<VulnerablePower>("HOPE_VULNERABLE", Var(hope, "VulnerablePower"))
            ],
            lifecycle: EnemyCardLifecycle.Exhaust));

        CannotBeingHuman cannot = ModelDb.Card<CannotBeingHuman>();
        AddInitial(Define(
            "CANNOT",
            cannot,
            EnemyCardTag.Gain,
            new EnemyCardScoreProfile(
                dexterity: Var(cannot, "DexterityPower"),
                atField: Var(cannot, "AtFieldPower")),
            effects:
            [
                SelfPower<DexterityPower>("CANNOT_DEXTERITY", Var(cannot, "DexterityPower")),
                SelfPower<AtFieldPower>("CANNOT_AT_FIELD", Var(cannot, "AtFieldPower"))
            ]));

        Woodlouse woodlouse = ModelDb.Card<Woodlouse>();
        AddInitial(BlockDefinition(
            "WOODLOUSE",
            woodlouse,
            EnemyCardTag.Defense | EnemyCardTag.CollectionGenerator));

        Hitoshizuku hitoshizuku = ModelDb.Card<Hitoshizuku>();
        EnemyCardId hitoshizukuTokenId = CardId("HITOSHIZUKU_TOKEN");
        AddInitial(Define(
            "HITOSHIZUKU",
            hitoshizuku,
            EnemyCardTag.Attack | EnemyCardTag.Compose,
            new EnemyCardScoreProfile(attack: HitoshizukuDamage),
            materials: [EnemyMaterialRequest.Compose(CardType.Attack, 1)],
            effects: [Attack("HITOSHIZUKU_ATTACK", HitoshizukuDamage)],
            lifecycle: EnemyCardLifecycle.Exhaust,
            tokenTiming: EnemyCardTokenTiming.Immediate,
            composeResultCardId: hitoshizukuTokenId));

        NamelessPaper nameless = ModelDb.Card<NamelessPaper>();
        EnemyCardId songId = CardId("SONG_OF_BE_HUMAN");
        AddInitial(Define(
            "NAMELESS_PAPER",
            nameless,
            EnemyCardTag.Attack | EnemyCardTag.Buff | EnemyCardTag.Compose,
            new EnemyCardScoreProfile(
                attack: NamelessPaperDamage,
                buffPowerStacks: NamelessPaperVulnerable),
            materials: [EnemyMaterialRequest.Compose(CardType.Attack, 1)],
            effects:
            [
                Attack("NAMELESS_ATTACK", NamelessPaperDamage),
                AllPlayersPower<VulnerablePower>("NAMELESS_VULNERABLE", NamelessPaperVulnerable)
            ],
            lifecycle: EnemyCardLifecycle.Exhaust,
            tokenTiming: EnemyCardTokenTiming.RetainedNextTurn,
            composeResultCardId: songId));

        HitoshizukuToken hitoshizukuToken = ModelDb.Card<HitoshizukuToken>();
        all.Add(Define(
            "HITOSHIZUKU_TOKEN",
            hitoshizukuToken,
            EnemyCardTag.Attack,
            new EnemyCardScoreProfile(attack: hitoshizukuToken.DynamicVars.Damage.BaseValue * 2m),
            effects: [Attack("HITOSHIZUKU_TOKEN_ATTACK", hitoshizukuToken.DynamicVars.Damage.BaseValue, 2)]));

        SongOfBeHuman song = ModelDb.Card<SongOfBeHuman>();
        EnemyCardId haruhikageId = CardId("HARUHIKAGE");
        all.Add(Define(
            "SONG_OF_BE_HUMAN",
            song,
            EnemyCardTag.Compose | EnemyCardTag.Gain | EnemyCardTag.Defense,
            new EnemyCardScoreProfile(block: 20m, dexterity: 5m),
            materials: [EnemyMaterialRequest.Compose(CardType.Skill, 2)],
            effects:
            [
                SelfPower<DexterityPower>("SONG_DEXTERITY", 5m),
                Block("SONG_BLOCK", 20m)
            ],
            lifecycle: EnemyCardLifecycle.Exhaust,
            failureDisposition: EnemyCardFailureDisposition.Retain,
            tokenTiming: EnemyCardTokenTiming.RetainedNextTurn,
            composeResultCardId: haruhikageId));

        Haruhikage haruhikage = ModelDb.Card<Haruhikage>();
        EnemyCardId prideId = CardId("PRIDE_MAN_SAKI");
        all.Add(Define(
            "HARUHIKAGE",
            haruhikage,
            EnemyCardTag.Compose | EnemyCardTag.Gain,
            new EnemyCardScoreProfile(atField: 20m),
            materials: [EnemyMaterialRequest.Compose(CardType.Status, 2)],
            effects: [SelfPower<AtFieldPower>("HARUHIKAGE_AT_FIELD", 20m)],
            lifecycle: EnemyCardLifecycle.Exhaust,
            failureDisposition: EnemyCardFailureDisposition.Retain,
            tokenTiming: EnemyCardTokenTiming.RetainedNextTurn,
            composeResultCardId: prideId));

        PrideManSaki pride = ModelDb.Card<PrideManSaki>();
        all.Add(Define(
            "PRIDE_MAN_SAKI",
            pride,
            EnemyCardTag.Attack,
            new EnemyCardScoreProfile(attack: pride.DynamicVars.Damage.BaseValue * 10m),
            effects: [Attack("PRIDE_ATTACK", pride.DynamicVars.Damage.BaseValue, 10)],
            lifecycle: EnemyCardLifecycle.Exhaust,
            failureDisposition: EnemyCardFailureDisposition.Retain));

        InitialDefinitions = Array.AsReadOnly(initial.ToArray());
        DefinitionsById = all.ToDictionary(definition => definition.CardId);
        return;

        void AddInitial(EnemyCardDefinition definition)
        {
            initial.Add(definition);
            all.Add(definition);
        }
    }

    /// <summary>获取十三种初始牌的不可变定义顺序。</summary>
    public static IReadOnlyList<EnemyCardDefinition> InitialCardDefinitions => InitialDefinitions;

    /// <summary>获取包含初始牌和衍生牌的完整定义目录。</summary>
    public static IReadOnlyDictionary<EnemyCardId, EnemyCardDefinition> AllDefinitions => DefinitionsById;

    /// <summary>
    /// 按定义标识创建一个尚未绑定战斗身份的新实例。
    /// </summary>
    /// <param name="cardId">已注册测试牌定义标识。</param>
    /// <returns>独立敌人卡牌实例。</returns>
    public static BaseEnemyCard CreateCard(EnemyCardId cardId) =>
        new CatalogEnemyCard(DefinitionsById.TryGetValue(cardId, out EnemyCardDefinition? definition)
            ? definition
            : throw new KeyNotFoundException($"未知测试敌人卡牌定义 {cardId}。"));

    /// <summary>
    /// 创建十三种初始牌各两份的固定模板工厂。
    /// </summary>
    /// <returns>保持定义顺序与副本顺序的独立实例工厂。</returns>
    public static IReadOnlyList<Func<BaseEnemyCard>> CreateInitialDeckFactories() =>
        InitialDefinitions
            .SelectMany(definition => Enumerable.Range(0, 2)
                .Select(_ => (Func<BaseEnemyCard>)(() => new CatalogEnemyCard(definition))))
            .ToArray();

    /// <summary>创建稳定测试敌人卡牌标识。</summary>
    private static EnemyCardId CardId(string suffix) => new($"{IdPrefix}{suffix}");

    /// <summary>读取玩家牌规范动态变量。</summary>
    private static decimal Var(CardModel model, string name) => model.DynamicVars[name].BaseValue;

    /// <summary>创建标准全体攻击节点。</summary>
    private static IEnemyCardEffectNode Attack(string id, decimal damage, int hits = 1) =>
        new EnemyAttackAllEffect($"CARD_INTENT:{id}", damage, hits);

    /// <summary>创建标准敌人格挡节点。</summary>
    private static IEnemyCardEffectNode Block(string id, decimal block) =>
        new EnemyBlockEffect($"CARD_INTENT:{id}", block);

    /// <summary>创建标准敌人自身 Power 节点。</summary>
    private static IEnemyCardEffectNode SelfPower<TPower>(string id, decimal amount)
        where TPower : PowerModel, new() =>
        new EnemySelfPowerEffect<TPower>($"CARD_INTENT:{id}", amount);

    /// <summary>创建标准全体玩家 Power 节点。</summary>
    private static IEnemyCardEffectNode AllPlayersPower<TPower>(string id, decimal amount)
        where TPower : PowerModel, new() =>
        new EnemyAllPlayersPowerEffect<TPower>($"CARD_INTENT:{id}", amount);

    /// <summary>按玩家牌规范伤害创建攻击定义。</summary>
    private static EnemyCardDefinition AttackDefinition(
        string suffix,
        CardModel model,
        EnemyCardTag tags) =>
        Define(
            suffix,
            model,
            tags,
            new EnemyCardScoreProfile(attack: model.DynamicVars.Damage.BaseValue),
            effects: [Attack($"{suffix}_ATTACK", model.DynamicVars.Damage.BaseValue)]);

    /// <summary>按玩家牌规范格挡创建防御定义。</summary>
    private static EnemyCardDefinition BlockDefinition(
        string suffix,
        CardModel model,
        EnemyCardTag tags) =>
        Define(
            suffix,
            model,
            tags,
            new EnemyCardScoreProfile(block: model.DynamicVars.Block.BaseValue),
            effects: [Block($"{suffix}_BLOCK", model.DynamicVars.Block.BaseValue)]);

    /// <summary>创建一项完整不可变测试卡定义。</summary>
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
        string descriptionOverride = "") =>
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
            descriptionOverride: descriptionOverride);

    /// <summary>承载目录定义且不包含额外对象状态的敌人卡牌实例。</summary>
    private sealed class CatalogEnemyCard : BaseEnemyCard
    {
        /// <summary>从共享不可变定义创建独立运行时实例。</summary>
        public CatalogEnemyCard(EnemyCardDefinition definition) : base(definition)
        {
        }
    }
}
