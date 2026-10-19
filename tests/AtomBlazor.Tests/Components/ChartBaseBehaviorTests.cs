using AtomBlazor.Primitives;
using System.Reflection;
using System.Text.Json;
using AtomBlazor;
using AtomBlazor.Tests.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Xunit;

namespace AtomBlazor.Tests.Components;

/// <summary>
/// 自写补全波：ChartBase 的纯逻辑面与 JS 侧状态机——不启宿主、不经渲染器，以 NoopJavaScript 作
/// mock（照 Scheduler 测试波先例）直接驱动 OnAfterRenderAsync 的首绘与重绘两路，断言 option 的
/// 组合序、默认调色填充、图例避让的布局调整、序列化比对的短路时机，以及点击互通的建立与拆除、
/// 容器样式的叠加次序。
/// </summary>
/// <remarks>
/// 父类私有成员（JS、chartId、lastSerializedJson、BuildOption 等）按声明类型逐级反射取用：
/// ComponentProbe 只在实例自身类型上找非公共成员，继承来的私有件须沿基类链查。
/// </remarks>
public class ChartBaseBehaviorTests
{
    private sealed record Sample(string Month, double? Revenue);

    /// <summary>可编排默认面（系列、雷达、色表）与轴互换/系列型的探针图表。</summary>
    private class ProbeChart : ChartBase
    {
        internal string Override { get; init; } = "probe";

        internal bool Swap { get; init; }

        internal List<EChartsSeriesOption> Defaults { get; } = [];

        internal EChartsRadarOption? RadarSeed { get; set; }

        internal List<object>? SeedColors { get; set; }

        internal EChartsOption? SeenByDefaults { get; private set; }

        internal override string DataSlot => "probe-slot";

        internal override string SeriesType => Override;

        internal override bool SwapAxes => Swap;

        internal override void ApplyChartDefaults(EChartsOption option)
        {
            SeenByDefaults = option;

            foreach (var series in Defaults)
            {
                option.Series.Add(series);
            }

            option.Radar = RadarSeed;
            option.Color = SeedColors;
        }
    }

    private static readonly BindingFlags declared = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;

    private static MethodInfo Method(object target, string name)
    {
        for (var type = target.GetType(); type is not null; type = type.BaseType)
        {
            if (type.GetMethod(name, declared) is { } method)
            {
                return method;
            }
        }

        throw new MissingMethodException(target.GetType().FullName, name);
    }

    private static object? FieldOrProperty(object target, string name)
    {
        for (var type = target.GetType(); type is not null; type = type.BaseType)
        {
            if (type.GetProperty(name, declared) is { } property)
            {
                return property.GetValue(target);
            }

            if (type.GetField(name, declared) is { } field)
            {
                return field.GetValue(target);
            }
        }

        throw new MissingMemberException(target.GetType().FullName, name);
    }

    private static T Value<T>(object target, string name) => (T)FieldOrProperty(target, name)!;

    private static void SetJs(ChartBase chart, IJSRuntime js)
    {
        for (var type = chart.GetType(); type is not null; type = type.BaseType)
        {
            if (type.GetProperty("JS", declared)?.GetSetMethod(nonPublic: true) is { } setter)
            {
                setter.Invoke(chart, [js]);
                return;
            }
        }

        throw new MissingMemberException(chart.GetType().FullName, "JS");
    }

