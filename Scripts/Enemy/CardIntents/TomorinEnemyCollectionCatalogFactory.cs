using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using STS2_Tomorin_Mod.Cards.Collections;
using STS2_Tomorin_Mod.Powers;

namespace STS2_Tomorin_Mod.Enemy.CardIntents;

/// <summary>为开发目录与影灯正式目录构造同一套收藏品定义和敌人效果程序。</summary>
public static class TomorinEnemyCollectionCatalogFactory
{
    public const string BrokenNoteId = "STS2_TOMORIN_MOD:COLLECTION_BROKEN_NOTE";
    public const string ColdRedTeaId = "STS2_TOMORIN_MOD:COLLECTION_COLD_RED_TEA";
    public const string CrumpledPaperId = "STS2_TOMORIN_MOD:COLLECTION_CRUMPLED_PAPER";
    public const string LeftoverBuffetId = "STS2_TOMORIN_MOD:COLLECTION_LEFTOVER_BUFFET";
    public const string MidnightCoffeeId = "STS2_TOMORIN_MOD:COLLECTION_MIDNIGHT_COFFEE";
    public const string StarStoneId = "STS2_TOMORIN_MOD:COLLECTION_STAR_STONE";

    public static EnemyCollectionCatalog Create() => new(
    [
        Definition<BrokenNote>(BrokenNoteId, false, "COLLECTION:BROKEN_NOTE"),
        Definition<ColdRedTea>(ColdRedTeaId, false, "COLLECTION:COLD_RED_TEA"),
        Definition<CrumpledPaper>(CrumpledPaperId, false, "COLLECTION:CRUMPLED_PAPER"),
        Definition<LeftoverBuffet>(LeftoverBuffetId, false, "COLLECTION:LEFTOVER_BUFFET"),
        Definition<MidnightCoffee>(MidnightCoffeeId, false, "COLLECTION:MIDNIGHT_COFFEE"),
        Definition<StarStone>(StarStoneId, true, "COLLECTION:STAR_STONE")
    ]);

    public static IReadOnlyDictionary<string, EnemyCollectionEffectProgram> CreateEffectPrograms()
    {
        BrokenNote brokenNote = ModelDb.Card<BrokenNote>();
        ColdRedTea coldRedTea = ModelDb.Card<ColdRedTea>();
        return new Dictionary<string, EnemyCollectionEffectProgram>(StringComparer.Ordinal)
        {
            ["COLLECTION:BROKEN_NOTE"] = new(
                "COLLECTION:BROKEN_NOTE",
                [
                    new EnemyBlockEffect(
                        "COLLECTION:BROKEN_NOTE:BLOCK",
                        brokenNote.DynamicVars.Block.BaseValue),
                    new EnemySelfPowerEffect<BrokenNotePower>(
                        "COLLECTION:BROKEN_NOTE:POWER",
                        decimal.One)
                ]),
            ["COLLECTION:COLD_RED_TEA"] = new(
                "COLLECTION:COLD_RED_TEA",
                [
                    new EnemyAllPlayersPowerEffect<WeakPower>(
                        "COLLECTION:COLD_RED_TEA:WEAK",
                        coldRedTea.DynamicVars["WeakPower"].BaseValue),
                    new EnemyAllPlayersPowerEffect<CustomConstrictPower>(
                        "COLLECTION:COLD_RED_TEA:CONSTRICT",
                        coldRedTea.DynamicVars["CustomConstrictPower"].BaseValue)
                ]),
            ["COLLECTION:CRUMPLED_PAPER"] = new(
                "COLLECTION:CRUMPLED_PAPER",
                specialResolutionKind: EnemyCollectionSpecialResolutionKind.DrawAndExecuteImmediateCard),
            ["COLLECTION:LEFTOVER_BUFFET"] = new(
                "COLLECTION:LEFTOVER_BUFFET",
                specialResolutionKind: EnemyCollectionSpecialResolutionKind.RecoverConsumedEntry),
            ["COLLECTION:MIDNIGHT_COFFEE"] = new(
                "COLLECTION:MIDNIGHT_COFFEE",
                specialResolutionKind: EnemyCollectionSpecialResolutionKind.DrawAndExecuteImmediateCard),
            ["COLLECTION:STAR_STONE"] = new("COLLECTION:STAR_STONE")
        };
    }

    private static EnemyCollectionDefinition Definition<TCardModel>(
        string id,
        bool isEpiphany,
        string effectProgramId)
        where TCardModel : CardModel =>
        new(
            id,
            typeof(TCardModel),
            CardType.Status,
            isEpiphany,
            effectProgramId,
            static () => ModelDb.Card<TCardModel>());
}
