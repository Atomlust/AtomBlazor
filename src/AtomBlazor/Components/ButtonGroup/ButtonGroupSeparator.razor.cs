using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 专为在 ButtonGroup 内使用而设计的分隔符组件。
/// </summary>
/// <remarks>
/// <para>
/// ButtonGroupSeparator 组件在按钮组的各部分之间提供视觉分隔线，
/// 并依据父按钮组的布局自动调整方向与间距。
/// </para>
/// <para>
/// 特性：
/// - 自动支持方向（水平/垂直）
/// - 组内视觉间隔的合适边距
/// - 以按钮组专属样式包装 Separator 组件
/// - 与 ButtonGroup 布局无缝集成
/// </para>
/// </remarks>
/// <example>
/// <code>
/// &lt;ButtonGroup&gt;
///     &lt;Button Variant="ButtonVariant.Outline"&gt;Edit&lt;/Button&gt;
///     &lt;ButtonGroupSeparator /&gt;
///     &lt;Button Variant="ButtonVariant.Outline"&gt;Delete&lt;/Button&gt;
/// &lt;/ButtonGroup&gt;
/// </code>
/// </example>
public partial class ButtonGroupSeparator : ComponentBase
{
    /// <summary>
    /// 获取或设置分隔符的方向。
    /// </summary>
    /// <remarks>
    /// 应与父按钮组的方向一致。
    /// 默认值为 <see cref="SeparatorOrientation.Vertical"/>（对应水平按钮组）。
    /// </remarks>
    [Parameter]
    public SeparatorOrientation Orientation { get; set; } = SeparatorOrientation.Vertical;

    /// <summary>
    /// 获取或设置分隔符是否为纯装饰性元素。
    /// </summary>
    /// <remarks>
    /// 为 true（默认）时，分隔符按装饰性处理，对辅助技术不可见。
    /// </remarks>
    [Parameter]
    public bool Decorative { get; set; } = true;

    /// <summary>
    /// 获取或设置应用到分隔符上的额外 CSS 类。
    /// </summary>
    /// <remarks>
    /// 自定义类追加在组件基础类之后，便于样式覆盖与扩展。
    /// </remarks>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置应用到根元素的额外 HTML 属性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>
    /// 获取分隔符外层容器计算后的 CSS 类。
    /// </summary>
    /// <remarks>
    /// 外层容器提供 flex 布局与方向专属边距。
    /// </remarks>
    private string WrapperCssClass => ClassNames.cn(
        // flex 布局，正确容纳分隔符
        "atom:flex atom:items-center",

        // 方向专属边距（细微间隔）
        Orientation == SeparatorOrientation.Vertical ? "atom:mx-1" : "atom:my-1"
    );

    /// <summary>
    /// 获取分隔符元素本身计算后的 CSS 类。
    /// </summary>
    /// <remarks>
    /// 即来自 Class 参数的自定义类。
    /// 使用 cn() 工具做智能类名合并与 Tailwind 冲突消解。
    /// </remarks>
    private string? CssClass => Class;
}
