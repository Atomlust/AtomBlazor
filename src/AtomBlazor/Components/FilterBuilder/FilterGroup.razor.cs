using Microsoft.AspNetCore.Components;
using AtomBlazor.Primitives;
using AtomBlazor.Primitives.Filtering;

namespace AtomBlazor;

/// <summary>
/// 渲染一组筛选条件，带 AND/OR 切换、嵌套子组，
/// 以及添加条件或分组的按钮。
/// </summary>
public partial class FilterGroup : ComponentBase
{
    [Inject] private ILocalizer Localizer { get; set; } = default!;

    // Built from the localizer in OnInitialized — can't be static since it depends on injected state.
    private IEnumerable<SelectOption<LogicalOperator>> logicalOperatorOptions = default!;

    protected override void OnInitialized()
    {
        logicalOperatorOptions = new[]
        {
            new SelectOption<LogicalOperator>(LogicalOperator.And, Localizer["FilterBuilder.OperatorAnd"]),
            new SelectOption<LogicalOperator>(LogicalOperator.Or, Localizer["FilterBuilder.OperatorOr"])
        };
    }

    /// <summary>
    /// 获取或设置筛选分组数据。
    /// </summary>
    [Parameter]
    public FilterDefinition Group { get; set; } = null!;

    /// <summary>
    /// 获取或设置该分组当前的嵌套深度。
    /// </summary>
    [Parameter]
    public int Depth { get; set; }

    /// <summary>
    /// 获取或设置该分组是否为根分组。
    /// </summary>
    [Parameter]
    public bool IsRoot { get; set; }

    /// <summary>
    /// 获取或设置需要移除该分组时调用的回调。
    /// </summary>
    [Parameter]
    public EventCallback OnRemove { get; set; }

    /// <summary>
    /// 获取或设置应用到根元素的附加 HTML 属性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>
    /// 获取级联传入的 FilterBuilder 上下文。
    /// </summary>
    [CascadingParameter]
    private FilterBuilderContext? Context { get; set; }

    private Task HandleOperatorChanged(LogicalOperator newOperator)
    {
        Group.Operator = newOperator;
        HandleChanged();
        return Task.CompletedTask;
    }

    private void AddCondition()
    {
        if (Context?.IsAtConditionLimit == true)
        {
            return;
        }

        Group.Conditions.Add(new Primitives.Filtering.FilterCondition());
        HandleChanged();
    }

    private void AddGroup()
    {
        if (Context == null || Depth >= Context.MaxDepth || Context.IsAtConditionLimit)
        {
            return;
        }

        var newGroup = new FilterDefinition
        {
            Operator = Context.DefaultOperator
        };
        newGroup.Conditions.Add(new Primitives.Filtering.FilterCondition());
        Group.Groups.Add(newGroup);
        HandleChanged();
    }

    private void RemoveCondition(Primitives.Filtering.FilterCondition condition)
    {
        Group.Conditions.Remove(condition);
        HandleChanged();
    }

    private void RemoveGroup(FilterDefinition group)
    {
        Group.Groups.Remove(group);
        HandleChanged();
    }

    private async Task HandleRemoveGroup() =>
        await OnRemove.InvokeAsync();

    private void HandleChanged()
    {
        Context?.NotifyChanged();
        StateHasChanged();
    }

    /// <summary>
    /// 该分组的无障碍名称。其两部分原本都是英文字面量，于是本地化的应用无论页面其余部分
    /// 用什么语言，都只会用英文播报其筛选分组。
    /// </summary>
    private string GroupAriaLabel => IsRoot
        ? Localizer["FilterBuilder.RootGroup"]
        : Localizer["FilterBuilder.NestedGroup", Depth];

    // Depth-based accent colors for visual nesting
    private static readonly string[] depthBorderColors = new[]
    {
        "atom:border-s-primary",
        "atom:border-s-blue-500",
        "atom:border-s-purple-500",
        "atom:border-s-orange-500"
    };

    private string DepthBorderColor => depthBorderColors[Depth % depthBorderColors.Length];

    private string GroupCssClass => ClassNames.cn(
        "atom:rounded-lg atom:border atom:p-3 atom:space-y-3",
        !IsRoot ? ClassNames.cn("atom:border-s-4", DepthBorderColor, "atom:bg-muted/30") : "atom:bg-background"
    );

    private static string HeaderCssClass => "atom:flex atom:items-center atom:justify-between";

    private static string ConditionsCssClass => "atom:space-y-2";
}
