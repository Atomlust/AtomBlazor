using AtomBlazor.Primitives;
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
/// Timeline 组件层测试：空态兜底与 ChildContent 空片段绕空怪癖、Size/Align 类串沿、
/// Status 状态机沿（TimelineStatus 实为 Completed/InProgress/Pending 三成员——简报所列
/// Complete/Active/Inactive/Error 与枚举面不符，按实证纠偏）、Connector 样式×状态×显式
/// 覆盖配色表、Icon 变体×配色 10 行映射表与 Pending 降级 Muted、ConnectorFit 外环沿、
/// Alternate 反序布局（RegisterItem 索引沿）、简写树（Title/Time/Description）与自定义
/// IconContent/TimeContent 替换沿、Collapsible 集成沿（TimelineItem 未接 OpenChanged
/// headless 下直发按钮 onclick 以状态沿覆盖展开/收起，不造假）、无父级 Context 缺失沿
/// （以可空级联降级、不抛异常，锁形）、枚举 7 族声明序、默认结构快照。
/// 零专属测试，本文件照 PaginationTests 先例全自写。共 39 枚用例（≤40 预算）。
/// </summary>
public class TimelineTests
{
    private static int Count(string haystack, string needle) => haystack.Split(needle).Length - 1;

    private static ComponentTestRenderer CreateRenderer()
    {
        // Timeline 注入 ILocalizer（ol 的 aria-label）；Collapsible 触发器经 [Inject]
        // 解析 IServiceProvider（照 CollapsibleTests 先例的最小组合）。
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(new NoopJavaScript());
        services.AddSingleton<ILocalizer, DefaultLocalizer>();
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

    /// <summary>按参数元组构造一个 TimelineItem（子内容也经参数面传入，避免命名/位置混排）。</summary>
    private static RenderFragment Item(params (string Name, object Value)[] parameters) =>
        El<TimelineItem>(null, parameters);

    private static RenderFragment Text(string content) => builder => builder.AddContent(0, content);

    private static RenderFragment Seq(params RenderFragment[] parts) => builder =>
    {
        foreach (var part in parts)
        {
            part(builder);
        }
    };

    /// <summary>一个携带图标列+连接线的默认条目（子内容为纯文本，不走简写树）。</summary>
    private static RenderFragment PlainItem(string label) =>
        Item(("ChildContent", (RenderFragment)Text(label)));

    private static async Task<ComponentTestRenderer> MountTimelineAsync(Dictionary<string, object?> parameters)
    {
        var renderer = CreateRenderer();
        await renderer.MountAsync<Timeline>(parameters);
        await renderer.Dispatcher.InvokeAsync(() => { });
        return renderer;
    }

    private static Task ClickAsync(ComponentTestRenderer renderer, int occurrence = 0) =>
        renderer.Dispatcher.InvokeAsync(() => renderer.DispatchAsync("onclick", new MouseEventArgs(), occurrence));

    // ------------------------------------------------------------------ 容器与空态沿

    [Fact]
    public async Task 无子内容应渲染语义列表与空态兜底且空片段绕过空态怪癖()
    {
        var renderer = await MountTimelineAsync(new Dictionary<string, object?>
        {
            ["data-testid"] = "tl-empty",
        });
        var markup = renderer.Markup();

        Assert.Contains("<ol", markup);
        Assert.Contains("aria-label=\"Timeline\"", markup);
        Assert.Contains("No timeline items to display", markup);
        Assert.Contains("atom:p-8", markup); // TimelineEmpty 容器
        Assert.Contains("data-testid=\"tl-empty\"", markup);
        Assert.Equal(0, Count(markup, "<li"));

        // 怪癖：ChildContent 传入非 null 的空片段时 @if (ChildContent != null) 为真
        // 空态组件被绕过——什么都不渲染。
        var bypass = await MountTimelineAsync(new Dictionary<string, object?>
        {
            ["ChildContent"] = (RenderFragment)(_ => { }),
        });
        Assert.DoesNotContain("No timeline items to display", bypass.Markup());
    }

    [Theory]
    [InlineData(TimelineSize.Small, "atom:gap-2")]
    [InlineData(TimelineSize.Medium, "atom:gap-4")]
    [InlineData(TimelineSize.Large, "atom:gap-6")]
    public async Task 容器尺寸三档应给出对应间距类串(TimelineSize size, string expected)
    {
        var renderer = await MountTimelineAsync(new Dictionary<string, object?>
        {
            ["Size"] = size,
        });

        Assert.Contains(expected, renderer.Markup());
    }

    // ------------------------------------------------------------------ 条目数据驱动渲染沿

    [Fact]
    public async Task 多条目默认各带连接线且关闭开关与尺寸间隙及自定义类各沿正确()
    {
        var renderer = await MountTimelineAsync(new Dictionary<string, object?>
        {
            ["ChildContent"] = Seq(PlainItem("一"), PlainItem("二")),
        });
        var markup = renderer.Markup();

        Assert.Equal(2, Count(markup, "<li"));
        Assert.Equal(2, Count(markup, "atom:w-0.5"));  // Connector 默认 ShowConnector=true
        Assert.Equal(2, Count(markup, "atom:ring-8")); // Icon 默认 Spaced 外环
        Assert.Contains("一", markup);
        Assert.Contains("二", markup);

        // ShowConnector=false：连接线整条省略。
        var noConnector = await MountTimelineAsync(new Dictionary<string, object?>
        {
            ["ChildContent"] = Item(("ChildContent", (RenderFragment)Text("条")), ("ShowConnector", false)),
        });
        Assert.DoesNotContain("atom:w-0.5", noConnector.Markup());

        // 父级 Size=Large → 间隙档 -mb-6；ConnectorClass 追加在 cn 尾部。
        var gap = await MountTimelineAsync(new Dictionary<string, object?>
        {
            ["Size"] = TimelineSize.Large,
            ["ChildContent"] = Item(("ChildContent", (RenderFragment)Text("条")), ("ConnectorClass", "x-conn")),
        });
        // cn("atom:flex-1 atom:min-h-16", "atom:-mb-6", "x-conn")——三者无冲突，按序拼接。
        Assert.Contains("atom:flex-1 atom:min-h-16 atom:-mb-6 x-conn", gap.Markup());
    }

    [Theory]
    [InlineData(TimelineAlign.Left, "atom:grid-cols-[auto_1fr]")]
    [InlineData(TimelineAlign.Right, "atom:grid-cols-[1fr_auto]")]
    [InlineData(TimelineAlign.Center, "atom:grid-cols-[1fr_auto_1fr]")]
    public async Task 对齐三档应切换内容网格列定义(TimelineAlign align, string expected)
    {
        var renderer = await MountTimelineAsync(new Dictionary<string, object?>
        {
            ["Align"] = align,
            ["ChildContent"] = PlainItem("条"),
        });

        Assert.Contains(expected, renderer.Markup());
    }

    [Fact]
    public async Task Alternate布局应仅奇数索引条目右置且注册序号沿父级重置()
    {
        var items = Seq(PlainItem("零"), PlainItem("一"), PlainItem("二"));
        var renderer = await MountTimelineAsync(new Dictionary<string, object?>
        {
            ["Align"] = TimelineAlign.Alternate,
            ["ChildContent"] = items,
        });

        // RegisterItem 依初始化顺序取 0/1/2，仅 _itemIndex % 2 != 0 的第二项走反序分支。
        Assert.Equal(1, Count(renderer.Markup(), "data-timeline-end"));
        Assert.Equal(1, Count(renderer.Markup(), "atom:justify-items-end"));

        // 重渲染后 OnParametersSet 把计数器清零，索引不漂移（三条目仍只反序一枚）。
        await renderer.RerenderRootAsync(0, new Dictionary<string, object?>
        {
            ["Align"] = TimelineAlign.Alternate,
            ["ChildContent"] = items,
        });
        Assert.Equal(1, Count(renderer.Markup(), "data-timeline-end"));
    }

    [Fact]
    public async Task InProgress条目网格应携带当前步标记其余状态不带()
    {
        var active = await MountTimelineAsync(new Dictionary<string, object?>
        {
            ["ChildContent"] = Item(
                ("ChildContent", (RenderFragment)Text("进行中")),
                ("Status", TimelineStatus.InProgress)),
        });
        Assert.Contains("aria-current-step", active.Markup());

        var done = await MountTimelineAsync(new Dictionary<string, object?>
        {
            ["ChildContent"] = PlainItem("完成"),
        });
        Assert.DoesNotContain("aria-current-step", done.Markup());
    }

    // ------------------------------------------------------------------ 简写树与自定义替换沿

    [Fact]
    public async Task 仅提供Title应自动渲染标题时间描述全树()
    {
        var renderer = await MountTimelineAsync(new Dictionary<string, object?>
        {
            ["ChildContent"] = Item(
                ("Title", "发布 v1.0"),
                ("Time", "Jan 2024"),
                ("Description", "首个版本")),
        });
        var markup = renderer.Markup();

        Assert.Contains("<h3 class=\"atom:font-semibold", markup);
        Assert.Contains("发布 v1.0", markup);
        Assert.Contains("<time", markup);
        Assert.Contains("Jan 2024", markup);
        Assert.Contains("atom:text-end atom:pe-4", markup); // 简写时间在日期列的类串
        Assert.Contains("<p class=\"atom:max-w-sm", markup);
        Assert.Contains("首个版本", markup);
    }

    [Fact]
    public async Task 简写在无描述时省略描述段且带ChildContent时整体不触发怪癖锁存()
    {
        var noDesc = await MountTimelineAsync(new Dictionary<string, object?>
        {
            ["ChildContent"] = Item(("Title", "只有标题")),
        });
        Assert.Contains("<h3", noDesc.Markup());
        Assert.DoesNotContain("<p class=\"atom:max-w-sm", noDesc.Markup());
        Assert.DoesNotContain("<time", noDesc.Markup()); // Time 为空则日期列只剩空容器

        // UseShorthand => ChildContent == null && Title != null：两者同给时子内容优先
        // 标题文本不渲染。
        var withBody = await MountTimelineAsync(new Dictionary<string, object?>
        {
            ["ChildContent"] = Item(
                ("ChildContent", (RenderFragment)Text("自定义正文")),
                ("Title", "被忽略的标题")),
        });
        Assert.Contains("自定义正文", withBody.Markup());
        Assert.DoesNotContain("<h3", withBody.Markup());
    }

    [Fact]
    public async Task IconContent与TimeContent应各自替换默认图标与默认时间列()
    {
        var renderer = await MountTimelineAsync(new Dictionary<string, object?>
        {
            ["ChildContent"] = Item(
                ("ChildContent", (RenderFragment)Text("正文")),
                ("Title", "标题"),
                ("Time", "Feb 2024"),
                ("IconContent", (RenderFragment)Text("#图标#")),
                ("TimeContent", (RenderFragment)Text("#日期#"))),
        });
        var markup = renderer.Markup();

        Assert.Contains("#图标#", markup);
        Assert.Equal(0, Count(markup, "atom:ring-8")); // 默认 TimelineIcon 未渲染
        Assert.Contains("#日期#", markup);
        Assert.DoesNotContain("<time", markup);       // 默认 TimelineTime 未渲染
        Assert.DoesNotContain("atom:text-end atom:pe-4", markup);
    }

    // ------------------------------------------------------------------ Icon 状态机与配色映射

    [Theory]
    [InlineData(TimelineColor.Primary, "atom:bg-primary atom:text-primary-foreground")]
    [InlineData(TimelineColor.Secondary, "atom:bg-secondary atom:text-secondary-foreground")]
    [InlineData(TimelineColor.Muted, "atom:bg-muted atom:text-muted-foreground")]
    [InlineData(TimelineColor.Accent, "atom:bg-accent atom:text-accent-foreground")]
    [InlineData(TimelineColor.Destructive, "atom:bg-destructive atom:text-destructive-foreground")]
    public async Task 实心变体应逐色给出底色与前景色类串(TimelineColor color, string expected)
    {
        var renderer = CreateRenderer();
        await renderer.MountAsync<TimelineIcon>(new Dictionary<string, object?> { ["Color"] = color });

        Assert.Contains(expected, renderer.Markup());
    }

    [Theory]
    [InlineData(TimelineColor.Primary, "atom:bg-background atom:border-2 atom:border-primary atom:text-primary")]
    [InlineData(TimelineColor.Secondary, "atom:bg-background atom:border-2 atom:border-secondary atom:text-secondary")]
    [InlineData(TimelineColor.Muted, "atom:bg-background atom:border-2 atom:border-muted atom:text-muted-foreground")]
    [InlineData(TimelineColor.Accent, "atom:bg-background atom:border-2 atom:border-accent atom:text-accent")]
    [InlineData(TimelineColor.Destructive, "atom:bg-background atom:border-2 atom:border-destructive atom:text-destructive")]
    public async Task 描边变体应逐色给出边框与文字色类串(TimelineColor color, string expected)
    {
        var renderer = CreateRenderer();
        await renderer.MountAsync<TimelineIcon>(new Dictionary<string, object?>
        {
            ["Color"] = color,
            ["Variant"] = TimelineIconVariant.Outline,
        });

        Assert.Contains(expected, renderer.Markup());
    }

    [Fact]
    public async Task Pending状态应把显式配色降级且大尺寸图标逐档放大并叠加脉动()
    {
        var muted = CreateRenderer();
        await muted.MountAsync<TimelineIcon>(new Dictionary<string, object?>
        {
            ["Color"] = TimelineColor.Accent,
            ["Status"] = TimelineStatus.Pending,
        });
        var markup = muted.Markup();

        // EffectiveColor：Status==Pending 时无视 Color=Accent，取 Muted。
        Assert.Contains("atom:bg-muted atom:text-muted-foreground", markup);
        Assert.DoesNotContain("atom:bg-accent", markup);

        var large = CreateRenderer();
        await large.MountAsync<TimelineIcon>(new Dictionary<string, object?>
        {
            ["Size"] = TimelineSize.Large,
            ["Loading"] = true,
        });
        var big = large.Markup();

        Assert.Contains("atom:h-12 atom:w-12", big); // 外圈
        Assert.Contains("atom:h-6 atom:w-6", big);   // 内点 IconSizeClass
        Assert.Contains("atom:animate-pulse", big);
    }

    [Fact]
    public async Task 贴合方式应经父级级联控制图标外环有无()
    {
        var spaced = await MountTimelineAsync(new Dictionary<string, object?>
        {
            ["ChildContent"] = PlainItem("条"),
        });
        Assert.Equal(1, Count(spaced.Markup(), "atom:ring-8"));

        var connected = await MountTimelineAsync(new Dictionary<string, object?>
        {
            ["ConnectorFit"] = TimelineConnectorFit.Connected,
            ["ChildContent"] = PlainItem("条"),
        });
        Assert.Equal(0, Count(connected.Markup(), "atom:ring-8"));
    }

    // ------------------------------------------------------------------ Connector 状态机沿

    [Theory]
    [InlineData(TimelineConnectorStyle.Solid, "atom:w-0.5 atom:bg-primary")]
    [InlineData(TimelineConnectorStyle.Dashed, "atom:w-0.5 atom:border-s-2 atom:border-dashed atom:border-primary")]
    [InlineData(TimelineConnectorStyle.Dotted, "atom:w-0.5 atom:border-s-2 atom:border-dotted atom:border-primary")]
    public async Task 连接线三样式在缺省完成态应给出实心或描边类串(TimelineConnectorStyle style, string expected)
    {
        var renderer = CreateRenderer();
        await renderer.MountAsync<TimelineConnector>(new Dictionary<string, object?> { ["ConnectorStyle"] = style });

        Assert.Contains(expected, renderer.Markup());
    }

    [Theory]
    [InlineData(TimelineStatus.InProgress, "atom:bg-linear-to-b atom:from-primary atom:to-muted")]
    [InlineData(TimelineStatus.Pending, "atom:w-0.5 atom:bg-muted")]
    public async Task 实心连接线无显式色时应随状态切换配色(TimelineStatus status, string expected)
    {
        var renderer = CreateRenderer();
        await renderer.MountAsync<TimelineConnector>(new Dictionary<string, object?> { ["Status"] = status });

        Assert.Contains(expected, renderer.Markup());
    }

    [Theory]
    [InlineData(TimelineConnectorStyle.Solid, TimelineColor.Accent, "atom:bg-accent")]
    [InlineData(TimelineConnectorStyle.Dotted, TimelineColor.Destructive, "atom:border-destructive")]
    public async Task 显式连接线配色应优先于状态配色(TimelineConnectorStyle style, TimelineColor color, string expected)
    {
        var renderer = CreateRenderer();
        await renderer.MountAsync<TimelineConnector>(new Dictionary<string, object?>
        {
            ["ConnectorStyle"] = style,
            ["Color"] = color,
            ["Status"] = TimelineStatus.Pending, // 状态配色为 muted，应被覆盖
        });
        var markup = renderer.Markup();

        Assert.Contains(expected, markup);
        Assert.DoesNotContain("atom:bg-muted", markup);
        Assert.DoesNotContain("atom:border-muted", markup);
    }

    // ------------------------------------------------------------------ Collapsible 集成沿

    [Fact]
    public async Task 可折叠条目应内嵌触发按钮并按onclick切换展开收起状态()
    {
        var renderer = await MountTimelineAsync(new Dictionary<string, object?>
        {
            ["ChildContent"] = Item(
                ("ChildContent", (RenderFragment)Text("摘要")),
                ("IsCollapsible", true),
                ("DetailContent", (RenderFragment)Text("细节"))),
        });
        var open = renderer.Markup();

        // DefaultOpen 默认 true：初始即展开。
        Assert.Contains("<button", open);
        Assert.Contains("data-state=\"open\"", open);
        Assert.Equal(1, Count(open, "<svg")); // LucideIcon chevron-down
        Assert.Contains("摘要", open);
        Assert.Contains("细节", open);

        // 点击触发按钮收起；再点恢复展开。上报沿说明： TimelineItem 只传 DefaultOpen
        // 未接 Collapsible.OpenChanged，故 headless 下以状态沿覆盖（不造假直读回调）。
        await ClickAsync(renderer);
        var closed = renderer.Markup();
        Assert.Contains("data-state=\"closed\"", closed);
        Assert.Contains("aria-hidden=\"true\"", closed);

        await ClickAsync(renderer);
        Assert.Contains("data-state=\"open\"", renderer.Markup());
    }

    [Fact]
    public async Task 可折叠为真但缺细节内容时不渲染触发按钮怪癖锁存()
    {
        // IsCollapsible=true 但 DetailContent==null：条件 `IsCollapsible && DetailContent != null`
        // 为假，落回普通子内容分支——可折叠开关单独无效。
        var noDetail = await MountTimelineAsync(new Dictionary<string, object?>
        {
            ["ChildContent"] = Item(
                ("ChildContent", (RenderFragment)Text("正文")),
                ("IsCollapsible", true)),
        });
        Assert.DoesNotContain("<button", noDetail.Markup());
        Assert.Contains("正文", noDetail.Markup());
    }

    // ------------------------------------------------------------------ 子组件结构沿与 Context 缺失沿

    [Fact]
    public async Task 标题件应可换标签并透传捕获属性()
    {
        var renderer = CreateRenderer();
        await renderer.MountAsync<TimelineTitle>(new Dictionary<string, object?>
        {
            ["As"] = "h4",
            ["Class"] = "x-title",
            ["data-testid"] = "ttl",
            ["ChildContent"] = Text("标题"),
        });
        var markup = renderer.Markup();

        Assert.Contains("<h4", markup);
        Assert.Contains("x-title", markup);
        Assert.Contains("data-testid=\"ttl\"", markup);
    }

    [Fact]
    public async Task 内容家族各件应各自渲染语义容器且空态件自定义优先()
    {
        var renderer = CreateRenderer();
        await renderer.MountAsync<TimelineContent>(new Dictionary<string, object?>
        {
            ["ChildContent"] = El<TimelineHeader>(
                child: El<TimelineTime>(child: Text("Mar 2024"))),
        });
        var markup = renderer.Markup();

        Assert.Contains("atom:flex atom:flex-col atom:gap-2 atom:ps-2", markup); // Content
        Assert.Contains("atom:flex atom:items-center atom:gap-4", markup);       // Header
        Assert.Contains("<time", markup);
        Assert.Contains("Mar 2024", markup);

        var description = CreateRenderer();
        await description.MountAsync<TimelineDescription>(new Dictionary<string, object?>
        {
            ["ChildContent"] = Text("一段描述"),
        });
        Assert.Contains("<p class=\"atom:max-w-sm atom:text-sm atom:text-muted-foreground\">", description.Markup());

        var empty = CreateRenderer();
        await empty.MountAsync<TimelineEmpty>(new Dictionary<string, object?>
        {
            ["ChildContent"] = Text("自定义空态"),
        });
        Assert.Contains("自定义空态", empty.Markup());
        Assert.DoesNotContain("No timeline items to display", empty.Markup());
    }

    [Fact]
    public async Task 无父级时间线的条目不应抛异常并按默认级联值降级渲染()
    {
        // Context 缺失沿：TimelineItem/TimelineIcon 的 ParentTimeline 为可空级联参数
        // 缺父级时不抛异常——Align 落 Center、索引落 0、连接线间隙按 Medium 档。
        var renderer = CreateRenderer();
        await renderer.MountAsync<TimelineItem>(new Dictionary<string, object?>
        {
            ["ChildContent"] = Text("孤立条目"),
        });
        var markup = renderer.Markup();

        Assert.Contains("atom:grid-cols-[1fr_auto_1fr]", markup);
        Assert.Contains("atom:flex-1 atom:min-h-16 atom:-mb-4", markup); // Medium 间隙档
    }

    // ------------------------------------------------------------------ 枚举面

    [Fact]
    public void 七族枚举成员应按声明序取值()
    {
        Assert.Equal(0, (int)TimelineAlign.Center);
        Assert.Equal(3, (int)TimelineAlign.Alternate);
        Assert.Equal(0, (int)TimelineColor.Primary);
        Assert.Equal(4, (int)TimelineColor.Destructive);
        Assert.Equal(0, (int)TimelineConnectorFit.Spaced);
        Assert.Equal(1, (int)TimelineConnectorFit.Connected);
        Assert.Equal(0, (int)TimelineConnectorStyle.Solid);
        Assert.Equal(2, (int)TimelineConnectorStyle.Dotted);
        Assert.Equal(0, (int)TimelineIconVariant.Solid);
        Assert.Equal(1, (int)TimelineIconVariant.Outline);
        Assert.Equal(0, (int)TimelineSize.Small);
        Assert.Equal(2, (int)TimelineSize.Large);
        Assert.Equal(0, (int)TimelineStatus.Completed);
        Assert.Equal(2, (int)TimelineStatus.Pending);
    }

    // ------------------------------------------------------------------ 快照

    [Fact]
    public async Task 默认结构快照应与基线一致()
    {
        var renderer = await MountTimelineAsync(new Dictionary<string, object?>
        {
            ["ChildContent"] = Seq(
                Item(
                    ("Title", "发布 v1.0"),
                    ("Time", "Jan 2024"),
                    ("Description", "首个版本")),
                PlainItem("自定义条目正文")),
        });

        // 渲染面为静态类串与文本，无实例 id（本家族无 Guid/UniqueId 生成点，实证核对）
        // id 归一照 T 族先例为恒等变换。
        await Verify(renderer.Markup());
    }
}
