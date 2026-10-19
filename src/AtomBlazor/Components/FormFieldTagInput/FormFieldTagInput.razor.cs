using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// <see cref="TagInput"/> 的表单字段封装，提供自动的标签、
/// 帮助文本与错误消息展示。
/// </summary>
public partial class FormFieldTagInput : FormFieldBase
{
    /// <summary>
    /// 获取或设置标签列表。
    /// </summary>
    [Parameter]
    public IReadOnlyList<string>? Tags { get; set; }

    /// <summary>
    /// 获取或设置标签列表变化时调用的回调。
    /// </summary>
    [Parameter]
    public EventCallback<IReadOnlyList<string>?> TagsChanged { get; set; }

    /// <summary>
    /// 获取或设置 Tags 属性的表达式，用于 EditForm 校验。
    /// </summary>
    [Parameter]
    public Expression<Func<IReadOnlyList<string>?>>? TagsExpression { get; set; }

    /// <summary>
    /// 获取或设置占位符文本。
    /// </summary>
    [Parameter]
    public string? Placeholder { get; set; }

    /// <summary>
    /// 获取或设置允许的最大标签数量。
    /// </summary>
    [Parameter]
    public int MaxTags { get; set; } = int.MaxValue;

    /// <summary>
    /// 获取或设置单个标签的最大字符长度。
    /// </summary>
    [Parameter]
    public int MaxTagLength { get; set; } = 50;

    /// <summary>
    /// 获取或设置是否允许重复标签。
    /// </summary>
    [Parameter]
    public bool AllowDuplicates { get; set; }

    /// <summary>
    /// 获取或设置触发标签创建的按键。
    /// </summary>
    [Parameter]
    public TagInputTrigger AddTrigger { get; set; } = TagInputTrigger.Enter | TagInputTrigger.Comma;

    /// <summary>
    /// 获取或设置可选的校验函数。
    /// </summary>
    [Parameter]
    public Func<string, bool>? Validate { get; set; }

    /// <summary>
    /// 获取或设置标签被拒绝时调用的回调。
    /// </summary>
    [Parameter]
    public EventCallback<string> OnTagRejected { get; set; }

    /// <summary>
    /// 获取或设置静态建议列表。
    /// </summary>
    [Parameter]
    public IEnumerable<string>? Suggestions { get; set; }

    /// <summary>
    /// 获取或设置异步加载建议的函数。
    /// </summary>
    [Parameter]
    public Func<string, CancellationToken, Task<IEnumerable<string>>>? OnSearchSuggestions { get; set; }

    /// <summary>
    /// 获取或设置建议搜索的去抖间隔（毫秒）。
    /// </summary>
    [Parameter]
    public int SuggestionDebounceMs { get; set; } = 300;

    /// <summary>
    /// 获取或设置渲染标签的视觉变体。
    /// </summary>
    [Parameter]
    public TagInputVariant Variant { get; set; } = TagInputVariant.Default;

    /// <summary>
    /// 获取或设置是否显示全部清除按钮。
    /// </summary>
    [Parameter]
    public bool Clearable { get; set; }

    /// <summary>
    /// 获取或设置组件是否禁用。
    /// </summary>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>
    /// 获取或设置每个标签的可选自定义渲染模板。
    /// </summary>
    [Parameter]
    public RenderFragment<string>? TagTemplate { get; set; }

    /// <summary>
    /// 获取或设置应用到内部 TagInput 容器的额外 CSS 类。
    /// </summary>
    [Parameter]
    public string? InputClass { get; set; }

    /// <inheritdoc />
    protected override LambdaExpression? GetFieldExpression() => TagsExpression;

    private async Task HandleTagsChanged(IReadOnlyList<string>? tags)
    {
        Tags = tags;
        await TagsChanged.InvokeAsync(tags);
        NotifyFieldChanged();
    }
}
