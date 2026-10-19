using System.Diagnostics.CodeAnalysis;
using AtomBlazor.Primitives.Utilities;
using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 基于 Sortable.js 的带样式拖拽排序列表/网格组件。
/// </summary>
/// <remarks>
/// <para>
/// 在无头基元 <see cref="AtomBlazor.Primitives.SortablePrimitive{TItem}"/> 外层包裹
/// 预置的 Tailwind CSS 布局类。通过 <see cref="Layout"/> 参数支持垂直列表与网格布局。
/// </para>
/// <para>
/// 支持单列表、互联多列表（项目在列表间移动）以及网格布局。
/// 拖拽由 Sortable.js 处理；状态在 .NET 侧通过 <see cref="OnUpdate"/> 与
/// <see cref="OnRemove"/> 回调管理。
/// </para>
/// </remarks>
/// <typeparam name="TItem">可排序列表中的项目类型。</typeparam>
public partial class Sortable<TItem> : ComponentBase
{
    private IReadOnlyDictionary<string, string> LocalizedKeyboardAnnouncements => new Dictionary<string, string>
    {
        ["Cancelled"] = Localizer["Sortable.Cancelled"],
        ["Unchanged"] = Localizer["Sortable.Unchanged"],
        ["MoveFailed"] = Localizer["Sortable.MoveFailed"],
        ["Transferred"] = Localizer["Sortable.Transferred"],
        ["TransferRejected"] = Localizer["Sortable.TransferRejected"],
        ["TransferFailed"] = Localizer["Sortable.TransferFailed"],
        ["PickedUp"] = Localizer["Sortable.PickedUp"],
        ["Disabled"] = Localizer["Sortable.Disabled"],
        ["Position"] = Localizer["Sortable.Position"],
    };

    /// <summary>
    /// 获取或设置用于显示每个项目的渲染片段。
    /// <c>context</c> 参数提供类型为 <typeparamref name="TItem"/> 的数据项。
    /// </summary>
    [Parameter]
    public RenderFragment<TItem>? ItemTemplate { get; set; }

    /// <summary>可选的装饰性指针拖拽预览。按项目以不可交互方式渲染并在拖拽时被克隆；用于启用回退渲染器。</summary>
    [Parameter] public RenderFragment<TItem>? DragOverlayTemplate { get; set; }

    /// <summary>
    /// 获取或设置要渲染与排序的项目列表。
    /// </summary>
    [Parameter, AllowNull]
    public IList<TItem> Items { get; set; }

    /// <summary>
    /// 当项目在本列表内被重新排序时触发。
    /// 元组提供移动前的原始索引与移动后的新索引。
    /// </summary>
    [Parameter]
    public EventCallback<(int OldIndex, int NewIndex)> OnUpdate { get; set; }

    /// <summary>
    /// 当项目从本列表被拖入另一个互联列表时触发。
    /// 元组提供项目在本列表中的原始索引，以及其在目标列表中的落点索引。
    /// </summary>
    [Parameter]
    public EventCallback<(int OldIndex, int NewIndex)> OnRemove { get; set; }

    /// <summary>
    /// 当来自互联列表的项目被放入本列表时触发。
    /// 元组提供项目在源列表中的原始索引，以及其在本列表中的落点索引。
    /// </summary>
    /// <remarks>
    /// 与源列表上的 <see cref="OnRemove"/> 配合以协调跨列表移动：
    /// 在 <see cref="OnRemove"/> 处理程序中把项目从源列表移除并暂存；
    /// 在 <see cref="OnAdd"/> 处理程序中把暂存的项目插入本列表。
    /// </remarks>
    [Parameter]
    public EventCallback<(int OldIndex, int NewIndex)> OnAdd { get; set; }

    /// <summary>
    /// 获取或设置可排序容器的 HTML 元素 id。
    /// 默认自动生成。
    /// </summary>
    [Parameter]
    public string Id { get; set; } = IdGenerator.GenerateId("sortable");

    /// <summary>
    /// 获取或设置用于连接多个列表的 Sortable.js 组名。
    /// 共享同一组名的列表可以交换项目。
    /// 默认取唯一值（孤立列表）。
    /// </summary>
    [Parameter]
    public string Group { get; set; } = IdGenerator.GenerateId("sortable-group");

    /// <summary>
    /// 获取或设置组的拉取（pull）行为。
    /// 使用 <c>"clone"</c> 表示复制而非移动项目，
    /// 或 <c>null</c>（默认）表示移动项目。
    /// </summary>
    [Parameter]
    public string? Pull { get; set; }

    /// <summary>
    /// 获取或设置是否允许来自互联列表的项目被放入本列表。
    /// 默认为 <c>true</c>。
    /// </summary>
    [Parameter]
    public bool Put { get; set; } = true;

    /// <summary>
    /// 获取或设置本列表内的项目能否被重新排序。
    /// 设为 <c>false</c> 可创建仅接收其他列表项目的列表。
    /// 默认为 <c>true</c>。
    /// </summary>
    [Parameter]
    public bool Sort { get; set; } = true;

    /// <summary>启用本列表内的键盘拾取、移动、放置与取消操作。</summary>
    [Parameter]
    public bool KeyboardSorting { get; set; } = true;

    /// <summary>键盘排序句柄的可访问操作说明。</summary>
    [Parameter]
    public string? KeyboardInstructions { get; set; }

    /// <summary>可选谓词，在接纳列表内移动之前求值。</summary>
    [Parameter]
    public Func<AtomBlazor.Primitives.SortableMoveContext<TItem>, bool>? CanMove { get; set; }

    /// <summary>跨列表放置的源侧许可判定，在任一列表回调运行之前执行。</summary>
    [Parameter]
    public Func<AtomBlazor.Primitives.SortableDropContext<TItem>, bool>? CanDrop { get; set; }

    /// <summary>
    /// 获取或设置作为每个条目内拖拽句柄元素的 CSS 选择器。
    /// 设置后，仅匹配到的子元素可以发起拖拽。
    /// </summary>
    [Parameter]
    public string Handle { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置不可拖拽子元素的 CSS 选择器。
    /// </summary>
    [Parameter]
    public string? Filter { get; set; }

    /// <summary>
    /// 获取或设置是否使用软件回退渲染器，而非浏览器原生的
    /// HTML5 拖拽 API。
    /// 默认为 <c>true</c>，以保证跨浏览器行为一致。
    /// </summary>
    [Parameter]
    public bool ForceFallback { get; set; } = true;

    /// <summary>
    /// 获取或设置可排序容器的视觉布局。
    /// </summary>
    [Parameter]
    public SortableLayout Layout { get; set; } = SortableLayout.List;

    /// <summary>
    /// 获取或设置应用到可排序容器的额外 CSS 类。
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置应用到根元素的额外 HTML 属性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>
    /// 获取或设置可排序容器的可访问标签，
    /// 以容器元素上的 <c>aria-label</c> 形式暴露。
    /// </summary>
    [Parameter]
    public string? AriaLabel { get; set; }

    private string CssClass => ClassNames.cn(
        Layout switch
        {
            SortableLayout.Grid => "atom:grid atom:grid-cols-2 atom:gap-2",
            _ => "atom:flex atom:flex-col atom:gap-2"
        },
        Class
    );
}
