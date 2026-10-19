using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using System.Linq.Expressions;

namespace AtomBlazor;

/// <summary>
/// 定义 Switch 组件的尺寸变体。
/// </summary>
public enum SwitchSize
{
    /// <summary>
    /// 小号开关（h-5 w-9，thumb h-4 w-4）。
    /// </summary>
    Small,

    /// <summary>
    /// 中号开关（默认）（h-6 w-11，thumb h-5 w-5）。
    /// </summary>
    Medium,

    /// <summary>
    /// 大号开关（h-7 w-14，thumb h-6 w-6）。
    /// </summary>
    Large
}

/// <summary>
/// 遵循 shadcn/ui 设计体系的开关组件。
/// </summary>
/// <remarks>
/// <para>
/// Switch 组件提供可定制的拨动控件，支持双向数据绑定、表单校验与禁用态。
/// 它自带 ARIA 属性与键盘支持，并接入 Blazor 的事件与表单体系。
/// </para>
/// <para>
/// 特性：
/// - 通过 @bind-Checked 支持双向绑定
/// - 表单校验集成（EditContext）
/// - 多种尺寸变体（Small、Medium、Large）
/// - 支持禁用态
/// - 键盘支持（Space/Enter 拨动）
/// - 自带 ARIA 属性
/// - 通过 CSS 变量兼容暗色模式
/// </para>
/// </remarks>
/// <example>
/// <code>
/// &lt;Switch @bind-Checked="isEnabled" Size="SwitchSize.Medium"&gt;
/// &lt;/Switch&gt;
/// </code>
/// </example>
public partial class Switch : ComponentBase
{
    private FieldIdentifier _fieldIdentifier;
    private EditContext? _editContext;

    /// <summary>
    /// 获取或设置从父级 EditForm 级联而来的 EditContext。
    /// </summary>
    [CascadingParameter]
    private EditContext? CascadedEditContext { get; set; }

    /// <summary>
    /// 获取或设置开关是否处于打开（checked）状态。
    /// </summary>
    /// <remarks>
    /// 通过 <c>@bind-Checked</c> 指令支持双向绑定；该属性变化时触发 <see cref="CheckedChanged"/> 回调。
    /// </remarks>
    [Parameter]
    public bool Checked { get; set; }

    /// <summary>
    /// 获取或设置选中状态变化时调用的回调。
    /// </summary>
    /// <remarks>
    /// 该事件回调支撑 <c>@bind-Checked</c> 双向绑定，用户拨动开关时都会调用它。
    /// </remarks>
    [Parameter]
    public EventCallback<bool> CheckedChanged { get; set; }

    /// <summary>
    /// 获取或设置开关是否禁用。
    /// </summary>
    /// <remarks>
    /// 禁用后：
    /// - 无法点击或聚焦
    /// - 降低不透明度
    /// - 关闭指针事件
    /// - aria-disabled 属性置为 true
    /// </remarks>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>
    /// 获取或设置开关的尺寸变体。
    /// </summary>
    /// <remarks>
    /// 可选尺寸：
    /// - Small：紧凑布局用的小开关
    /// - Medium：默认尺寸（推荐）
    /// - Large：突出操作用的大开关
    /// </remarks>
    [Parameter]
    public SwitchSize Size { get; set; } = SwitchSize.Medium;

    /// <summary>
    /// 获取或设置应用到开关的额外 CSS 类。
    /// </summary>
    /// <remarks>
    /// 自定义类追加在组件基础类之后，便于样式覆盖与扩展。
    /// </remarks>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置要应用到根元素的额外 HTML 属性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>
    /// 获取或设置应用到开关 thumb 的额外 CSS 类。
    /// </summary>
    [Parameter]
    public string? ThumbClass { get; set; }

    /// <summary>
    /// 获取或设置打开状态下应用到 thumb 的额外 CSS 类。
    /// </summary>
    [Parameter]
    public string? ThumbCheckedClass { get; set; }

    /// <summary>
    /// 获取或设置关闭状态下应用到 thumb 的额外 CSS 类。
    /// </summary>
    [Parameter]
    public string? ThumbUncheckedClass { get; set; }

    /// <summary>
    /// 获取或设置在开关 thumb 内部渲染的自定义内容。
    /// 上下文参数是表示打开状态的 bool。
    /// </summary>
    [Parameter]
    public RenderFragment<bool>? ThumbContent { get; set; }

    /// <summary>
    /// 获取或设置开关的 ARIA 标签。
    /// </summary>
    /// <remarks>
    /// 当开关没有配套标签文本时，为屏幕阅读器提供可读名称。
    /// </remarks>
    [Parameter]
    public string? AriaLabel { get; set; }

    /// <summary>
    /// 获取或设置开关元素的 ID 特性。
    /// </summary>
    /// <remarks>
    /// 用于通过 label 的 for 属性把开关与标签元素关联起来。
    /// </remarks>
    [Parameter]
    public string? Id { get; set; }

    /// <summary>
    /// 获取或设置描述该开关的元素 ID 集合，用于无障碍。
    /// </summary>
    [Parameter]
    public string? AriaDescribedBy { get; set; }

