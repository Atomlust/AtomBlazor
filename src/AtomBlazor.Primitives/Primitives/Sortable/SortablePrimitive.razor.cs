using System.Globalization;
using System.Diagnostics.CodeAnalysis;
using AtomBlazor.Primitives.Utilities;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using AtomBlazor.Primitives.Services;

namespace AtomBlazor.Primitives;

/// <summary>
/// 基于 Sortable.js 的无头（headless）拖拽排序基元。
/// </summary>
/// <remarks>
/// <para>
/// 提供可排序列表的全部行为功能：JS interop 生命周期、事件回调、
/// 无障碍（ARIA 实时播报），以及通过自动 <c>@key</c> 包装实现的稳定 Blazor 差异比对。
/// 不携带任何 CSS——样式由消费方组件或带样式的
/// <c>AtomBlazor.Components.Sortable</c> 包裹件负责。
/// </para>
/// <para>
/// 支持单列表、互联多列表（项目通过共享的 <see cref="Group"/> 组名在列表间移动）
/// 以及任意视觉布局。
/// </para>
/// </remarks>
/// <typeparam name="TItem">可排序列表中的项目类型。</typeparam>
public partial class SortablePrimitive<TItem> : ComponentBase, IAsyncDisposable
{
    private IJSObjectReference? _module;
    private DotNetObjectReference<SortablePrimitive<TItem>>? _ref;
    private string _liveAnnouncement = string.Empty;
    private bool _jsInitialized;
    private bool _needsReinit;

    // Track previous config values for re-initialization detection
    private string _prevGroup = string.Empty;
    private string? _prevPull;
    private bool _prevPut = true;
    private bool _prevSort = true;
    private string _prevHandle = string.Empty;
    private string? _prevFilter;
    private bool _prevForceFallback = true;

    [Inject]
    private IJSRuntime JsRuntime { get; set; } = null!;

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
    /// 在 <see cref="OnRemove"/> 处理程序中把项目从源列表移除并暂存
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
    /// 使用 <c>"clone"</c> 表示复制而非移动项目
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

    /// <summary>启用本列表内的键盘拾取、方向键移动、放置与取消操作。</summary>
    [Parameter]
    public bool KeyboardSorting { get; set; } = true;

    /// <summary>与每个键盘排序句柄关联的可访问操作说明。</summary>
    [Parameter]
    public string KeyboardInstructions { get; set; } = "Press Space or Enter to pick up. Use arrows to reorder, Control plus Left or Right to transfer to a connected list, Space or Enter to drop, or Escape to cancel.";

    /// <summary>本地化的键盘播报，键为 Cancelled、Unchanged、MoveFailed、Transferred、TransferRejected、TransferFailed、PickedUp、Disabled 与 Position。带位置的模板接受 {0} 与 {1}。</summary>
    [Parameter] public IReadOnlyDictionary<string, string>? KeyboardAnnouncements { get; set; }


    /// <summary>
    /// 当 <see cref="CanMove"/> 拒绝一次重排时播报。
    /// </summary>
    /// <remarks>
    /// 这些播报原本是方法体内的英文字符串字面量，本地化应用仍会听到英文。
    /// 把它们做成参数，是为了让带样式的组件可以提供译文
    /// 保留默认值则让只用基元的应用照常工作、无需改动。
    /// </remarks>
    [Parameter]
    public string MoveRejectedAnnouncement { get; set; } = "The move was not allowed.";

    /// <summary>当 <see cref="CanDrop"/> 拒绝一次跨列表放置时播报。</summary>
    [Parameter]
    public string DropRejectedAnnouncement { get; set; } = "The drop was not allowed.";

    /// <summary>
    /// 重排完成后播报。<c>{0}</c> 为从一起算的旧位置，<c>{1}</c> 为新位置。
    /// </summary>
    [Parameter]
    public string MovedAnnouncement { get; set; } = "Item moved from position {0} to position {1}.";

    /// <summary>
    /// 项目离开本列表时播报。<c>{0}</c> 为离开的从一起算位置
    /// <c>{1}</c> 为它在另一列表取得的位置。
    /// </summary>
    [Parameter]
    public string RemovedAnnouncement { get; set; } = "Item removed from position {0} and placed at position {1} in another list.";

    /// <summary>
    /// 项目从另一列表到达时播报。<c>{0}</c> 为其从一起算的位置。
    /// </summary>
    [Parameter]
    public string ReceivedAnnouncement { get; set; } = "Item received at position {0}.";

    /// <summary>可选谓词，在列表内重排调用 OnUpdate 之前求值。返回 false 即拒绝该重排。</summary>
    [Parameter]
    public Func<SortableMoveContext<TItem>, bool>? CanMove { get; set; }

    /// <summary>跨列表放置的源侧许可判定，在任一列表回调运行之前执行。</summary>
    [Parameter]
    public Func<SortableDropContext<TItem>, bool>? CanDrop { get; set; }

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
    /// 获取或设置可排序容器的可访问标签
    /// 以容器元素上的 <c>aria-label</c> 形式暴露。
    /// </summary>
    [Parameter]
    public string? AriaLabel { get; set; }

    /// <summary>
    /// 获取或设置应用到容器元素的任意额外 HTML 属性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <inheritdoc />
    private bool hadDragOverlay;

