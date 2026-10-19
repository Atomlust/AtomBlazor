using System.Linq.Expressions;
using AtomBlazor.Primitives;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace AtomBlazor;

/// <summary>
/// 两个列表，外加在两者之间移动选项的按钮。
/// </summary>
/// <remarks>
/// <para>
/// 勾选行后按下按钮来移动，而非拖拽。对于长到需要滚动条的列表，
/// 这正是可用控件与令人抓狂控件的分界——而且与拖拽不同，它可以用键盘完成。
/// </para>
/// <para>
/// 只有一个绑定，而不是两个。<see cref="Options"/> 持有全部选项，
/// <see cref="Values"/> 持有被挑选的选项；可用窗格显示剩余部分。
/// 两个绑定的集合可能彼此脱节——同一选项同时出现在两个窗格，或都不出现——
/// 而这里没有任何一方被指望去调和这种不一致。
/// </para>
/// </remarks>
/// <typeparam name="TValue">选项值的类型。</typeparam>
public partial class PickList<TValue> : ComponentBase
{
    private IEnumerable<TValue>? sourceSelection;
    private IEnumerable<TValue>? targetSelection;
    private string? sourceSearch;
    private string? targetSearch;

    private EditContext? editContext;
    private FieldIdentifier fieldIdentifier;

    [CascadingParameter] private EditContext? CascadedEditContext { get; set; }

    /// <summary>
    /// 获取或设置全部选项，按可用窗格应显示的顺序排列。
    /// </summary>
    [Parameter]
    public IEnumerable<SelectOption<TValue>>? Options { get; set; }

    /// <summary>
    /// 获取或设置已挑选的值，按已选窗格显示的顺序排列。
    /// </summary>
    /// <remarks>
    /// 顺序按移动发生的先后保留，而非按 <see cref="Options"/> 中的列举顺序，
    /// 因为穿梭框常常是在构建一个有序的东西——一组列、一份播放列表、一串步骤。
    /// </remarks>
    [Parameter]
    public IEnumerable<TValue>? Values { get; set; }

    /// <summary>
    /// 获取或设置在已挑选的值变化时调用的回调。与
    /// <c>@bind-Values</c> 搭配使用。
    /// </summary>
    [Parameter]
    public EventCallback<IEnumerable<TValue>?> ValuesChanged { get; set; }

    /// <summary>
    /// 获取或设置标识绑定值集合的表达式，用于
    /// <c>EditForm</c> 内的校验。
    /// </summary>
    [Parameter]
    public Expression<Func<IEnumerable<TValue>?>>? ValuesExpression { get; set; }

    /// <summary>
    /// 获取或设置在选项于两个窗格之间移动之后调用的回调。
    /// </summary>
    [Parameter]
    public EventCallback<PickListMoveEventArgs<TValue>> OnMove { get; set; }

    /// <summary>
    /// 获取或设置显示在尚未挑选选项窗格上方的标签。
    /// </summary>
    [Parameter]
    public string? SourceLabel { get; set; }

    /// <summary>
    /// 获取或设置显示在已挑选选项窗格上方的标签。
    /// </summary>
    [Parameter]
    public string? TargetLabel { get; set; }

    /// <summary>
    /// 获取或设置在可用窗格为空时显示的消息。
    /// </summary>
    [Parameter]
    public string? SourceEmptyMessage { get; set; }

    /// <summary>
    /// 获取或设置在已选窗格为空时显示的消息。
    /// </summary>
    [Parameter]
    public string? TargetEmptyMessage { get; set; }

    /// <summary>
    /// 获取或设置每个窗格是否带搜索框。
    /// </summary>
    [Parameter]
    public bool ShowSearch { get; set; }

    /// <summary>
    /// 获取或设置每个窗格是否带全选复选框。
    /// </summary>
    [Parameter]
    public bool ShowSelectAll { get; set; } = true;

