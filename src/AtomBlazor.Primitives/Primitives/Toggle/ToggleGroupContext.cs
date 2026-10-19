namespace AtomBlazor.Primitives;

/// <summary>
/// 用于在 ToggleGroup 与 ToggleGroupItem 组件之间共享状态的上下文类。
/// </summary>
/// <typeparam name="TValue">与切换按钮组各项关联的值类型。</typeparam>
public class ToggleGroupContext<TValue>
{
    /// <summary>
    /// 获取或设置当前选中的值（单选模式）。
    /// </summary>
    public TValue? Value { get; set; }

    /// <summary>
    /// 获取或设置当前选中的值集合（多选模式）。
    /// </summary>
    public List<TValue> Values { get; set; } = new();

    /// <summary>
    /// 获取或设置选择类型（单选 Single 或多选 Multiple）。
    /// </summary>
    public ToggleGroupType Type { get; set; }

    /// <summary>
    /// 获取或设置组是否禁用。
    /// </summary>
    public bool Disabled { get; set; }

    /// <summary>
    /// 获取或设置某个切换项被拨动时调用的回调。
    /// </summary>
    public Func<TValue, Task>? ToggleItem { get; set; }

    /// <summary>
    /// 获取或设置组的排列方向。
    /// </summary>
    public string Orientation { get; set; } = "horizontal";

    /// <summary>
    /// 获取或设置已注册的切换组项列表。
    /// </summary>
    public List<ToggleGroupItem<TValue>> Items { get; set; } = new();

    /// <summary>
    /// 获取或设置某个项获得焦点时触发的回调，让组的方向键导航从用户实际所在的项
    /// 出发，而不是从组自己上一次移动到的项出发。
    /// </summary>
    public Action<ToggleGroupItem<TValue>>? ItemFocused { get; set; }
}
