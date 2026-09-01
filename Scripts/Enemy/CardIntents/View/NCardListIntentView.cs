using Godot;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.Nodes.Combat;
using STS2_Tomorin_Mod.Enemy.CardIntents.Intents;
using STS2_Tomorin_Mod.Enemy.CardIntents.Presentation;
using STS2_Tomorin_Mod.Patch;

namespace STS2_Tomorin_Mod.Enemy.CardIntents.View;

/// <summary>
/// 在原版 NIntent 根下显示按实例键增量复用、以角色头顶为中心向两侧增长的逐牌 Intent 卡列。
/// </summary>
public partial class NCardListIntentView : Control
{
    private const float CardWidth = 96f;
    private const float CardColumnHeight = 216f;
    private const float CardSpacing = 6f;
    private const float GlobalStatusHeight = 72f;

    private readonly Dictionary<EnemyIntentDisplayKey, NEnemyIntentCardSlot> _slotsByKey = [];
    private readonly List<NEnemyIntentCardSlot> _orderedSlots = [];
    private readonly HashSet<string> _reportedProjectionFingerprints = new(StringComparer.Ordinal);
    private Control? _centerAnchor;
    private HBoxContainer? _cardRow;
    private Control? _projectionStatusHost;
    private NIntent? _globalUnknownNode;
    private Control? _hoverLayer;
    private NEnemyCardHoverPreview? _hoverPreview;
    private CardListIntent? _boundIntent;
    private Creature? _owner;
    private Creature? _localPlayerCreature;
    private Creature[] _targets = [];
    private long _bindingGeneration;
    private long _queuedAttackRefreshGeneration = -1;
    private bool _attackRefreshQueued;

    /// <summary>
    /// 获取当前绑定是否拥有可显示的非故障公开卡列；投影不完整仍保留已知逐牌显示。
    /// </summary>
    public bool HasDisplayableCards =>
        _boundIntent is { IsFaulted: false } && _boundIntent.IntentTimeline.Entries.Count > 0;

    /// <summary>
    /// 创建保持鼠标点击穿透的复合视图根，并启用中央 Hover 与延迟刷新帧循环。
    /// </summary>
    public NCardListIntentView()
    {
        Name = nameof(NCardListIntentView);
        MouseFilter = MouseFilterEnum.Ignore;
        FocusMode = FocusModeEnum.None;
        SetProcessInput(false);
        SetProcessUnhandledInput(false);
        SetProcessUnhandledKeyInput(false);
        SetAnchorsPreset(LayoutPreset.Center);
        OffsetLeft = 0f;
        OffsetRight = 0f;
        OffsetTop = -CardColumnHeight;
        OffsetBottom = GlobalStatusHeight;
        SetProcess(true);
    }

    /// <summary>
    /// 幂等绑定复合 Intent、怪物和目标，切换上下文时完整迁移事件订阅并立即刷新逐牌显示。
    /// </summary>
    public void Bind(CardListIntent intent, Creature owner, IEnumerable<Creature> targets)
    {
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(targets);
        if (intent.IsFaulted)
        {
            throw new InvalidOperationException("故障 CardListIntent 不应创建自定义卡牌视图。");
        }

        Creature[] frozenTargets = targets.ToArray();
        Creature? localPlayerCreature = ResolveLocalPlayerCreature(owner);
        bool bindingChanged = !ReferenceEquals(_boundIntent, intent) ||
                              !ReferenceEquals(_owner, owner) ||
                              !ReferenceEquals(_localPlayerCreature, localPlayerCreature);
        if (bindingChanged)
        {
            UnsubscribeFromBoundContexts();
            _bindingGeneration++;
            _boundIntent = intent;
            _owner = owner;
            _localPlayerCreature = localPlayerCreature;
            SubscribeToBoundContexts();
        }

        _targets = frozenTargets;
        EnsureStructure();
        if (!HasDisplayableCards)
        {
            ClearSlots();
            SetGlobalUnknownVisible(false);
            _hoverPreview!.HideAndRelease();
            Visible = false;
            return;
        }

        try
        {
            RefreshPresentation();
            Visible = true;
        }
        catch
        {
            Unbind();
            throw;
        }
    }

