using AtomBlazor.Primitives;
using System.Text.RegularExpressions;
using AtomBlazor;
using AtomBlazor.Tests.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Xunit;

namespace AtomBlazor.Tests.Components;

/// <summary>
/// CheckboxGroup 家族测试：项与组的上下文联动（勾选上报新集合、宿主回写值集合后
/// 对应项呈现选中）、标签 for/id 接线、组禁用级联、以及「全选」复选框的全选/半选/取消三态。
/// </summary>
public class CheckboxGroupTests
{
    /// <summary>
    /// 归一化各项自动生成的随机 id，使快照可确定。
    /// </summary>
    private static string Scrub(string markup) =>
        Regex.Replace(
            Regex.Replace(markup, "cbg-[0-9a-f]{32}", "cbg-<id>"),
            "checkbox-[0-9a-f]{8}", "checkbox-<id>");

    private static ComponentTestRenderer CreateRenderer()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(new NoopJavaScript());
        return new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance);
    }

    /// <summary>排空渲染队列，确保 StateHasChanged 的后续批已落定。</summary>
    private static Task SettleAsync(ComponentTestRenderer renderer) =>
        renderer.Dispatcher.InvokeAsync(() => Task.CompletedTask);

    private static RenderFragment Items(params (string Value, string? Label)[] items) => builder =>
    {
        var seq = 0;
        foreach (var (value, label) in items)
        {
            builder.OpenComponent<CheckboxGroupItem<string>>(seq++);
            builder.AddAttribute(seq++, nameof(CheckboxGroupItem<string>.Value), value);
            if (label is not null)
            {
                builder.AddAttribute(seq++, nameof(CheckboxGroupItem<string>.Label), label);
            }

            builder.CloseComponent();
        }
    };

    private static Dictionary<string, object?> GroupParams(
        RenderFragment childContent,
        IReadOnlyCollection<string>? values = null,
        EventCallback<IReadOnlyCollection<string>> valuesChanged = default,
        bool? showSelectAll = null,
        string? selectAllLabel = null,
        bool? disabled = null,
        string? ariaLabel = null,
        string? @class = null)
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(CheckboxGroup<string>.ChildContent)] = childContent,
        };
        if (values is not null)
        {
            parameters[nameof(CheckboxGroup<string>.Values)] = values;
        }
        if (valuesChanged.HasDelegate)
        {
            parameters[nameof(CheckboxGroup<string>.ValuesChanged)] = valuesChanged;
        }
        if (showSelectAll is not null)
        {
            parameters[nameof(CheckboxGroup<string>.ShowSelectAll)] = showSelectAll.Value;
        }
        if (selectAllLabel is not null)
        {
            parameters[nameof(CheckboxGroup<string>.SelectAllLabel)] = selectAllLabel;
        }
        if (disabled is not null)
        {
            parameters[nameof(CheckboxGroup<string>.Disabled)] = disabled.Value;
        }
        if (ariaLabel is not null)
        {
            parameters[nameof(CheckboxGroup<string>.AriaLabel)] = ariaLabel;
        }
        if (@class is not null)
        {
            parameters[nameof(CheckboxGroup<string>.Class)] = @class;
        }

        return parameters;
    }

    private static EventCallback<IReadOnlyCollection<string>> NullValuesChanged() =>
        EventCallback.Factory.Create<IReadOnlyCollection<string>>(new object(), _ => { });

    // ------------------------------------------------------------------
    // 快照
    // ------------------------------------------------------------------

    [Fact]
    public async Task 默认结构快照应与基线一致()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<CheckboxGroup<string>>(GroupParams(
            Items(("a", "选项 A"), ("b", "选项 B")),
            values: ["a"],
            valuesChanged: NullValuesChanged()));
        await SettleAsync(renderer);

        await Verify(Scrub(renderer.Markup()));
    }

    [Fact]
    public async Task 全选行结构快照应与基线一致()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<CheckboxGroup<string>>(GroupParams(
            Items(("a", "选项 A"), ("b", "选项 B")),
            values: ["a"],
            valuesChanged: NullValuesChanged(),
            showSelectAll: true,
            selectAllLabel: "全选"));
        await SettleAsync(renderer);

        await Verify(Scrub(renderer.Markup()));
    }

    // ------------------------------------------------------------------
    // 项与组联动
    // ------------------------------------------------------------------

    [Fact]
    public async Task 组根应携带group角色与可访问名称()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<CheckboxGroup<string>>(GroupParams(
            Items(("a", "选项 A")), ariaLabel: "通知方式"));

        var markup = renderer.Markup();
        Assert.Contains("role=\"group\"", markup);
        Assert.Contains("aria-label=\"通知方式\"", markup);
        Assert.Contains("atom:grid atom:gap-3", markup);
    }

    [Fact]
    public async Task 勾选未选项应上报加入该值后的集合()
    {
        var reports = new List<IReadOnlyCollection<string>>();
        var callback = EventCallback.Factory.Create<IReadOnlyCollection<string>>(
            new object(), value => reports.Add(value));
        var renderer = CreateRenderer();

        await renderer.MountAsync<CheckboxGroup<string>>(GroupParams(
            Items(("a", "选项 A"), ("b", "选项 B")), values: ["a"], valuesChanged: callback));
        await SettleAsync(renderer);

        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs(), occurrence: 1));

        Assert.Single(reports);
        Assert.Equal(["a", "b"], reports[0]);
    }

    [Fact]
    public async Task 取消已选项应上报移除该值后的集合()
    {
        var reports = new List<IReadOnlyCollection<string>>();
        var callback = EventCallback.Factory.Create<IReadOnlyCollection<string>>(
            new object(), value => reports.Add(value));
        var renderer = CreateRenderer();

        await renderer.MountAsync<CheckboxGroup<string>>(GroupParams(
            Items(("a", "选项 A"), ("b", "选项 B")), values: ["a", "b"], valuesChanged: callback));
        await SettleAsync(renderer);

        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs()));

        Assert.Single(reports);
        Assert.Equal(["b"], reports[0]);
    }

    [Fact]
    public async Task 宿主回写值集合后对应项应呈现选中并渲染勾选图标()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<CheckboxGroup<string>>(GroupParams(
            Items(("a", "选项 A"), ("b", "选项 B")),
            values: ["a"],
            valuesChanged: NullValuesChanged()));
        await SettleAsync(renderer);

        Assert.Contains("aria-checked=\"true\"", renderer.Markup());
        Assert.Contains("aria-checked=\"false\"", renderer.Markup());
        Assert.Single(Regex.Matches(renderer.Markup(), "<polyline"));

        await renderer.RerenderRootAsync(0, GroupParams(
            Items(("a", "选项 A"), ("b", "选项 B")),
            values: ["a", "b"],
            valuesChanged: NullValuesChanged()));
        await SettleAsync(renderer);

        // Regex.Count 静态重载是 (input, pattern)：markup 在前、模式在后。
        Assert.Equal(2, Regex.Count(renderer.Markup(), "<polyline"));
    }

    [Fact]
    public async Task 标签应按for指向项内复选框的id()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<CheckboxGroup<string>>(GroupParams(
            Items(("a", "选项 A")), values: [], valuesChanged: NullValuesChanged()));
        await SettleAsync(renderer);

        var markup = Scrub(renderer.Markup());
        var id = Regex.Match(markup, "id=\"(cbg-<id>)\"");
        Assert.True(id.Success, "项内复选框应携带生成的 id");
        Assert.Contains("for=\"cbg-<id>\"", markup);
        Assert.Contains("选项 A", markup);
    }

    [Fact]
    public async Task 组禁用应级联到全部项()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<CheckboxGroup<string>>(GroupParams(
            Items(("a", "选项 A"), ("b", "选项 B")),
            values: ["a"],
            valuesChanged: NullValuesChanged(),
            disabled: true));
        await SettleAsync(renderer);

        Assert.Equal(2, Regex.Count(renderer.Markup(), "aria-disabled=\\\"true\\\""));
    }

    [Fact]
    public async Task 项自身禁用应只影响该项()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<CheckboxGroup<string>>(GroupParams(
            (RenderFragment)(builder =>
            {
                builder.OpenComponent<CheckboxGroupItem<string>>(0);
                builder.AddAttribute(1, nameof(CheckboxGroupItem<string>.Value), "a");
                builder.AddAttribute(2, nameof(CheckboxGroupItem<string>.Label), (string?)"选项 A");
                builder.CloseComponent();

                builder.OpenComponent<CheckboxGroupItem<string>>(3);
                builder.AddAttribute(4, nameof(CheckboxGroupItem<string>.Value), "b");
                builder.AddAttribute(5, nameof(CheckboxGroupItem<string>.Label), (string?)"选项 B");
                builder.AddAttribute(6, nameof(CheckboxGroupItem<string>.Disabled), true);
                builder.CloseComponent();
            }),
            values: ["a"],
            valuesChanged: NullValuesChanged()));
        await SettleAsync(renderer);

        Assert.Single(Regex.Matches(renderer.Markup(), "aria-disabled=\\\"true\\\""));
    }

    // ------------------------------------------------------------------
    // 全选
    // ------------------------------------------------------------------

    [Fact]
    public async Task 部分选中时全选复选框应呈半选态()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<CheckboxGroup<string>>(GroupParams(
            Items(("a", "选项 A"), ("b", "选项 B")),
            values: ["a"],
            valuesChanged: NullValuesChanged(),
            showSelectAll: true));
        await SettleAsync(renderer);

        Assert.Contains("aria-checked=\"mixed\"", renderer.Markup());
        Assert.Contains("data-state=\"indeterminate\"", renderer.Markup());
    }

    [Fact]
    public async Task 全部选中时全选复选框应呈选中态()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<CheckboxGroup<string>>(GroupParams(
            Items(("a", "选项 A"), ("b", "选项 B")),
            values: ["a", "b"],
            valuesChanged: NullValuesChanged(),
            showSelectAll: true));
        await SettleAsync(renderer);

        Assert.Contains("aria-checked=\"true\"", renderer.Markup());
        Assert.DoesNotContain("mixed", renderer.Markup());
    }

    [Fact]
    public async Task 点击全选应上报全部已注册项的值()
    {
        var reports = new List<IReadOnlyCollection<string>>();
        var callback = EventCallback.Factory.Create<IReadOnlyCollection<string>>(
            new object(), value => reports.Add(value));
        var renderer = CreateRenderer();

        await renderer.MountAsync<CheckboxGroup<string>>(GroupParams(
            Items(("a", "选项 A"), ("b", "选项 B")),
            values: ["a"],
            valuesChanged: callback,
            showSelectAll: true));
        await SettleAsync(renderer);

        // occurrence 0 是全选行的复选框。
        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs()));

        Assert.Single(reports);
        Assert.Equal(["a", "b"], reports[0]);
    }

    [Fact]
    public async Task 取消全选应上报空集合()
    {
        var reports = new List<IReadOnlyCollection<string>>();
        var callback = EventCallback.Factory.Create<IReadOnlyCollection<string>>(
            new object(), value => reports.Add(value));
        var renderer = CreateRenderer();

        await renderer.MountAsync<CheckboxGroup<string>>(GroupParams(
            Items(("a", "选项 A"), ("b", "选项 B")),
            values: ["a", "b"],
            valuesChanged: callback,
            showSelectAll: true));
        await SettleAsync(renderer);

        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs()));

        Assert.Single(reports);
        Assert.Empty(reports[0]);
    }

    [Fact]
    public async Task 未开启全选时不应渲染全选行()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<CheckboxGroup<string>>(GroupParams(
            Items(("a", "选项 A")), values: ["a"], valuesChanged: NullValuesChanged()));
        await SettleAsync(renderer);

        Assert.DoesNotContain("atom:border-b", renderer.Markup());
        Assert.DoesNotContain("Select all", renderer.Markup());
    }
}
