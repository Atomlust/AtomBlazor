using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 为 <see cref="Textarea"/> 提供的表单字段封装组件，自动呈现标签、
/// 帮助文本与错误消息。
/// </summary>
/// <remarks>
/// <para>
/// FormFieldTextarea 在 <see cref="Textarea"/> 之上提供更高层次的抽象，
/// 自动处理标签关联、帮助文本展示与错误消息渲染。
/// </para>
/// <para>
/// 若需要完全掌控布局与错误展示，请直接使用 <see cref="Textarea"/>
/// 配合 <see cref="Field"/> 组件体系。
/// </para>
/// </remarks>
/// <example>
/// <code>
/// &lt;FormFieldTextarea @bind-Value="description"
///                     Label="Description"
///                     HelperText="Max 500 characters"
///                     MaxLength="500"
///                     ShowCharacterCount="true" /&gt;
/// </code>
/// </example>
public partial class FormFieldTextarea : FormFieldBase
{
    private Textarea? _textareaRef;

    // --- Textarea 透传参数 ---

    /// <summary>
    /// 获取或设置文本域的当前值。
    /// </summary>
    [Parameter]
    public string? Value { get; set; }

    /// <summary>
    /// 获取或设置文本域值变化时调用的回调。
    /// </summary>
    [Parameter]
    public EventCallback<string?> ValueChanged { get; set; }

    /// <summary>
    /// 获取或设置标识绑定值的表达式，用于 EditForm 集成。
    /// 由 <c>@bind-Value</c> 自动提供。
    /// </summary>
    [Parameter]
    public Expression<Func<string?>>? ValueExpression { get; set; }

    /// <summary>
    /// 获取或设置 HTML name 属性，透传给内部 Textarea。
    /// </summary>
    [Parameter]
    public string? Name { get; set; }

    /// <summary>
    /// 获取或设置占位文本。
    /// </summary>
    [Parameter]
    public string? Placeholder { get; set; }

    /// <summary>
    /// 获取或设置文本域是否禁用。
    /// </summary>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>
    /// 获取或设置文本域是否必填。
    /// </summary>
    [Parameter]
    public bool Required { get; set; }

    /// <summary>
    /// 获取或设置允许的最大字符数。
    /// </summary>
    [Parameter]
    public int? MaxLength { get; set; }

    /// <summary>
    /// 获取或设置是否在文本域下方显示字符计数。
    /// </summary>
    [Parameter]
    public bool ShowCharacterCount { get; set; }

    /// <summary>
    /// 获取或设置 ValueChanged 的触发时机。
    /// </summary>
    [Parameter]
    public UpdateTiming UpdateTiming { get; set; } = UpdateTiming.OnChange;

    /// <summary>
    /// 获取或设置防抖延迟（毫秒）。
    /// </summary>
    [Parameter]
    public int DebounceInterval { get; set; } = 500;

    /// <summary>
    /// 获取或设置应用到内部 Textarea 元素的额外 CSS 类。
    /// </summary>
    [Parameter]
    public string? InputClass { get; set; }

    /// <summary>
    /// 获取底层 Textarea 组件引用。
    /// </summary>
    public Textarea? TextareaRef => _textareaRef;

    /// <summary>
    /// 让底层文本域元素获得焦点。
    /// </summary>
    public ValueTask FocusAsync() => _textareaRef?.FocusAsync() ?? ValueTask.CompletedTask;

    /// <inheritdoc />
    protected override LambdaExpression? GetFieldExpression() => ValueExpression;

    private async Task HandleValueChanged(string? value)
    {
        Value = value;
        await ValueChanged.InvokeAsync(value);
        NotifyFieldChanged();
    }
}
