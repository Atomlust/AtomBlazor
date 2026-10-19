using System.Globalization;

namespace AtomBlazor.Primitives;

/// <summary>
/// 用通向嵌套分组中某一分组的有序键（由最外层向内）来唯一标识它。
/// </summary>
/// <remarks>
/// 原始键不足以标识嵌套分组：先按 Department 再按 Status 分组时，
/// 键为 <c>Active</c> 的组在每个部门下都存在，而扁平的折叠键集合会把它们
/// 全部一起折叠。完整路径可以消除这种歧义。
/// <para>
/// 相等性基于整条路径的值比较，因此下次渲染时重建的路径能与折叠集合中
/// 存储的路径匹配。键使用 <see cref="object.Equals(object?)"/> 比较，
/// 且 null 键是合法的路径段——分组列为 null 的行自成一个分组。
/// </para>
/// </remarks>
public sealed class GroupPath : IEquatable<GroupPath>
{
    private readonly object?[] keys;
    private readonly int hash;

    /// <summary>
    /// 由一组有序键创建路径，最外层在前。
    /// </summary>
    /// <param name="keys">各键，最外层分组在前。</param>
    /// <exception cref="ArgumentNullException">当 keys 为 null 时抛出。</exception>
    public GroupPath(params object?[] keys)
    {
        ArgumentNullException.ThrowIfNull(keys);

        this.keys = (object?[])keys.Clone();

        var accumulated = new HashCode();
        foreach (var key in this.keys)
        {
            accumulated.Add(key);
        }

        hash = accumulated.ToHashCode();
    }

    /// <summary>
    /// 由一条有序键序列创建路径，最外层在前。
    /// </summary>
    /// <param name="keys">各键，最外层分组在前。</param>
    public GroupPath(IEnumerable<object?> keys)
        : this((keys ?? throw new ArgumentNullException(nameof(keys))).ToArray())
    {
    }

    /// <summary>
    /// 获取路径中的各键，最外层分组在前。
    /// </summary>
    public IReadOnlyList<object?> Keys => keys;

    /// <summary>
    /// 获取该组的嵌套深度，从 0 开始。最外层深度为 0。
    /// </summary>
    public int Depth => keys.Length - 1;

    /// <summary>
    /// 获取该组自身的键——路径的最后一段。
    /// </summary>
    public object? Key => keys.Length > 0 ? keys[^1] : null;

    /// <summary>
    /// 获取空路径，它是每个最外层分组的父路径。
    /// </summary>
    public static GroupPath Root { get; } = new();

    /// <summary>
    /// 返回向下一层的新路径，以 <paramref name="key"/> 结尾。
    /// </summary>
    /// <param name="key">子分组的键。</param>
    /// <returns>子路径。</returns>
    public GroupPath Append(object? key)
    {
        var extended = new object?[keys.Length + 1];
        Array.Copy(keys, extended, keys.Length);
        extended[^1] = key;
        return new GroupPath(extended);
    }

    /// <summary>
    /// 返回该组父分组的路径；当前是最外层分组时返回 <see cref="Root"/>。
    /// </summary>
    /// <returns>父路径。</returns>
    public GroupPath Parent()
    {
        if (keys.Length == 0)
        {
            return Root;
        }

        return new GroupPath(keys[..^1]);
    }

    /// <summary>
    /// 获取 <paramref name="other"/> 是否为该路径本身或位于其之下。
    /// </summary>
    /// <param name="other">要测试的路径。</param>
    /// <returns>other 以该路径开头时为 true。</returns>
    public bool IsAncestorOfOrSelf(GroupPath other)
    {
        ArgumentNullException.ThrowIfNull(other);

        if (other.keys.Length < keys.Length)
        {
            return false;
        }

        for (var i = 0; i < keys.Length; i++)
        {
            if (!Equals(keys[i], other.keys[i]))
            {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc />
    public bool Equals(GroupPath? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (hash != other.hash || keys.Length != other.keys.Length)
        {
            return false;
        }

        for (var i = 0; i < keys.Length; i++)
        {
            if (!Equals(keys[i], other.keys[i]))
            {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as GroupPath);

    /// <inheritdoc />
    public override int GetHashCode() => hash;

    /// <inheritdoc />
    public override string ToString() =>
        string.Join(" › ", keys.Select(k => Convert.ToString(k, CultureInfo.CurrentCulture) ?? "(none)"));
}
