using AtomBlazor.Primitives;
using System.Text.RegularExpressions;
using AtomBlazor;
using AtomBlazor.Primitives.Extensions;
using AtomBlazor.Tests.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Xunit;

namespace AtomBlazor.Tests.Components;

/// <summary>
/// ListBox 组件层行为测试：单选/多选两模式的 ARIA 面（role=listbox、aria-selected、
/// aria-multiselectable、data-selected、aria-activedescendant）、搜索词参数与键入两路过滤
/// （含 No options/No matches 空态文案与 EmptyMessage 覆盖）、OptionDisabled 的 aria-disabled
/// 与 pointer-events 落点与拒击、全选行的出现条件（仅多选且有可见项）与 None/Indeterminate/All
/// 三态及只作用搜索后可见项、SelectedCount 计数文案、整表禁用的 Tab 序列与点击拒绝、
/// AdditionalAttributes 落根元素、ItemTemplate 替换默认文本、Height 落滚动区样式，
/// 以及键盘沿（onfocus 后 aria-activedescendant 挂接、方向键在单选下移动即提交、Enter 在
/// 多选下拨动、搜索框 ArrowDown 请求聚焦列表）。
/// 键盘与指针全部经渲染器真实事件沿触发，不反射私有成员；headless 下不断言真实焦点沿，
/// 照先例只断言可公开观察的渲染面与 interop 载荷（NoopJavaScript，仿 /c 口径）。
/// </summary>
public class ListBoxTests
{
    /// <summary>Options 模式数据：值小写、文本首字母大写，供过滤与文本断言。</summary>
    private static readonly SelectOption<string>[] defaultOptions =
    [
        new("alpha", "Alpha"),
        new("beta", "Beta"),
        new("gamma", "Gamma"),
    ];

    private static readonly string[] allThreeValues = ["alpha", "beta", "gamma"];

    private static readonly string[] oneBetaValue = ["beta"];

