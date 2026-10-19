using System.Linq.Expressions;
using AtomBlazor.Primitives; // 经 GlobalUsings.cs 全局引用 Primitives，本仓无全局 using 惯例，此处文件级显式引用 SelectOption{TValue}
using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 为 MultiSelect 组件提供的表单字段封装组件
/// 自动呈现标签、帮助文本与错误消息。
/// </summary>
/// <typeparam name="TValue">所选中值的类型。</typeparam>
public partial class FormFieldMultiSelect<TValue> : FormFieldBase
{
    /// <summary>该选择是否必填。</summary>
    [Parameter] public bool Required { get; set; }
    /// <summary>应用到打开态触发器的类。</summary>
    [Parameter] public string? ActiveClass { get; set; } = "atom:bg-accent atom:text-accent-foreground";

    /// <summary>
    /// 获取或设置要展示在多选框中的选项集合。
    /// 提供时使用 Options 模式；为 null 时使用组合模式（ChildContent）。
    /// </summary>
    [Parameter]
    public IEnumerable<SelectOption<TValue>>? Options { get; set; }

    /// <summary>
    /// 获取或设置组合模式下的子内容。
    /// 以 MultiSelectItem 组件作为子内容可实现丰富的条目渲染。
    /// 仅在 Options 为 null 时使用。
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// 获取或设置当前选中的值集合。
    /// </summary>
    [Parameter]
    public IEnumerable<TValue>? Values { get; set; }

    /// <summary>
    /// 获取或设置选中值集合变化时调用的回调。
    /// </summary>
    [Parameter]
    public EventCallback<IEnumerable<TValue>?> ValuesChanged { get; set; }

    /// <summary>
    /// 获取或设置标识绑定值集合的表达式，用于 EditForm 集成。
    /// </summary>
    [Parameter]
    public Expression<Func<IEnumerable<TValue>?>>? ValuesExpression { get; set; }

    /// <summary>
    /// 获取或设置未选中任何条目时展示的占位文本。
    /// </summary>
    [Parameter]
    public string Placeholder { get; set; } = "Select items...";

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
    /// 获取或设置"全选"条目的标签文本。
    /// </summary>
    [Parameter]
    public string SelectAllLabel { get; set; } = "Select All";

    /// <summary>
    /// 获取或设置是否展示"全选"条目。
    /// </summary>
    [Parameter]
    public bool ShowSelectAll { get; set; } = true;

    /// <summary>
    /// 获取或设置清除按钮的标签文本。
    /// </summary>
    [Parameter]
    public string ClearLabel { get; set; } = "Clear";

    /// <summary>
    /// 获取或设置关闭按钮的标签文本。
    /// </summary>
    [Parameter]
    public string CloseLabel { get; set; } = "Close";

    /// <summary>
    /// 获取或设置多选框是否禁用。
    /// </summary>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>
    /// 获取或设置徽标达到多少枚后折叠显示为"+N more"。
    /// </summary>
    [Parameter]
    public int MaxDisplayTags { get; set; } = 3;

    /// <summary>
    /// 获取或设置应用到内部 MultiSelect 元素的额外 CSS 类。
    /// </summary>
    [Parameter]
    public string? InputClass { get; set; }

    /// <summary>
    /// 获取或设置浮层内容的宽度。
    /// </summary>
    [Parameter]
    public string PopoverWidth { get; set; } = "atom:w-[300px]";

    /// <summary>
    /// 获取或设置是否让下拉列表宽度对齐触发元素宽度。
    /// </summary>
    [Parameter]
    public bool MatchTriggerWidth { get; set; }

    /// <summary>
    /// 获取或设置点击下拉列表外部时是否自动关闭。
    /// </summary>
    [Parameter]
    public bool AutoClose { get; set; } = true;

    /// <summary>
    /// 获取或设置搜索查询变化时调用的回调。
    /// 用于服务端过滤或加载外部数据。
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

    /// <inheritdoc />
    protected override LambdaExpression? GetFieldExpression() => ValuesExpression;

    private async Task HandleValuesChanged(IEnumerable<TValue>? values)
    {
        Values = values;
        await ValuesChanged.InvokeAsync(values);
        NotifyFieldChanged();
    }
}
