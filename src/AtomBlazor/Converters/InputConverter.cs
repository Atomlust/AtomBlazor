using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace AtomBlazor;

/// <summary>
/// 为 <see cref="InputField{TValue}"/> 组件提供类型转换。
/// </summary>
/// <remarks>
/// <para>
/// 在字符串表示（供 HTML input 元素使用）与强类型值之间双向转换。
/// 采用三级解析链：实例级 → 全局级 → 内置默认。
/// </para>
/// <para>
/// 内置默认覆盖：string、int、long、float、double、decimal、bool、
/// DateTime、DateTimeOffset、DateOnly、TimeOnly、Guid——及其全部可空变体。
/// </para>
/// </remarks>
/// <typeparam name="TValue">目标值类型。</typeparam>
/// <example>
/// <code>
/// // 全局注册（在 Program.cs 中）
/// InputConverter&lt;int?&gt;.GlobalGetFunc = input =&gt;
///     string.IsNullOrEmpty(input) ? null : int.Parse(input);
///
/// // 组件实例级
/// var converter = new InputConverter&lt;decimal&gt;
/// {
///     GetFunc = input =&gt; decimal.Parse(input, CultureInfo.CurrentCulture),
///     SetFunc = value =&gt; value.ToString("C", CultureInfo.CurrentCulture)
/// };
/// </code>
/// </example>
[SuppressMessage("Design", "CA1000:不要在泛型类型中声明静态成员",
    Justification = "全局转换器注册需要按类型参数隔离的静态状态（如 InputConverter<int>.GlobalGetFunc）。")]
public class InputConverter<TValue>
{
    private static volatile Func<string, TValue>? _globalGetFunc;
    private static volatile Func<TValue, string?>? _globalSetFunc;

    /// <summary>
    /// 获取或设置解析与格式化使用的区域文化。
    /// 解析顺序：Culture → GlobalCulture → CultureInfo.CurrentCulture。
    /// </summary>
    public CultureInfo? Culture { get; set; }

    private static volatile CultureInfo? _globalCulture;

    /// <summary>
    /// 获取或设置全局（应用级）的解析与格式化区域文化。
    /// </summary>
    [SuppressMessage("Design", "CA1000:不要在泛型类型中声明静态成员",
        Justification = "全局区域文化设置需要按类型参数隔离的静态状态。")]
    public static CultureInfo? GlobalCulture
    {
        get => _globalCulture;
        set => _globalCulture = value;
    }

    /// <summary>
    /// 获取或设置全局（应用级）的字符串到 <typeparamref name="TValue"/> 解析函数。
    /// </summary>
    /// <remarks>
    /// 在 Program.cs 启动时设置一次。作用于全部 <see cref="InputField{TValue}"/>
    /// 组件，除非被实例级 <see cref="GetFunc"/> 覆盖。
    /// </remarks>
    public static Func<string, TValue>? GlobalGetFunc
    {
        get => _globalGetFunc;
        set => _globalGetFunc = value;
    }

    /// <summary>
    /// 获取或设置全局（应用级）的 <typeparamref name="TValue"/> 到字符串格式化函数。
    /// </summary>
    /// <remarks>
    /// 在 Program.cs 启动时设置一次。作用于全部 <see cref="InputField{TValue}"/>
    /// 组件，除非被实例级 <see cref="SetFunc"/> 覆盖。
    /// </remarks>
    public static Func<TValue, string?>? GlobalSetFunc
    {
        get => _globalSetFunc;
        set => _globalSetFunc = value;
    }

    /// <summary>
    /// 获取或设置实例级的字符串到 <typeparamref name="TValue"/> 解析函数。
    /// </summary>
    /// <remarks>
    /// 在解析链中优先级最高，覆盖 <see cref="GlobalGetFunc"/> 与内置默认。
    /// </remarks>
    public Func<string, TValue>? GetFunc { get; set; }

    /// <summary>
    /// 获取或设置实例级的 <typeparamref name="TValue"/> 到字符串格式化函数。
    /// </summary>
    /// <remarks>
    /// 在解析链中优先级最高，覆盖 <see cref="GlobalSetFunc"/> 与内置默认。
    /// </remarks>
    public Func<TValue, string?>? SetFunc { get; set; }

    private CultureInfo ResolvedCulture => Culture ?? GlobalCulture ?? CultureInfo.CurrentCulture;

    /// <summary>
    /// 把字符串输入转换为 <typeparamref name="TValue"/>。
    /// </summary>
    /// <remarks>
    /// 解析顺序：<see cref="GetFunc"/> → <see cref="GlobalGetFunc"/> → 内置默认。
    /// 若目标类型没有可用转换器则抛出 <see cref="InvalidOperationException"/>。
    /// </remarks>
    /// <param name="input">来自输入元素的字符串值。</param>
    /// <returns>解析得到的 <typeparamref name="TValue"/>。</returns>
    /// <exception cref="FormatException">输入字符串不符合目标类型的有效格式。</exception>
    /// <exception cref="InvalidOperationException">未为 <typeparamref name="TValue"/> 注册转换器。</exception>
    public TValue Get(string input)
    {
        if (GetFunc is not null)
        {
            return GetFunc(input);
        }

        if (GlobalGetFunc is not null)
        {
            return GlobalGetFunc(input);
        }

        return DefaultGet(input);
    }