    private static ComponentTestRenderer CreateRenderer(out NoopJavaScript js)
    {
        var noop = new NoopJavaScript();
        js = noop;
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(noop);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddAtomBlazorPrimitives();
        services.AddAtomBlazorComponents();
        return new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance);
    }

    private static ComponentTestRenderer CreateRenderer() => CreateRenderer(out _);

    /// <summary>排空渲染队列，确保事件沿触发的后续批已落定。</summary>
    private static Task SettleAsync(ComponentTestRenderer renderer) =>
        renderer.Dispatcher.InvokeAsync(() => Task.CompletedTask);

    private static Task ClickAsync(ComponentTestRenderer renderer, int occurrence = 0) =>
        renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs(), occurrence));

    private static Task KeyAsync(ComponentTestRenderer renderer, string key, int occurrence = 0) =>
        renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onkeydown", new KeyboardEventArgs { Key = key }, occurrence));

    private static Task FocusAsync(ComponentTestRenderer renderer, int occurrence = 0) =>
        renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onfocus", new FocusEventArgs(), occurrence));

    private static Task InputAsync(ComponentTestRenderer renderer, string value, int occurrence = 0) =>
        renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("oninput", new ChangeEventArgs { Value = value }, occurrence));

    private static Dictionary<string, object?> Params(
        SelectOption<string>[]? options = null,
        ListBoxSelectionMode mode = ListBoxSelectionMode.Single,
        string? value = null,
        EventCallback<string?> valueChanged = default,
        IEnumerable<string>? values = null,
        EventCallback<IEnumerable<string>?> valuesChanged = default,
        string? searchText = null,
        bool? showSelectAll = null)
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(ListBox<string>.Options)] = options ?? defaultOptions,
            [nameof(ListBox<string>.SelectionMode)] = mode,
        };
        if (value is not null)
        {
            parameters[nameof(ListBox<string>.Value)] = value;
        }
        if (valueChanged.HasDelegate)
        {
            parameters[nameof(ListBox<string>.ValueChanged)] = valueChanged;
        }
        if (values is not null)
        {
            parameters[nameof(ListBox<string>.Values)] = values;
        }
        if (valuesChanged.HasDelegate)
        {
            parameters[nameof(ListBox<string>.ValuesChanged)] = valuesChanged;
        }
        if (searchText is not null)
        {
            parameters[nameof(ListBox<string>.SearchText)] = searchText;
        }
        if (showSelectAll is not null)
        {
            parameters[nameof(ListBox<string>.ShowSelectAll)] = showSelectAll.Value;
        }

        return parameters;
    }

    /// <summary>稳定短 id 归一化：atom-listbox-{guid} → atom-listbox-&lt;id&gt;。</summary>
    private static string ScrubIds(string markup) =>
        Regex.Replace(markup, "atom-listbox-[0-9a-f]{32}", "atom-listbox-<id>");

    private static int Count(string haystack, string needle) => haystack.Split(needle).Length - 1;

    // ------------------------------------------------------------------
    // 单选模式
    // ------------------------------------------------------------------

    [Fact]
    public async Task 单选应渲染列表框角色与选中无障碍面()
    {
        var renderer = CreateRenderer();
        await renderer.MountAsync<ListBox<string>>(Params(value: "beta"));

        var markup = ScrubIds(renderer.Markup());
        Assert.Contains("role=\"listbox\"", markup);
        Assert.Contains("id=\"atom-listbox-<id>\"", markup);
        Assert.Contains("id=\"atom-listbox-<id>-option-0\"", markup);
        // 单选不声明可多选
        Assert.DoesNotContain("aria-multiselectable", markup);
        // 恰一行选中，且选中态同时落 aria-selected 与 data-selected
        Assert.Equal(1, Count(markup, "aria-selected=\"true\""));
        Assert.Equal(1, Count(markup, "data-selected=\"true\""));
        Assert.Contains("Beta", markup);
        // 未聚焦时不挂活动后代
        Assert.DoesNotContain("aria-activedescendant", markup);
    }

    [Fact]
    public async Task 单选点击应回填新值并保持唯一选中()
    {
        var reports = new List<string?>();
        var renderer = CreateRenderer();
        await renderer.MountAsync<ListBox<string>>(Params(
            valueChanged: EventCallback.Factory.Create<string?>(new object(), v => reports.Add(v))));

        // 无搜索框/全选行时，onclick 次序即选项目：0=Alpha、1=Beta、2=Gamma
        await ClickAsync(renderer, occurrence: 0);
        await SettleAsync(renderer);

        Assert.Equal(["alpha"], reports);
        var markup = renderer.Markup();
        Assert.Equal(1, Count(markup, "aria-selected=\"true\""));
        Assert.Equal(1, Count(markup, "data-selected=\"true\""));
        // 单选指示器为纯容器，不带多选复选方块的填充类
        Assert.DoesNotContain("atom:bg-primary", markup);
    }

    // ------------------------------------------------------------------
    // 多选模式
    // ------------------------------------------------------------------

    [Fact]
    public async Task 多选应声明可多选并渲染复选指示器()
    {
        var renderer = CreateRenderer();
        await renderer.MountAsync<ListBox<string>>(Params(
            mode: ListBoxSelectionMode.Multiple,
            values: oneBetaValue,
            showSelectAll: false));

        var markup = renderer.Markup();
        Assert.Contains("aria-multiselectable=\"true\"", markup);
        Assert.Equal(1, Count(markup, "aria-selected=\"true\""));
        // 选中行填充复选方块并带勾形图标；未选行为空框
        Assert.Contains("atom:bg-primary", markup);
        Assert.Equal(1, Count(markup, "<svg"));
    }

    [Fact]
    public async Task 多选点击应拨动选项累加并在清空时回填null()
    {
        var reports = new List<IEnumerable<string>?>();
        var renderer = CreateRenderer();
        await renderer.MountAsync<ListBox<string>>(Params(
            mode: ListBoxSelectionMode.Multiple,
            valuesChanged: EventCallback.Factory.Create<IEnumerable<string>?>(new object(), v => reports.Add(v)),
            showSelectAll: false));

        // 全选行关闭后 onclick 次序：Alpha(0)、Beta(1)、Gamma(2)
        await ClickAsync(renderer, occurrence: 0);
        await SettleAsync(renderer);
        await ClickAsync(renderer, occurrence: 1);
        await SettleAsync(renderer);
        // 再次点击已选 Alpha：拨出，余 Beta
        await ClickAsync(renderer, occurrence: 0);
        await SettleAsync(renderer);
        // 末项也拨出：清空回填 null
        await ClickAsync(renderer, occurrence: 1);
        await SettleAsync(renderer);

        Assert.Equal(4, reports.Count);
        Assert.Equal(["alpha"], reports[0]);
        Assert.Equal(["alpha", "beta"], reports[1]);
        Assert.Equal(["beta"], reports[2]);
        Assert.Null(reports[3]);
    }

    // ------------------------------------------------------------------
    // 搜索
    // ------------------------------------------------------------------

    [Fact]
    public async Task 搜索词参数应筛除不匹配选项()
    {
        var renderer = CreateRenderer();
        await renderer.MountAsync<ListBox<string>>(Params(searchText: "ga"));

        var markup = renderer.Markup();
        Assert.Contains("Gamma", markup);
        Assert.DoesNotContain("Alpha", markup);
        Assert.DoesNotContain("Beta", markup);
    }

    [Fact]
    public async Task 空列表与无匹配应分别呈现本地化空态文案且可被EmptyMessage覆盖()
    {
        var empty = CreateRenderer();
        await empty.MountAsync<ListBox<string>>(Params(options: []));
        Assert.Contains("No options", empty.Markup()); // Localizer["ListBox.Empty"]

        var noMatch = CreateRenderer();
        await noMatch.MountAsync<ListBox<string>>(Params(searchText: "zzz"));
        Assert.Contains("No matches", noMatch.Markup()); // Localizer["ListBox.NoMatches"]

        var custom = CreateRenderer();
        await custom.MountAsync<ListBox<string>>(new Dictionary<string, object?>
        {
            [nameof(ListBox<string>.Options)] = Array.Empty<SelectOption<string>>(),
            [nameof(ListBox<string>.EmptyMessage)] = "暂无可选项",
        });
        var markup = custom.Markup();
        Assert.Contains("暂无可选项", markup);
        Assert.DoesNotContain("No options", markup);
    }

    [Fact]
    public async Task 键入搜索应过滤列表并旁路上报搜索词()
    {
        var queries = new List<string?>();
        var renderer = CreateRenderer();
        await renderer.MountAsync<ListBox<string>>(new Dictionary<string, object?>
        {
            [nameof(ListBox<string>.Options)] = defaultOptions,
            [nameof(ListBox<string>.ShowSearch)] = true,
            [nameof(ListBox<string>.SearchTextChanged)] =
                EventCallback.Factory.Create<string?>(new object(), t => queries.Add(t)),
        });

        var markup = renderer.Markup();
        Assert.Contains("Search...", markup); // Localizer["ListBox.SearchPlaceholder"]

        await InputAsync(renderer, "be");
        await SettleAsync(renderer);

        Assert.Equal(["be"], queries);
        markup = renderer.Markup();
        Assert.Contains("Beta", markup);
        Assert.DoesNotContain(">Alpha<", markup);
        Assert.DoesNotContain(">Gamma<", markup);
        // 输入框回显当前查询词
        Assert.Contains("value=\"be\"", markup);
    }

    // ------------------------------------------------------------------
    // OptionDisabled 与整表禁用
    // ------------------------------------------------------------------

    [Fact]
    public async Task 禁用选项应播报不可用拒绝点击并落指针事件屏蔽类()
    {
        var reports = new List<string?>();
        var renderer = CreateRenderer();
        await renderer.MountAsync<ListBox<string>>(new Dictionary<string, object?>
        {
            [nameof(ListBox<string>.Options)] = defaultOptions,
            [nameof(ListBox<string>.OptionDisabled)] =
                (Func<SelectOption<string>, bool>)(o => o.Value == "beta"),
            [nameof(ListBox<string>.ValueChanged)] =
                EventCallback.Factory.Create<string?>(new object(), v => reports.Add(v)),
        });

        var markup = renderer.Markup();
        Assert.Contains("aria-disabled=\"true\"", markup);
        Assert.Contains("atom:pointer-events-none", markup);

        // onclick 次序：Alpha(0)、Beta(1)、Gamma(2)
        await ClickAsync(renderer, occurrence: 1);
        await SettleAsync(renderer);
        Assert.Empty(reports);

        // 相邻可用项照旧可点
        await ClickAsync(renderer, occurrence: 2);
        await SettleAsync(renderer);
        Assert.Equal(["gamma"], reports);
    }

    [Fact]
    public async Task 整表禁用应移出Tab序列忽略点击并加灰()
    {
        var reports = new List<string?>();
        var renderer = CreateRenderer();
        await renderer.MountAsync<ListBox<string>>(new Dictionary<string, object?>
        {
            [nameof(ListBox<string>.Options)] = defaultOptions,
            [nameof(ListBox<string>.Disabled)] = true,
            [nameof(ListBox<string>.ValueChanged)] =
                EventCallback.Factory.Create<string?>(new object(), v => reports.Add(v)),
        });

        var markup = renderer.Markup();
        Assert.Contains("aria-disabled=\"true\"", markup);
        Assert.Contains("tabindex=\"-1\"", markup);
        Assert.Contains("atom:opacity-60", markup);

        await ClickAsync(renderer, occurrence: 0);
        await SettleAsync(renderer);
        Assert.Empty(reports);
    }

    // ------------------------------------------------------------------
    // 全选行
    // ------------------------------------------------------------------

    [Fact]
    public async Task 全选行应仅在多选且有可见选项时出现()
    {
        var single = CreateRenderer();
        await single.MountAsync<ListBox<string>>(new Dictionary<string, object?>
        {
            [nameof(ListBox<string>.Options)] = defaultOptions,
            [nameof(ListBox<string>.ShowSelectAll)] = true,
        });
        Assert.DoesNotContain("Select all", single.Markup());

        var multiple = CreateRenderer();
        await multiple.MountAsync<ListBox<string>>(Params(mode: ListBoxSelectionMode.Multiple));
        var markup = multiple.Markup();
        Assert.Contains("Select all", markup); // Localizer["ListBox.SelectAll"]
        Assert.Contains("role=\"checkbox\"", markup);
        // 全选行在列表之外，不占 option 席位
        Assert.Equal(3, Count(markup, "role=\"option\""));

        // 选项被清空后即便多选+ShowSelectAll 也不出现（无可见项）
        var emptyOptions = CreateRenderer();
        await emptyOptions.MountAsync<ListBox<string>>(new Dictionary<string, object?>
        {
            [nameof(ListBox<string>.Options)] = Array.Empty<SelectOption<string>>(),
            [nameof(ListBox<string>.SelectionMode)] = ListBoxSelectionMode.Multiple,
            [nameof(ListBox<string>.ShowSelectAll)] = true,
        });
        var emptyMarkup = emptyOptions.Markup();
        Assert.DoesNotContain("Select all", emptyMarkup);
        Assert.DoesNotContain("role=\"checkbox\"", emptyMarkup);
    }

    [Fact]
    public async Task 全选未选态点击应勾选全部可见项()
    {
        var reports = new List<IEnumerable<string>?>();
        var renderer = CreateRenderer();
        await renderer.MountAsync<ListBox<string>>(Params(
            mode: ListBoxSelectionMode.Multiple,
            valuesChanged: EventCallback.Factory.Create<IEnumerable<string>?>(new object(), v => reports.Add(v))));

        // 全选行在 ul 之前：onclick 次序 SelectAll(0)、Alpha(1)、Beta(2)、Gamma(3)
        Assert.Contains("data-state=\"unchecked\"", renderer.Markup());
        await ClickAsync(renderer, occurrence: 0);
        await SettleAsync(renderer);

        var all = Assert.Single(reports);
        Assert.Equal(
            ["alpha", "beta", "gamma"],
            all?.OrderBy(v => v, StringComparer.Ordinal));
    }

    [Fact]
    public async Task 全选部分选中应呈不确定态并携带计数文案()
    {
        var reports = new List<IEnumerable<string>?>();
        var renderer = CreateRenderer();
        await renderer.MountAsync<ListBox<string>>(Params(
            mode: ListBoxSelectionMode.Multiple,
            values: oneBetaValue,
            valuesChanged: EventCallback.Factory.Create<IEnumerable<string>?>(new object(), v => reports.Add(v))));

        var markup = renderer.Markup();
        Assert.Contains("data-state=\"indeterminate\"", markup);
        Assert.Contains("1 selected", markup); // Localizer["ListBox.SelectedCount", 1]

        await ClickAsync(renderer, occurrence: 0);
        await SettleAsync(renderer);

        var all = Assert.Single(reports);
        Assert.Equal(
            ["alpha", "beta", "gamma"],
            all?.OrderBy(v => v, StringComparer.Ordinal));
    }

    [Fact]
    public async Task 全选已选态点击应清空并回填null()
    {
        var reports = new List<IEnumerable<string>?>();
        var renderer = CreateRenderer();
        await renderer.MountAsync<ListBox<string>>(Params(
            mode: ListBoxSelectionMode.Multiple,
            values: allThreeValues,
            valuesChanged: EventCallback.Factory.Create<IEnumerable<string>?>(new object(), v => reports.Add(v))));

        Assert.Contains("data-state=\"checked\"", renderer.Markup());
        Assert.Contains("3 selected", renderer.Markup());

        await ClickAsync(renderer, occurrence: 0);
        await SettleAsync(renderer);

        var cleared = Assert.Single(reports);
        Assert.Null(cleared);
        Assert.Contains("data-state=\"unchecked\"", renderer.Markup());
    }

    [Fact]
    public async Task 全选应只作用搜索后可见项()
    {
        var reports = new List<IEnumerable<string>?>();
        var renderer = CreateRenderer();
        await renderer.MountAsync<ListBox<string>>(Params(
            mode: ListBoxSelectionMode.Multiple,
            searchText: "ga",
            valuesChanged: EventCallback.Factory.Create<IEnumerable<string>?>(new object(), v => reports.Add(v))));

        // 唯一可见项 Gamma 之外的 Alpha/Beta 已被搜索滤出，全选不得越权勾选
        await ClickAsync(renderer, occurrence: 0); // 全选
        await SettleAsync(renderer);

        var only = Assert.Single(reports);
        Assert.Equal(["gamma"], only);
    }

    // ------------------------------------------------------------------
    // 键盘沿
    // ------------------------------------------------------------------

    [Fact]
    public async Task 聚焦应挂接活动后代且方向键在单选下移动即提交()
    {
        var reports = new List<string?>();
        var renderer = CreateRenderer();
        await renderer.MountAsync<ListBox<string>>(Params(
            valueChanged: EventCallback.Factory.Create<string?>(new object(), v => reports.Add(v))));

        // 无搜索框/全选行时，onfocus 唯一携带者为 ul
        await FocusAsync(renderer);
        await SettleAsync(renderer);
        var markup = renderer.Markup();
        Assert.Matches("aria-activedescendant=\"atom-listbox-[0-9a-f]{32}-option-0\"", markup);
        Assert.Contains("data-active=\"true\"", markup);

        await KeyAsync(renderer, "ArrowDown");
        await SettleAsync(renderer);

        Assert.Equal(["beta"], reports);
        markup = renderer.Markup();
        Assert.Matches("aria-activedescendant=\"atom-listbox-[0-9a-f]{32}-option-1\"", markup);
        Assert.Equal(1, Count(markup, "aria-selected=\"true\""));
    }

    [Fact]
    public async Task 回车应在多选下拨动活动项而不提交单选值()
    {
        var multiReports = new List<IEnumerable<string>?>();
        var renderer = CreateRenderer();
        await renderer.MountAsync<ListBox<string>>(Params(
            mode: ListBoxSelectionMode.Multiple,
            valuesChanged: EventCallback.Factory.Create<IEnumerable<string>?>(new object(), v => multiReports.Add(v)),
            showSelectAll: false));

        await FocusAsync(renderer);
        await SettleAsync(renderer);
        await KeyAsync(renderer, "Enter");
        await SettleAsync(renderer);

        Assert.Equal(["alpha"], multiReports.Single());
        Assert.Contains("data-active=\"true\"", renderer.Markup());
    }

    [Fact]
    public async Task 搜索框内按下应把活动项交给列表并请求聚焦列表()
    {
        var renderer = CreateRenderer(out var js);
        await renderer.MountAsync<ListBox<string>>(new Dictionary<string, object?>
        {
            [nameof(ListBox<string>.Options)] = defaultOptions,
            [nameof(ListBox<string>.ShowSearch)] = true,
        });

        // onkeydown 次序：搜索框(0)、列表(1)
        await KeyAsync(renderer, "ArrowDown", occurrence: 0);
        await SettleAsync(renderer);

        var invocation = Assert.Single(js.Invocations, i => i.Identifier == "elementUtils.focusElement");
        Assert.Matches(@"^atom-listbox-[0-9a-f]{32}$", Assert.IsType<string>(invocation.Args![0]));
    }

    // ------------------------------------------------------------------
    // 参数面
    // ------------------------------------------------------------------

    [Fact]
    public async Task 附加属性自定义类与高度应落根元素与滚动区()
    {
        var renderer = CreateRenderer();
        await renderer.MountAsync<ListBox<string>>(new Dictionary<string, object?>
        {
            [nameof(ListBox<string>.Options)] = defaultOptions,
            [nameof(ListBox<string>.Class)] = "atom:my-listbox",
            [nameof(ListBox<string>.AriaLabel)] = "字母表",
            [nameof(ListBox<string>.Height)] = "42rem",
            [nameof(ListBox<string>.AdditionalAttributes)] = new Dictionary<string, object>
            {
                ["data-custom"] = "splatted",
            },
        });

        var markup = renderer.Markup();
        Assert.Contains("data-slot=\"listbox\"", markup);
        Assert.Contains("data-custom=\"splatted\"", markup);
        Assert.Contains("atom:my-listbox", markup);
        Assert.Contains("aria-label=\"字母表\"", markup);
        // 高度参数落列表滚动区样式
        Assert.Contains("style=\"max-height: 42rem\"", markup);
    }

    [Fact]
    public async Task 自定义项模板应替换默认文本但保留指示器()
    {
        var renderer = CreateRenderer();
        await renderer.MountAsync<ListBox<string>>(new Dictionary<string, object?>
        {
            [nameof(ListBox<string>.Options)] = defaultOptions,
            [nameof(ListBox<string>.ItemTemplate)] = (RenderFragment<SelectOption<string>>)(option => builder =>
            {
                builder.OpenElement(0, "span");
                builder.AddContent(1, $"[{option.Text}]");
                builder.CloseElement();
            }),
        });

        var markup = renderer.Markup();
        Assert.Contains("[Alpha]", markup);
        Assert.Contains("[Gamma]", markup);
        Assert.DoesNotContain(">Alpha<", markup);
        // 指示器照旧绘制
        Assert.Contains("aria-hidden=\"true\"", markup);
    }
}
