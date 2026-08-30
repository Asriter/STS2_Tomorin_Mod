using System.Text.RegularExpressions;

namespace STS2_Tomorin_Mod.Enemy.CardIntents;

/// <summary>
/// 表示敌人卡牌定义的稳定标识；该标识只用于运行时注册与快照，不使用本地化文本。
/// </summary>
public readonly record struct EnemyCardId
{
    private static readonly Regex ValidPattern = new(
        "^STS2_TOMORIN_MOD:[A-Z][A-Z0-9_]*$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>
    /// 使用规范化字符串创建敌人卡牌标识。
    /// </summary>
    /// <param name="value">形如 <c>STS2_TOMORIN_MOD:NAME</c> 的稳定字符串。</param>
    /// <exception cref="ArgumentException">字符串为空或不符合命名空间格式时抛出。</exception>
    public EnemyCardId(string value)
    {
        if (!IsValidValue(value))
        {
            throw new ArgumentException(
                "敌人卡牌标识必须使用 STS2_TOMORIN_MOD:NAME 格式，且 NAME 只能包含大写字母、数字和下划线。",
                nameof(value));
        }

        Value = value;
    }

    /// <summary>
    /// 获取用于注册和快照的规范化字符串。
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// 获取当前值是否为有效的非默认标识。
    /// </summary>
    public bool IsValid => IsValidValue(Value);

    /// <summary>
    /// 尝试把字符串解析为敌人卡牌标识。
    /// </summary>
    /// <param name="value">待解析字符串。</param>
    /// <param name="cardId">解析成功时返回稳定标识。</param>
    /// <returns>字符串合法时为 <see langword="true"/>。</returns>
    public static bool TryParse(string? value, out EnemyCardId cardId)
    {
        if (!IsValidValue(value))
        {
            cardId = default;
            return false;
        }

        cardId = new EnemyCardId(value!);
        return true;
    }

    /// <summary>
    /// 把字符串解析为敌人卡牌标识。
    /// </summary>
    /// <param name="value">待解析字符串。</param>
    /// <returns>已校验的稳定标识。</returns>
    public static EnemyCardId Parse(string value) => new(value);

    /// <summary>
    /// 返回稳定标识字符串；默认值返回空字符串，便于诊断未初始化值。
    /// </summary>
    /// <returns>规范化字符串或空字符串。</returns>
    public override string ToString() => Value ?? string.Empty;

    /// <summary>
    /// 校验原始字符串是否符合模组敌人卡牌命名空间规则。
    /// </summary>
    /// <param name="value">待校验字符串。</param>
    /// <returns>格式合法时为 <see langword="true"/>。</returns>
    private static bool IsValidValue(string? value) =>
        !string.IsNullOrWhiteSpace(value) && ValidPattern.IsMatch(value);
}
