using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using STS2_Tomorin_Mod.Enemy.CardIntents.Presentation;
using STS2_Tomorin_Mod.Enemy.CardIntents.Test;

namespace STS2_Tomorin_Mod.Enemy.CardIntents.Intents;

/// <summary>
/// 作为原版 NIntent 生命周期根的复合卡牌列表标记，只暴露运行时只读状态而不修改牌堆。
/// </summary>
public sealed class CardListIntent : UnknownIntent
{
    private static readonly string[] CompositeUiAssetPaths =
    [
        "res://scenes/combat/intent.tscn",
        "res://scenes/cards/card.tscn",
        "res://scenes/cards/card_portrait_blur_material.tres",
        "res://scenes/cards/card_canvas_group_blur_material.tres",
        "res://scenes/cards/card_canvas_group_mask_blur_material.tres",
        "res://scenes/cards/card_canvas_group_mask_material.tres"
    ];

    private static readonly string[] TimelineCardAssetPaths = CardIntentTestCardCatalog.AllDefinitions.Values
        .Select(definition => definition.CardModel)
        .Concat(CardIntentTestCollectionCatalog.Catalog.Definitions
            .Select(definition => definition.ResolveCardModel()))
        .SelectMany(EnemyCardDeckRegistry.GetCardModelAssetPaths)
        .Where(path => !string.IsNullOrWhiteSpace(path))
        .Distinct(StringComparer.Ordinal)
        .ToArray();

    private readonly CardIntentMoveRuntime _runtime;

    /// <summary>
    /// 使用状态构造适配器创建固定标记，运行时仅在状态内部拥有可变权威数据。
    /// </summary>
    internal CardListIntent(CardIntentMoveRuntime runtime)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    }

    /// <summary>
    /// 获取当前冻结手牌的只读顺序，显示与攻击预览必须共同读取该顺序。
    /// </summary>
    public IReadOnlyList<BaseEnemyCard> CardList => _runtime.CardList;

    /// <summary>获取包含素材、收藏品与即时 Token 的真实展示时间线。</summary>
    public EnemyIntentTimeline IntentTimeline => _runtime.IntentTimeline;

    /// <summary>
    /// 获取所属牌组稳定标识，用于无副作用地查询预加载资源。
    /// </summary>
    public EnemyCardDeckId DeckId => _runtime.DeckId;

    /// <summary>
    /// 获取运行时是否已进入安全故障状态；故障时视图必须回退原版 Unknown。
    /// </summary>
    public bool IsFaulted => _runtime.IsFaulted;

    /// <summary>
    /// 获取逻辑层最近一次逐来源、逐重放、逐目标实时投影；未计算时为空。
    /// </summary>
    public LiveActionProjection? LiveProjection => _runtime.LiveProjection;

    /// <summary>
    /// 为复合视图从冻结计划取得当前目标对应的结构投影，不触发卡列事件或写入战斗状态。
    /// </summary>
    /// <param name="targets">原版 Intent 当前绑定的目标顺序。</param>
    /// <returns>可由逐牌展示构建器消费的完整结构投影。</returns>
    public LiveActionProjection GetLiveProjectionForDisplay(IReadOnlyList<Creature> targets) =>
        _runtime.GetLiveProjectionForDisplay(targets);

    /// <summary>获取所属行动状态的稳定标识，供不完整投影诊断关联。</summary>
    public string StateId => _runtime.State.StateId;

    /// <summary>
    /// 转发冻结手牌变化事件，供程序化视图进行幂等刷新。
    /// </summary>
    public event Action? CardListChanged
    {
        add => _runtime.CardListChanged += value;
        remove => _runtime.CardListChanged -= value;
    }

    /// <summary>
    /// 禁止根 Unknown Intent 生成自定义提示，缩略牌和下层原版 Intent 均保持无额外交互。
    /// </summary>
    public override bool HasIntentTip => false;

    /// <summary>
    /// 预加载根 Unknown、NCard、整副牌 CardModel 与逐牌原版 Intent 所需的全部资源。
    /// </summary>
    public override IEnumerable<string> AssetPaths => base.AssetPaths
        .Concat(CompositeUiAssetPaths)
        .Concat(EnemyCardDeckRegistry.GetAssetPaths(DeckId))
        .Concat(TimelineCardAssetPaths)
        .Concat(new SingleAttackIntent(0).AssetPaths)
        .Concat(new MultiAttackIntent(0, 1).AssetPaths)
        .Concat(new DefendIntent().AssetPaths)
        .Concat(new BuffIntent().AssetPaths)
        .Concat(new DebuffIntent(false).AssetPaths)
        .Concat(new UnknownIntent().AssetPaths)
        .Distinct(StringComparer.Ordinal);
}
