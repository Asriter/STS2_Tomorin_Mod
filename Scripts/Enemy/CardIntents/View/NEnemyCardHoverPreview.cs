using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.UI;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Pooling;

namespace STS2_Tomorin_Mod.Enemy.CardIntents.View;

/// <summary>
/// 管理整列唯一的池化放大 NCard，并以缩略牌中心为基准显示且保持输入穿透。
/// </summary>
public partial class NEnemyCardHoverPreview : Control
{
    private const float PreviewScale = 0.72f;
    private NCard? _previewCard;
    private BaseEnemyCard? _boundCard;
    private Action<string, Exception?>? _diagnosticSink;

    /// <summary>
    /// 创建不参与布局、鼠标和焦点处理的共享前景层。
    /// </summary>
    public NEnemyCardHoverPreview()
    {
        Name = nameof(NEnemyCardHoverPreview);
        MouseFilter = MouseFilterEnum.Ignore;
        FocusMode = FocusModeEnum.None;
        ZIndex = 100;
        SetProcessInput(false);
        SetProcessUnhandledInput(false);
        SetProcessUnhandledKeyInput(false);
    }

    /// <summary>获取当前是否持有并显示有效的放大卡牌。</summary>
    public bool IsShowing => GodotObject.IsInstanceValid(_previewCard) && _previewCard!.Visible;

    /// <summary>获取当前放大预览绑定的稳定实例键；未显示时为空。</summary>
    public EnemyCardInstanceKey? HoveredCardKey => _boundCard?.InstanceKey;

    /// <summary>
    /// 懒取得或换绑共享放大牌，并将其中心定位到命中的缩略牌中心。
    /// </summary>
    /// <param name="card">被悬停的领域卡牌。</param>
    /// <param name="thumbnailRect">缩略牌全局命中矩形。</param>
    /// <param name="diagnosticSink">单卡描述兼容错误接收器。</param>
    public void ShowCard(
        BaseEnemyCard card,
        Rect2 thumbnailRect,
        Action<string, Exception?> diagnosticSink)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(diagnosticSink);
        _diagnosticSink = diagnosticSink;
        EnsurePreviewCard(card);

        if (!ReferenceEquals(_boundCard, card))
        {
            _boundCard = card;
            BindVisuals(card);
        }

        _previewCard!.Visible = true;
        _previewCard.PivotOffset = _previewCard.Size / 2f;
        _previewCard.Scale = Vector2.One * PreviewScale;
        // Godot Control 围绕 PivotOffset 缩放；未缩放尺寸的一半才是保持视觉中心不偏移的原点补偿。
        _previewCard.GlobalPosition = thumbnailRect.GetCenter() - _previewCard.Size / 2f;
    }

    /// <summary>
    /// 取得当前放大牌的全局矩形，供父视图在离开缩略牌后继续保持 Hover。
    /// </summary>
    public bool TryGetPreviewGlobalRect(out Rect2 rect)
    {
        rect = default;
        if (!IsShowing || !_previewCard!.IsInsideTree())
        {
            return false;
        }

        return NEnemyIntentCardSlot.TryGetScaledGlobalRect(_previewCard, out rect);
    }

    /// <summary>
    /// 隐藏时立即解除绑定并把共享 NCard 归还 NodePool，避免池化卡面残留覆写文本。
    /// </summary>
    public void HideAndRelease()
    {
        if (GodotObject.IsInstanceValid(_previewCard))
        {
            _previewCard!.Visible = false;
            _previewCard.GetParent()?.RemoveChild(_previewCard);
            NodePool.Free(_previewCard);
        }

        _previewCard = null;
        _boundCard = null;
        _diagnosticSink = null;
    }

    /// <summary>
    /// 离开场景树时幂等归还共享预览卡牌。
    /// </summary>
    public override void _ExitTree()
    {
        HideAndRelease();
        base._ExitTree();
    }

    /// <summary>
    /// 首次悬停时从原版池取得 NCard，并关闭其全部交互与高亮。
    /// </summary>
    private void EnsurePreviewCard(BaseEnemyCard card)
    {
        if (GodotObject.IsInstanceValid(_previewCard))
        {
            return;
        }

        NCard previewCard = NCard.Create(card.CardModel, ModelVisibility.Visible) ??
                            throw new InvalidOperationException("原版 NCard.Create 未返回 Hover 预览节点。");
        _previewCard = previewCard;
        AddChild(previewCard);
        previewCard.SetAnchorsAndOffsetsPreset(LayoutPreset.TopLeft, LayoutPresetMode.KeepSize);
        NEnemyIntentCardSlot.DisableInteractionRecursive(previewCard);
        _boundCard = null;
    }

    /// <summary>
    /// 先执行原版视觉刷新以清理池化状态，再在描述非空时覆写专用标签。
    /// </summary>
    private void BindVisuals(BaseEnemyCard card)
    {
        if (_previewCard is null)
        {
            throw new InvalidOperationException("共享 Hover 卡牌尚未创建。");
        }

        _previewCard.Model = card.CardModel;
        _previewCard.UpdateVisuals(PileType.None, CardPreviewMode.None);
        _previewCard.KillRarityGlow();
        if (GodotObject.IsInstanceValid(_previewCard.CardHighlight))
        {
            _previewCard.CardHighlight.Visible = false;
        }

        string? overrideText = EnemyCardDescriptionPresenter.BuildOverrideText(card.DescriptionOverride);
        if (overrideText is null)
        {
            return;
        }

        try
        {
            // 通过 Godot 属性写入以兼容游戏程序集与模组资源中同名 MegaRichTextLabel 的类型边界。
            _previewCard.GetNode<Node>("%DescriptionLabel").Set("text", overrideText);
        }
        catch (Exception exception)
        {
            _diagnosticSink?.Invoke($"卡牌 {card.InstanceKey} 的 Hover 预览无法应用描述覆写，已保留原版描述。", exception);
        }
    }
}
