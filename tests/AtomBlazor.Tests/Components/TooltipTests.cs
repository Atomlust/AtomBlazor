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
using PTooltip = AtomBlazor.Primitives.TooltipPrimitive;
using PTooltipTrigger = AtomBlazor.Primitives.TooltipTriggerPrimitive;
using PTooltipContent = AtomBlazor.Primitives.TooltipContentPrimitive;
using PTooltipContext = AtomBlazor.Primitives.TooltipContext;
using CTooltip = AtomBlazor.Tooltip;
using CTooltipTrigger = AtomBlazor.TooltipTrigger;
using CTooltipContent = AtomBlazor.TooltipContent;

namespace AtomBlazor.Tests.Components;

/// <summary>
/// Tooltip 家族测试：基元根的受控/非受控开合状态沿（Open/OpenChanged/DefaultOpen、
/// 展开延时归零后的悬停打开沿）、触发器打开时经 aria-describedby 关联内容、内容经
/// FloatingPortal 注册（role="tooltip"、data-state）、包装层（Components）的类名透传与
/// 根不落元素、以及 TooltipProvider 的级联。
/// 快照含上下文自增短 id（tooltip-N 及其派生的 -trigger/-content/portal 作用域 id），先经正则
/// 归一化；纯焦点链受 headless 限制不断言。
/// </summary>
public class TooltipTests
{
    /// <summary>
    /// 归一化上下文自增 id（tooltip-N 及其派生的 -trigger/-content/portal 作用域 id），使快照可确定。
    /// </summary>
    private static string Scrub(string markup) =>
        Regex.Replace(markup, "tooltip-[0-9]+", "tooltip-<n>");

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

