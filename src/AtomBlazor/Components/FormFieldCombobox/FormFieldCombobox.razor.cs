using System.Linq.Expressions;
using AtomBlazor.Primitives; // 经 GlobalUsings.cs 全局引用 Primitives，本仓无全局 using 惯例，此处文件级显式引用 SelectOption{TValue}
using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 为 Combobox 组件提供的表单字段封装组件
/// 自动呈现标签、帮助文本与错误消息。
/// </summary>
/// <typeparam name="TValue">所选中值的类型。</typeparam>
public partial class FormFieldCombobox<TValue> : FormFieldBase
{
    /// <summary>
    /// 获取或设置要展示在组合框中的选项集合。
    /// 提供时使用 Options 模式；为 null 时使用组合模式（ChildContent）。
    /// </summary>
    [Parameter]
    public IEnumerable<SelectOption<TValue>>? Options { get; set; }

    /// <summary>
    /// 获取或设置组合模式下的子内容。
    /// 以 ComboboxItem 组件作为子内容可实现丰富的条目渲染。
    /// 仅在 Options 为 null 时使用。
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

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
    /// 获取或设置未选中任何条目时展示的占位文本。
    /// </summary>
    [Parameter]
    public string Placeholder { get; set; } = "Select an option...";

    /// <summary>
    /// 获取或设置搜索输入框中展示的占位文本。
    /// </summary>
    [Parameter]
    public string SearchPlaceholder { get; set; } = "Search...";

    /// <summary>
    /// 获取或设置没有条目匹配搜索词时展示的消息。
    /// </summary>
    [Parameter]
    public string EmptyMessage { get; set; } = "No results found.";

    /// <summary>
    /// 获取或设置当前的搜索查询文本。
    /// 与 <see cref="SearchQueryChanged"/> 搭配可做双向绑定以响应过滤变化
    /// 例如用于服务端过滤或加载追加数据。
    /// </summary>
    [Parameter]
    public string SearchQuery { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置搜索查询变化时调用的回调。
    /// </summary>
    [Parameter]
    public EventCallback<string> SearchQueryChanged { get; set; }

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

    /// <summary>
    /// 获取或设置组合框是否禁用。
    /// </summary>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>
    /// 获取或设置组合框是否必填。
    /// </summary>
    [Parameter]
    public bool Required { get; set; }

    /// <summary>
    /// 获取或设置应用到内部 Combobox 元素的额外 CSS 类。
    /// </summary>
    [Parameter]
    public string? InputClass { get; set; }

    /// <summary>
    /// 获取或设置浮层内容的宽度。
    /// </summary>
    [Parameter]
    public string PopoverWidth { get; set; } = "atom:w-[200px]";

    /// <summary>
    /// 获取或设置是否让下拉列表宽度对齐触发元素宽度。
    /// </summary>
    [Parameter]
    public bool MatchTriggerWidth { get; set; }

    /// <summary>
    /// 获取或设置下拉列表打开时应用到触发器的 CSS 类。
    /// 缺省为 <c>"bg-accent text-accent-foreground"</c>。
    /// 设为 <c>null</c> 或空串即禁用激活样式。
    /// </summary>
    [Parameter]
    public string? ActiveClass { get; set; } = "atom:bg-accent atom:text-accent-foreground";

    /// <inheritdoc />
    protected override LambdaExpression? GetFieldExpression() => ValueExpression;

    /// <summary>
    /// 转发给内部组合框的搜索查询文本。仅在调用方主动绑定该值时
    /// 才把本地 <see cref="SearchQuery"/> 的值透出
    /// 否则返回空串，让内部组合框自行管理
    /// 搜索状态而不被父组件重渲染覆盖。
    /// </summary>
    private string InnerSearchQuery => SearchQueryChanged.HasDelegate ? SearchQuery : string.Empty;

    /// <summary>
    /// 转发给内部组合框的搜索查询变化回调。仅在调用方绑定
    /// <see cref="SearchQueryChanged"/> 时才接线；否则返回
    /// 默认值（无委托），让内部组合框保持内部文本过滤
    /// 而不切换为外部过滤模式。
    /// </summary>
    private EventCallback<string> InnerSearchQueryChanged => SearchQueryChanged.HasDelegate
        ? EventCallback.Factory.Create<string>(this, HandleSearchQueryChanged)
        : default;

    private async Task HandleValueChanged(TValue? value)
    {
        Value = value;
        await ValueChanged.InvokeAsync(value);
        NotifyFieldChanged();
    }

    private async Task HandleSearchQueryChanged(string query)
    {
        SearchQuery = query;
        await SearchQueryChanged.InvokeAsync(query);
    }
}
