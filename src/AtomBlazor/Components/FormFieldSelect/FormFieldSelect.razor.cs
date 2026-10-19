using System.Linq.Expressions;
using AtomBlazor.Primitives; // 经 GlobalUsings.cs 全局引用 Primitives，本仓无全局 using 惯例，此处文件级显式引用 SelectOption{TValue}
using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 为 Select 组件提供的表单字段封装组件，以更简化的 API
/// 自动呈现标签、帮助文本与错误消息。
/// </summary>
/// <remarks>
/// FormFieldSelect 自动生成 SelectTrigger、SelectValue 与 SelectContent
/// 使用者只需通过 ChildContent 提供 SelectItem 子内容。
/// </remarks>
/// <typeparam name="TValue">所选中值的类型。</typeparam>
public partial class FormFieldSelect<TValue> : FormFieldBase
{
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
    /// </summary>
    [Parameter]
    public Expression<Func<TValue?>>? ValueExpression { get; set; }

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
    /// 获取或设置未选中任何值时展示的占位文本。
    /// </summary>
    [Parameter]
    public string? Placeholder { get; set; }

    /// <summary>
    /// 获取或设置从选中值推导展示文本的函数。
    /// </summary>
    [Parameter]
    public Func<TValue, string>? DisplayTextSelector { get; set; }

    /// <summary>
    /// 获取或设置应用到内部 Select 元素的额外 CSS 类。
    /// </summary>
    [Parameter]
    public string? InputClass { get; set; }

    /// <summary>
    /// 获取或设置要展示的值/文本配对集合。
    /// 每个选项包含一个值（选中时绑定）与展示文本（在下拉列表与触发器中显示）。
    /// 提供时自动生成 SelectItem 子内容，优先级高于 <see cref="ChildContent"/>。
    /// </summary>
    [Parameter]
    public IEnumerable<SelectOption<TValue>>? Options { get; set; }

    /// <summary>
    /// 获取或设置渲染在下拉列表内部的内容，应包含 SelectItem 组件。
    /// 提供 Options 时被忽略。
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// 获取或设置在用户滚动到下拉列表底部附近时调用的回调。
    /// 用于无限滚动场景下加载追加条目。
    /// </summary>
    [Parameter]
    public EventCallback OnLoadMore { get; set; }

    /// <summary>
    /// 获取或设置是否正在加载追加条目。
    /// 为 true 时在列表底部显示加载指示器，并在加载完成前抑制
    /// <see cref="OnLoadMore"/>。
    /// </summary>
    [Parameter]
    public bool IsLoading { get; set; }

    /// <summary>
    /// 获取或设置在所有条目加载完毕后显示在列表底部的消息。
    /// 仅在 <see cref="IsLoading"/> 为 false 时显示；设为 <c>null</c> 或空串即隐藏。
    /// </summary>
    [Parameter]
    public string? EndOfListMessage { get; set; }

    /// <inheritdoc />
    protected override LambdaExpression? GetFieldExpression() => ValueExpression;

    private async Task HandleValueChanged(TValue? value)
    {
        Value = value;
        await ValueChanged.InvokeAsync(value);
        NotifyFieldChanged();
    }

    private Func<TValue, string>? GetEffectiveDisplayTextSelector()
    {
        if (Options is not null)
        {
            var options = Options;
            return v => options
                .FirstOrDefault(o => EqualityComparer<TValue>.Default.Equals(o.Value, v))
                ?.Text ?? v?.ToString() ?? "";
        }

        return DisplayTextSelector;
    }
}