    /// <summary>
    /// 获取或设置是否显示"移动全部"按钮。
    /// </summary>
    /// <remarks>
    /// 搜索生效时，它们只移动搜索过滤后仍可见的项，而非被隐藏的其余项。
    /// 一个会悄悄移动用户看不到的行的按钮，还不如没有。
    /// </remarks>
    [Parameter]
    public bool ShowMoveAll { get; set; } = true;

    /// <summary>
    /// 获取或设置两个窗格的排列方式。
    /// </summary>
    [Parameter]
    public PickListOrientation Orientation { get; set; } = PickListOrientation.Horizontal;

    /// <summary>
    /// 获取或设置将个别选项标记为不可用的谓词。被禁用的选项无法被挑选，
    /// "移动全部"按钮也会让它留在原地。
    /// </summary>
    [Parameter]
    public Func<SelectOption<TValue>, bool>? OptionDisabled { get; set; }

    /// <summary>
    /// 获取或设置选项内容的模板，两个窗格共用。
    /// </summary>
    [Parameter]
    public RenderFragment<SelectOption<TValue>>? ItemTemplate { get; set; }

    /// <summary>
    /// 获取或设置以 CSS 长度表示的每个窗格滚动区域高度，默认为
    /// <c>16rem</c>。
    /// </summary>
    [Parameter]
    public string Height { get; set; } = "16rem";

    /// <summary>
    /// 获取或设置是否禁用整个控件。
    /// </summary>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>
    /// 获取或设置根元素的额外 CSS 类。
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置根元素的额外 HTML 属性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    private bool IsVertical => Orientation == PickListOrientation.Vertical;

    private List<SelectOption<TValue>> allOptions = [];
    private List<TValue> picked = [];

    /// <summary>
    /// 尚未挑选的选项，按 <see cref="Options"/> 顺序排列。
    /// </summary>
    private List<SelectOption<TValue>> SourceOptions { get; set; } = [];

    /// <summary>
    /// 已挑选的选项，按移动发生的顺序排列。
    /// </summary>
    private List<SelectOption<TValue>> TargetOptions { get; set; } = [];

    private bool HasSourceSelection => sourceSelection?.Any() == true;

    private bool HasTargetSelection => targetSelection?.Any() == true;

    private string EffectiveSourceLabel => SourceLabel ?? Localizer["PickList.Available"];

    private string EffectiveTargetLabel => TargetLabel ?? Localizer["PickList.Selected"];

    private string RootClass => ClassNames.cn(
        "atom:w-full",
        Disabled ? "atom:opacity-60" : null,
        Class);

    // items-start, not items-center: the panes rarely hold the same number of rows, and centring
    // them leaves the two labels at different heights with nothing lining up.
    private string PaneWrapperClass => IsVertical
        ? "atom:flex atom:flex-col atom:gap-3"
        : "atom:flex atom:flex-col atom:gap-3 atom:sm:flex-row atom:sm:items-start";

    private string PaneClass => IsVertical ? "atom:w-full" : "atom:flex-1 atom:min-w-0";

    private string ButtonsClass => IsVertical
        ? "atom:flex atom:flex-row atom:items-center atom:justify-center atom:gap-2"
        // Stacks into a row on a narrow screen, where the panes are stacked too and a column of
        // left/right arrows between them would point the wrong way. The buttons are the one thing
        // that does sit centred, against the taller of the two panes.
        : "atom:flex atom:flex-row atom:sm:flex-col atom:items-center atom:justify-center atom:gap-2 atom:sm:self-center atom:sm:pt-7";

