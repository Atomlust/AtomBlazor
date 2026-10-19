using AtomBlazor.Primitives;
using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 可在 RadioGroup 内被选中的单选按钮项。
/// </summary>
/// <typeparam name="TValue">与这个单选项关联的值类型。</typeparam>
/// <remarks>
/// <para>
/// RadioGroupItem 组件表示 RadioGroup 中的单个可选项。
/// 选中时呈现为带内点的圆圈，遵循 shadcn/ui 设计。
/// </para>
/// <para>
/// 特性：
/// - 圆圈加内点的视觉样式
/// - 选中状态由父级 RadioGroup 管理
/// - 支持禁用态
/// - 键盘导航（Space/Enter 选中）
/// - 面向无障碍的 ARIA 属性
/// - 为键盘导航提供的焦点管理
/// </para>
/// </remarks>
/// <example>
/// 使用 Label 参数（推荐）：
/// <code>
/// &lt;RadioGroupItem Value="@("option1")" Label="Option 1" /&gt;
/// </code>
///
/// 不使用 Label（自行接线 label）：
/// <code>
/// &lt;RadioGroupItem Value="@("option1")" Id="r1" /&gt;
/// </code>
/// </example>
public partial class RadioGroupItem<TValue> : ComponentBase
{
    private AtomBlazor.Primitives.RadioGroupItem<TValue>? primitiveRef;

    /// <summary>
    /// 获取或设置从父级级联而来的 RadioGroup 上下文。
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
    public TValue Value { get; set; } = default!;

    /// <summary>
    /// 获取或设置该单项是否禁用。
    /// </summary>
    /// <remarks>
    /// 禁用后该项不可被选中，并以降低的不透明度呈现。
    /// </remarks>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>
    /// 获取或设置应用到该单选项的额外 CSS 类。
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置要应用到根元素的额外 HTML 属性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>
    /// 获取或设置该单选项的 ARIA 标签。
    /// </summary>
    /// <remarks>
    /// 当单选项没有配套 label 元素时，为屏幕阅读器提供可读名称。
    /// </remarks>
    [Parameter]
    public string? AriaLabel { get; set; }

    /// <summary>
    /// 获取或设置单选按钮元素的 ID 特性。
    /// </summary>
    /// <remarks>
    /// 用于通过 label 的 for 属性把单选项与标签元素关联起来。
    /// </remarks>
    [Parameter]
    public string? Id { get; set; }

    /// <summary>
    /// 获取或设置在单选项旁显示的可选标签文本。
    /// </summary>
    /// <remarks>
    /// 提供后，组件渲染一个包装 div，内含单选按钮与自动完成 for/id 接线的 label 元素。
    /// 点击标签即可激活单选按钮。标签通过 Tailwind peer 工具类尊重禁用态样式。
    /// 为 null 时组件只渲染单选按钮（与旧用法兼容）。
    /// </remarks>
    [Parameter]
    public string? Label { get; set; }

    /// <summary>
    /// 获取该单选项是否存在需要渲染的标签。
    /// </summary>
    private bool HasLabel => !string.IsNullOrEmpty(Label);

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
    /// 获取单选按钮的计算 CSS 类。
    /// </summary>
    private string CssClass => ClassNames.cn(
        "atom:aspect-square atom:h-4 atom:w-4 atom:rounded-full atom:border-2",
        IsChecked ? "atom:border-primary atom:bg-primary" : "atom:border-input",
        "atom:text-primary atom:ring-offset-background",
        "atom:focus:outline-none atom:focus-visible:ring-2 atom:focus-visible:ring-ring atom:focus-visible:ring-offset-2",
        "atom:disabled:cursor-not-allowed atom:disabled:opacity-50",
        "atom:flex atom:items-center atom:justify-center",
        HasLabel ? "atom:peer" : null,
        Class
    );

    /// <summary>
    /// 获取内圈指示点的计算 CSS 类。
    /// </summary>
    private string CircleIndicatorClass => ClassNames.cn(
        "atom:h-2 atom:w-2 atom:rounded-full atom:bg-background",
        !IsChecked ? "atom:scale-0" : null,
        "atom:transition-transform atom:duration-100"
    );

    /// <summary>
    /// 获取 label 元素的计算 CSS 类。
    /// </summary>
    private static string LabelCssClass => "atom:text-sm atom:font-medium atom:leading-none atom:peer-disabled:cursor-not-allowed atom:peer-disabled:opacity-70";

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        base.OnInitialized();

        if (HasLabel && string.IsNullOrEmpty(Id))
        {
            Id = $"radio-{Guid.NewGuid():N}";
        }
    }

    /// <summary>
    /// 以编程方式让该单选项获得焦点。
    /// </summary>
    internal async Task FocusAsync()
    {
        if (primitiveRef != null)
        {
            await primitiveRef.FocusAsync();
        }
    }
}
