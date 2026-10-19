using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace AtomBlazor.Primitives;

/// <summary>
/// 无外观单选组基元：只提供行为与可访问性能力，不带任何样式。
/// </summary>
/// <typeparam name="TValue">与单选组各项绑定的值的类型。</typeparam>
/// <remarks>
/// <para>
/// RadioGroup 组件是一枚无外观基元，负责单选组的全部行为、
/// 键盘导航（方向键）与可访问性特性；它不提供任何默认样式。
/// </para>
/// <para>
/// 该基元管理一组互斥的单选项，其中同一时刻只能有一项
/// 被选中。它通过 Blazor 的 CascadingValue 机制向子级 RadioGroupItem 组件
/// 提供上下文。
/// </para>
/// <para>
/// 内置的可访问性特性：
/// <list type="bullet">
/// <item>语义化 radiogroup 角色</item>
/// <item>方向键导航（上/下、左/右）</item>
/// <item>禁用状态输出 aria-disabled 属性</item>
/// <item>aria-label 为辅助技术提供语境</item>
/// <item>导航过程中自动管理焦点</item>
/// </list>
/// </para>
/// </remarks>
/// <example>
/// 基础无外观单选组：
/// <code>
/// &lt;RadioGroup @bind-Value="selectedOption"&gt;
///     &lt;RadioGroupItem Value="@("option1")" /&gt;
///     &lt;RadioGroupItem Value="@("option2")" /&gt;
/// &lt;/RadioGroup&gt;
/// </code>
/// </example>
public partial class RadioGroupPrimitive<TValue> : ComponentBase
{
    private RadioGroupContext<TValue> context = new();
    private List<RadioGroupItem<TValue>>? _cachedEnabledItems;
    private int _lastItemsVersion;

    /// <summary>
    /// 获取或设置当前选中的值。
    /// </summary>
    /// <remarks>
    /// 通过 <c>@bind-Value</c> 指令支持双向绑定；
    /// 该属性变化时触发 <see cref="ValueChanged"/> 回调。
    /// </remarks>
    [Parameter]
    public TValue? Value { get; set; }

    /// <summary>
    /// 获取或设置选中值变化时调用的回调。
    /// </summary>
    /// <remarks>
    /// 该事件回调支撑 <c>@bind-Value</c> 双向绑定，
    /// 选中某个单选按钮时都会调用它。
    /// </remarks>
    [Parameter]
    public EventCallback<TValue> ValueChanged { get; set; }

    /// <summary>
    /// 获取或设置整个单选组是否禁用。
    /// </summary>
    /// <remarks>
    /// 禁用后组内所有单选按钮都不可再被选中。
    /// </remarks>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>
    /// 获取或设置单选组在表单上下文中是否必填。
    /// 为 true 时为屏幕阅读器输出 <c>aria-required="true"</c>。
    /// </summary>
    [Parameter]
    public bool Required { get; set; }

    /// <summary>
    /// 获取或设置单选组的 ARIA 标签。
    /// </summary>
    /// <remarks>
    /// 为辅助技术描述该单选组的用途。
    /// </remarks>
    [Parameter]
    public string? AriaLabel { get; set; }

    /// <summary>
    /// 获取或设置在单选组内部渲染的内容。
    /// </summary>
    /// <remarks>
    /// 应包含 RadioGroupItem 组件。
    /// </remarks>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// 获取或设置应用到所有 RadioGroupItem 子项的默认 CSS 类。
    /// 各项会把它与自己的 class 特性合并。
    /// </summary>
    [Parameter]
    public string? ItemClass { get; set; }

    /// <summary>
    /// 获取或设置应用到容器元素的额外属性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>
    /// 选中一个值并通知监听方。
    /// </summary>
    /// <param name="value">要选中的值。</param>
    private async Task SelectValueAsync(TValue value)
    {
        if (Disabled)
        {
            return;
        }

        Value = value;
        await ValueChanged.InvokeAsync(value);
    }

    /// <summary>
    /// 判断给定值是否为当前选中值。
    /// </summary>
    /// <param name="value">要检查的值。</param>
    /// <returns>已选中返回 true，否则返回 false。</returns>
    private bool IsSelected(TValue? value)
    {
        if (Value == null && value == null)
        {
            return true;
        }

        if (Value == null || value == null)
        {
            return false;
        }

        return EqualityComparer<TValue>.Default.Equals(Value, value);
    }

    /// <summary>
    /// 下一次 key-down 是否应抑制浏览器的默认动作。
    /// <para>
    /// 该标记曾被硬编码为 <c>true</c>，于是抑制了所有按键——包括 Tab，
    /// 因此焦点再无法用键盘离开单选组，
    /// 也包括选中某项所用的 Space 与 Enter。
    /// </para>
    /// </summary>
    private bool shouldPreventDefault;

