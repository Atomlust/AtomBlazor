using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace AtomBlazor.Primitives;

/// <summary>
/// 无外观复选框基元：只提供行为与可访问性能力，不带任何样式。
/// </summary>
/// <remarks>
/// <para>
/// CheckboxPrimitive 组件是一枚无外观基元，负责处理复选框的全部行为、
/// 键盘导航与可访问性特性；它不提供任何默认样式。
/// </para>
/// <para>
/// 该基元支持三种状态：
/// <list type="bullet">
/// <item>选中（true）</item>
/// <item>未选中（false）</item>
/// <item>不确定（mixed）——通常用于「全选」场景</item>
/// </list>
/// </para>
/// <para>
/// 内置的可访问性特性：
/// <list type="bullet">
/// <item>带 checkbox 角色的语义化 button 元素</item>
/// <item>aria-checked 属性（不确定态取 mixed）</item>
/// <item>键盘支持（空格键切换）</item>
/// <item>禁用状态配套相应 ARIA 属性</item>
/// <item>data-state 属性供 CSS 挂样式钩子</item>
/// </list>
/// </para>
/// </remarks>
/// <example>
/// 基础无外观复选框：
/// <code>
/// &lt;CheckboxPrimitive @bind-Checked="isAccepted" /&gt;
/// </code>
///
/// 自定义类名的带样式复选框：
/// <code>
/// &lt;CheckboxPrimitive @bind-Checked="isChecked"
///                    class="h-4 w-4 rounded border border-primary"&gt;
///     @if (isChecked)
///     {
///         &lt;svg&gt;...checkmark icon...&lt;/svg&gt;
///     }
/// &lt;/CheckboxPrimitive&gt;
/// </code>
///
/// 不确定态复选框：
/// <code>
/// &lt;CheckboxPrimitive Checked="@someChecked"
///                    Indeterminate="@someIndeterminate"
///                    CheckedChanged="@HandleCheckedChanged" /&gt;
/// </code>
/// </example>
public partial class CheckboxPrimitive : ComponentBase
{
    /// <summary>
    /// 获取或设置复选框是否选中。
    /// </summary>
    /// <remarks>
    /// 通过 <c>@bind-Checked</c> 指令支持双向绑定；
    /// 该属性变化时会触发 <see cref="CheckedChanged"/> 回调。
    /// </remarks>
    [Parameter]
    public bool Checked { get; set; }

    /// <summary>
    /// 获取或设置选中状态变化时调用的回调。
    /// </summary>
    /// <remarks>
    /// 该事件回调支撑 <c>@bind-Checked</c> 双向绑定，
    /// 用户拨动复选框状态时都会调用它。
    /// </remarks>
    [Parameter]
    public EventCallback<bool> CheckedChanged { get; set; }

    /// <summary>
    /// 获取或设置复选框是否处于不确定（indeterminate）状态。
    /// </summary>
    /// <remarks>
    /// 不确定状态通常用于「全选」复选框——子项只有部分被选中时。
    /// 为 true 时 <c>aria-checked</c> 属性写作 <c>mixed</c>，
    /// 屏幕阅读器据此播报三态。
    /// </remarks>
    [Parameter]
    public bool Indeterminate { get; set; }

    /// <summary>
    /// 获取或设置不确定状态变化时调用的回调。
    /// </summary>
    [Parameter]
    public EventCallback<bool> IndeterminateChanged { get; set; }

    /// <summary>
    /// 获取或设置复选框是否禁用。
    /// </summary>
    /// <remarks>
    /// 禁用后：
    /// - 无法点击或聚焦
    /// - aria-disabled 属性置为 true
    /// - 键盘事件被忽略
    /// </remarks>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>
    /// 获取或设置复选框元素的 ID 特性。
    /// </summary>
    /// <remarks>
    /// 用于通过 label 的 for 属性把复选框与标签元素关联起来。
    /// </remarks>
    [Parameter]
    public string? Id { get; set; }

    /// <summary>
    /// 获取或设置复选框在表单上下文中是否必填。
    /// 为 true 时为屏幕阅读器输出 <c>aria-required="true"</c>。
    /// </summary>
    [Parameter]
    public bool Required { get; set; }

    /// <summary>
    /// 获取或设置复选框的 ARIA 标签。
    /// </summary>
    /// <remarks>
    /// 当复选框没有配套标签文本时，
    /// 为屏幕阅读器提供可读名称。
    /// </remarks>
    [Parameter]
    public string? AriaLabel { get; set; }

    /// <summary>
    /// 获取或设置渲染在复选框内部的内容。
    /// </summary>
    /// <value>
    /// 包含复选框内容（例如勾选图标）的 <see cref="RenderFragment"/>，或为 <c>null</c>。
    /// </value>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// 获取或设置要应用到 button 元素的额外属性。
    /// </summary>
    /// <value>
    /// 包含 class、style 等额外 HTML 属性的字典。
    /// </value>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>
    /// 获取 <c>aria-checked</c> 属性的取值。
    /// </summary>
    /// <remarks>
    /// 不确定态返回 <c>"mixed"</c>，选中返回 <c>"true"</c>，未选中返回 <c>"false"</c>。
    /// </remarks>
    private string AriaCheckedValue
    {
        get
        {
            if (Indeterminate)
            {
                return "mixed";
            }

            return Checked.ToString().ToLower(CultureInfo.InvariantCulture);
        }
    }

    /// <summary>
    /// 获取供 CSS 使用的 <c>data-state</c> 属性取值。
    /// </summary>
    private string DataState
    {
        get
        {
            if (Indeterminate)
            {
                return "indeterminate";
            }

            return Checked ? "checked" : "unchecked";
        }
    }

    /// <summary>
    /// 处理复选框点击事件。
    /// </summary>
    /// <param name="args">鼠标事件参数。</param>
    /// <remarks>
    /// 拨动选中状态并清除不确定状态。
    /// 复选框禁用时不做任何事。
    /// </remarks>
    private async Task HandleClick(MouseEventArgs args)
    {
        if (!Disabled)
        {
            await ToggleChecked();
        }
    }

    /// <summary>
    /// 拨动选中状态并通知监听方。
    /// </summary>
    /// <remarks>
    /// 用户点击时清除不确定状态。
    /// </remarks>
    private async Task ToggleChecked()
    {
        // 用户点击时清除不确定状态。
        if (Indeterminate)
        {
            Indeterminate = false;
            await IndeterminateChanged.InvokeAsync(false);
        }

        Checked = !Checked;
        await CheckedChanged.InvokeAsync(Checked);
    }
}