    protected override void OnParametersSet()
    {
        allOptions = [.. Options?.Where(o => o is not null) ?? []];
        var known = allOptions.Select(o => o.Value).ToHashSet();

        // A bound value with no matching option cannot be shown or moved, so it is dropped rather
        // than silently disappearing from the pane while still counting as picked.
        picked = [.. (Values ?? []).Where(known.Contains).Distinct()];

        var pickedSet = picked.ToHashSet();
        SourceOptions = [.. allOptions.Where(o => !pickedSet.Contains(o.Value))];

        // A lookup by value would need TValue constrained to notnull, which would rule out a
        // nullable option value for no good reason. The scan is bounded by the picked count times
        // the option count, and it only runs when the parameters change.
        TargetOptions = new List<SelectOption<TValue>>(picked.Count);
        foreach (var value in picked)
        {
            var match = allOptions.Find(o => EqualityComparer<TValue>.Default.Equals(o.Value, value));
            if (match is not null)
            {
                TargetOptions.Add(match);
            }
        }

        if (CascadedEditContext is not null && ValuesExpression is not null)
        {
            editContext = CascadedEditContext;
            fieldIdentifier = FieldIdentifier.Create(ValuesExpression);
        }
    }

    /// <summary>
    /// 每个窗格实际呈现在屏幕上的内容。
    /// </summary>
    /// <remarks>
    /// 窗格过滤的是它自己那份拷贝，因此完整选项列表并不等于用户正在看的东西。
    /// "移动全部"必须作用于用户可见的项，这就要求在这里套用同一个搜索——
    /// 这也是绑定搜索词并共享 <see cref="ListBoxSearch"/> 规则的原因。
    /// </remarks>
    private List<SelectOption<TValue>> VisibleSource =>
        [.. SourceOptions.Where(o => ListBoxSearch.Matches(o.Text, sourceSearch))];

    private List<SelectOption<TValue>> VisibleTarget =>
        [.. TargetOptions.Where(o => ListBoxSearch.Matches(o.Text, targetSearch))];

    private Task MoveSelectedToTargetAsync() => MoveAsync(Chosen(sourceSelection, SourceOptions), toTarget: true);

    private Task MoveSelectedToSourceAsync() => MoveAsync(Chosen(targetSelection, TargetOptions), toTarget: false);

    private Task MoveAllToTargetAsync() => MoveAsync(Movable(VisibleSource), toTarget: true);

    private Task MoveAllToSourceAsync() => MoveAsync(Movable(VisibleTarget), toTarget: false);

    private List<TValue> Chosen(IEnumerable<TValue>? selection, List<SelectOption<TValue>> pane)
    {
        if (selection is null)
        {
            return [];
        }

        var chosen = selection.ToHashSet();
        // Walk the pane rather than the selection, so the moved values keep the pane's order
        // instead of whatever order they happened to be clicked in.
        return [.. pane.Where(o => chosen.Contains(o.Value) && !IsDisabled(o)).Select(o => o.Value)];
    }

    private List<TValue> Movable(List<SelectOption<TValue>> pane) =>
        [.. pane.Where(o => !IsDisabled(o)).Select(o => o.Value)];

    private bool IsDisabled(SelectOption<TValue> option) => OptionDisabled?.Invoke(option) ?? false;

    private async Task MoveAsync(List<TValue> values, bool toTarget)
    {
        if (Disabled || values.Count == 0)
        {
            return;
        }

        if (toTarget)
        {
            picked.AddRange(values.Where(v => !picked.Contains(v)));
        }
        else
        {
            var leaving = values.ToHashSet();
            picked.RemoveAll(leaving.Contains);
        }

        // The moved rows are no longer in the pane they were chosen from, so a selection pointing
        // at them would light up whatever landed in their place.
        sourceSelection = null;
        targetSelection = null;

        Values = picked.Count > 0 ? [.. picked] : null;
        await ValuesChanged.InvokeAsync(Values);

        if (editContext is not null && fieldIdentifier.FieldName is not null)
        {
            editContext.NotifyFieldChanged(fieldIdentifier);
        }

        await OnMove.InvokeAsync(new PickListMoveEventArgs<TValue>(values, toTarget));
    }
}
