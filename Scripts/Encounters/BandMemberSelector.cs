#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace STS2_Tomorin_Mod.Encounters;

/// <summary>
/// 表示乐队精英房可选择的原始成员身份。
/// </summary>
public enum BandMemberKind
{
    /// <summary>
    /// 表示 Anon 成员身份。
    /// </summary>
    Anon,

    /// <summary>
    /// 表示 Taki 成员身份。
    /// </summary>
    Taki,

    /// <summary>
    /// 表示 Soyo 成员身份。
    /// </summary>
    Soyo,

    /// <summary>
    /// 表示 Raana 成员身份。
    /// </summary>
    Raana
}

/// <summary>
/// 表示一次确定性的左右成员选择结果。
/// </summary>
public sealed class BandMemberSelection
{
    /// <summary>
    /// 使用两个不同的成员身份创建左右选择结果。
    /// </summary>
    /// <param name="left">位于玩家左侧的成员身份。</param>
    /// <param name="right">位于玩家右侧的成员身份。</param>
    /// <exception cref="ArgumentException">左右成员身份相同时抛出。</exception>
    public BandMemberSelection(BandMemberKind left, BandMemberKind right)
    {
        if (left == right)
        {
            throw new ArgumentException(
                $"BandMemberSelection 要求左右成员不同，但两侧均为 '{left}'。",
                nameof(right));
        }

        Left = left;
        Right = right;
    }

    /// <summary>
    /// 获取位于玩家左侧的成员身份。
    /// </summary>
    public BandMemberKind Left { get; }

    /// <summary>
    /// 获取位于玩家右侧的成员身份。
    /// </summary>
    public BandMemberKind Right { get; }
}

/// <summary>
/// 按稳定顺序选择乐队精英房左右成员，并提供可持久化的稳定名称映射。
/// </summary>
public static class BandMemberSelector
{
    private static readonly ReadOnlyCollection<BandMemberKind> CandidateOrder =
        Array.AsReadOnly(
        [
            BandMemberKind.Anon,
            BandMemberKind.Taki,
            BandMemberKind.Soyo,
            BandMemberKind.Raana
        ]);

    private static readonly ReadOnlyDictionary<BandMemberKind, string> MemberStableNames =
        new(
            new Dictionary<BandMemberKind, string>
            {
                [BandMemberKind.Anon] = "Anon",
                [BandMemberKind.Taki] = "Taki",
                [BandMemberKind.Soyo] = "Soyo",
                [BandMemberKind.Raana] = "Raana"
            });

    private static readonly ReadOnlyDictionary<string, BandMemberKind> StableNameMembers =
        new(
            new Dictionary<string, BandMemberKind>(StringComparer.Ordinal)
            {
                ["Anon"] = BandMemberKind.Anon,
                ["Taki"] = BandMemberKind.Taki,
                ["Soyo"] = BandMemberKind.Soyo,
                ["Raana"] = BandMemberKind.Raana
            });

    /// <summary>
    /// 获取成员选择使用的稳定候选顺序。
    /// </summary>
    public static IReadOnlyList<BandMemberKind> FixedOrder => CandidateOrder;

    /// <summary>
    /// 获取成员身份到持久化稳定名称的只读映射。
    /// </summary>
    public static IReadOnlyDictionary<BandMemberKind, string> StableNames => MemberStableNames;

    /// <summary>
    /// 根据当前局已遇到的原始 Boss 身份，确定性地选择不同的左右成员。
    /// </summary>
    /// <param name="encounteredMembers">当前局已遇到的原始 Boss 身份；传入 <see langword="null"/> 时按空集合处理。</param>
    /// <returns>按固定顺序形成的左右成员选择结果。</returns>
    /// <exception cref="ArgumentOutOfRangeException">输入包含未知成员身份时抛出。</exception>
    /// <exception cref="InvalidOperationException">内部候选表无法形成两个不同成员时抛出。</exception>
    public static BandMemberSelection Select(IEnumerable<BandMemberKind>? encounteredMembers)
    {
        var encountered = new HashSet<BandMemberKind>();
        if (encounteredMembers is not null)
        {
            foreach (var member in encounteredMembers)
            {
                if (!MemberStableNames.ContainsKey(member))
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(encounteredMembers),
                        member,
                        $"BandMemberSelector 收到未知成员身份 '{member}'（底层值：{Convert.ToInt64(member)}）。");
                }

                encountered.Add(member);
            }
        }

        var selected = new List<BandMemberKind>(capacity: 2);

        foreach (var candidate in CandidateOrder)
        {
            if (!encountered.Contains(candidate))
            {
                selected.Add(candidate);
                if (selected.Count == 2)
                {
                    break;
                }
            }
        }

        if (selected.Count < 2)
        {
            foreach (var candidate in CandidateOrder)
            {
                if (!selected.Contains(candidate))
                {
                    selected.Add(candidate);
                    if (selected.Count == 2)
                    {
                        break;
                    }
                }
            }
        }

        if (selected.Count != 2 || selected[0] == selected[1])
        {
            throw new InvalidOperationException(
                "BandMemberSelector 无法形成两个不同成员。" +
                $"候选数量={CandidateOrder.Count}，已遇到成员={DescribeMembers(encountered)}，" +
                $"已选择数量={selected.Count}。");
        }

        return new BandMemberSelection(selected[0], selected[1]);
    }

    /// <summary>
    /// 获取指定成员身份的持久化稳定名称。
    /// </summary>
    /// <param name="member">需要转换的成员身份。</param>
    /// <returns>区分大小写的稳定名称。</returns>
    /// <exception cref="ArgumentOutOfRangeException">成员身份未知时抛出。</exception>
    public static string GetStableName(BandMemberKind member)
    {
        if (MemberStableNames.TryGetValue(member, out var stableName))
        {
            return stableName;
        }

        throw new ArgumentOutOfRangeException(
            nameof(member),
            member,
            $"BandMemberSelector 无法为未知成员身份 '{member}'（底层值：{Convert.ToInt64(member)}）提供稳定名称。");
    }

    /// <summary>
    /// 尝试将区分大小写的持久化稳定名称解析为成员身份。
    /// </summary>
    /// <param name="stableName">待解析的稳定名称；数字枚举字符串不会被接受。</param>
    /// <param name="member">解析成功时返回对应的成员身份。</param>
    /// <returns>名称严格匹配已知稳定名称时返回 <see langword="true"/>，否则返回 <see langword="false"/>。</returns>
    public static bool TryParseStableName(string? stableName, out BandMemberKind member)
    {
        if (stableName is not null && StableNameMembers.TryGetValue(stableName, out member))
        {
            return true;
        }

        member = default;
        return false;
    }

    /// <summary>
    /// 将成员集合格式化为开发错误所需的上下文文本。
    /// </summary>
    /// <param name="members">需要描述的成员集合。</param>
    /// <returns>稳定且便于诊断的成员列表文本。</returns>
    private static string DescribeMembers(IEnumerable<BandMemberKind> members)
    {
        var builder = new StringBuilder();
        foreach (var member in CandidateOrder)
        {
            var containsMember = false;
            foreach (var encounteredMember in members)
            {
                if (encounteredMember == member)
                {
                    containsMember = true;
                    break;
                }
            }

            if (!containsMember)
            {
                continue;
            }

            if (builder.Length > 0)
            {
                builder.Append(',');
            }

            builder.Append(GetStableName(member));
        }

        return builder.Length == 0 ? "<empty>" : builder.ToString();
    }
}