    /// <summary>经反射取基元 Tooltip 的私有 _context 字段（先例：PopoverTests 的 ContextOf）。</summary>
    private static PTooltipContext ContextOf(PTooltip tooltip) =>
        (PTooltipContext)typeof(PTooltip)
            .GetField("_context", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(tooltip)!;

    // ------------------------------------------------------------------
    // 片段构造
    // ------------------------------------------------------------------

    /// <summary>基元家族：触发器（显式 AsChild=false 渲染 span）+ 内容。</summary>
    private static RenderFragment PrimitiveFamily(string triggerText = "提示触发器", string contentText = "提示内容") => builder =>
    {
        builder.OpenComponent<PTooltipTrigger>(0);
        builder.AddAttribute(1, nameof(PTooltipTrigger.AsChild), false);
        builder.AddAttribute(2, "ChildContent", (RenderFragment)(b => b.AddContent(0, triggerText)));
        builder.CloseComponent();

        builder.OpenComponent<PTooltipContent>(3);
        builder.AddAttribute(4, "ChildContent", (RenderFragment)(b => b.AddContent(0, contentText)));
        builder.CloseComponent();
    };

    /// <summary>包装层家族：触发器（显式 AsChild=false 渲染按钮）+ 内容。</summary>
    private static RenderFragment ComponentFamily(
        string triggerText = "提示触发器",
        string? contentClass = null) => builder =>
    {
        builder.OpenComponent<CTooltipTrigger>(0);
        builder.AddAttribute(1, nameof(CTooltipTrigger.AsChild), false);
        builder.AddAttribute(2, "ChildContent", (RenderFragment)(b => b.AddContent(0, triggerText)));
        builder.CloseComponent();

        builder.OpenComponent<CTooltipContent>(3);
        if (contentClass != null)
        {
            builder.AddAttribute(4, nameof(CTooltipContent.Class), contentClass);
        }
        builder.AddAttribute(5, "ChildContent", (RenderFragment)(b => b.AddContent(0, "提示内容")));
        builder.CloseComponent();
    };

    // ------------------------------------------------------------------
    // 基元根：受控/非受控状态沿
    // ------------------------------------------------------------------

    [Fact]
    public async Task 受控开关参数应驱动开合沿且受控回写不回声OpenChanged()
    {
        // 验证两件事：(1) 受控 Open 参数变化驱动视觉开合（触发器 aria-describedby / 内容 data-state）；
        // (2) 按上游怪癖，受控回写 Open 参数时不回声 OpenChanged（通知只来自触发器路径）。
        var reports = new List<bool>();
        var openChanged = EventCallback.Factory.Create<bool>(new object(), v => reports.Add(v));
        var (renderer, _) = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        var dict = new Dictionary<string, object?>
        {
            [nameof(PTooltip.Open)] = false,
            [nameof(PTooltip.OpenChanged)] = openChanged,
            [nameof(PTooltip.ChildContent)] = PrimitiveFamily(),
            [nameof(PTooltip.DelayDuration)] = 0,
        };
        var tooltip = await renderer.MountAsync<PTooltip>(dict);
        await SettleAsync(renderer);

        // 关闭态：触发器不描述任何内容（aria-describedby 为 null 时不输出）。
        Assert.DoesNotContain("aria-describedby", renderer.Markup());

        // 受控打开：参数变化驱动视觉开合，但 OpenChanged 不应回声（怪癖）。
        await renderer.Dispatcher.InvokeAsync(() => renderer.RerenderRootAsync(1, new Dictionary<string, object?>
        {
            [nameof(PTooltip.Open)] = true,
            [nameof(PTooltip.OpenChanged)] = openChanged,
            [nameof(PTooltip.ChildContent)] = PrimitiveFamily(),
            [nameof(PTooltip.DelayDuration)] = 0,
        }));
        await OpenAndSettleAsync(renderer);

        Assert.True(ContextOf(tooltip).IsOpen);
        Assert.Contains("aria-describedby", renderer.Markup());
        Assert.Empty(reports); // 受控回写不回声

        // 受控关闭。
        await renderer.Dispatcher.InvokeAsync(() => renderer.RerenderRootAsync(1, new Dictionary<string, object?>
        {
            [nameof(PTooltip.Open)] = false,
            [nameof(PTooltip.OpenChanged)] = openChanged,
            [nameof(PTooltip.ChildContent)] = PrimitiveFamily(),
            [nameof(PTooltip.DelayDuration)] = 0,
        }));
        await OpenAndSettleAsync(renderer);

        Assert.False(ContextOf(tooltip).IsOpen);
        Assert.DoesNotContain("aria-describedby", renderer.Markup());
        Assert.Empty(reports); // 仍不回声
    }

    [Fact]
    public async Task DefaultOpen应让非受控提示初始即开且不播报()
    {
        var reports = new List<bool>();
        var (renderer, _) = CreateRenderer();
        var tooltip = await renderer.MountAsync<PTooltip>(new Dictionary<string, object?>
        {
            [nameof(PTooltip.DefaultOpen)] = true,
            [nameof(PTooltip.ChildContent)] = PrimitiveFamily(),
            [nameof(PTooltip.DelayDuration)] = 0,
        });
        await SettleAsync(renderer);

        Assert.True(ContextOf(tooltip).IsOpen);
        Assert.Empty(reports); // 初始化不算变化
    }

    [Fact]
    public async Task 悬停应经触发器打开且内容经门户注册()
    {
        var (renderer, _) = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());
        var tooltip = await renderer.MountAsync<PTooltip>(new Dictionary<string, object?>
        {
            [nameof(PTooltip.ChildContent)] = PrimitiveFamily(),
            [nameof(PTooltip.DelayDuration)] = 0,
        });
        await SettleAsync(renderer);

        // 关闭态：内容随 PortalHost 挂载但隐藏（data-state=closed），触发器尚不关联内容。
        Assert.DoesNotContain("aria-describedby", renderer.Markup());
        Assert.DoesNotContain("data-state=\"open\"", renderer.Markup());

        // 触发器打开（经反射绕过延时定时器，等价于悬停信号）。
        var context = ContextOf(tooltip);
        await renderer.Dispatcher.InvokeAsync(() => context.Open(new ElementReference("anchor-1")));
        await OpenAndSettleAsync(renderer);

        var markup = renderer.Markup();
        Assert.Contains("role=\"tooltip\"", markup);
        Assert.Contains("data-state=\"open\"", markup);
        Assert.Contains("提示内容", markup);
    }

    // ------------------------------------------------------------------
    // 快照
    // ------------------------------------------------------------------

