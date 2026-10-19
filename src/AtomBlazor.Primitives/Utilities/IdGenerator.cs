namespace AtomBlazor.Primitives.Utilities;

/// <summary>
/// 线程安全的唯一组件 ID 生成工具。
/// 用于 ARIA 特性与 DOM 元素标识。
/// </summary>
public static class IdGenerator
{
    private static int _counter;

    /// <summary>
    /// 生成带指定前缀的唯一 ID。
    /// 使用 Interlocked 操作，线程安全。
    /// </summary>
    /// <param name="prefix">ID 的前缀（默认："shadcn"）。</param>
    /// <returns>形如 "prefix-{counter}" 的唯一 ID 字符串。</returns>
    public static string GenerateId(string prefix = "shadcn")
    {
        var id = Interlocked.Increment(ref _counter);
        return $"{prefix}-{id}";
    }

    /// <summary>
    /// 用同一前缀批量生成多个唯一 ID。
    /// </summary>
    /// <param name="count">要生成的 ID 数量。</param>
    /// <param name="prefix">ID 的前缀（默认："shadcn"）。</param>
    /// <returns>唯一 ID 字符串数组。</returns>
    public static string[] GenerateIds(int count, string prefix = "shadcn")
    {
        var ids = new string[count];
        for (var i = 0; i < count; i++)
        {
            ids[i] = GenerateId(prefix);
        }
        return ids;
    }

    /// <summary>
    /// 将计数器重置为零。
    /// 警告：仅用于测试场景。
    /// </summary>
    internal static void Reset() =>
        Interlocked.Exchange(ref _counter, 0);
}
