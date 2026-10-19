namespace AtomBlazor;

/// <summary>
/// 定义一种币种的属性。
/// </summary>
public class CurrencyDefinition
{
    /// <summary>
    /// 获取或设置 ISO 4217 币种代码。
    /// </summary>
    public required string Code { get; init; }

    /// <summary>
    /// 获取或设置币种符号。
    /// </summary>
    public required string Symbol { get; init; }

    /// <summary>
    /// 获取或设置小数位数。
    /// </summary>
    public required int DecimalPlaces { get; init; }

    /// <summary>
    /// 获取或设置用于格式化的区域设置名称。
    /// </summary>
    public required string CultureName { get; init; }

    /// <summary>
    /// 获取或设置币种名称。
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// 获取或设置符号是否显示在金额之前。
    /// </summary>
    public bool SymbolBefore { get; init; } = true;
}