    /// <summary>
    /// 每帧先合并处理 Power 事件请求，再用全局鼠标位置执行缩略优先的共享 Hover 命中。
    /// </summary>
    public override void _Process(double delta)
    {
        base._Process(delta);
        TryAttachDeferredLocalPlayer();
        FlushQueuedAttackIntentRefresh();
        UpdateHoverFromGlobalMouse();
    }

    /// <summary>
    /// 幂等解除卡列、怪物和本地玩家事件，清理全部动态槽位及共享 Hover。
    /// </summary>
    public void Unbind()
    {
        _bindingGeneration++;
        _attackRefreshQueued = false;
        _queuedAttackRefreshGeneration = -1;
        UnsubscribeFromBoundContexts();
        _boundIntent = null;
        _owner = null;
        _localPlayerCreature = null;
        _targets = [];
        ClearSlots();
        if (GodotObject.IsInstanceValid(_hoverPreview))
        {
            _hoverPreview!.HideAndRelease();
        }

        SetGlobalUnknownVisible(false);
        Visible = false;
    }

    /// <summary>
    /// 离开场景树时解除所有领域事件并释放全局 Unknown 及共享预览节点。
    /// </summary>
    public override void _ExitTree()
    {
        Unbind();
        ReleaseGlobalUnknownNode();
        base._ExitTree();
    }

    /// <summary>
    /// 首次需要时构造居中卡列、全局 Unknown Host 和独立 Hover 前景层。
    /// </summary>
    private void EnsureStructure()
    {
        if (GodotObject.IsInstanceValid(_centerAnchor))
        {
            return;
        }

        _centerAnchor = new Control
        {
            Name = "CenterAnchor",
            MouseFilter = MouseFilterEnum.Ignore,
            FocusMode = FocusModeEnum.None
        };
        _centerAnchor.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_centerAnchor);

        _cardRow = new HBoxContainer
        {
            Name = "CardRow",
            MouseFilter = MouseFilterEnum.Ignore,
            FocusMode = FocusModeEnum.None,
            Alignment = BoxContainer.AlignmentMode.Begin
        };
        _cardRow.AddThemeConstantOverride("separation", (int)CardSpacing);
        _centerAnchor.AddChild(_cardRow);
        _cardRow.SetAnchorsAndOffsetsPreset(LayoutPreset.TopWide);

        _projectionStatusHost = new Control
        {
            Name = "ProjectionStatusHost",
            MouseFilter = MouseFilterEnum.Ignore,
            FocusMode = FocusModeEnum.None,
            Position = new Vector2(0f, CardColumnHeight),
            CustomMinimumSize = new Vector2(0f, GlobalStatusHeight)
        };
        _centerAnchor.AddChild(_projectionStatusHost);

