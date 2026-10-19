using AtomBlazor.Primitives;
using System.Text.RegularExpressions;
using AtomBlazor;
using AtomBlazor.Primitives.Services;
using AtomBlazor.Tests.Performance;
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
/// Pagination 组件层测试：自动布局结构沿、页码窗口算法沿（First/Last 恒显、
/// 省略号出现条件、总页数不大于 7 全列）、活动态 aria-current、页码上报
/// （CurrentPageChanged/OnPageChanged 双通道）、State 模式导航按钮边界禁用与跳转、
/// Info/PageDisplay 格式沿、Link 的 Href/Disabled/尺寸沿、Next/Previous 的 ShowText 沿、
/// PageSizeSelector 与 State 协作沿（经私有处理器直调，headless 不可达 Select 弹层链以
/// 状态沿覆盖）、怪癖锁存（State-only 不自动渲染；非受控点击活动态不移动）、
/// 枚举面与默认结构快照。三家族零专属测试，本文件按 TabsTests/AccordionTests 先例自写。
/// </summary>
public class PaginationTests
{
    private static int Count(string haystack, string needle) => haystack.Split(needle).Length - 1;

    /// <summary>按渲染顺序取出全部页码按钮上的数字文本。</summary>
    private static int[] PageNumbers(string markup) =>
        Regex.Matches(markup, "<button[^>]*>(\\d+)</button>")
            .Select(m => int.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture)).ToArray();

    private static ComponentTestRenderer CreateRenderer()
    {
        // PageSizeSelector 内嵌 Select 家族，需要全套基元服务（照 SelectTests 先例）。
        var js = new NoopJavaScript();
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(js);
        services.AddSingleton<IPortalService>(new PortalService(NullLogger<PortalService>.Instance));
        services.AddSingleton<IPositioningService>(new PositioningService(js));
        services.AddSingleton<IFocusManager>(new FocusManager(js));
        services.AddSingleton<DropdownManagerService>();
        services.AddSingleton<ILocalizer>(new DefaultLocalizer());
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        return new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance);
    }

    private static RenderFragment El<T>(RenderFragment? child = null, params (string Name, object Value)[] parameters)
        where T : IComponent
        => builder =>
        {
            builder.OpenComponent<T>(0);
            var sequence = 1;
            if (child is not null)
            {
                builder.AddComponentParameter(sequence++, "ChildContent", child);
            }

            foreach (var (name, value) in parameters)
            {
                builder.AddComponentParameter(sequence++, name, value);
            }

            builder.CloseComponent();
        };

    private static RenderFragment Wrap<T>(RenderFragment inner) where T : IComponent =>
        El<T>(child: inner);

    private static RenderFragment Seq(params RenderFragment[] parts) => builder =>
    {
        foreach (var part in parts)
        {
            part(builder);
        }
    };

    private static RenderFragment AutoPieces() => Seq(
        Wrap<PaginationContent>(Seq(
            Wrap<PaginationItem>(El<PaginationPrevious>()),
            Wrap<PaginationItem>(El<PaginationFirst>()),
            Wrap<PaginationItem>(El<PaginationLast>()),
            Wrap<PaginationItem>(El<PaginationNext>()))),
        El<PaginationInfo>(),
        El<PaginationPageDisplay>(),
        El<PaginationPageSizeSelector>());

    private static Dictionary<string, object?> AutoParams(int totalPages, int currentPage = 1, Action<int>? onPageChanged = null)
    {
        var parameters = new Dictionary<string, object?>
        {
            ["TotalPages"] = totalPages,
            ["CurrentPage"] = currentPage,
        };

        if (onPageChanged is not null)
        {
            parameters["OnPageChanged"] = EventCallback.Factory.Create<int>(new object(), onPageChanged);
        }

        return parameters;
    }

    private static async Task<(ComponentTestRenderer Renderer, Pagination Pagination)> MountAsync(
        Dictionary<string, object?> parameters)
    {
        var renderer = CreateRenderer();
        var pagination = await renderer.MountAsync<Pagination>(parameters);
        await renderer.Dispatcher.InvokeAsync(() => { });
        return (renderer, pagination);
    }

    private static Task ClickAsync(ComponentTestRenderer renderer, int occurrence = 0) =>
        renderer.Dispatcher.InvokeAsync(() => renderer.DispatchAsync("onclick", new MouseEventArgs(), occurrence));

    // ------------------------------------------------------------------ 自动布局与窗口算法沿

    [Fact]
    public async Task 自动布局应渲染上下页按钮与全部页码并带本地化标签()
    {
        var (renderer, _) = await MountAsync(AutoParams(5));
        var markup = renderer.Markup();

        Assert.Contains("aria-label=\"Pagination\"", markup);
        Assert.Equal(7, Count(markup, "<button")); // prev + 5 页 + next
        Assert.Equal([1, 2, 3, 4, 5], PageNumbers(markup));
        // ShowText=false 的上下页按钮文本只供屏幕阅读器。
        Assert.Equal(2, Count(markup, "atom:sr-only"));
    }

    [Fact]
    public async Task 总页数不大于七时应列出全部页码且无省略号()
    {
        var (renderer, _) = await MountAsync(AutoParams(7));
        var markup = renderer.Markup();

        Assert.Equal([1, 2, 3, 4, 5, 6, 7], PageNumbers(markup));
        Assert.Equal(0, Count(markup, "aria-hidden=\"true\""));
    }

    [Fact]
    public async Task 首页附近窗口应恒含首页末页且省略号只出现在右侧()
    {
        var (renderer, _) = await MountAsync(AutoParams(10, currentPage: 1));
        var markup = renderer.Markup();

        Assert.Equal([1, 2, 10], PageNumbers(markup));
        Assert.Equal(1, Count(markup, "aria-hidden=\"true\""));
    }

    [Fact]
    public async Task 中段窗口应在两侧各出现省略号并含当前页邻域()
    {
        var (renderer, _) = await MountAsync(AutoParams(10, currentPage: 5));
        var markup = renderer.Markup();

        Assert.Equal([1, 4, 5, 6, 10], PageNumbers(markup));
        Assert.Equal(2, Count(markup, "aria-hidden=\"true\""));
    }

    [Fact]
    public async Task 末页附近窗口省略号只应出现在左侧()
    {
        var (renderer, _) = await MountAsync(AutoParams(10, currentPage: 9));
        var markup = renderer.Markup();

        Assert.Equal([1, 8, 9, 10], PageNumbers(markup));
        Assert.Equal(1, Count(markup, "aria-hidden=\"true\""));
    }

    [Fact]
    public async Task 活动页码应唯一带当前页属性()
    {
        var (renderer, _) = await MountAsync(AutoParams(10, currentPage: 5));
        var markup = renderer.Markup();

        Assert.Equal(1, Count(markup, "aria-current=\"page\""));
        Assert.Contains(">5</button>", markup);
    }

    // ------------------------------------------------------------------ 上报与受控沿

    [Fact]
    public async Task 点击页码应经OnPageChanged上报且未回写参数时活动态不移动怪癖锁存()
    {
        var reported = new List<int>();
        var (renderer, _) = await MountAsync(AutoParams(10, currentPage: 5, onPageChanged: reported.Add));

        // 按钮顺序：prev(0)，页码 1(1)、4(2)、5(3)、6(4)、10(5)，next(6)。点页码 4。
        await ClickAsync(renderer, 2);

        Assert.Equal([4], reported);
        // EffectiveCurrentPage 只读 CurrentPage 参数：父组件不回写则高亮仍停在 5（行为）。
        var active = Regex.Matches(renderer.Markup(), "<button[^>]*aria-current=\"page\"[^>]*>(\\d+)</button>");
        Assert.Single(active);
        Assert.Equal("5", active[0].Groups[1].Value);
    }

    [Fact]
    public async Task 回写CurrentPage参数后窗口与活动态应随新页移动()
    {
        var (renderer, _) = await MountAsync(AutoParams(10, currentPage: 1));

        await renderer.RerenderRootAsync(0, new Dictionary<string, object?> { ["CurrentPage"] = 5 });

        Assert.Equal([1, 4, 5, 6, 10], PageNumbers(renderer.Markup()));
        Assert.Equal(2, Count(renderer.Markup(), "aria-hidden=\"true\""));
    }

    [Fact]
    public async Task 单页时上下页按钮均应禁用且点击无上报()
    {
        var reported = new List<int>();
        var (renderer, _) = await MountAsync(AutoParams(1, onPageChanged: reported.Add));
        var markup = renderer.Markup();

        // 布尔特性在本渲染器里写作裸 disabled（后随 class），据此计数。
        Assert.Equal(2, Count(markup, " disabled"));
        await ClickAsync(renderer, 0); // 点 Previous（禁用）
        await ClickAsync(renderer, 2); // 点 Next（禁用）
        Assert.Empty(reported);
    }

    // ------------------------------------------------------------------ State 模式沿

    [Fact]
    public async Task 仅提供State不提供ChildContent时导航内应为空怪癖锁存()
    {
        var (renderer, _) = await MountAsync(new Dictionary<string, object?>
        {
            ["State"] = new PaginationState { TotalItems = 95 },
        });

        Assert.Contains("<nav", renderer.Markup());
        Assert.DoesNotContain("<ul", renderer.Markup());
        Assert.DoesNotContain("<button", renderer.Markup());
    }

    [Fact]
    public async Task State模式应渲染信息区并在首页压制向前导航()
    {
        var reported = new List<int>();
        var (renderer, _) = await MountAsync(new Dictionary<string, object?>
        {
            ["State"] = new PaginationState { TotalItems = 95 },
            ["ChildContent"] = AutoPieces(),
            ["OnPageChanged"] = EventCallback.Factory.Create<int>(new object(), reported.Add),
        });
        var markup = renderer.Markup();

        Assert.Contains("Showing 1-10 of 95", markup);
        Assert.Contains("Page 1 of 10", markup);
        // 第 1 页：Previous(0) 与 First(1) 禁用，压制点击。
        Assert.Equal(2, Count(markup, " disabled"));
        await ClickAsync(renderer, 0);
        await ClickAsync(renderer, 1);
        Assert.Empty(reported);
    }

    [Fact]
    public async Task State模式点击Next与Last应移动页码并经回调上报()
    {
        var reported = new List<int>();
        var state = new PaginationState { TotalItems = 95 };
        var (renderer, _) = await MountAsync(new Dictionary<string, object?>
        {
            ["State"] = state,
            ["ChildContent"] = AutoPieces(),
            ["OnPageChanged"] = EventCallback.Factory.Create<int>(new object(), reported.Add),
        });

        await ClickAsync(renderer, 3); // Next
        await ClickAsync(renderer, 2); // Last

        Assert.Equal([2, 10], reported);
        Assert.Equal(10, state.CurrentPage);
        // 怪癖：PaginationState 无变更通知，兄弟子组件不随按钮自身的事件批重渲——
        // 信息区只有容器级重渲后才反映末页（headless 下以显式重渲覆盖该沿）。
        await renderer.RerenderRootAsync(0, new Dictionary<string, object?>());
        Assert.Contains("Page 10 of 10", renderer.Markup());
        // 末页：Next 与 Last 变禁用（前二者恢复可用），再点 Next 不再上报。
        await ClickAsync(renderer, 3);
        Assert.Equal([2, 10], reported);
    }

    [Fact]
    public async Task State模式点击First应回到首页且Info零条目时显示NoItems()
    {
        var reported = new List<int>();
        var state = new PaginationState { TotalItems = 95 };
        state.GoToPage(8);
        var (renderer, _) = await MountAsync(new Dictionary<string, object?>
        {
            ["State"] = state,
            ["ChildContent"] = AutoPieces(),
            ["OnPageChanged"] = EventCallback.Factory.Create<int>(new object(), reported.Add),
        });

        await ClickAsync(renderer, 1); // First
        Assert.Equal([1], reported);
        Assert.Equal(1, state.CurrentPage);
        await renderer.RerenderRootAsync(0, new Dictionary<string, object?>());
        Assert.Contains("Showing 1-10 of 95", renderer.Markup());

        var empty = CreateRenderer();
        await empty.MountAsync<Pagination>(new Dictionary<string, object?>
        {
            ["State"] = new PaginationState { TotalItems = 0 },
            ["ChildContent"] = El<PaginationInfo>(),
        });
        Assert.Contains("No items", empty.Markup());
    }

    [Fact]
    public async Task Info与PageDisplay应支持自定义格式覆盖()
    {
        var (renderer, _) = await MountAsync(new Dictionary<string, object?>
        {
            ["State"] = new PaginationState { TotalItems = 42 },
            ["ChildContent"] = Seq(
                El<PaginationInfo>(parameters: ("Format", "第 {0} 至 {1} 条，共 {2}")),
                El<PaginationPageDisplay>(parameters: ("Format", "{0}/{1} 页"))),
        });
        var markup = renderer.Markup();

        Assert.Contains("第 1 至 10 条，共 42", markup);
        Assert.Contains("1/5 页", markup);
    }

    // ------------------------------------------------------------------ Link 与导航件沿

    [Fact]
    public async Task 手动Link应支持锚点通路且Disabled应压制点击()
    {
        var clicks = 0;
        var renderer = CreateRenderer();
        await renderer.MountAsync<PaginationLink>(new Dictionary<string, object?>
        {
            ["Href"] = "/page/2",
            ["IsActive"] = true,
            ["OnClick"] = EventCallback.Factory.Create(new object(), () => clicks++),
        });
        Assert.Contains("<a href=\"/page/2\"", renderer.Markup());
        Assert.Contains("aria-current=\"page\"", renderer.Markup());

        var blocked = CreateRenderer();
        await blocked.MountAsync<PaginationLink>(new Dictionary<string, object?>
        {
            ["Disabled"] = true,
            ["OnClick"] = EventCallback.Factory.Create(new object(), () => clicks++),
        });
        Assert.Contains("<button", blocked.Markup());
        await ClickAsync(blocked);
        Assert.Equal(0, clicks);
        Assert.Contains("atom:pointer-events-none", blocked.Markup());
    }

    [Fact]
    public async Task Link尺寸Default与Icon应给出不同类串()
    {
        var renderer = CreateRenderer();
        await renderer.MountAsync<PaginationLink>(new Dictionary<string, object?>
        {
            ["Size"] = PaginationLinkSize.Default,
        });
        Assert.Contains("atom:h-10 atom:px-4 atom:py-2", renderer.Markup());

        var icon = CreateRenderer();
        await icon.MountAsync<PaginationLink>(new Dictionary<string, object?>
        {
            ["Size"] = PaginationLinkSize.Icon,
        });
        Assert.Contains("atom:h-10 atom:w-10", icon.Markup());
    }

    [Fact]
    public async Task Next与Previous的ShowText开关应切换可见文本与尺寸回退()
    {
        var renderer = CreateRenderer();
        await renderer.MountAsync<PaginationNext>(new Dictionary<string, object?>());
        var withText = renderer.Markup();
        Assert.Contains("<span>Next</span>", withText);
        Assert.Contains("atom:gap-1 atom:pe-2.5", withText);
        Assert.Contains("atom:px-4", withText); // ShowText=true 时用默认 Default 尺寸

        await renderer.RerenderRootAsync(0, new Dictionary<string, object?> { ["ShowText"] = false });
        var iconOnly = renderer.Markup();
        Assert.Contains("<span class=\"atom:sr-only\">Next</span>", iconOnly);
        Assert.Contains("atom:h-8 atom:w-8", iconOnly);

        var previous = CreateRenderer();
        await previous.MountAsync<PaginationPrevious>(new Dictionary<string, object?>
        {
            ["Text"] = "上一步",
        });
        Assert.Contains("<span>上一步</span>", previous.Markup());
    }

    [Fact]
    public async Task 省略号件应画图标并带更多页屏幕阅读器文本()
    {
        var renderer = CreateRenderer();
        await renderer.MountAsync<PaginationEllipsis>(new Dictionary<string, object?>());
        var markup = renderer.Markup();

        Assert.Contains("aria-hidden=\"true\"", markup);
        Assert.Contains("<svg", markup);
        Assert.Contains("More pages", markup);
    }

    // ------------------------------------------------------------------ PageSizeSelector 协作沿

    [Fact]
    public async Task PageSizeSelector变更页大小应写回State钳回首页并经上下文中报()
    {
        var reported = new List<int>();
        var state = new PaginationState { TotalItems = 95 };
        state.GoToPage(4);
        var (renderer, _) = await MountAsync(new Dictionary<string, object?>
        {
            ["State"] = state,
            ["ChildContent"] = AutoPieces(),
            ["OnPageSizeChanged"] = EventCallback.Factory.Create<int>(new object(), reported.Add),
        });
        var selector = renderer.FindComponent<PaginationPageSizeSelector>();

        await renderer.Dispatcher.InvokeAsync(async () =>
            await (Task)ComponentProbe.Call(selector, "HandlePageSizeChanged", "20")!);

        Assert.Equal([20], reported);
        Assert.Equal(20, state.PageSize);
        Assert.Equal(1, state.CurrentPage); // ：SizeChanged 里显式钳回第 1 页
        Assert.Contains("Rows per page", renderer.Markup());

        // 非数字值：int.TryParse 失败，State 与上报都不动。
        await renderer.Dispatcher.InvokeAsync(async () =>
            await (Task)ComponentProbe.Call(selector, "HandlePageSizeChanged", "abc")!);
        Assert.Equal([20], reported);
        Assert.Equal(20, state.PageSize);
    }

    [Fact]
    public async Task 枚举成员应按声明序取值()
    {
        Assert.Equal(0, (int)PaginationLinkSize.Default);
        Assert.Equal(1, (int)PaginationLinkSize.Icon);
    }

    // ------------------------------------------------------------------ 快照

    [Fact]
    public async Task 默认结构快照应与基线一致()
    {
        var (renderer, _) = await MountAsync(AutoParams(10, currentPage: 5));

        await Verify(renderer.Markup());
    }
}
