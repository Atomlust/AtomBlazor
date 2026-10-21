using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using AtomBlazor.Primitives.Services;

namespace AtomBlazor;

public abstract partial class ChartBase : ComponentBase, IAsyncDisposable
{
    private IJSObjectReference? jsModule;
    private DotNetObjectReference<ChartBase>? dotNetRef;
    private bool clickHandlersEnabled;
    private bool jsInitialized;
    private bool disposed;
    private readonly string chartId = Guid.NewGuid().ToString("N");
    private readonly List<IChartComponent> registeredComponents = [];
    private bool hasRenderedOnce;
    private string? lastSerializedJson;

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    [Parameter]
    public object? Data { get; set; }

    [Parameter]
    public ChartConfig? Config { get; set; }

    /// <summary>
    /// 数据点被点击时触发，携带该点的系列、序号与值。
    /// </summary>
    /// <remarks>
    /// <see cref="ChartClickEventArgs.DataIndex"/> 直接映射回你所绑定集合中的位置
    /// 下钻因此得以直接可循。
    /// 除非此事件或 <see cref="OnChartClick"/> 挂了处理器，否则完全不建立 interop 通路。
    /// </remarks>
    [Parameter]
    public EventCallback<ChartClickEventArgs> OnDataPointClick { get; set; }

    /// <summary>
    /// 图表在数据点之外的位置被点击时触发——适合清除经
    /// <see cref="OnDataPointClick"/> 做出的选择。
    /// </summary>
    /// <remarks>
    /// 点击数据点只触发 <see cref="OnDataPointClick"/>；同一次点击
    /// 绝不会两个事件都触发。
    /// </remarks>
    [Parameter]
    public EventCallback OnChartClick { get; set; }

    [Parameter]
    public string Height { get; set; } = "350px";

    [Parameter]
    public string Width { get; set; } = "100%";

    [Parameter]
    public string? Class { get; set; }

    [Parameter]
    public string? Title { get; set; }

    [Parameter]
    public bool EnableAnimations { get; set; } = true;

    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    internal abstract string DataSlot { get; }

    internal abstract string SeriesType { get; }

    /// <summary>
    /// 为 true 时，XAxis 可组合件写入 option.YAxis，YAxis 可组合件写入 option.XAxis。
    /// BarChart 在 Horizontal 为 true 时借此互换 category 轴与 value 轴。
    /// </summary>
    internal virtual bool SwapAxes => false;

    internal abstract void ApplyChartDefaults(EChartsOption option);

    internal void RegisterComponent(IChartComponent component)
    {
        registeredComponents.Add(component);
        if (hasRenderedOnce)
        {
            StateHasChanged();
        }
    }

    internal void UnregisterComponent(IChartComponent component) =>
        registeredComponents.Remove(component);

    private EChartsOption BuildOption()
    {
        var option = new EChartsOption
        {
            Animation = EnableAnimations
        };

        if (!string.IsNullOrEmpty(Title))
        {
            option.AddTitle(new EChartsTitleOption
            {
                Text = Title
            });
        }

        ApplyChartDefaults(option);

        foreach (var component in registeredComponents)
        {
            component.ApplyTo(option);
        }

        if (option.Color == null || option.Color.Count == 0)
        {
            option.Color = new List<object>(ChartColor.DefaultColors);
        }

        AdjustLayoutForLegend(option);

        return option;
    }

    /// <summary>
    /// 当存在可见 legend 时调整图表布局。
    /// legend 采用 <c>type: "scroll"</c>，条目再多其高度也有界。
    /// 这里施加一致的偏移，把图表内容从 legend 所占的那条边推开。
    /// </summary>
    private static void AdjustLayoutForLegend(EChartsOption option)
    {
        if (option.Legend?.Show != true)
        {
            return;
        }

        var legendAtBottom = option.Legend.Bottom != null;
        var legendAtTop = option.Legend.Top != null;

        if (!legendAtBottom && !legendAtTop)
        {
            return;
        }

        // 滚动式 legend 固定为单行高度（约 30px）。
        // 将图表内容从那条边推开一致的幅度。
        const string gridPadding = "30";
        var centerY = legendAtBottom ? "45%" : "55%";

        // 基于 grid 的图表（Line、Bar、Area）：为 grid 增加边距
        if (option.Grid != null)
        {
            if (legendAtBottom)
            {
                option.Grid.Bottom = gridPadding;
            }
            else
            {
                option.Grid.Top = gridPadding;
            }
        }

        // 以中心定位的图表（Pie）：平移 center 并收缩 radius
        foreach (var series in option.Series)
        {
            if (series.Type is "pie" or "funnel" && series.Center == null)
            {
                series.Center = ["50%", centerY];
                ShrinkPieRadius(series, 0.85);
            }
            // sankey 自行占位而不坐在 grid 里，故须告诫它别去挤 legend
            // 所在的那条边，否则两者会叠着画。
            else if (series.Type is "sankey")
            {
                if (legendAtBottom)
                {
                    series.Bottom = gridPadding;
                }
                else
                {
                    series.Top = gridPadding;
                }
            }
        }

        // Radar：平移 center 并收缩 radius
        if (option.Radar != null && option.Radar.Center == null)
        {
            option.Radar.Center = ["50%", centerY];
            option.Radar.Radius = "65%";
        }
    }

