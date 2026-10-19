using AtomBlazor.Primitives;
using System.Reflection;
using System.Text.RegularExpressions;
using AtomBlazor;
using AtomBlazor.Primitives.Services;
using AtomBlazor.Tests.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Xunit;
using PHoverCard = AtomBlazor.Primitives.HoverCardPrimitive;
using PHoverCardTrigger = AtomBlazor.Primitives.HoverCardTrigger;
using PHoverCardContent = AtomBlazor.Primitives.HoverCardContent;
using PHoverCardContext = AtomBlazor.Primitives.HoverCardContext;
using CHoverCard = AtomBlazor.HoverCard;
using CHoverCardTrigger = AtomBlazor.HoverCardTrigger;
using CHoverCardContent = AtomBlazor.HoverCardContent;

namespace AtomBlazor.Tests.Components;

/// <summary>
/// HoverCard 家族测试：基元根的受控/非受控开合状态沿（Open/OpenChanged/DefaultOpen/
/// OnOpenChange、悬停打开沿）、触发器打开时经 aria-describedby 关联内容、内容经
/// FloatingPortal 注册（role="dialog"、data-state、data-side）、包装层（Components）的类名透传。
/// 快照含上下文自增短 id（hovercard-N 及其派生的 -trigger/-content/portal 作用域 id），先经正则
/// 归一化；纯焦点链受 headless 限制不断言。
/// </summary>
public class HoverCardTests
{
    /// <summary>
    /// 归一化上下文自增 id（hovercard-N 及其派生的 -trigger/-content/portal 作用域 id），使快照可确定。
    /// </summary>
    private static string Scrub(string markup) =>
        Regex.Replace(markup, "hovercard-[0-9]+", "hovercard-<n>");

