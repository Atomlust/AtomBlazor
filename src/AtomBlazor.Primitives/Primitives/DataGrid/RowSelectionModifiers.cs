namespace AtomBlazor.Primitives;

/// <summary>
/// 行点击时按住的修饰键，用于决定
/// <see cref="DataGridSelectionBehavior.Replace"/> 如何更改选择。
/// </summary>
/// <param name="Extend">
/// 按住 Shift 时为 true：选中从锚点到被点击行的范围。
/// </param>
/// <param name="Additive">
/// 按住 Ctrl（macOS 上为 Cmd）时为 true：仅增删被点击的行。
/// </param>
public readonly record struct RowSelectionModifiers(bool Extend, bool Additive)
{
    /// <summary>
    /// 获取未按任何修饰键的点击对应的修饰符。
    /// </summary>
    public static RowSelectionModifiers None => new(false, false);
}
