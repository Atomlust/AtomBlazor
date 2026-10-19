using System.Diagnostics.CodeAnalysis;

#pragma warning disable CA1716 // 命名空间与既有 Select 组件族重名
namespace AtomBlazor.Primitives;
#pragma warning restore CA1716

/// <summary>
/// 表示一个值/文本配对，供 Select 组件的 <c>Options</c> 参数使用。
/// 当绑定值与展示文本不同（例如绑定 ID 而显示名称）时使用它。
/// </summary>
/// <typeparam name="TValue">选项值的类型。</typeparam>
/// <param name="Value">选中该选项时绑定到 Select 的值。</param>
/// <param name="Text">在下拉列表与触发器中展示的文本。</param>
public record SelectOption<TValue>(TValue Value, string Text);

/// <summary>
/// 用于创建 <see cref="SelectOption{TValue}"/> 实例的工厂方法。
/// </summary>
public static class SelectOption
{
    /// <summary>
    /// 创建 <see cref="SelectOption{TValue}"/>，展示文本由 <c>ToString()</c> 推导。
    /// 适用于值与标签一致的场景。
    /// </summary>
    public static SelectOption<TValue> FromValue<TValue>(TValue value) =>
        new(value, value?.ToString() ?? "");
}

/// <summary>
/// 从 <see cref="SelectOption{TValue}"/> 集合查找展示文本的扩展方法。
/// 免去为每个集合手写辅助函数。
/// </summary>
public static class SelectOptionExtensions
{
    /// <summary>
    /// 获取单个值的展示文本。
    /// 返回匹配选项的 <see cref="SelectOption{TValue}.Text"/>，
    /// 找不到时回退到 <c>value.ToString()</c>。
    /// </summary>
    public static string GetText<TValue>(this IEnumerable<SelectOption<TValue>> options, TValue? value)
    {
        if (value is null)
        {
            return string.Empty;
        }

        return options
            .FirstOrDefault(o => EqualityComparer<TValue>.Default.Equals(o.Value, value))?.Text
            ?? value.ToString()
            ?? string.Empty;
    }

    /// <summary>
    /// 获取多个值的展示文本。
    /// 每个值返回匹配选项的 <see cref="SelectOption{TValue}.Text"/>。
    /// </summary>
    public static IEnumerable<string> GetTexts<TValue>(this IEnumerable<SelectOption<TValue>> options, IEnumerable<TValue>? values)
    {
        if (values is null)
        {
            return [];
        }

        return values.Select(v => options.GetText(v));
    }
}