    protected override void OnParametersSet()
    {
        if (!_jsInitialized)
        {
            return;
        }

        if (Group != _prevGroup ||
            Pull != _prevPull ||
            Put != _prevPut ||
            Sort != _prevSort ||
            Handle != _prevHandle ||
            Filter != _prevFilter ||
            ForceFallback != _prevForceFallback || hadDragOverlay != (DragOverlayTemplate is not null))
        {
            _needsReinit = true;
        }
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _ref = DotNetObjectReference.Create(this);
            _module = await PrimitiveModules.GetAsync(JsRuntime);
            try
            {
                await _module.InvokeVoidAsync(
"sortable.init", Id, Group, Pull, Put, Sort, Handle, Filter, _ref, ForceFallback || DragOverlayTemplate is not null);
                _jsInitialized = true;
                SnapshotParameters();
            }
            catch (Exception ex) when (ex is JSDisconnectedException or JSException or TaskCanceledException or ObjectDisposedException)
            {
                // Expected during circuit disconnect
            }
        }
        else if (_needsReinit && _module is not null)
        {
            _needsReinit = false;
            try
            {
                await _module.InvokeVoidAsync(
"sortable.init", Id, Group, Pull, Put, Sort, Handle, Filter, _ref, ForceFallback || DragOverlayTemplate is not null);
                SnapshotParameters();
            }
            catch (Exception ex) when (ex is JSDisconnectedException or JSException or TaskCanceledException or ObjectDisposedException)
            {
                // Expected during circuit disconnect
            }
        }
    }

    [UnconditionalSuppressMessage("System.Text.Json", "IL2026: JsonSerializer.Serialize 在 trim 下需静态分析类型，可能对裁剪代码造成破坏", Justification = "序列化 IReadOnlyDictionary<string,string>，键值均为编译期可知的基元类型，裁剪/AOT 不影响运行。")]
    [UnconditionalSuppressMessage("AOT", "IL3050: JsonSerializer.Serialize 在 AOT 下可能需运行时代码生成", Justification = "字典键值均为 string，无需动态代码生成；抑制等价于 net10 未开 AOT 兼容的原始行为。")]
    private string KeyboardAnnouncementsJson => System.Text.Json.JsonSerializer.Serialize(KeyboardAnnouncements ?? new System.Collections.Generic.Dictionary<string, string>());

    private void SnapshotParameters()
    {
        hadDragOverlay = DragOverlayTemplate is not null;
        _prevGroup = Group;
        _prevPull = Pull;
        _prevPut = Put;
        _prevSort = Sort;
        _prevHandle = Handle;
        _prevFilter = Filter;
        _prevForceFallback = ForceFallback;
    }

    /// <summary>
    /// 当项目在本列表内被重新排序时由 JavaScript 调用。
    /// </summary>
    [JSInvokable]
    public async Task OnUpdateJS(int oldIndex, int newIndex)
    {
        if (!Sort || Items == null || oldIndex < 0 || oldIndex >= Items.Count || newIndex < 0 || newIndex >= Items.Count || oldIndex == newIndex)
        {
            return;
        }
        if (CanMove?.Invoke(new SortableMoveContext<TItem>(Items[oldIndex], oldIndex, newIndex)) == false)
        {
            _liveAnnouncement = MoveRejectedAnnouncement;
            StateHasChanged();
            return;
        }
        _liveAnnouncement = string.Format(CultureInfo.CurrentCulture, MovedAnnouncement, oldIndex + 1, newIndex + 1);
        await OnUpdate.InvokeAsync((oldIndex, newIndex));
        StateHasChanged();
    }

    /// <summary>校验一次拟议的跨列表放置，不改动任何一个列表。</summary>
    [JSInvokable]
    public bool CanDropJS(int oldIndex, int newIndex, string targetId, bool isClone)
    {
        if (Items is null || oldIndex < 0 || oldIndex >= Items.Count || newIndex < 0) { return false; }
        var allowed = CanDrop?.Invoke(new SortableDropContext<TItem>(Items[oldIndex], Id, targetId, oldIndex, newIndex, isClone)) != false;
        if (!allowed)
        {
            _liveAnnouncement = DropRejectedAnnouncement;
            StateHasChanged();
        }
        return allowed;
    }

    /// <summary>
    /// 当项目从本列表被移动进另一个列表时由 JavaScript 调用。
    /// </summary>
    [JSInvokable]
    public async Task OnRemoveJS(int oldIndex, int newIndex)
    {
        _liveAnnouncement = string.Format(CultureInfo.CurrentCulture, RemovedAnnouncement, oldIndex + 1, newIndex + 1);
        await OnRemove.InvokeAsync((oldIndex, newIndex));
        StateHasChanged();
    }

    /// <summary>
    /// 当来自互联列表的项目被放入本列表时由 JavaScript 调用。
    /// </summary>
    [JSInvokable]
    public async Task OnAddJS(int oldIndex, int newIndex)
    {
        _liveAnnouncement = string.Format(CultureInfo.CurrentCulture, ReceivedAnnouncement, newIndex + 1);
        await OnAdd.InvokeAsync((oldIndex, newIndex));
        StateHasChanged();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);

        if (_module is not null)
        {
            try
            {
                await _module.InvokeVoidAsync("sortable.destroy", Id);
            }
            catch (Exception ex) when (ex is JSDisconnectedException or JSException or TaskCanceledException or ObjectDisposedException)
            {
                // Expected during circuit disconnect
            }
        }

        _ref?.Dispose();
    }
}
