using System.Collections.ObjectModel;
using System.Runtime.ExceptionServices;

namespace STS2_Tomorin_Mod.Enemy.CardIntents;

/// <summary>
/// 保存可原子应用的收藏品可用区、已消耗区和下一实例序号快照。
/// </summary>
public sealed class EnemyCollectionInventorySnapshot
{
    /// <summary>
    /// 创建冻结库存快照；结构合法性在应用到权威库存时统一校验。
    /// </summary>
    /// <param name="available">按队列顺序排列的可用实例。</param>
    /// <param name="consumed">按消费顺序排列的已消耗实例。</param>
    /// <param name="nextSequence">下一次生成实例使用的单调序号。</param>
    public EnemyCollectionInventorySnapshot(
        IEnumerable<EnemyCollectionInstance> available,
        IEnumerable<EnemyCollectionInstance> consumed,
        long nextSequence)
    {
        ArgumentNullException.ThrowIfNull(available);
        ArgumentNullException.ThrowIfNull(consumed);
        Available = Array.AsReadOnly(available.ToArray());
        Consumed = Array.AsReadOnly(consumed.ToArray());
        NextSequence = nextSequence;
    }

    /// <summary>获取按队列顺序冻结的可用实例。</summary>
    public IReadOnlyList<EnemyCollectionInstance> Available { get; }

    /// <summary>获取按消费顺序冻结的已消耗实例。</summary>
    public IReadOnlyList<EnemyCollectionInstance> Consumed { get; }

    /// <summary>获取下一次生成实例使用的单调序号。</summary>
    public long NextSequence { get; }
}

/// <summary>
/// 作为收藏品可用队列和已消耗区域的唯一写入口，并支持事务克隆与原子应用。
/// </summary>
public sealed class EnemyCollectionInventory
{
    private readonly List<EnemyCollectionInstance> _available = [];
    private readonly List<EnemyCollectionInstance> _consumed = [];
    private readonly ReadOnlyCollection<EnemyCollectionInstance> _availableView;
    private readonly ReadOnlyCollection<EnemyCollectionInstance> _consumedView;
    private IReadOnlyList<Exception> _lastNotificationExceptions = Array.Empty<Exception>();

    /// <summary>
    /// 创建空收藏品库存。
    /// </summary>
    public EnemyCollectionInventory()
    {
        _availableView = _available.AsReadOnly();
        _consumedView = _consumed.AsReadOnly();
    }

    /// <summary>在每次成功库存写入后发布一次确定性变更通知。</summary>
    public event EventHandler? InventoryChanged;

    /// <summary>
    /// 获取最近一次库存通知中被隔离的订阅者异常；订阅者失败不会回滚已提交的权威状态。
    /// </summary>
    public IReadOnlyList<Exception> LastNotificationExceptions => _lastNotificationExceptions;

    /// <summary>获取按队列顺序排列的只读可用收藏品。</summary>
    public IReadOnlyList<EnemyCollectionInstance> Available => _availableView;

    /// <summary>获取按消费顺序排列的只读已消耗收藏品。</summary>
    public IReadOnlyList<EnemyCollectionInstance> Consumed => _consumedView;

    /// <summary>获取下一次生成实例使用的单调序号。</summary>
    public long NextSequence { get; private set; }

    /// <summary>
    /// 按队列尾部追加一个新生成的收藏品实例。
    /// </summary>
    /// <param name="definition">待实例化的已注册定义。</param>
    /// <returns>具有新稳定序号的收藏品实例。</returns>
    public EnemyCollectionInstance Append(EnemyCollectionDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        EnemyCollectionInstance instance = new(definition, NextSequence);
        checked
        {
            NextSequence++;
        }

        _available.Add(instance);
        RaiseInventoryChanged();
        return instance;
    }

    /// <summary>
    /// 将指定可用实例移动到已消耗区域尾部。
    /// </summary>
    /// <param name="instance">必须位于可用区的实例。</param>
    /// <returns>被移动的原实例。</returns>
    public EnemyCollectionInstance Consume(EnemyCollectionInstance instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        int index = FindById(_available, instance.CollectionInstanceId);
        if (index < 0)
        {
            throw new InvalidOperationException(
                $"收藏品实例 {instance.CollectionInstanceId} 不在可用队列中，不能消费。");
        }

        EnemyCollectionInstance authoritative = _available[index];
        _available.RemoveAt(index);
        _consumed.Add(authoritative);
        RaiseInventoryChanged();
        return authoritative;
    }

    /// <summary>
    /// 按稳定实例标识将可用收藏品移动到已消耗区域尾部。
    /// </summary>
    /// <param name="collectionInstanceId">待消费的稳定实例标识。</param>
    /// <returns>被移动的权威实例。</returns>
    public EnemyCollectionInstance Consume(string collectionInstanceId)
    {
        if (string.IsNullOrWhiteSpace(collectionInstanceId))
        {
            throw new ArgumentException("收藏品实例标识不能为空。", nameof(collectionInstanceId));
        }

        int index = FindById(_available, collectionInstanceId);
        if (index < 0)
        {
            throw new InvalidOperationException($"收藏品实例 {collectionInstanceId} 不在可用队列中，不能消费。");
        }

        return Consume(_available[index]);
    }

