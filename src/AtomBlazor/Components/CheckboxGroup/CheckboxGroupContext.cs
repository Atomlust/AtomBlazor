namespace AtomBlazor;

/// <summary>
/// 用于在 CheckboxGroup 与 CheckboxGroupItem 组件之间共享状态的上下文。
/// </summary>
/// <typeparam name="TValue">与复选框组各项关联的值类型。</typeparam>
internal sealed class CheckboxGroupContext<TValue>
{
    /// <summary>当前选中的值集合。</summary>
    public required IReadOnlyCollection<TValue> SelectedValues { get; init; }

    /// <summary>整组是否禁用。</summary>
    public required bool IsDisabled { get; init; }

    /// <summary>某项被拨动时调用的回调。</summary>
    public required Func<TValue, bool, Task> ToggleItem { get; init; }

    /// <summary>注册一个项的值。</summary>
    public required Action<TValue> RegisterItem { get; init; }

    /// <summary>注销一个项的值。</summary>
    public required Action<TValue> UnregisterItem { get; init; }
}