    /// <summary>
    /// 获取或设置标识绑定值的表达式。
    /// </summary>
    /// <remarks>
    /// 用于表单校验集成。提供后，开关会注册到 EditContext 并参与表单校验。
    /// </remarks>
    [Parameter]
    public Expression<Func<bool>>? CheckedExpression { get; set; }

    /// <summary>
    /// 获取开关是否处于无效状态（用于校验）。
    /// </summary>
    private bool IsInvalid
    {
        get
        {
            if (_editContext != null && CheckedExpression != null && _fieldIdentifier.FieldName != null)
            {
                return _editContext.GetValidationMessages(_fieldIdentifier).Any();
            }

            return false;
        }
    }

    /// <summary>
    /// 获取开关轨道元素的计算 CSS 类。
    /// </summary>
    /// <remarks>
    /// 组合：
    /// - 开关轨道基础样式（圆角、过渡）
    /// - 尺寸相关宽高
    /// - 状态相关类（打开、禁用）
    /// - 键盘导航的焦点环
    /// - Class 参数带来的自定义类
    /// 使用 cn() 工具做智能类合并与 Tailwind 冲突消解。
    /// </remarks>
    private string CssClass => ClassNames.cn(
        // 开关基础样式（ shadcn/ui）
        "atom:peer atom:inline-flex atom:shrink-0 atom:cursor-pointer atom:items-center atom:rounded-full atom:border-2 atom:border-transparent",
        "atom:transition-colors atom:focus-visible:outline-none atom:focus-visible:ring-2",
        "atom:focus-visible:ring-ring atom:focus-visible:ring-offset-2 atom:focus-visible:ring-offset-background",
        "atom:disabled:cursor-not-allowed atom:disabled:opacity-50",
        // 尺寸变体
        Size switch
        {
            SwitchSize.Small => "atom:h-5 atom:w-9",
            SwitchSize.Medium => "atom:h-6 atom:w-11",
            SwitchSize.Large => "atom:h-7 atom:w-14",
            _ => "atom:h-6 atom:w-11"
        },
        // 打开态样式
        Checked ? "atom:bg-primary" : "atom:bg-input",
        // 自定义类（若提供）
        Class
    );

    /// <summary>
    /// 获取开关 thumb 元素的计算 CSS 类。
    /// </summary>
    /// <remarks>
    /// 组合：
    /// - thumb 基础样式（圆形、背景）
    /// - 尺寸相关宽高
    /// - 依据打开状态计算的位移
    /// - 平滑过渡
    /// 使用 cn() 工具做智能类合并与 Tailwind 冲突消解。
    /// </remarks>
    private string ThumbCssClass
    {
        get
        {
            // 尺寸变体与位移。transform 没有逻辑形式，因此 dir="rtl" 下位移取反——
            // 否则 thumb 会朝远离后缘的方向移动，跑出轨道之外。
            var (thumbSize, translateX) = Size switch
            {
                SwitchSize.Small => ("atom:h-4 atom:w-4", Checked ? "atom:translate-x-4 atom:rtl:-translate-x-4" : "atom:translate-x-0"),
                SwitchSize.Medium => ("atom:h-5 atom:w-5", Checked ? "atom:translate-x-5 atom:rtl:-translate-x-5" : "atom:translate-x-0"),
                SwitchSize.Large => ("atom:h-6 atom:w-6", Checked ? "atom:translate-x-7 atom:rtl:-translate-x-7" : "atom:translate-x-0"),
                _ => ("atom:h-5 atom:w-5", Checked ? "atom:translate-x-5 atom:rtl:-translate-x-5" : "atom:translate-x-0")
            };

            return ClassNames.cn(
                // thumb 基础样式
                "atom:pointer-events-none atom:block atom:rounded-full atom:bg-background atom:shadow-lg atom:ring-0 atom:transition-transform",
                // 尺寸与位移
                thumbSize,
                translateX,
                // 提供 ThumbContent 时的居中对齐
                ThumbContent is not null ? "atom:flex atom:items-center atom:justify-center" : null,
                // 自定义类（若提供）
                ThumbClass,
                // 状态相关类
                Checked ? ThumbCheckedClass : ThumbUncheckedClass
            );
        }
    }

    /// <summary>
    /// 处理来自原语的选中状态变化，并接入表单校验。
    /// </summary>
    private async Task HandleCheckedChanged(bool value)
    {
        Checked = value;
        await CheckedChanged.InvokeAsync(Checked);

        // 通知 EditContext 字段已变化以触发校验。
        if (_editContext != null && CheckedExpression != null && _fieldIdentifier.FieldName != null)
        {
            _editContext.NotifyFieldChanged(_fieldIdentifier);
        }
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        base.OnParametersSet();

        // 存在级联 EditContext 时初始化校验集成。
        if (CascadedEditContext != null && CheckedExpression != null)
        {
            _editContext = CascadedEditContext;
            _fieldIdentifier = FieldIdentifier.Create(CheckedExpression);
        }
    }
}