    private static void ShrinkPieRadius(EChartsSeriesOption series, double scale)
    {
        if (series.Radius is object[] arr)
        {
            if (arr.Length == 2 && arr[1] is string outer && outer.EndsWith('%'))
            {
                var val = int.Parse(outer.TrimEnd('%'), System.Globalization.CultureInfo.InvariantCulture);
                arr[1] = $"{(int)(val * scale)}%";
            }
        }
        else if (series.Radius is string single && single.EndsWith('%'))
        {
            var val = int.Parse(single.TrimEnd('%'), System.Globalization.CultureInfo.InvariantCulture);
            series.Radius = $"{(int)(val * scale)}%";
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            hasRenderedOnce = true;
            await InitializeChartAsync();
        }
        else
        {
            await UpdateChartAsync();
        }
    }

    private async Task InitializeChartAsync()
    {
        // 序列化失败属于配置/数据错误，必须让调用方看到，不能混入
        // 预渲染期间 JS interop 不可用的预期异常。
        var option = BuildOption();
        var json = ChartOptionSerializer.Serialize(option);

        using var document = JsonDocument.Parse(json);
        var serialized = document.RootElement.Clone();

        // 只有确实有监听者时我才把引用交给 JS，无处理器的图表
        // 因此不添额外开销、也无需释放。
        if (OnDataPointClick.HasDelegate || OnChartClick.HasDelegate)
        {
            dotNetRef ??= DotNetObjectReference.Create(this);
        }

        try
        {
            jsModule = await JsModules.GetAsync(JS, "./_content/AtomBlazor/js/echarts-renderer.js");
            await jsModule.InvokeVoidAsync("initialize", chartId, serialized, dotNetRef);
            lastSerializedJson = json;
            jsInitialized = true;
            clickHandlersEnabled = dotNetRef is not null;
        }
        catch (Exception ex) when (ex is JSDisconnectedException or TaskCanceledException or ObjectDisposedException)
        {
            // Blazor Server 电路断开时的预期情形
        }
        catch (InvalidOperationException)
        {
            // 预渲染期间 JS interop 不可用
        }
    }

    private async Task UpdateChartAsync()
    {
        if (disposed || !jsInitialized || jsModule == null)
        {
            return;
        }

        var option = BuildOption();
        var json = ChartOptionSerializer.Serialize(option);
        var changed = json != lastSerializedJson;

        using var document = changed ? JsonDocument.Parse(json) : null;
        var serialized = changed ? document!.RootElement.Clone() : default;

        if (changed)
        {
            lastSerializedJson = json;
        }

        try
        {
            var enableClicks = OnDataPointClick.HasDelegate || OnChartClick.HasDelegate;
            if (enableClicks != clickHandlersEnabled)
            {
                if (enableClicks)
                {
                    dotNetRef ??= DotNetObjectReference.Create(this);
                }
                await jsModule.InvokeVoidAsync("setClickHandler", chartId, enableClicks ? dotNetRef : null);
                clickHandlersEnabled = enableClicks;
            }

            if (changed)
            {
                await jsModule.InvokeVoidAsync("update", chartId, serialized);
            }
        }
        catch (Exception ex) when (ex is JSDisconnectedException or TaskCanceledException or ObjectDisposedException)
        {
            // Blazor Server 电路断开时的预期情形
        }
        catch (InvalidOperationException)
        {
            // 预渲染期间 JS interop 不可用
        }
    }

    private string ContainerCssClass => ClassNames.cn("atom:w-full", Class);

    private string ContainerStyle => $"height: {Height}; width: {Width};";

    /// <summary>
    /// 图表自身的尺寸在前、使用者的 style 在后，传入的 <c>style</c>
    /// 因而是叠加而非替换。
    /// </summary>
    /// <remarks>
    /// 属性展开（splat）过去会覆盖图表为其画布定尺所需的 height 与 width
    /// 使图表落得一个无可度量的盒子。
    /// </remarks>
    private string? MergedContainerStyle => InlineStyleMerge.Merge(ContainerStyle, AdditionalAttributes);

    /// <summary>数据点被点击时由 JS 调用。不属于公共 API。</summary>
    [JSInvokable]
    public async Task HandleDataPointClick(ChartClickEventArgs args)
    {
        if (disposed || !OnDataPointClick.HasDelegate)
        {
            return;
        }

        await OnDataPointClick.InvokeAsync(args);
    }

    /// <summary>图表在数据点之外的位置被点击时由 JS 调用。不属于公共 API。</summary>
    [JSInvokable]
    public async Task HandleChartClick()
    {
        if (disposed || !OnChartClick.HasDelegate)
        {
            return;
        }

        await OnChartClick.InvokeAsync();
    }

    public async ValueTask DisposeAsync()
    {
        disposed = true;

        if (jsModule != null)
        {
            try
            {
                await jsModule.InvokeVoidAsync("dispose", chartId);
            }
            catch (Exception ex) when (ex is JSDisconnectedException or JSException or TaskCanceledException or ObjectDisposedException)
            {
                // 电路已断开，忽略
            }
            catch (InvalidOperationException)
            {
                // JS interop 不可用
            }
        }

        dotNetRef?.Dispose();
        dotNetRef = null;

        GC.SuppressFinalize(this);
    }
}