        _hoverLayer = new Control
        {
            Name = "HoverLayer",
            MouseFilter = MouseFilterEnum.Ignore,
            FocusMode = FocusModeEnum.None,
            ZIndex = 100
        };
        _hoverLayer.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_hoverLayer);

        _hoverPreview = new NEnemyCardHoverPreview();
        _hoverLayer.AddChild(_hoverPreview);
    }

    /// <summary>
    /// 从冻结计划生成最新纯展示模型，并按实例键协调槽位、全局 Unknown 与投影诊断。
    /// </summary>
    private void RefreshPresentation()
    {
        CardListIntent intent = _boundIntent ?? throw new InvalidOperationException("逐牌视图尚未绑定 CardListIntent。");
        Creature owner = _owner ?? throw new InvalidOperationException("逐牌视图尚未绑定怪物实体。");
        LiveActionProjection projection;
        EnemyCardListPresentation presentation;
        try
        {
            projection = intent.GetLiveProjectionForDisplay(_targets);
            presentation = EnemyCardIntentPresentationBuilder.Build(intent.IntentTimeline, projection);
        }
        catch (Exception exception)
        {
            string diagnostic = $"状态 {intent.StateId} 无法从冻结计划构建逐牌展示：{exception.Message}";
            ReportProjectionFailureOnce($"BUILD|{intent.StateId}|{string.Join(',', intent.IntentTimeline.Entries.Select(entry => entry.DisplayKey))}",
                diagnostic, exception);
            presentation = CreateUnknownFallbackPresentation(intent.IntentTimeline, diagnostic);
        }

        ReconcileSlots(presentation, owner);
        SetGlobalUnknownVisible(presentation.RequiresGlobalUnknown);
        if (presentation.RequiresGlobalUnknown)
        {
            string sourceKeys = string.Join(',', presentation.Cards.Select(card => card.DisplayKey));
            string fingerprint = $"INCOMPLETE|{intent.StateId}|{sourceKeys}|{string.Join('|', presentation.Diagnostics)}";
            ReportProjectionFailureOnce(
                fingerprint,
                $"状态 {intent.StateId} 的逐牌投影不完整；来源 {sourceKeys}；{string.Join("；", presentation.Diagnostics)}",
                null);
        }
    }

    /// <summary>
    /// 投影构建异常时仍为所有公开牌保留缩略牌、单卡 Unknown 和全局 Unknown。
    /// </summary>
    private static EnemyCardListPresentation CreateUnknownFallbackPresentation(
        EnemyIntentTimeline timeline,
        string diagnostic)
    {
        return new EnemyCardListPresentation(
            timeline.Entries.Select(entry => new EnemyCardIntentPresentation(
                entry,
                [new EnemyUnknownPresentation(diagnostic)])),
            requiresGlobalUnknown: true,
            [diagnostic]);
    }

    /// <summary>
    /// 按 EnemyCardInstanceKey 复用、创建、移动和删除槽位，严格保持 CardList 的视觉顺序。
    /// </summary>
    private void ReconcileSlots(EnemyCardListPresentation presentation, Creature owner)
    {
        if (_cardRow is null)
        {
            throw new InvalidOperationException("动态 CardRow 尚未创建。");
        }

        HashSet<EnemyIntentDisplayKey> retainedKeys = presentation.Cards
            .Select(card => card.DisplayKey)
            .ToHashSet();
        foreach (EnemyIntentDisplayKey removedKey in _slotsByKey.Keys.Where(key => !retainedKeys.Contains(key)).ToArray())
        {
            NEnemyIntentCardSlot removedSlot = _slotsByKey[removedKey];
            _slotsByKey.Remove(removedKey);
            _cardRow.RemoveChild(removedSlot);
            removedSlot.Release();
            removedSlot.QueueFree();
        }

        _orderedSlots.Clear();
        for (int index = 0; index < presentation.Cards.Count; index++)
        {
            EnemyCardIntentPresentation cardPresentation = presentation.Cards[index];
            if (!_slotsByKey.TryGetValue(cardPresentation.DisplayKey, out NEnemyIntentCardSlot? slot))
            {
                slot = new NEnemyIntentCardSlot();
                _slotsByKey.Add(cardPresentation.DisplayKey, slot);
                _cardRow.AddChild(slot);
            }

            slot.Bind(cardPresentation, _targets, owner, ReportUiDiagnostic);
            _cardRow.MoveChild(slot, index);
            _orderedSlots.Add(slot);
        }

        UpdateDynamicWidth(presentation.Cards.Count);
    }

    /// <summary>
    /// 修改容器自然宽度和根水平边界，使卡列始终围绕原版 Intent 锚点对称增长。
    /// </summary>
    private void UpdateDynamicWidth(int cardCount)
    {
        Vector2 horizontalOffsets = CalculateHorizontalOffsets(cardCount);
        float width = horizontalOffsets.Y - horizontalOffsets.X;
        CustomMinimumSize = new Vector2(width, CardColumnHeight + GlobalStatusHeight);
        OffsetLeft = horizontalOffsets.X;
        OffsetRight = horizontalOffsets.Y;
        if (_cardRow is not null)
        {
            _cardRow.CustomMinimumSize = new Vector2(width, CardColumnHeight);
            _cardRow.Size = new Vector2(width, CardColumnHeight);
        }

        if (_projectionStatusHost is not null)
        {
            _projectionStatusHost.CustomMinimumSize = new Vector2(width, GlobalStatusHeight);
            _projectionStatusHost.Size = new Vector2(width, GlobalStatusHeight);
        }
    }

    /// <summary>
    /// 根据卡牌数量计算相对于原版角色头顶 Intent 中心的对称左右边界。
    /// </summary>
    private static Vector2 CalculateHorizontalOffsets(int cardCount)
    {
        float width = cardCount == 0
            ? 0f
            : cardCount * CardWidth + (cardCount - 1) * CardSpacing;
        float halfWidth = width / 2f;
        return new Vector2(-halfWidth, halfWidth);
    }

    /// <summary>
    /// 懒创建并切换卡列级原版 Unknown 节点，不隐藏已经成功映射的逐牌图标。
    /// </summary>
    private void SetGlobalUnknownVisible(bool visible)
    {
        if (!visible)
        {
            if (GodotObject.IsInstanceValid(_globalUnknownNode))
            {
                _globalUnknownNode!.Visible = false;
            }

            return;
        }

        if (_projectionStatusHost is null || _owner is null)
        {
            throw new InvalidOperationException("显示全局 Unknown 前缺少节点或怪物上下文。");
        }

        if (!GodotObject.IsInstanceValid(_globalUnknownNode))
        {
            _globalUnknownNode = NIntent.Create(0f);
            _globalUnknownNode.Name = "GlobalUnknownIntent";
            _globalUnknownNode.MouseFilter = MouseFilterEnum.Ignore;
            _globalUnknownNode.FocusMode = FocusModeEnum.None;
            _projectionStatusHost.AddChild(_globalUnknownNode);
            _globalUnknownNode.SetAnchorsAndOffsetsPreset(LayoutPreset.Center, LayoutPresetMode.KeepSize);
            NEnemyIntentCardSlot.DisableInteractionRecursive(_globalUnknownNode);
        }

        _globalUnknownNode.UpdateIntent(new UnknownIntent(), _targets, _owner);
        _globalUnknownNode.Visible = true;
    }

    /// <summary>
    /// 使用全局鼠标位置执行中央命中：缩略牌优先，随后预览矩形保持，否则清理预览。
    /// </summary>
    private void UpdateHoverFromGlobalMouse()
    {
        if (!Visible || !HasDisplayableCards || !GodotObject.IsInstanceValid(_hoverPreview))
        {
            _hoverPreview?.HideAndRelease();
            return;
        }

        Vector2 mousePosition = GetGlobalMousePosition();
        foreach (NEnemyIntentCardSlot slot in _orderedSlots)
        {
            if (slot.TryGetThumbnailGlobalRect(out Rect2 thumbnailRect) && thumbnailRect.HasPoint(mousePosition))
            {
                _hoverPreview!.ShowCard(
                    slot.CardModel,
                    slot.DescriptionOverride,
                    slot.DisplayKey,
                    thumbnailRect,
                    ReportUiDiagnostic);
                return;
            }
        }

        if (_hoverPreview!.TryGetPreviewGlobalRect(out Rect2 previewRect) && previewRect.HasPoint(mousePosition))
        {
            return;
        }

        _hoverPreview.HideAndRelease();
    }

    /// <summary>
    /// 响应冻结卡列或投影变化，沿同一增量路径刷新并通知 Patch 切换 Holder。
    /// </summary>
    private void OnCardListChanged()
    {
        try
        {
            if (!HasDisplayableCards)
            {
                ClearSlots();
                SetGlobalUnknownVisible(false);
                _hoverPreview?.HideAndRelease();
                Visible = false;
                NotifyRootDisplayabilityChanged(showComposite: false);
                return;
            }

            EnsureStructure();
            RefreshPresentation();
            Visible = true;
            NotifyRootDisplayabilityChanged(showComposite: true);
        }
        catch (Exception exception)
        {
            Creature? owner = _owner;
            if (owner is not null && GetParent() is NIntent intentNode)
            {
                NIntentCardListPatch.HandleDeferredViewFailure(intentNode, owner, exception);
                return;
            }

            Unbind();
        }
    }

    /// <summary>
    /// 怪物或本地玩家的任一 Power 事件只排队一次下一帧攻击 Intent 刷新。
    /// </summary>
    private void QueueAttackIntentRefresh()
    {
        _attackRefreshQueued = true;
        _queuedAttackRefreshGeneration = _bindingGeneration;
    }

    /// <summary>
    /// 下一帧校验绑定世代与节点有效性后，只更新现存攻击 Intent，不触碰计划或牌区。
    /// </summary>
    private void FlushQueuedAttackIntentRefresh()
    {
        if (!_attackRefreshQueued)
        {
            return;
        }

        long queuedGeneration = _queuedAttackRefreshGeneration;
        _attackRefreshQueued = false;
        _queuedAttackRefreshGeneration = -1;
        if (queuedGeneration != _bindingGeneration || _boundIntent is null || _owner is null || !IsInsideTree())
        {
            return;
        }

        try
        {
            foreach (NEnemyIntentCardSlot slot in _orderedSlots)
            {
                if (GodotObject.IsInstanceValid(slot))
                {
                    slot.RefreshAttackIntents();
                }
            }
        }
        catch (Exception exception)
        {
            if (GetParent() is NIntent intentNode)
            {
                NIntentCardListPatch.HandleDeferredViewFailure(intentNode, _owner, exception);
            }
        }
    }

    /// <summary>
    /// 解析当前客户端玩家实体；战斗上下文尚未建立时安全返回空且只订阅怪物。
    /// </summary>
    private static Creature? ResolveLocalPlayerCreature(Creature owner)
    {
        MegaCrit.Sts2.Core.Combat.ICombatState? combatState = owner.CombatState;
        if (combatState is null)
        {
            return null;
        }

        try
        {
            return LocalContext.GetMe(combatState)?.Creature;
        }
        catch (InvalidOperationException)
        {
            // 客户端玩家列表可能在 Intent 根创建时仍处于瞬时恢复阶段，稍后 Bind 会重新解析。
            return null;
        }
    }

    /// <summary>
    /// 本地玩家在 Intent 创建帧尚不可用时逐帧轻量重试，成功后补订阅并请求一次实时攻击刷新。
    /// </summary>
    private void TryAttachDeferredLocalPlayer()
    {
        if (_owner is null || _localPlayerCreature is not null || _boundIntent is null)
        {
            return;
        }

        Creature? resolved = ResolveLocalPlayerCreature(_owner);
        if (resolved is null)
        {
            return;
        }

        _localPlayerCreature = resolved;
        if (!ReferenceEquals(resolved, _owner))
        {
            SubscribePowerEvents(resolved);
        }

        _bindingGeneration++;
        QueueAttackIntentRefresh();
    }

    /// <summary>
    /// 订阅 CardList 及怪物、本地玩家的四类 Power 事件；同实体时避免重复订阅。
    /// </summary>
    private void SubscribeToBoundContexts()
    {
        if (_boundIntent is not null)
        {
            _boundIntent.CardListChanged += OnCardListChanged;
        }

        SubscribePowerEvents(_owner);
        if (!ReferenceEquals(_localPlayerCreature, _owner))
        {
            SubscribePowerEvents(_localPlayerCreature);
        }
    }

    /// <summary>
    /// 完整解除 CardList 及旧怪物、旧本地玩家事件，允许 owner 或客户端身份安全切换。
    /// </summary>
    private void UnsubscribeFromBoundContexts()
    {
        if (_boundIntent is not null)
        {
            _boundIntent.CardListChanged -= OnCardListChanged;
        }

        UnsubscribePowerEvents(_owner);
        if (!ReferenceEquals(_localPlayerCreature, _owner))
        {
            UnsubscribePowerEvents(_localPlayerCreature);
        }
    }

    /// <summary>
    /// 对一个实体订阅 Power 应用、增加、减少和移除事件。
    /// </summary>
    private void SubscribePowerEvents(Creature? creature)
    {
        if (creature is null)
        {
            return;
        }

        creature.PowerApplied += OnPowerApplied;
        creature.PowerIncreased += OnPowerIncreased;
        creature.PowerDecreased += OnPowerDecreased;
        creature.PowerRemoved += OnPowerRemoved;
    }

    /// <summary>
    /// 对一个实体解除全部四类 Power 事件。
    /// </summary>
    private void UnsubscribePowerEvents(Creature? creature)
    {
        if (creature is null)
        {
            return;
        }

        creature.PowerApplied -= OnPowerApplied;
        creature.PowerIncreased -= OnPowerIncreased;
        creature.PowerDecreased -= OnPowerDecreased;
        creature.PowerRemoved -= OnPowerRemoved;
    }

    /// <summary>Power 应用事件只请求合并刷新。</summary>
    private void OnPowerApplied(PowerModel power) => QueueAttackIntentRefresh();

    /// <summary>Power 增加事件只请求合并刷新。</summary>
    private void OnPowerIncreased(PowerModel power, int amount, bool showEffect) => QueueAttackIntentRefresh();

    /// <summary>Power 减少事件只请求合并刷新。</summary>
    private void OnPowerDecreased(PowerModel power, bool showEffect) => QueueAttackIntentRefresh();

    /// <summary>Power 移除事件只请求合并刷新。</summary>
    private void OnPowerRemoved(PowerModel power) => QueueAttackIntentRefresh();

    /// <summary>
    /// 通知 Patch 通过精确原版 Holder 桥在空列与复合显示之间切换。
    /// </summary>
    private void NotifyRootDisplayabilityChanged(bool showComposite)
    {
        if (_owner is Creature owner && GetParent() is NIntent intentNode)
        {
            NIntentCardListPatch.UpdateDeferredViewVisibility(intentNode, this, owner, showComposite);
        }
    }

    /// <summary>
    /// 清理并释放全部动态槽位，同时归还共享 Hover，保留容器供同一根节点复用。
    /// </summary>
    private void ClearSlots()
    {
        _hoverPreview?.HideAndRelease();
        foreach (NEnemyIntentCardSlot slot in _slotsByKey.Values)
        {
            if (GodotObject.IsInstanceValid(slot))
            {
                slot.GetParent()?.RemoveChild(slot);
                slot.Release();
                slot.QueueFree();
            }
        }

        _slotsByKey.Clear();
        _orderedSlots.Clear();
        UpdateDynamicWidth(0);
    }

    /// <summary>
    /// 对相同投影指纹只向怪物统一错误通道报告一次，不改变卡列显示或战斗状态。
    /// </summary>
    private void ReportProjectionFailureOnce(string fingerprint, string message, Exception? exception)
    {
        if (!_reportedProjectionFingerprints.Add(fingerprint))
        {
            return;
        }

        ReportUiDiagnostic(message, exception);
    }

    /// <summary>
    /// 将 UI 或投影诊断写入卡牌怪物日志通道；非本框架怪物则保持安全静默。
    /// </summary>
    private void ReportUiDiagnostic(string message, Exception? exception)
    {
        if (_owner?.Monster is BaseCardIntentMonsterModel monster)
        {
            string detail = exception is null ? string.Empty : $" {exception}";
            monster.ReportCardIntentError($"[CardListIntentUI] {message}{detail}");
        }
    }

    /// <summary>
    /// 释放卡列级非池化 NIntent 节点；重复调用和无效节点均安全忽略。
    /// </summary>
    private void ReleaseGlobalUnknownNode()
    {
        if (!GodotObject.IsInstanceValid(_globalUnknownNode))
        {
            _globalUnknownNode = null;
            return;
        }

        _globalUnknownNode!.GetParent()?.RemoveChild(_globalUnknownNode);
        _globalUnknownNode.QueueFree();
        _globalUnknownNode = null;
    }
}
