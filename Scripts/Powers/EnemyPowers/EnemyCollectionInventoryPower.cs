using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;

namespace STS2_Tomorin_Mod.Powers;

/// <summary>
/// 将敌人权威收藏品队列投影为可见 Power，不直接修改收藏品库存。
/// </summary>
public sealed class EnemyCollectionInventoryPower : BasePowerModel
{
    private const string LocalizationKey = "STS2_TOMORIN_MOD-ENEMY_COLLECTION_INVENTORY_POWER";
    private const string EmptyQueueText = "—";
    private readonly List<string> _queueEntries = [];

    /// <inheritdoc />
    public override PowerType Type => PowerType.Buff;

    /// <inheritdoc />
    public override PowerStackType StackType => PowerStackType.Counter;

    /// <inheritdoc />
    public override bool AllowNegative => false;

    /// <inheritdoc />
    public override string CustomPackedIconPath =>
        "res://STS2_Tomorin_Mod/images/card_portraits/StarStone.png";

    /// <inheritdoc />
    public override string? CustomBigIconPath =>
        "res://STS2_Tomorin_Mod/images/card_portraits/big/StarStone.png";

    /// <inheritdoc />
    public override LocString Description => CreateDescription("description");

    /// <summary>
    /// 获取当前投影中的队列条目快照。
    /// </summary>
    public IReadOnlyList<string> QueueEntries => _queueEntries.AsReadOnly();

    /// <summary>
    /// 获取按实际顺序压缩后的队列摘要。
    /// </summary>
    public string QueueSummary => FormatQueue(_queueEntries);

    /// <summary>
    /// 将权威队列同步到所有者的收藏品 Power；数量为零时也会保留 Power。
    /// </summary>
    /// <param name="choiceContext">多人选择上下文。</param>
    /// <param name="owner">持有收藏品队列的敌人生物。</param>
    /// <param name="queueEntries">按权威顺序排列的收藏品显示名称或稳定标识。</param>
    /// <param name="source">触发本次同步的模型，可为空。</param>
    /// <returns>同步完成后的收藏品 Power。</returns>
    public static async Task<EnemyCollectionInventoryPower> SynchronizeAsync(
        PlayerChoiceContext choiceContext,
        Creature owner,
        IReadOnlyList<string> queueEntries,
        AbstractModel? source = null)
    {
        ArgumentNullException.ThrowIfNull(choiceContext);
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(queueEntries);

        EnemyCollectionInventoryPower? power = owner.HasPower<EnemyCollectionInventoryPower>()
            ? owner.GetPower<EnemyCollectionInventoryPower>()
            : null;

        if (power is null)
        {
            int initialAmount = Math.Max(1, queueEntries.Count);
            power = await PowerCmd.Apply<EnemyCollectionInventoryPower>(
                choiceContext,
                owner,
                initialAmount,
                owner,
                source as CardModel);

            if (power is null)
            {
                throw new InvalidOperationException("收藏品 Power 同步失败，游戏未返回已应用的 Power 实例。");
            }

            if (initialAmount != queueEntries.Count)
            {
                await PowerCmd.ModifyAmount(
                    choiceContext,
                    power,
                    queueEntries.Count - initialAmount,
                    owner,
                    source as CardModel);
            }
        }
        else
        {
            int amountDelta = queueEntries.Count - power.Amount;
            if (amountDelta != 0)
            {
                await PowerCmd.ModifyAmount(
                    choiceContext,
                    power,
                    amountDelta,
                    owner,
                    source as CardModel);
            }
        }

        power.UpdateProjection(queueEntries);
        return power;
    }

    /// <summary>
    /// 更新只读队列投影，不修改权威收藏品库存。
    /// </summary>
    /// <param name="queueEntries">按权威顺序排列的收藏品显示名称或稳定标识。</param>
    public void UpdateProjection(IReadOnlyList<string> queueEntries)
    {
        ArgumentNullException.ThrowIfNull(queueEntries);

        _queueEntries.Clear();
        foreach (string entry in queueEntries)
        {
            if (string.IsNullOrWhiteSpace(entry))
            {
                throw new ArgumentException("收藏品队列不能包含空白条目。", nameof(queueEntries));
            }

            _queueEntries.Add(entry);
        }

        InvokeDisplayAmountChanged();
    }

    /// <summary>
    /// 按输入顺序格式化收藏品队列，并且只合并相邻的相同条目。
    /// </summary>
    /// <param name="queueEntries">按实际顺序排列的收藏品条目。</param>
    /// <returns>适合显示在 Power 描述中的队列摘要。</returns>
    public static string FormatQueue(IReadOnlyList<string> queueEntries)
    {
        ArgumentNullException.ThrowIfNull(queueEntries);
        if (queueEntries.Count == 0)
        {
            return EmptyQueueText;
        }

        var segments = new List<string>();
        string current = ValidateEntry(queueEntries[0], queueEntries);
        int count = 1;

        for (int index = 1; index < queueEntries.Count; index++)
        {
            string next = ValidateEntry(queueEntries[index], queueEntries);
            if (StringComparer.Ordinal.Equals(current, next))
            {
                count++;
                continue;
            }

            segments.Add(FormatSegment(current, count));
            current = next;
            count = 1;
        }

        segments.Add(FormatSegment(current, count));
        return string.Join(" → ", segments);
    }

    /// <summary>
    /// 创建带当前队列摘要变量的本地化描述。
    /// </summary>
    /// <param name="suffix">本地化字段后缀。</param>
    /// <returns>包含队列变量的本地化字符串。</returns>
    private LocString CreateDescription(string suffix)
    {
        var description = new LocString("powers", $"{LocalizationKey}.{suffix}");
        description.Add("Queue", QueueSummary);
        return description;
    }

    /// <summary>
    /// 校验并返回单个队列条目。
    /// </summary>
    /// <param name="entry">待校验条目。</param>
    /// <param name="queueEntries">参数来源，用于异常信息。</param>
    /// <returns>非空白条目。</returns>
    private static string ValidateEntry(string entry, IReadOnlyList<string> queueEntries)
    {
        if (string.IsNullOrWhiteSpace(entry))
        {
            throw new ArgumentException("收藏品队列不能包含空白条目。", nameof(queueEntries));
        }

        return entry;
    }

    /// <summary>
    /// 将一个连续同类区段格式化为显示文本。
    /// </summary>
    /// <param name="entry">收藏品条目。</param>
    /// <param name="count">连续出现次数。</param>
    /// <returns>单个队列区段的显示文本。</returns>
    private static string FormatSegment(string entry, int count)
    {
        return count == 1 ? entry : $"{entry} ×{count}";
    }
}