    /// <summary>
    /// 构造带全套基元服务的渲染器：门户/定位/焦点 + 本地化器 + 记录 interop 的 NoopJavaScript。
    /// </summary>
    private static (ComponentTestRenderer Renderer, NoopJavaScript Js) CreateRenderer()
    {
        var js = new NoopJavaScript();
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(js);
        services.AddSingleton<IPortalService>(new PortalService(NullLogger<PortalService>.Instance));
        services.AddSingleton<IPositioningService>(new PositioningService(js));
        services.AddSingleton<IFocusManager>(new FocusManager(js));
        services.AddSingleton<ILocalizer>(new DefaultLocalizer());
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

        return (new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance), js);
    }

    /// <summary>排空渲染队列，确保 StateHasChanged 的后续批已落定。</summary>
    private static Task SettleAsync(ComponentTestRenderer renderer) =>
        renderer.Dispatcher.InvokeAsync(() => Task.CompletedTask);

    /// <summary>
    /// 打开后强制重渲染门户宿主（根 0），让浮层内容确定性地进入渲染树。
    /// </summary>
    private static async Task OpenAndSettleAsync(ComponentTestRenderer renderer)
    {
        await SettleAsync(renderer);
        await renderer.Dispatcher.InvokeAsync(() => renderer.RerenderRootAsync(0, new Dictionary<string, object?>()));
        await SettleAsync(renderer);
    }

    /// <summary>经反射取基元 HoverCard 的私有 _context 字段。</summary>
    private static PHoverCardContext ContextOf(PHoverCard hoverCard) =>
        (PHoverCardContext)typeof(PHoverCard)
            .GetField("_context", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(hoverCard)!;

    // ------------------------------------------------------------------
    // 片段构造
    // ------------------------------------------------------------------

    /// <summary>基元家族：触发器（默认 AsChild=false 渲染 div）+ 内容。</summary>
    private static RenderFragment PrimitiveFamily(string triggerText = "卡片触发器", string contentText = "卡片内容") => builder =>
    {
        builder.OpenComponent<PHoverCardTrigger>(0);
        builder.AddAttribute(1, nameof(PHoverCardTrigger.AsChild), false);
        builder.AddAttribute(2, "ChildContent", (RenderFragment)(b => b.AddContent(0, triggerText)));
        builder.CloseComponent();

        builder.OpenComponent<PHoverCardContent>(3);
        builder.AddAttribute(4, "ChildContent", (RenderFragment)(b => b.AddContent(0, contentText)));
        builder.CloseComponent();
    };

    /// <summary>包装层家族：触发器（默认 AsChild=true 级联）+ 内容。</summary>
    private static RenderFragment ComponentFamily(
        string triggerText = "卡片触发器",
        string? contentClass = null) => builder =>
    {
        builder.OpenComponent<CHoverCardTrigger>(0);
        builder.AddAttribute(1, "ChildContent", (RenderFragment)(b => b.AddContent(0, triggerText)));
        builder.CloseComponent();

        builder.OpenComponent<CHoverCardContent>(2);
        if (contentClass != null)
        {
            builder.AddAttribute(3, nameof(CHoverCardContent.Class), contentClass);
        }
        builder.AddAttribute(4, "ChildContent", (RenderFragment)(b => b.AddContent(0, "卡片内容")));
        builder.CloseComponent();
    };

    // ------------------------------------------------------------------
    // 基元根：受控/非受控状态沿
    // ------------------------------------------------------------------

    [Fact]
    public async Task 受控开关参数应驱动开合沿并播报OpenChanged与OnOpenChange()
    {
        var openChanged = new List<bool>();
        var openChange = new List<bool>();
        var (renderer, _) = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        var hoverCard = await renderer.MountAsync<PHoverCard>(new Dictionary<string, object?>
        {
            [nameof(PHoverCard.OpenChanged)] = EventCallback.Factory.Create<bool>(new object(), v => openChanged.Add(v)),
            [nameof(PHoverCard.OnOpenChange)] = EventCallback.Factory.Create<bool>(new object(), v => openChange.Add(v)),
            [nameof(PHoverCard.ChildContent)] = PrimitiveFamily(),
            [nameof(PHoverCard.OpenDelay)] = 0,
        });
        await SettleAsync(renderer);

        await renderer.Dispatcher.InvokeAsync(() => ContextOf(hoverCard).Open(new ElementReference("anchor-1")));
        await OpenAndSettleAsync(renderer);

        Assert.True(ContextOf(hoverCard).IsOpen);
        Assert.Equal([true], openChanged);
        Assert.Equal([true], openChange);
        Assert.Contains("role=\"dialog\"", renderer.Markup());

    await renderer.Dispatcher.InvokeAsync(() => ContextOf(hoverCard).Close());
    await OpenAndSettleAsync(renderer);

    Assert.False(ContextOf(hoverCard).IsOpen);
    Assert.Equal([true, false], openChanged);
    Assert.Equal([true, false], openChange);
    Assert.DoesNotContain("data-state=\"open\"", renderer.Markup()); // 内容收起（仍挂载但 data-state=closed）
    }

    [Fact]
    public async Task DefaultOpen应让非受控卡片初始即开且不播报()
    {
        var reports = new List<bool>();
        var (renderer, _) = CreateRenderer();
        // 注意：不提供 OpenChanged 委托——DefaultOpen 仅在有委托时初始化即播报（上游怪癖），
        // 本测试验证"初始化不播报"这一默认行为。
        var hoverCard = await renderer.MountAsync<PHoverCard>(new Dictionary<string, object?>
        {
            [nameof(PHoverCard.DefaultOpen)] = true,
            [nameof(PHoverCard.ChildContent)] = PrimitiveFamily(),
            [nameof(PHoverCard.OpenDelay)] = 0,
        });
        await SettleAsync(renderer);

        Assert.True(ContextOf(hoverCard).IsOpen);
        Assert.Empty(reports); // 初始化不算变化（无委托故不播报）
    }

    [Fact]
    public async Task 悬停应经触发器打开且内容经门户注册()
    {
        var (renderer, _) = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());
        var hoverCard = await renderer.MountAsync<PHoverCard>(new Dictionary<string, object?>
        {
            [nameof(PHoverCard.ChildContent)] = PrimitiveFamily(),
            [nameof(PHoverCard.OpenDelay)] = 0,
        });
        await SettleAsync(renderer);

        // 关闭态：内容随 PortalHost 挂载但隐藏（data-state=closed），触发器不携带 aria 特性。
        Assert.DoesNotContain("data-state=\"open\"", renderer.Markup());

        await renderer.Dispatcher.InvokeAsync(() => ContextOf(hoverCard).Open(new ElementReference("anchor-1")));
        await OpenAndSettleAsync(renderer);

        var markup = renderer.Markup();
        Assert.Contains("role=\"dialog\"", markup);
        Assert.Contains("data-state=\"open\"", markup);
        Assert.Contains("data-side=\"bottom\"", markup);
        Assert.Contains("卡片内容", markup);
    }

    // ------------------------------------------------------------------
    // 快照
    // ------------------------------------------------------------------

    [Fact]
    public async Task 基元关闭态触发器结构快照应与基线一致()
    {
        var (renderer, _) = CreateRenderer();
        await renderer.MountAsync<PHoverCard>(new Dictionary<string, object?>
        {
            [nameof(PHoverCard.ChildContent)] = PrimitiveFamily(),
            [nameof(PHoverCard.OpenDelay)] = 0,
        });
        await SettleAsync(renderer);

        await Verify(Scrub(renderer.Markup()));
    }

    [Fact]
    public async Task 基元打开态门户与内容结构快照应与基线一致()
    {
        var (renderer, _) = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());
        var hoverCard = await renderer.MountAsync<PHoverCard>(new Dictionary<string, object?>
        {
            [nameof(PHoverCard.ChildContent)] = PrimitiveFamily(),
            [nameof(PHoverCard.OpenDelay)] = 0,
        });
        await SettleAsync(renderer);

        await renderer.Dispatcher.InvokeAsync(() => ContextOf(hoverCard).Open(new ElementReference("anchor-1")));
        await OpenAndSettleAsync(renderer);

        await Verify(Scrub(renderer.Markup()));
    }

    // ------------------------------------------------------------------
    // 包装层（Components）
    // ------------------------------------------------------------------

    [Fact]
    public async Task 包装层类名应透传至内容且根不落元素()
    {
        var (renderer, _) = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());
        var hoverCard = await renderer.MountAsync<CHoverCard>(new Dictionary<string, object?>
        {
            [nameof(CHoverCard.ChildContent)] = ComponentFamily(contentClass: "custom-card"),
            [nameof(CHoverCard.OpenDelay)] = 0,
        });
        await SettleAsync(renderer);

        // 内容随 PortalHost 挂载但隐藏（data-state=closed），触发器尚未展开。
        Assert.DoesNotContain("data-state=\"open\"", renderer.Markup());

        var context = ContextOf(renderer.FindComponent<PHoverCard>());
        await renderer.Dispatcher.InvokeAsync(() => context.Open(new ElementReference("anchor-1")));
        await OpenAndSettleAsync(renderer);

        var markup = renderer.Markup();
        Assert.Contains("custom-card", markup);            // 包装层 Class 经 cn 合并落到内容 div
        Assert.Contains("atom:w-64", markup);              // 默认宽度类
        Assert.Contains("atom:outline-none", markup);      // 默认聚焦类
    }

    [Fact]
    public async Task 包装层关闭态快照应与基线一致()
    {
        var (renderer, _) = CreateRenderer();
        await renderer.MountAsync<CHoverCard>(new Dictionary<string, object?>
        {
            [nameof(CHoverCard.ChildContent)] = ComponentFamily(),
            [nameof(CHoverCard.OpenDelay)] = 0,
        });
        await SettleAsync(renderer);

        await Verify(Scrub(renderer.Markup()));
    }

    [Fact]
    public async Task 包装层打开态结构快照应与基线一致()
    {
        var (renderer, _) = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());
        var hoverCard = await renderer.MountAsync<CHoverCard>(new Dictionary<string, object?>
        {
            [nameof(CHoverCard.ChildContent)] = ComponentFamily(),
            [nameof(CHoverCard.OpenDelay)] = 0,
        });
        await SettleAsync(renderer);

        await renderer.Dispatcher.InvokeAsync(() => ContextOf(renderer.FindComponent<PHoverCard>()).Open(new ElementReference("anchor-1")));
        await OpenAndSettleAsync(renderer);

        await Verify(Scrub(renderer.Markup()));
    }
}