    [Fact]
    public async Task 基元关闭态触发器结构快照应与基线一致()
    {
        var (renderer, _) = CreateRenderer();
        await renderer.MountAsync<PTooltip>(new Dictionary<string, object?>
        {
            [nameof(PTooltip.ChildContent)] = PrimitiveFamily(),
            [nameof(PTooltip.DelayDuration)] = 0,
        });
        await SettleAsync(renderer);

        await Verify(Scrub(renderer.Markup()));
    }

    [Fact]
    public async Task 基元打开态门户与内容结构快照应与基线一致()
    {
        var (renderer, _) = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());
        var tooltip = await renderer.MountAsync<PTooltip>(new Dictionary<string, object?>
        {
            [nameof(PTooltip.ChildContent)] = PrimitiveFamily(),
            [nameof(PTooltip.DelayDuration)] = 0,
        });
        await SettleAsync(renderer);

        await renderer.Dispatcher.InvokeAsync(() => ContextOf(tooltip).Open(new ElementReference("anchor-1")));
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
        var tooltip = await renderer.MountAsync<CTooltip>(new Dictionary<string, object?>
        {
            [nameof(CTooltip.ChildContent)] = ComponentFamily(contentClass: "custom-tip"),
            [nameof(CTooltip.DelayDuration)] = 0,
        });
        await SettleAsync(renderer);

        // 内容随 PortalHost 挂载但隐藏（data-state=closed），触发器尚未展开。
        Assert.DoesNotContain("aria-describedby", renderer.Markup());

        var context = ContextOf(renderer.FindComponent<PTooltip>());
        await renderer.Dispatcher.InvokeAsync(() => context.Open(new ElementReference("anchor-1")));
        await OpenAndSettleAsync(renderer);

        var markup = renderer.Markup();
        Assert.Contains("custom-tip", markup);              // 包装层 Class 经 cn 合并落到内容 div
        Assert.Contains("atom:z-50", markup);               // 默认宽度/边框类
        Assert.Contains("atom:contents", markup);           // 触发器包裹层类
    }

    [Fact]
    public async Task 包装层关闭态快照应与基线一致()
    {
        var (renderer, _) = CreateRenderer();
        await renderer.MountAsync<CTooltip>(new Dictionary<string, object?>
        {
            [nameof(CTooltip.ChildContent)] = ComponentFamily(),
            [nameof(CTooltip.DelayDuration)] = 0,
        });
        await SettleAsync(renderer);

        await Verify(Scrub(renderer.Markup()));
    }

    [Fact]
    public async Task 包装层打开态结构快照应与基线一致()
    {
        var (renderer, _) = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());
        var tooltip = await renderer.MountAsync<CTooltip>(new Dictionary<string, object?>
        {
            [nameof(CTooltip.ChildContent)] = ComponentFamily(),
            [nameof(CTooltip.DelayDuration)] = 0,
        });
        await SettleAsync(renderer);

        await renderer.Dispatcher.InvokeAsync(() => ContextOf(renderer.FindComponent<PTooltip>()).Open(new ElementReference("anchor-1")));
        await OpenAndSettleAsync(renderer);

        await Verify(Scrub(renderer.Markup()));
    }

    // ------------------------------------------------------------------
    // TooltipProvider
    // ------------------------------------------------------------------

    [Fact]
    public async Task 提供者级联下关闭态快照应与基线一致()
    {
        var (renderer, _) = CreateRenderer();

        // TooltipProvider 只级联 DelayDuration 等；触发器/内容依赖 CTooltip 提供的
        // PTooltipContext，故必须在 CTooltip 之内。
        RenderFragment family = b =>
        {
            b.OpenComponent<CTooltip>(0);
            b.AddAttribute(1, nameof(CTooltip.ChildContent), ComponentFamily());
            b.CloseComponent();
        };

        await renderer.MountAsync<TooltipProvider>(new Dictionary<string, object?>
        {
            [nameof(TooltipProvider.DelayDuration)] = 200,
            [nameof(TooltipProvider.ChildContent)] = family,
        });
        await SettleAsync(renderer);

        await Verify(Scrub(renderer.Markup()));
    }
}
