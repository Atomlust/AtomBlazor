namespace AtomBlazor;

/// <summary>
/// 把组件自己的内联样式与使用者经由 <c>AdditionalAttributes</c> 传入的样式合并。
/// <para>
/// Razor 按源码顺序应用属性，<c>@attributes</c> 展开就落在指令所在的位置。因此写成
/// <c>style="@Merged" @attributes="AdditionalAttributes"</c> 的元素会先算好合并值、
/// 再把它丢掉：使用者原始的 <c>style</c> 后到并顶替掉先前写入的一切。
/// 改为展开 <see cref="WithoutStyle"/> 就能保住合并后的值。
/// </para>
/// </summary>
internal static class InlineStyleMerge
{
    /// <summary>
    /// 返回组件样式在前、使用者样式在后的合并值：冲突时使用者的声明胜出，
    /// 不冲突时两者叠加。两侧都可能缺席。
    /// </summary>
    public static string? Merge(string? componentStyle, IReadOnlyDictionary<string, object>? attributes)
    {
        var consumerStyle = ConsumerStyle(attributes);

        if (string.IsNullOrEmpty(componentStyle))
        {
            return string.IsNullOrEmpty(consumerStyle) ? null : consumerStyle;
        }

        return string.IsNullOrEmpty(consumerStyle)
            ? componentStyle
            : $"{componentStyle}; {consumerStyle}";
    }

    /// <summary>
    /// 返回移除了 <c>style</c> 键的同一份属性字典，供与合并样式并列展开。
    /// </summary>
    public static Dictionary<string, object>? WithoutStyle(Dictionary<string, object>? attributes)
    {
        if (attributes is null || !attributes.ContainsKey("style"))
        {
            return attributes;
        }

        var filtered = new Dictionary<string, object>(attributes, StringComparer.Ordinal);
        filtered.Remove("style");
        return filtered.Count > 0 ? filtered : null;
    }

    private static string? ConsumerStyle(IReadOnlyDictionary<string, object>? attributes) =>
        attributes is not null && attributes.TryGetValue("style", out var style)
            ? style?.ToString()
            : null;
}
