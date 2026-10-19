using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace AtomBlazor.Primitives;

/// <summary>
/// 无外观单选项基元：可在 RadioGroup 内被选中。
/// </summary>
/// <typeparam name="TValue">与本单选项绑定的值的类型。</typeparam>
/// <remarks>
/// <para>
/// RadioGroupItemPrimitive 组件表示 RadioGroup 内的一个可选项。
/// 它不提供任何默认样式，只提供行为与可访问性特性。
/// </para>
/// <para>
/// 本组件必须作为 RadioGroup 组件的子级使用。
/// 它通过 Blazor 的 CascadingParameter 接收单选组上下文。
/// </para>
/// <para>
/// 内置的可访问性特性：
/// <list type="bullet">
/// <item>带 radio 角色的语义化 button 元素</item>
/// <item>aria-checked 属性反映选中状态</item>
/// <item>禁用状态输出 aria-disabled 属性</item>
/// <item>aria-label 为辅助技术提供语境</item>
/// <item>键盘支持（空格/回车选中）</item>
/// <item>为漫游 tabindex 模式提供正确的 tabindex</item>
/// <item>data-state 属性供 CSS 挂样式钩子</item>
/// </list>
/// </para>
/// </remarks>
/// <example>
/// 基础无外观单选项：
/// <code>
/// &lt;RadioGroupItemPrimitive Value="@("option1")" /&gt;
/// </code>
///
/// 自定义类名的带样式单选项：
/// <code>
/// &lt;RadioGroupItemPrimitive Value="@("option1")"
///                          class="h-4 w-4 rounded-full border"&gt;
///     &lt;span class="inner-circle"&gt;&lt;/span&gt;
/// &lt;/RadioGroupItemPrimitive&gt;
/// </code>
/// </example>
public partial class RadioGroupItemPrimitive<TValue> : ComponentBase, IDisposable
{
    private ElementReference buttonElement;
    private bool shouldPreventDefault;

    /// <summary>
    /// 获取或设置上级 RadioGroup 的上下文。
    /// </summary>
    [CascadingParameter]
    private RadioGroupContext<TValue>? Context { get; set; }

    /// <summary>
    /// 获取或设置与这个单选项关联的值。
    /// </summary>
    /// <remarks>
    /// 该项被选中时，这个值就成为 RadioGroup 的 Value。
    /// </remarks>
    [Parameter, EditorRequired]
    public TValue? Value { get; set; }

    /// <summary>
    /// 获取或设置该单项是否禁用。
    /// </summary>
    /// <remarks>
    /// 禁用后该项不可被选中，并以降低的不透明度呈现。
    /// </remarks>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>
    /// 获取或设置单选按钮元素的 ID 特性。
    /// </summary>
    /// <remarks>
    /// 用于通过 label 的 for 属性把单选项与标签元素关联起来。
    /// 未提供时自动生成。
    /// </remarks>
    [Parameter]
    public string? Id { get; set; }

    /// <summary>
    /// 获取或设置该单选项的 ARIA 标签。
    /// </summary>
    /// <remarks>
    /// 当单选项没有配套 label 元素时，
    /// 为屏幕阅读器提供可读名称。
    /// </remarks>
    [Parameter]
    public string? AriaLabel { get; set; }

    /// <summary>
    /// 获取或设置在该单选项内部渲染的内容。
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// 获取或设置应用到 button 元素的额外属性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>
    /// 获取该单选项是否处于选中态。
    /// </summary>
    private bool IsChecked
    {
        get
        {
            if (Context == null)
            {
                return false;
            }

            return EqualityComparer<TValue?>.Default.Equals(Context.Value, Value);
        }
    }

    /// <summary>
    /// 获取该单选项是否禁用（单项禁用或整组禁用）。
    /// </summary>
    private bool IsDisabled => Disabled || (Context?.Disabled ?? false);

    /// <summary>
    /// 获取供 CSS 使用的 <c>data-state</c> 属性取值。
    /// </summary>
    private string DataState => IsChecked ? "checked" : "unchecked";

    /// <summary>
    /// 获取把上级 ItemClass 与 AdditionalAttributes 合并后的属性集合。
    /// </summary>
    private Dictionary<string, object>? MergedAttributes
    {
        get
        {
            var itemClass = Context?.ItemClass;
            if (string.IsNullOrEmpty(itemClass))
            {
                return AdditionalAttributes;
            }

            var merged = AdditionalAttributes != null
                ? new Dictionary<string, object>(AdditionalAttributes)
                : new Dictionary<string, object>();

            if (merged.TryGetValue("class", out var existing))
            {
                merged["class"] = $"{itemClass} {existing}";
            }
            else
            {
                merged["class"] = itemClass;
            }

            return merged;
        }
    }

    /// <summary>
    /// 处理单选项的点击事件。
    /// </summary>
    /// <param name="args">鼠标事件参数。</param>
    private async Task HandleClick(MouseEventArgs args)
    {
        if (!IsDisabled && Context?.SelectValue != null && Value != null)
        {
            await Context.SelectValue(Value);
        }
    }

    /// <summary>
    /// 处理无障碍访问所需的键盘事件。
    /// </summary>
    /// <param name="args">键盘事件参数。</param>
    /// <remarks>
    /// 支持 Space 与 Enter 选中该单选项，
    /// 并抑制默认行为以避免页面滚动。
    /// </remarks>
    private async Task HandleKeyDown(KeyboardEventArgs args)
    {
        if (!IsDisabled && Context?.SelectValue != null && Value != null &&
            (args.Key == " " || args.Key == "Enter"))
        {
            shouldPreventDefault = true;
            await Context.SelectValue(Value);
        }
        else
        {
            shouldPreventDefault = false;
        }
    }

    /// <summary>
    /// 以编程方式让该单选项获得焦点。
    /// </summary>
    public async Task FocusAsync()
    {
        try
        {
            await buttonElement.FocusAsync();
        }
        catch
        {
            // 电路断开时属于预期情形。
        }
    }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        base.OnInitialized();

        // 未提供 ID 时自动生成（对无障碍与 label 关联至关重要）。
        if (string.IsNullOrEmpty(Id))
        {
            Id = $"radio-{Guid.NewGuid():N}";
        }

        // 向上级 RadioGroup 注册。
        Context?.Items.Add(this);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        GC.SuppressFinalize(this);
        // 从上级 RadioGroup 注销。
        Context?.Items.Remove(this);
    }
}