    private static Task AfterRender(ChartBase chart, bool first) =>
        (Task)typeof(ChartBase)
            .GetMethod("OnAfterRenderAsync", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(chart, [first])!;

    private static EChartsOption Build(ChartBase chart) => (EChartsOption)Method(chart, "BuildOption").Invoke(chart, null)!;

    private static ProbeChart Chart(IJSRuntime js, object? data = null, ChartConfig? config = null)
    {
        var chart = new ProbeChart { Data = data, Config = config };
        SetJs(chart, js);
        return chart;
    }

    private static void Register(ChartBase chart, IChartComponent? component, object? parent = null)
    {
        if (component is null)
        {
            return;
        }

        if (parent is not null)
        {
            component.GetType()
                .GetProperty("ParentChart", BindingFlags.NonPublic | BindingFlags.Instance)!
                .SetValue(component, parent);
        }

        chart.RegisterComponent(component);
    }

    private static List<string> Identifiers(NoopJavaScript js) => js.Invocations.Select(entry => entry.Identifier).ToList();

    private static object?[] Args(NoopJavaScript js, string identifier) =>
        js.Invocations.Last(entry => entry.Identifier == identifier).Args!;

    private static int Count(NoopJavaScript js, string identifier) =>
        js.Invocations.Count(entry => entry.Identifier == identifier);

    [Fact]
    public async Task 首绘经模块加载并派发初始化()
    {
        var js = new NoopJavaScript();
        var chart = Chart(js, new List<Sample> { new("一月", 10) });

        await AfterRender(chart, true);

        Assert.Equal(["import", "initialize"], Identifiers(js));
        Assert.Equal("./_content/AtomBlazor/js/echarts-renderer.js", (string)Args(js, "import")[0]!);

        var args = Args(js, "initialize");
        Assert.Equal(Value<string>(chart, "chartId"), args[0]);
        Assert.Equal(JsonValueKind.Object, ((JsonElement)args[1]!).ValueKind);
        Assert.Null(args[2]);
        Assert.True(Value<bool>(chart, "jsInitialized"));
        Assert.False(Value<bool>(chart, "clickHandlersEnabled"));
        Assert.NotNull(Value<string>(chart, "lastSerializedJson"));
    }

    [Fact]
    public async Task 未初始化时重绘直接闭口不派任何互送()
    {
        var js = new NoopJavaScript();
        var chart = Chart(js, new List<Sample> { new("一月", 10) });

        await AfterRender(chart, false);

        Assert.Empty(js.Invocations);
    }

    [Fact]
    public async Task 同内容重绘按序列化比对短路()
    {
        var js = new NoopJavaScript();
        var chart = Chart(js, new List<Sample> { new("一月", 10), new("二月", 20) });
        await AfterRender(chart, true);
        var serialized = Value<string>(chart, "lastSerializedJson");

        await AfterRender(chart, false);

        Assert.DoesNotContain("update", Identifiers(js));
        Assert.Equal(serialized, Value<string>(chart, "lastSerializedJson"));
    }

    [Fact]
    public async Task 数据变化后重绘派发一次更新并前滚比对基线()
    {
        var js = new NoopJavaScript();
        var data = new List<Sample> { new("一月", 10) };
        var chart = Chart(js, data);
        Register(chart, new ReadingSeries { Chart = chart, DataKey = "Revenue", Name = "营收" });
        await AfterRender(chart, true);
        var baseline = Value<string>(chart, "lastSerializedJson");

        data.Add(new Sample("二月", 20));
        await AfterRender(chart, false);

        Assert.Equal(["import", "initialize", "update"], Identifiers(js));
        var updated = Value<string>(chart, "lastSerializedJson");
        Assert.NotEqual(baseline, updated);
        Assert.Contains("20", updated);

        await AfterRender(chart, false);
        Assert.Equal(1, Count(js, "update"));
    }

    [Fact]
    public async Task 有点击监听时才把托管引用交给脚本()
    {
        var js = new NoopJavaScript();
        var chart = Chart(js, new List<Sample> { new("一月", 10) });
        ChartClickEventArgs? received = null;
        chart.OnDataPointClick = EventCallback.Factory.Create<ChartClickEventArgs>(this, args => received = args);

        await AfterRender(chart, true);

        Assert.NotNull(Args(js, "initialize")[2]);
        Assert.True(Value<bool>(chart, "clickHandlersEnabled"));

        await chart.HandleDataPointClick(new ChartClickEventArgs { DataIndex = 2, Value = 10 });
        Assert.Equal(2, received!.DataIndex);

        // 数据点之外的点击未挂监听者，故静默；引用也只交接一次。
        await chart.HandleChartClick();
        Assert.Single(js.Invocations, entry => entry.Identifier == "initialize");
    }

    [Fact]
    public async Task 监听后置时先改派点击通路开关再于撤除时再派()
    {
        var js = new NoopJavaScript();
        var chart = Chart(js, new List<Sample> { new("一月", 10) });
        await AfterRender(chart, true);
        Assert.DoesNotContain("setClickHandler", Identifiers(js));

        chart.OnChartClick = EventCallback.Factory.Create(this, () => { });
        await AfterRender(chart, false);

        Assert.Equal(["import", "initialize", "setClickHandler"], Identifiers(js));
        Assert.True(Value<bool>(chart, "clickHandlersEnabled"));
        Assert.NotNull(Args(js, "setClickHandler")[1]);

        // 本运行时（net11.0）的 EventCallback.Empty 内含缓存的 no-op 委托，
        // HasDelegate 仍为 true；只有 default 才令监听通路真正消失。
        chart.OnChartClick = default;
        Assert.False(chart.OnDataPointClick.HasDelegate);
        Assert.False(chart.OnChartClick.HasDelegate);
        await AfterRender(chart, false);

        Assert.Equal(["import", "initialize", "setClickHandler", "setClickHandler"], Identifiers(js));
        Assert.Equal(2, Count(js, "setClickHandler"));
        Assert.Null(Args(js, "setClickHandler")[1]);
        Assert.False(Value<bool>(chart, "clickHandlersEnabled"));
        Assert.DoesNotContain("update", Identifiers(js));
    }

    [Fact]
    public async Task 空事件对象在本运行时仍算监听者故照样交接引用()
    {
        var js = new NoopJavaScript();
        var chart = Chart(js, new List<Sample> { new("一月", 10) });

        // 与 default 相反：EventCallback.Empty 携一个缓存的 no-op 委托，
        // HasDelegate 为真，于是图表仍按“有监听者”建通路。
        chart.OnChartClick = EventCallback.Empty;
        await AfterRender(chart, true);

        Assert.NotNull(Args(js, "initialize")[2]);
        Assert.True(Value<bool>(chart, "clickHandlersEnabled"));
        Assert.DoesNotContain("setClickHandler", Identifiers(js));
    }

    [Fact]
    public async Task 无对应监听者的点击回调静默而不抛()
    {
        var chart = Chart(new NoopJavaScript());
        var raised = 0;
        chart.OnChartClick = EventCallback.Factory.Create(this, () => raised++);

        await chart.HandleDataPointClick(new ChartClickEventArgs { DataIndex = 1 });
        Assert.Equal(0, raised);

        await chart.HandleChartClick();
        Assert.Equal(1, raised);
    }

    [Fact]
    public async Task 释放后派发拆卸且后续重绘闭口()
    {
        var js = new NoopJavaScript();
        var chart = Chart(js, new List<Sample> { new("一月", 10) });
        await AfterRender(chart, true);
        chart.OnChartClick = EventCallback.Factory.Create(this, () => { });

        await chart.DisposeAsync();

        Assert.Contains("dispose", Identifiers(js));
        Assert.Equal(Value<string>(chart, "chartId"), Args(js, "dispose")[0]);

        await chart.HandleChartClick();
        await AfterRender(chart, false);
        Assert.Equal(0, Count(js, "update"));
        Assert.Equal(0, Count(js, "setClickHandler"));
    }

    [Fact]
    public async Task 模块加载失败于断线与预渲染下皆自愈()
    {
        var js = new BrokenJavaScript();
        var chart = Chart(js);

        await AfterRender(chart, true);

        Assert.False(Value<bool>(chart, "jsInitialized"));
        Assert.Null(FieldOrProperty(chart, "lastSerializedJson"));

        // 未就绪时重绘不再重试，也不抛。
        await AfterRender(chart, false);
        Assert.Equal(1, js.Attempts);
    }

    [Fact]
    public void 可组合件在图表默认之后施加()
    {
        var js = new NoopJavaScript();
        var chart = new Chart { Data = new List<Sample> { new("一月", 10) } };
        SetJs(chart, js);

        var option = Build(chart);
        Assert.Equal("12", option.Grid!.Left);
        Assert.Equal("value", option.YAxis!.Type);

        var grid = new Grid { Left = "5%" };
        Register(chart, (IChartComponent)grid, chart);
        var combined = Build(chart);

        // 图表默认的内缩被后到的 Grid 可组合件覆写，即组合序为默认在前、可组合件在后。
        Assert.Equal("5%", combined.Grid!.Left);
        Assert.Equal("value", combined.YAxis!.Type);
    }

    [Fact]
    public void 注册顺序即系列写入顺序()
    {
        var chart = Chart(new NoopJavaScript());
        chart.Defaults.Add(new EChartsSeriesOption { Type = "default", Name = "默认" });

        var first = new EChartsSeriesOption { Type = "line", Name = "首个" };
        var second = new EChartsSeriesOption { Type = "bar", Name = "次个" };
        Register(chart, Series(first, chart));
        Register(chart, Series(second, chart));

        var option = Build(chart);

        Assert.Equal(["默认", "首个", "次个"], option.Series.Select(s => s.Name));
    }

    [Fact]
    public void 无人上色时填入默认调色板而上色者不受覆写()
    {
        var bare = Chart(new NoopJavaScript());
        Assert.Equal(
            ["var(--chart-1)", "var(--chart-2)", "var(--chart-3)", "var(--chart-4)", "var(--chart-5)"],
            Build(bare).Color!.Select(c => (string)c));

        var seeded = Chart(new NoopJavaScript());
        seeded.SeedColors = ["#101010"];
        Assert.Equal(["#101010"], Build(seeded).Color!.Select(c => (string)c));

        // 空色表视同无人上色。
        var emptied = Chart(new NoopJavaScript());
        emptied.SeedColors = [];
        Assert.Equal(5, Build(emptied).Color!.Count);
    }

    [Fact]
    public void 标题与动画开关随参数入选项()
    {
        var untitled = Chart(new NoopJavaScript());
        Assert.Null(Build(untitled).Titles);
        Assert.True(Build(untitled).Animation!.Value);

        var titled = Chart(new NoopJavaScript());
        titled.Title = "季度营收";
        titled.EnableAnimations = false;
        var option = Build(titled);

        Assert.False(option.Animation!.Value);
        var title = Assert.Single(option.Titles!);
        Assert.Equal("季度营收", title.Text);
        Assert.Equal("center", title.Left);
    }

    [Fact]
    public void 图例居下时网格底边让出滚动画行()
    {
        var chart = Chart(new NoopJavaScript());
        Register(chart, (IChartComponent)new Grid(), chart);
        var option = WithLegendOn(chart, LegendPosition.Bottom);

        Assert.Equal("30", option.Grid!.Bottom);
        Assert.Equal("0", option.Grid.Top);
    }

    [Fact]
    public void 图例居上时网格顶边让出滚动画行()
    {
        var chart = Chart(new NoopJavaScript());
        Register(chart, (IChartComponent)new Grid(), chart);
        var option = WithLegendOn(chart, LegendPosition.Top);

        Assert.Equal("30", option.Grid!.Top);
        Assert.Equal("0", option.Grid.Bottom);
    }

    [Fact]
    public void 图例隐藏或居左右时网格不受扰动()
    {
        var hidden = Chart(new NoopJavaScript());
        Register(hidden, (IChartComponent)new Grid(), hidden);
        var hiddenOption = WithLegendOn(hidden, LegendPosition.Hidden);

        Assert.False(hiddenOption.Legend!.Show!.Value);
        Assert.Equal("0", hiddenOption.Grid!.Bottom);
        Assert.Equal("0", hiddenOption.Grid.Top);

        foreach (var position in new[] { LegendPosition.Left, LegendPosition.Right })
        {
            var chart = Chart(new NoopJavaScript());
            Register(chart, (IChartComponent)new Grid(), chart);
            var option = WithLegendOn(chart, position);

            Assert.True(option.Legend!.Show!.Value);

            // 左右排布也写 Top="center"，故按上沿图例同样让位——此为其既有语义。
            Assert.Equal("30", option.Grid!.Top);
            Assert.Equal("0", option.Grid.Bottom);
        }
    }

    [Fact]
    public void 饼系在图例让位时平移中心并收缩半径()
    {
        var shrinkable = new EChartsSeriesOption { Type = "pie", Radius = new object?[] { "40%", "70%" } };
        var anchored = new EChartsSeriesOption { Type = "pie", Center = ["10%", "10%"], Radius = "80%" };
        var chart = Chart(new NoopJavaScript());
        chart.Defaults.Add(shrinkable);
        chart.Defaults.Add(anchored);

        WithLegendOn(chart, LegendPosition.Bottom);

        Assert.Equal(["50%", "45%"], shrinkable.Center);
        Assert.Equal(new object?[] { "40%", "59%" }, (object?[])shrinkable.Radius!);

        // 显式定过中心的饼不动。
        Assert.Equal(["10%", "10%"], anchored.Center);
        Assert.Equal("80%", anchored.Radius);
    }

    [Fact]
    public void 单值半径的饼按同比例收缩()
    {
        var pie = new EChartsSeriesOption { Type = "funnel", Radius = "60%" };
        var chart = Chart(new NoopJavaScript());
        chart.Defaults.Add(pie);

        WithLegendOn(chart, LegendPosition.Top);

        Assert.Equal("51%", pie.Radius);
        Assert.Equal(["50%", "55%"], pie.Center);
    }

    [Fact]
    public void 桑基在图例让位时改其四边()
    {
        var bottom = new EChartsSeriesOption { Type = "sankey" };
        var chart = Chart(new NoopJavaScript());
        chart.Defaults.Add(bottom);
        WithLegendOn(chart, LegendPosition.Bottom);

        Assert.Equal("30", bottom.Bottom);
        Assert.Null(bottom.Top);

        var top = new EChartsSeriesOption { Type = "sankey" };
        var other = Chart(new NoopJavaScript());
        other.Defaults.Add(top);
        WithLegendOn(other, LegendPosition.Top);

        Assert.Equal("30", top.Top);
        Assert.Null(top.Bottom);
    }

    [Fact]
    public void 雷达在图例让位时平移中心并收缩半径而定向者不动()
    {
        var chart = Chart(new NoopJavaScript());
        Assert.Null(WithLegendOn(chart, LegendPosition.Top).Radar);

        var free = new EChartsRadarOption();
        var mover = Chart(new NoopJavaScript());
        mover.RadarSeed = free;
        WithLegendOn(mover, LegendPosition.Top);

        Assert.Equal(["50%", "55%"], free.Center);
        Assert.Equal("65%", free.Radius);

        var fixedRadar = new EChartsRadarOption { Center = ["30%", "30%"], Radius = "80%" };
        var anchored = Chart(new NoopJavaScript());
        anchored.RadarSeed = fixedRadar;
        WithLegendOn(anchored, LegendPosition.Bottom);

        Assert.Equal(["30%", "30%"], fixedRadar.Center);
        Assert.Equal("80%", fixedRadar.Radius);
    }

    [Fact]
    public void 容器样式先自定尺寸后叠用传入者()
    {
        var chart = Chart(new NoopJavaScript());
        chart.Height = "220px";
        chart.Width = "80%";
        chart.Class = "my-chart";
        Assert.Equal("height: 220px; width: 80%;", Value<string>(chart, "ContainerStyle"));
        Assert.Equal("atom:w-full my-chart", Value<string>(chart, "ContainerCssClass"));

        chart.AdditionalAttributes = new Dictionary<string, object> { ["style"] = "opacity: 0.5;", ["data-x"] = "1" };

        // 图表自身的尺寸在前、传入者在后，故 splat 不再顶掉定尺所需的 height 与 width。
        Assert.Equal("height: 220px; width: 80%;; opacity: 0.5;", Value<string>(chart, "MergedContainerStyle"));
    }

    [Fact]
    public void 缺省尺寸与槽名随图表类型而定()
    {
        var chart = Chart(new NoopJavaScript());
        Assert.Equal("height: 350px; width: 100%;", Value<string>(new ProbeChart(), "ContainerStyle"));
        Assert.Equal("atom:w-full", Value<string>(new ProbeChart(), "ContainerCssClass"));
        Assert.Equal("probe-slot", chart.DataSlot);
        Assert.Equal("probe", chart.SeriesType);
        Assert.False(chart.SwapAxes);
        Assert.True(new SwappingChart().SwapAxes);
        Assert.Equal("chart", new Chart().DataSlot);
        Assert.Equal("composite", new Chart().SeriesType);
    }

    private static EChartsOption WithLegendOn(ProbeChart chart, LegendPosition position)
    {
        var legend = new ChartLegend { Position = position };
        Register(chart, (IChartComponent)legend, chart);
        return Build(chart);
    }

    private static FixedSeries Series(EChartsSeriesOption built, ChartBase parent) => new(built) { Chart = parent };

    /// <summary>随父图表数据实时取数的系列，令 option 文本随数据变动而变动。</summary>
    private sealed class ReadingSeries : SeriesBase
    {
        internal ChartBase? Chart
        {
            get => ParentChart;
            set => ParentChart = value;
        }

        internal override EChartsSeriesOption BuildSeriesCore() => new()
        {
            Type = "line",
            Name = GetResolvedName(),
            Data = GetSeriesData()
        };
    }

    private sealed class FixedSeries(EChartsSeriesOption built) : SeriesBase
    {
        internal ChartBase? Chart
        {
            get => ParentChart;
            set => ParentChart = value;
        }

        internal override EChartsSeriesOption BuildSeriesCore() => built;
    }

    private sealed class SwappingChart : ProbeChart
    {
        internal override bool SwapAxes => true;
    }

    /// <summary>模拟预渲染/断线：加载模块即抛 InvalidOperationException，互送不可用。</summary>
    private sealed class BrokenJavaScript : IJSRuntime
    {
        internal int Attempts { get; private set; }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => Fail<TValue>();

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => Fail<TValue>();

        private ValueTask<TValue> Fail<TValue>()
        {
            Attempts++;
            return ValueTask.FromException<TValue>(new InvalidOperationException("JS interop 在预渲染期间不可用。"));
        }
    }
}
