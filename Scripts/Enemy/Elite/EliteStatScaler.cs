#nullable enable

using System;

namespace STS2_Tomorin_Mod.Enemy.Elite;

/// <summary>
/// 为精英敌人的基础整数属性提供统一的向下取整缩放。
/// </summary>
public static class EliteStatScaler
{
    /// <summary>
    /// 将非负基础属性乘以正倍率，并返回向下取整后的整数结果。
    /// </summary>
    /// <param name="baseValue">需要缩放的非负基础属性。</param>
    /// <param name="multiplier">需要应用的正十进制倍率。</param>
    /// <returns>精确乘积向下取整后的整数。</returns>
    /// <exception cref="ArgumentOutOfRangeException">基础属性为负数或倍率不是正数时抛出。</exception>
    /// <exception cref="OverflowException">乘积无法用 <see cref="int"/> 表示时抛出。</exception>
    public static int ScaleDown(int baseValue, decimal multiplier)
    {
        if (baseValue < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(baseValue),
                baseValue,
                $"EliteStatScaler 不接受负基础属性：baseValue={baseValue}，multiplier={multiplier}。");
        }

        if (multiplier <= 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(multiplier),
                multiplier,
                $"EliteStatScaler 要求倍率为正数：baseValue={baseValue}，multiplier={multiplier}。");
        }

        decimal scaledValue;
        try
        {
            scaledValue = decimal.Floor(baseValue * multiplier);
        }
        catch (OverflowException exception)
        {
            throw new OverflowException(
                $"EliteStatScaler 计算乘积时溢出：baseValue={baseValue}，multiplier={multiplier}。",
                exception);
        }

        if (scaledValue > int.MaxValue)
        {
            throw new OverflowException(
                $"EliteStatScaler 缩放结果超出 Int32 范围：baseValue={baseValue}，" +
                $"multiplier={multiplier}，floorResult={scaledValue}。");
        }

        return decimal.ToInt32(scaledValue);
    }
}
