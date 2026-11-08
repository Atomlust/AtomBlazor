using System.Collections.Concurrent;

namespace AtomBlazor;

/// <summary>
/// 按运行时枚举类型缓存的字符串解析委托，供输入控件热路径复用。
/// </summary>
/// <remarks>
/// 非泛型 <see cref="Enum.Parse(Type, string, bool)"/> 每次调用都要在运行时
/// 类型上重新做名称→值匹配；NativeSelect、InputConverter 这类输入组件在
/// 每次选择或提交时触发解析。按 <see cref="Type"/> 缓存 ignoreCase 解析委托后，
/// 重复解析只剩一次委托调用。语义与直接调用
/// <see cref="Enum.Parse(Type, string, bool)"/> 完全一致：无法解析时抛出
/// <see cref="ArgumentException"/>，由调用方按原有方式捕获。
/// </remarks>
internal static class EnumParserCache
{
    private static readonly ConcurrentDictionary<Type, Func<string, object>> Parsers = new();

    public static object Parse(Type enumType, string text)
    {
        var parser = Parsers.GetOrAdd(enumType, CreateParser);
        return parser(text);

        static Func<string, object> CreateParser(Type type) =>
            text => Enum.Parse(type, text, ignoreCase: true);
    }
}
