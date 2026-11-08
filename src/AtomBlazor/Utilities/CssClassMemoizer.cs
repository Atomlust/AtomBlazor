namespace AtomBlazor;

/// <summary>
/// 按键记忆化的 CSS 类计算结果，避免每次渲染重复执行 tailwind-merge。
/// </summary>
/// <remarks>
/// <see cref="ClassNames.cn"/> 内部虽有 LRU 结果缓存，但每次调用仍要分配
/// 参数数组、拼接输入串并做一次缓存查找。对自身状态高频变化而类名输入
/// 稳定的组件（拖拽中的 Slider、点选后的 Checkbox、开关翻动的 Switch 等），
/// 把参与类名计算的全部非常量输入组成元组作为键，在实例上记忆化即可
/// 完全跳过这两段成本：
/// <code>
/// private CssClassMemoizer&lt;(MyEnum Variant, MyEnum Size, string?, bool)&gt; _cssCache;
///
/// private string CssClass
/// {
///     get
///     {
///         var key = (Variant, Size, Class, IsDisabled);
///         if (!_cssCache.TryGet(key, out var cached))
///         {
///             cached = ClassNames.cn(基础串, 变体映射, 尺寸映射, IsDisabled ? "..." : null, Class);
///             _cssCache.Set(key, cached);
///         }
///         return cached!;
///     }
/// }
/// </code>
/// 键必须涵盖计算中用到的每一个非常量输入，遗漏会造成样式不随参数更新。
/// </remarks>
public struct CssClassMemoizer<TKey> where TKey : IEquatable<TKey>
{
    private TKey _key;
    private string? _value;

    /// <summary>
    /// 尝试取回与 <paramref name="key"/> 对应的缓存类名。
    /// </summary>
    public bool TryGet(TKey key, out string? value)
    {
        if (_value is not null && _key.Equals(key))
        {
            value = _value;
            return true;
        }

        value = null;
        return false;
    }

    /// <summary>
    /// 写入一次计算结果。
    /// </summary>
    public void Set(TKey key, string value)
    {
        _key = key;
        _value = value;
    }
}
