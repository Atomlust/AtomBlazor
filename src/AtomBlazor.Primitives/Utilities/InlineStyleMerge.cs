namespace AtomBlazor.Primitives.Utilities;

/// <summary>
/// 把组件自己的内联样式与消费者经 <c>AdditionalAttributes</c> 传入的样式合并。
/// <para>
/// Razor 按源码顺序应用特性，<c>@attributes</c> 展开会在该指令所处的位置生效。
/// 因此写成 <c>style="@Merged" @attributes="AdditionalAttributes"</c> 的元素，
/// 会先算好合并结果、随后又把它丢掉：消费者的原始 <c>style</c> 后到并取而代之。
/// 改为展开 <see cref="WithoutStyle"/> 即可保住合并后的值。
/// </para>
/// <para>
/// <see cref="PrimitiveBase"/> 已把下面两个操作以实例成员的形式暴露给继承它的组件；
/// 这里是共享实现，供不继承它的组件使用。
/// </para>
/// </summary>
internal static class InlineStyleMerge
{
    /// <summary>
    /// 返回组件样式在前、消费者样式在后的合并结果：声明冲突时消费者获胜，
    /// 其余情况两者叠加。两侧都可能缺失。
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
    /// 返回移除了 <c>style</c> 之后的同一份特性字典，供与合并样式一起展开使用。
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
