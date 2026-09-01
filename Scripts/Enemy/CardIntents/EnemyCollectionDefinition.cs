using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

namespace STS2_Tomorin_Mod.Enemy.CardIntents;

/// <summary>
/// 描述一种可显式注册的敌人收藏品及其稳定素材和效果身份。
/// </summary>
public sealed class EnemyCollectionDefinition
{
    private readonly Func<CardModel> _cardModelResolver;
    private CardModel? _resolvedCardModel;

    /// <summary>
    /// 创建不可变收藏品定义。
    /// </summary>
    /// <param name="collectionId">跨目录和重连稳定的定义标识。</param>
    /// <param name="cardModelType">用于显示资源映射的玩家卡牌模型类型。</param>
    /// <param name="materialCardType">作为素材时呈现的卡牌类型。</param>
    /// <param name="isEpiphany">是否能作为作词通配素材。</param>
    /// <param name="effectProgramId">敌人适配效果程序的稳定标识。</param>
    /// <param name="cardModelResolver">从 ModelDb 获取已注册规范卡牌模型的解析器。</param>
    public EnemyCollectionDefinition(
        string collectionId,
        Type cardModelType,
        CardType materialCardType,
        bool isEpiphany,
        string effectProgramId,
        Func<CardModel> cardModelResolver)
    {
        if (string.IsNullOrWhiteSpace(collectionId))
        {
            throw new ArgumentException("收藏品定义标识不能为空。", nameof(collectionId));
        }

        if (string.IsNullOrWhiteSpace(effectProgramId))
        {
            throw new ArgumentException("收藏品效果程序标识不能为空。", nameof(effectProgramId));
        }

        CollectionId = collectionId;
        CardModelType = cardModelType ?? throw new ArgumentNullException(nameof(cardModelType));
        MaterialCardType = materialCardType;
        IsEpiphany = isEpiphany;
        EffectProgramId = effectProgramId;
        _cardModelResolver = cardModelResolver ?? throw new ArgumentNullException(nameof(cardModelResolver));
    }

    /// <summary>获取跨目录和重连稳定的定义标识。</summary>
    public string CollectionId { get; }

    /// <summary>获取用于显示资源映射的玩家卡牌模型类型。</summary>
    public Type CardModelType { get; }

    /// <summary>获取收藏品作为素材时呈现的卡牌类型。</summary>
    public CardType MaterialCardType { get; }

    /// <summary>获取收藏品是否为作词通配素材。</summary>
    public bool IsEpiphany { get; }

    /// <summary>获取敌人适配效果程序的稳定标识。</summary>
    public string EffectProgramId { get; }

    /// <summary>
    /// 从 ModelDb 取得用于预加载和展示的已注册规范模型；禁止调用卡牌构造函数创建替代实例。
    /// </summary>
    public CardModel ResolveCardModel()
    {
        CardModel? cached = Volatile.Read(ref _resolvedCardModel);
        if (cached is not null)
        {
            return cached;
        }

        CardModel model = _cardModelResolver() ??
                          throw new InvalidOperationException($"收藏品 {CollectionId} 的 CardModel 解析器返回了 null。");
        if (model.GetType() != CardModelType)
        {
            throw new InvalidOperationException(
                $"收藏品 {CollectionId} 声明卡面类型 {CardModelType}，但解析到了 {model.GetType()}。");
        }

        return Interlocked.CompareExchange(ref _resolvedCardModel, model, null) ?? model;
    }
}
