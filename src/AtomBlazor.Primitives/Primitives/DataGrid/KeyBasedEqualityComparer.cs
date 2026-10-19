namespace AtomBlazor.Primitives;

/// <summary>
/// 委托给用户提供的键函数的相等性比较器。
/// 用于为数据项赋予稳定的标识，使选择和展开状态在数据重新
/// 获取后依然保留（此时对象引用会变化，但逻辑标识保持不变）。
/// </summary>
internal sealed class KeyBasedEqualityComparer<TData> : IEqualityComparer<TData> where TData : class
{
    private readonly Func<TData, object> keySelector;

    public KeyBasedEqualityComparer(Func<TData, object> keySelector)
    {
        ArgumentNullException.ThrowIfNull(keySelector);
        this.keySelector = keySelector;
    }

    public bool Equals(TData? x, TData? y)
    {
        if (ReferenceEquals(x, y))
        {
            return true;
        }

        if (x is null || y is null)
        {
            return false;
        }

        return Equals(keySelector(x), keySelector(y));
    }

    public int GetHashCode(TData obj)
    {
        ArgumentNullException.ThrowIfNull(obj);
        return keySelector(obj)?.GetHashCode() ?? 0;
    }
}