    /// <summary>
    /// 把 <typeparamref name="TValue"/> 转换为字符串表示。
    /// </summary>
    /// <remarks>
    /// 解析顺序：<see cref="SetFunc"/> → <see cref="GlobalSetFunc"/> → 内置默认。
    /// </remarks>
    /// <param name="value">要格式化的强类型值。</param>
    /// <returns>字符串表示；null/默认值时返回 null。</returns>
    public string? Set(TValue value)
    {
        if (SetFunc is not null)
        {
            return SetFunc(value);
        }

        if (GlobalSetFunc is not null)
        {
            return GlobalSetFunc(value);
        }

        return DefaultSet(value);
    }

    /// <summary>
    /// 使用格式字符串把 <typeparamref name="TValue"/> 转换为字符串表示。
    /// </summary>
    /// <remarks>
    /// 当值实现 <see cref="IFormattable"/> 时使用
    /// <see cref="IFormattable.ToString(string, IFormatProvider)"/>；值为 null 或
    /// 未实现 <see cref="IFormattable"/> 时回退到 <see cref="Set(TValue)"/>。
    /// Format 只影响显示（不参与解析）。
    /// </remarks>
    /// <param name="value">要格式化的强类型值。</param>
    /// <param name="format">格式字符串（如 "yyyy-MM-dd"、"N2"、"C"）。</param>
    /// <returns>格式化后的字符串表示。</returns>
    public string? SetWithFormat(TValue value, string format)
    {
        if (value is null)
        {
            return null;
        }

        if (value is IFormattable formattable)
        {
            return formattable.ToString(format, ResolvedCulture);
        }

        return Set(value);
    }

    private TValue DefaultGet(string input)
    {
        var targetType = typeof(TValue);
        var underlyingType = Nullable.GetUnderlyingType(targetType);
        var isNullable = underlyingType is not null;
        var parseType = underlyingType ?? targetType;

        if (string.IsNullOrWhiteSpace(input))
        {
            return default!;
        }

        var culture = ResolvedCulture;

        // 枚举按名称匹配，且必须先于类型 switch：枚举没有可穷举的固定类型集合，
        // switch 的兜底分支会把所有枚举都判为"未注册转换器"——此前枚举类型的
        // 原生选择控件正是栽在这个坑上，被静默重置为默认值。
        if (parseType.IsEnum)
        {
            return (TValue)Enum.Parse(parseType, input, ignoreCase: true);
        }

        object result = parseType switch
        {
            Type t when t == typeof(string) => input,
            Type t when t == typeof(int) => int.Parse(input, culture),
            Type t when t == typeof(long) => long.Parse(input, culture),
            Type t when t == typeof(float) => float.Parse(input, culture),
            Type t when t == typeof(double) => double.Parse(input, culture),
            Type t when t == typeof(decimal) => decimal.Parse(input, culture),
            Type t when t == typeof(bool) => bool.Parse(input),
            Type t when t == typeof(DateTime) => DateTime.Parse(input, culture),
            Type t when t == typeof(DateTimeOffset) => DateTimeOffset.Parse(input, culture),
            Type t when t == typeof(DateOnly) => DateOnly.Parse(input, culture),
            Type t when t == typeof(TimeOnly) => TimeOnly.Parse(input, culture),
            Type t when t == typeof(Guid) => Guid.Parse(input),
            _ => throw new InvalidOperationException(
                $"No converter registered for type '{targetType.FullName}'. " +
                $"Register a converter via InputConverter<{targetType.Name}>.GlobalGetFunc or " +
                $"provide an InputConverter<{targetType.Name}> instance with GetFunc set.")
        };

        if (isNullable)
        {
            return (TValue)result;
        }

        return (TValue)result;
    }

    private string? DefaultSet(TValue value)
    {
        if (value is null)
        {
            return null;
        }

        var targetType = typeof(TValue);
        var underlyingType = Nullable.GetUnderlyingType(targetType);
        var parseType = underlyingType ?? targetType;

        if (parseType == typeof(string))
        {
            return value as string;
        }

        // 输出枚举的名称而非数值：枚举本身是 IFormattable，带区域文化的
        // ToString 也会得到名称，但显式分支保证这里与上方解析严格互逆。
        if (parseType.IsEnum)
        {
            return value.ToString();
        }

        if (value is IFormattable formattable)
        {
            return formattable.ToString(null, ResolvedCulture);
        }

        return value.ToString();
    }
}