    /// <summary>上级 DirectionProvider 提供书写方向时记录于此。</summary>
    [CascadingParameter]
    private DirectionContext? Direction { get; set; }

    private bool IsRtl => DirectionContext.Resolve(Direction);

    /// <summary>
    /// 处理单选组的键盘导航。
    /// </summary>
    /// <param name="args">键盘事件参数。</param>
    /// <remarks>
    /// 键盘交互支持：
    /// - Arrow Down：下一个单选项；Arrow Up：上一个
    /// - Arrow Right/Left：沿书写方向移动，
    ///   因此右到左布局下两者互换
    /// - 导航到的项会被自动选中并聚焦
    /// - 仅对方向键抑制浏览器默认的滚动行为
    /// </remarks>
    private async Task HandleKeyDown(KeyboardEventArgs args)
    {
        if (Disabled)
        {
            return;
        }

        var enabledItems = GetEnabledItems();
        if (enabledItems.Count == 0)
        {
            return;
        }

        switch (args.Key)
        {
            case "ArrowDown":
                shouldPreventDefault = true;
                await NavigateNext(enabledItems);
                break;

            // 水平方向键跟随书写方向；Up 与 Down 永不翻转。
            case "ArrowRight":
                shouldPreventDefault = true;
                await (IsRtl ? NavigatePrevious(enabledItems) : NavigateNext(enabledItems));
                break;

            case "ArrowLeft":
                shouldPreventDefault = true;
                await (IsRtl ? NavigateNext(enabledItems) : NavigatePrevious(enabledItems));
                break;

            case "ArrowUp":
                shouldPreventDefault = true;
                await NavigatePrevious(enabledItems);
                break;

            default:
                shouldPreventDefault = false;
                break;
        }
    }

    /// <summary>
    /// 方向键一松开就解除抑制。
    /// </summary>
    /// <remarks>
    /// Blazor 是在元素渲染时求值 <c>@onkeydown:preventDefault</c> 的，而不是事件真正触发时，
    /// 所以这个标记描述的永远是上一次按键。若一直挂着，紧随方向键的 Tab 会被吞掉，
    /// 用户无法离开该组；在 key-up 时清零后，
    /// 该标记只在方向键确实被按住期间为 true，
    /// 而那恰恰是页面本来会滚动的时刻。
    /// </remarks>
    private void HandleKeyUp() => shouldPreventDefault = false;

    /// <summary>
    /// 获取可用项列表；
    /// 尽可能使用缓存版本，避免每次键盘导航都分配。
    /// </summary>
    private List<RadioGroupItem<TValue>> GetEnabledItems()
    {
        // 以项数作为简单版本判据——数量变了说明项变了。
        var currentVersion = context.Items.Count;
        if (_cachedEnabledItems == null || _lastItemsVersion != currentVersion)
        {
            _cachedEnabledItems = context.Items.Where(i => !i.Disabled).ToList();
            _lastItemsVersion = currentVersion;
        }
        return _cachedEnabledItems;
    }

    /// <summary>
    /// 导航到下一个单选项目。
    /// </summary>
    private async Task NavigateNext(List<RadioGroupItem<TValue>> enabledItems)
    {
        if (enabledItems.Count == 0)
        {
            return;
        }

        var currentIndex = enabledItems.FindIndex(i => IsSelected(i.Value));
        var nextIndex = (currentIndex + 1) % enabledItems.Count;

        var nextItem = enabledItems[nextIndex];
        if (nextItem.Value != null)
        {
            await SelectValueAsync(nextItem.Value);
            await nextItem.FocusAsync();
        }
    }

    /// <summary>
    /// 导航到上一个单选项目。
    /// </summary>
    private async Task NavigatePrevious(List<RadioGroupItem<TValue>> enabledItems)
    {
        if (enabledItems.Count == 0)
        {
            return;
        }

        var currentIndex = enabledItems.FindIndex(i => IsSelected(i.Value));
        var prevIndex = currentIndex <= 0 ? enabledItems.Count - 1 : currentIndex - 1;

        var prevItem = enabledItems[prevIndex];
        if (prevItem.Value != null)
        {
            await SelectValueAsync(prevItem.Value);
            await prevItem.FocusAsync();
        }
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        base.OnParametersSet();

        // 用当前状态刷新上下文。
        context.Value = Value;
        context.Disabled = Disabled;
        context.SelectValue = SelectValueAsync;
        context.ItemClass = ItemClass;
    }
}
