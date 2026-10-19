using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 为 <see cref="NativeSelect{TValue}"/> 提供的表单字段封装组件，
/// 自动呈现标签、帮助文本与错误消息。
/// </summary>
/// <remarks>
/// <para>
/// FormFieldNativeSelect 在 <see cref="NativeSelect{TValue}"/> 之上提供更高层次的抽象，
/// 自动处理标签关联、帮助文本展示与错误消息渲染。
/// </para>
/// <para>
/// 若需要完全掌控布局与错误展示，请直接使用 <see cref="NativeSelect{TValue}"/>
/// 配合 <see cref="Field"/> 组件体系。
/// </para>
/// </remarks>
/// <typeparam name="TValue">所选中值的类型。</typeparam>
/// <example>
/// <code>
/// &lt;FormFieldNativeSelect TValue="string"
///                          @bind-Value="country"
///                          Label="Country"
///                          Placeholder="Select a country"
///                          HelperText="Choose your country of residence"&gt;
///     &lt;option value="us"&gt;United States&lt;/option&gt;
///     &lt;option value="uk"&gt;United Kingdom&lt;/option&gt;
/// &lt;/FormFieldNativeSelect&gt;
/// </code>
/// </example>
public partial class FormFieldNativeSelect<TValue> : FormFieldBase
{
    // --- NativeSelect 透传参数 ---

    /// <summary>
    /// 获取或设置当前选中的值。
    /// </summary>
    [Parameter]
    public TValue? Value { get; set; }

    /// <summary>
    /// 获取或设置选中值变化时调用的回调。
    /// </summary>
    [Parameter]
    public EventCallback<TValue?> ValueChanged { get; set; }

    /// <summary>
    /// 获取或设置标识绑定值的表达式，用于 EditForm 集成。
    /// 由 <c>@bind-Value</c> 自动提供。
    /// </summary>
    [Parameter]
    public Expression<Func<TValue?>>? ValueExpression { get; set; }

    /// <summary>
    /// 获取或设置未选中任何值时展示的占位文本。
    /// </summary>
    [Parameter]
    public string? Placeholder { get; set; }

    /// <summary>
    /// 获取或设置选择框是否禁用。
    /// </summary>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>
    /// 获取或设置选择框是否必填。
    /// </summary>
    [Parameter]
    public bool Required { get; set; }

    /// <summary>
    /// 获取或设置选择框的尺寸变体。
    /// </summary>
    [Parameter]
    public NativeSelectSize Size { get; set; } = NativeSelectSize.Default;

    /// <summary>
    /// 获取或设置渲染在 select 元素内部的内容。
    /// 应包含 <c>&lt;option&gt;</c> 元素。
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// 获取或设置应用到内部 NativeSelect 元素的额外 CSS 类。
    /// </summary>
    [Parameter]
    public string? InputClass { get; set; }

    /// <inheritdoc />
    protected override LambdaExpression? GetFieldExpression() => ValueExpression;

    private async Task HandleValueChanged(TValue? value)
    {
        Value = value;
        await ValueChanged.InvokeAsync(value);
        NotifyFieldChanged();
    }
}