    /// <summary>
    /// 消费队列中最早满足条件的可用收藏品。
    /// </summary>
    /// <param name="predicate">合法收藏品条件。</param>
    /// <param name="instance">成功时返回被消费实例。</param>
    /// <returns>存在并消费合法实例时为 <see langword="true"/>。</returns>
    public bool TryConsumeFirst(
        Func<EnemyCollectionInstance, bool> predicate,
        out EnemyCollectionInstance? instance)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        int index = _available.FindIndex(item => predicate(item));
        if (index < 0)
        {
            instance = null;
            return false;
        }

        instance = Consume(_available[index]);
        return true;
    }

    /// <summary>
    /// 将指定已消耗实例从消耗区移到可用队列尾部。
    /// </summary>
    /// <param name="instance">必须位于已消耗区的实例。</param>
    /// <returns>被恢复的原实例。</returns>
    public EnemyCollectionInstance Recover(EnemyCollectionInstance instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        int index = FindById(_consumed, instance.CollectionInstanceId);
        if (index < 0)
        {
            throw new InvalidOperationException(
                $"收藏品实例 {instance.CollectionInstanceId} 不在已消耗区域中，不能恢复。");
        }

        EnemyCollectionInstance authoritative = _consumed[index];
        _consumed.RemoveAt(index);
        _available.Add(authoritative);
        RaiseInventoryChanged();
        return authoritative;
    }

    /// <summary>
    /// 按稳定实例标识将已消耗收藏品恢复到可用队列尾部。
    /// </summary>
    /// <param name="collectionInstanceId">待恢复的稳定实例标识。</param>
    /// <returns>被恢复的权威实例。</returns>
    public EnemyCollectionInstance Recover(string collectionInstanceId)
    {
        if (string.IsNullOrWhiteSpace(collectionInstanceId))
        {
            throw new ArgumentException("收藏品实例标识不能为空。", nameof(collectionInstanceId));
        }

        int index = FindById(_consumed, collectionInstanceId);
        if (index < 0)
        {
            throw new InvalidOperationException($"收藏品实例 {collectionInstanceId} 不在已消耗区域中，不能恢复。");
        }

        return Recover(_consumed[index]);
    }

    /// <summary>
    /// 捕获不随后续库存写入变化的冻结快照。
    /// </summary>
    /// <returns>当前可用区、已消耗区和下一序号的冻结副本。</returns>
    public EnemyCollectionInventorySnapshot CaptureSnapshot() =>
        new(_available, _consumed, NextSequence);

    /// <summary>
    /// 创建可独立变更的事务库存副本，不复制事件订阅者。
    /// </summary>
    /// <returns>与当前状态相同但后续写入互不影响的库存。</returns>
    public EnemyCollectionInventory CreateTransactionalClone()
    {
        EnemyCollectionInventory clone = new();
        if (!clone.TryApplySnapshot(CaptureSnapshot(), out string reason, notify: false))
        {
            throw new InvalidOperationException($"当前收藏品库存无法创建事务副本：{reason}");
        }

        return clone;
    }

    /// <summary>
    /// 全量验证后一次性应用库存快照，拒绝时保持当前状态不变。
    /// </summary>
    /// <param name="snapshot">待验证并应用的冻结快照。</param>
    /// <param name="reason">拒绝时返回结构诊断。</param>
    /// <returns>快照完整合法并已提交时为 <see langword="true"/>。</returns>
    public bool TryApplySnapshot(EnemyCollectionInventorySnapshot? snapshot, out string reason) =>
        TryApplySnapshot(snapshot, out reason, notify: true);

    /// <summary>
    /// 先完整验证库存目标，再在同一同步调用中提交库存与 owner；全部权威字段完成后才安全通知。
    /// </summary>
    /// <remarks>
    /// <paramref name="commitOwner"/> 抛出时先调用 <paramref name="rollbackOwner"/> 恢复 owner，
    /// 再恢复调用前库存且不通知。任一 rollback 失败会与原 commit 异常聚合，不能静默声称回滚成功。
    /// 库存观察者异常仍逐个隔离并记录，不从本方法传播。
    /// </remarks>
    internal bool TryCommitSnapshotAtomically(
        EnemyCollectionInventorySnapshot? snapshot,
        Action commitOwner,
        Action rollbackOwner,
        bool notify,
        out string reason)
    {
        ArgumentNullException.ThrowIfNull(commitOwner);
        ArgumentNullException.ThrowIfNull(rollbackOwner);
        reason = string.Empty;
        if (!TryValidateSnapshot(snapshot, out reason))
        {
            return false;
        }

        EnemyCollectionInventorySnapshot before = CaptureSnapshot();
        ApplyValidatedSnapshot(snapshot!);
        try
        {
            commitOwner();
        }
        catch (Exception commitException)
        {
            List<Exception> rollbackFailures = [];
            try
            {
                rollbackOwner();
            }
            catch (Exception ownerRollbackException)
            {
                rollbackFailures.Add(ownerRollbackException);
            }

            try
            {
                ApplyValidatedSnapshot(before);
            }
            catch (Exception inventoryRollbackException)
            {
                rollbackFailures.Add(inventoryRollbackException);
            }

            if (rollbackFailures.Count > 0)
            {
                throw new AggregateException(
                    "Compound commit 失败，且 owner 或 inventory rollback 未完整成功。",
                    new[] { commitException }.Concat(rollbackFailures));
            }

            ExceptionDispatchInfo.Capture(commitException).Throw();
            throw;
        }

        if (notify)
        {
            RaiseInventoryChanged();
        }

        return true;
    }

    /// <summary>
    /// 全量验证后一次性应用库存快照，并允许事务克隆抑制通知。
    /// </summary>
    /// <param name="snapshot">待验证并应用的冻结快照。</param>
    /// <param name="reason">拒绝时返回结构诊断。</param>
    /// <param name="notify">成功时是否发布库存变更事件。</param>
    /// <returns>快照完整合法并已提交时为 <see langword="true"/>。</returns>
    private bool TryApplySnapshot(
        EnemyCollectionInventorySnapshot? snapshot,
        out string reason,
        bool notify)
    {
        return TryCommitSnapshotAtomically(
            snapshot,
            static () => { },
            static () => { },
            notify,
            out reason);
    }

    /// <summary>
    /// 全量验证库存快照中的空值、实例唯一性和下一序号边界。
    /// </summary>
    /// <param name="snapshot">待验证快照。</param>
    /// <param name="reason">拒绝时返回结构诊断。</param>
    /// <returns>快照可安全原子应用时为 <see langword="true"/>。</returns>
    private static bool TryValidateSnapshot(
        EnemyCollectionInventorySnapshot? snapshot,
        out string reason)
    {
        if (snapshot is null)
        {
            reason = "收藏品库存快照缺失。";
            return false;
        }

        if (snapshot.NextSequence < 0)
        {
            reason = "收藏品库存下一序号不能为负数。";
            return false;
        }

        EnemyCollectionInstance[] all = snapshot.Available.Concat(snapshot.Consumed).ToArray();
        if (all.Any(instance => instance is null))
        {
            reason = "收藏品库存区域不能包含空实例。";
            return false;
        }

        if (all.Select(instance => instance.CollectionInstanceId).Distinct(StringComparer.Ordinal).Count() != all.Length ||
            all.Select(instance => instance.Sequence).Distinct().Count() != all.Length)
        {
            reason = "收藏品库存的可用区与已消耗区包含重复实例。";
            return false;
        }

        if (all.Any(instance => instance.CollectionInstanceId !=
                                $"{instance.Definition.CollectionId}@{instance.Sequence}"))
        {
            reason = "收藏品实例标识与定义和序号不一致。";
            return false;
        }

        long maximumSequence = all.Length == 0 ? -1 : all.Max(instance => instance.Sequence);
        if (snapshot.NextSequence <= maximumSequence)
        {
            reason = "收藏品库存下一序号没有越过现有实例序号。";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    /// <summary>只应用已经通过完整结构校验的快照，不发布通知。</summary>
    private void ApplyValidatedSnapshot(EnemyCollectionInventorySnapshot snapshot)
    {
        _available.Clear();
        _available.AddRange(snapshot.Available);
        _consumed.Clear();
        _consumed.AddRange(snapshot.Consumed);
        NextSequence = snapshot.NextSequence;
    }

    /// <summary>
    /// 按稳定实例标识查找区域中的权威索引。
    /// </summary>
    /// <param name="zone">待查找区域。</param>
    /// <param name="collectionInstanceId">稳定实例标识。</param>
    /// <returns>匹配索引；不存在时返回负一。</returns>
    private static int FindById(
        List<EnemyCollectionInstance> zone,
        string collectionInstanceId) =>
        zone.FindIndex(item => string.Equals(
            item.CollectionInstanceId,
            collectionInstanceId,
            StringComparison.Ordinal));

    /// <summary>
    /// 在成功写入后按标准事件模式发布一次库存变更通知。
    /// </summary>
    private void RaiseInventoryChanged()
    {
        EventHandler? handlers = InventoryChanged;
        if (handlers is null)
        {
            _lastNotificationExceptions = Array.Empty<Exception>();
            return;
        }

        List<Exception> failures = [];
        foreach (EventHandler handler in handlers.GetInvocationList().Cast<EventHandler>())
        {
            try
            {
                handler(this, EventArgs.Empty);
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }
        }

        _lastNotificationExceptions = Array.AsReadOnly(failures.ToArray());
    }
}
