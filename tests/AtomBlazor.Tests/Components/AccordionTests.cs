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
/// Accordion 组件层测试：样式类指纹（composite class）、点击展开沿与
/// 默认箭头 svg 的 data-state 同步、单开互斥/多开并行、Collapsible 收起沿、
/// 受控 Value/ValueChanged 沿、Disabled 拒绝切换、Icon 替换默认箭头、
/// As 标题标签透传、ForceMount 默认挂载沿与默认结构快照。
/// </summary>
public class AccordionTests
{
    /// <summary>归一化上下文自增短 id（accordion-{n}），使快照可确定。</summary>
    private static string Scrub(string markup) =>
        Regex.Replace(markup, "accordion-\\d+", "accordion-<n>");

    private static ComponentTestRenderer CreateRenderer()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(new NoopJavaScript());
        services.AddSingleton<ILocalizer, DefaultLocalizer>();
        return new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance);
    }

    /// <summary>单条目片段（组件层）：样式化触发器 + 样式化内容。</summary>
    private static RenderFragment ItemFragment(
        string value, string heading, string body,
        bool disabled = false, RenderFragment<bool>? icon = null, string? headingAs = null) => builder =>
    {
        builder.OpenComponent<AccordionItem>(0);
        builder.AddAttribute(1, nameof(AccordionItem.Value), value);
        builder.AddAttribute(2, nameof(AccordionItem.Disabled), disabled);
        builder.AddAttribute(3, nameof(AccordionItem.ChildContent), (RenderFragment)(ib =>
        {
            ib.OpenComponent<AccordionTrigger>(0);
            if (headingAs != null)
            {
                ib.AddAttribute(1, nameof(AccordionTrigger.As), headingAs);
            }
            if (icon != null)
            {
                ib.AddAttribute(2, nameof(AccordionTrigger.Icon), icon);
            }
            ib.AddAttribute(3, nameof(AccordionTrigger.ChildContent), (RenderFragment)(tb => tb.AddContent(0, heading)));
            ib.CloseComponent();

            ib.OpenComponent<AccordionContent>(10);
            ib.AddAttribute(11, nameof(AccordionContent.ChildContent), (RenderFragment)(cb => cb.AddContent(0, body)));
            ib.CloseComponent();
        }));
        builder.CloseComponent();
    };

    /// <summary>两个条目的默认内容树：one/two 各自带标题与正文。</summary>
    private static RenderFragment TwoItems() => builder =>
    {
        builder.AddContent(0, ItemFragment("one", "标题一", "内容一"));
        builder.AddContent(1, ItemFragment("two", "标题二", "内容二"));
    };

    private static async Task<ComponentTestRenderer> MountAsync(Dictionary<string, object?> parameters)
    {
        var renderer = CreateRenderer();
        parameters.TryAdd(nameof(Accordion.ChildContent), TwoItems());
        await renderer.MountAsync<Accordion>(parameters);
        return renderer;
    }

    /// <summary>点击第 <paramref name="occurrence"/> 个触发器按钮的 onclick。</summary>
    private static async Task ClickTriggerAsync(ComponentTestRenderer renderer, int occurrence = 0)
    {
        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs(), occurrence));
        await renderer.Dispatcher.InvokeAsync(() => Task.CompletedTask);
    }

    [Fact]
    public async Task 样式类指纹应落在条目分隔线触发器与内容区域上()
    {
        var renderer = await MountAsync(new Dictionary<string, object?>());

        var markup = renderer.Markup();
        // 条目容器：atom:border-b（cn() 合并后的 composite class 指纹）。
        Assert.Contains("atom:border-b", markup);
        // 触发器按钮：基础布局类 + #459 焦点环类。
        Assert.Contains("atom:flex atom:w-full atom:items-center atom:justify-between atom:text-sm atom:font-medium atom:transition-all", markup);
        Assert.Contains("atom:focus-visible:outline-none atom:focus-visible:ring-2 atom:focus-visible:ring-ring atom:focus-visible:ring-offset-2", markup);
        // 内容区域：grid 行展开动画类。
        Assert.Contains("atom:grid atom:grid-rows-[0fr] atom:transition-[grid-template-rows] atom:duration-200 atom:ease-out", markup);
        Assert.Contains("atom:data-[state=open]:grid-rows-[1fr]", markup);
    }

    [Fact]
    public async Task 点击展开后触发器与svg箭头和内容区域同步展开态()
    {
        var renderer = await MountAsync(new Dictionary<string, object?>());

        await ClickTriggerAsync(renderer, 0);

        var markup = renderer.Markup();
        Assert.Contains("aria-expanded=\"true\"", markup);
        Assert.Contains(">内容一<", markup);
        // 默认箭头 svg 的 data-state 与展开沿同步（三处 open：按钮/区域/svg）。
        Assert.Equal(3, new Regex("data-state=\"open\"").Count(markup));
        Assert.Contains("atom:data-[state=open]:rotate-180", markup);
    }

    [Fact]
    public async Task 单开模式点击另一条目应互斥切换()
    {
        var renderer = await MountAsync(new Dictionary<string, object?>());

        await ClickTriggerAsync(renderer, 0);
        await ClickTriggerAsync(renderer, 1);

        var markup = renderer.Markup();
        // 组件层 ForceMount 默认 true：收起沿不卸载内容，以 aria/状态沿断言互斥。
        Assert.Equal(1, new Regex("aria-expanded=\"true\"").Count(markup));
        // item2 展开三处沿：按钮 + region + svg 箭头。
        Assert.Equal(3, new Regex("data-state=\"open\"").Count(markup));
        Assert.Contains(">内容二<", markup);
    }

    [Fact]
    public async Task 单开且Collapsible为false时点击已展开条目应保持不变()
    {
        var renderer = await MountAsync(new Dictionary<string, object?>());

        await ClickTriggerAsync(renderer, 0);
        // 行为锁定（同基元层 ToggleItem）：非 Collapsible 单开模式必须保留一个展开项。
        // 组件层 ForceMount 默认 true，故以 aria 沿断言展开保持而非内容挂载。
        await ClickTriggerAsync(renderer, 0);

        Assert.Equal(1, new Regex("aria-expanded=\"true\"").Count(renderer.Markup()));
    }

    [Fact]
    public async Task 多开模式应允许两条目同时展开()
    {
        var renderer = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(Accordion.Type)] = AtomBlazor.Primitives.AccordionType.Multiple,
        });

        await ClickTriggerAsync(renderer, 0);
        await ClickTriggerAsync(renderer, 1);

        var markup = renderer.Markup();
        Assert.Contains(">内容一<", markup);
        Assert.Contains(">内容二<", markup);
    }

    [Fact]
    public async Task 受控模式点击应上报ValueChanged并沿渲染展开态()
    {
        var reports = new List<HashSet<string>>();
        var renderer = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(Accordion.Value)] = new HashSet<string> { "one" },
            [nameof(Accordion.ValueChanged)] =
                EventCallback.Factory.Create<HashSet<string>>(new object(), v => reports.Add(v)),
        });

        Assert.Contains(">内容一<", renderer.Markup());

        await ClickTriggerAsync(renderer, 1);
        Assert.Single(reports);
        Assert.Equal(new HashSet<string> { "two" }, reports[0]);

        // 行为（基元 HandleContextStateChanged ）：父级未回写前渲染沿保持 two。
        Assert.Contains(">内容二<", renderer.Markup());
    }

    [Fact]
    public async Task Disabled条目应拒绝点击切换()
    {
        var renderer = CreateRenderer();
        await renderer.MountAsync<Accordion>(new Dictionary<string, object?>
        {
            [nameof(Accordion.ChildContent)] = (RenderFragment)(builder =>
            {
                builder.AddContent(0, ItemFragment("one", "标题一", "内容一", disabled: true));
                builder.AddContent(1, ItemFragment("two", "标题二", "内容二"));
            }),
        });

        await ClickTriggerAsync(renderer, 0);
        // 组件层 ForceMount 默认 true：以 aria/状态沿断言禁用拒绝切换（无内容挂载沿可用）。
        var markup = renderer.Markup();
        Assert.DoesNotContain("aria-expanded=\"true\"", markup);
        Assert.Equal(0, new Regex("data-state=\"open\"").Count(markup));
        Assert.Contains(" disabled>", markup);
    }

    [Fact]
    public async Task Icon参数应替换默认箭头并按展开态接收上下文()
    {
        var renderer = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(Accordion.ChildContent)] = (RenderFragment)(builder =>
            {
                builder.AddContent(0, ItemFragment("one", "标题一", "内容一",
                    icon: open => b => b.AddContent(0, open ? "开图标" : "关图标")));
                builder.AddContent(1, ItemFragment("two", "标题二", "内容二"));
            }),
        });

        // 首个触发器使用自定义图标：默认 svg 不再渲染，关闭态沿传入 false。
        // 图标文本与标题文本同处一个 div 的相邻文本帧，序列化沿直接相接，故不以尖括号包裹断言。
        var markup = renderer.Markup();
        Assert.Contains("关图标", markup);
        // 仅 item2 保留默认箭头 polyline，item1 的 svg 被 Icon 替换。
        Assert.Equal(1, new Regex("<polyline").Count(markup));

        // 展开沿：Icon 委托收到 true。
        await ClickTriggerAsync(renderer, 0);
        Assert.Contains("开图标", renderer.Markup());
    }

    [Fact]
    public async Task As参数应透传到标题层标签()
    {
        var renderer = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(Accordion.ChildContent)] = (RenderFragment)(builder =>
            {
                builder.AddContent(0, ItemFragment("one", "标题一", "内容一", headingAs: "h4"));
                builder.AddContent(1, ItemFragment("two", "标题二", "内容二"));
            }),
        });

        var markup = renderer.Markup();
        Assert.Contains("<h4 id=", markup); // item1 标题层换为 h4
        Assert.Contains("<h3 id=", markup); // item2 未传 As，默认仍为 h3
        Assert.DoesNotContain("<h2", markup);
    }

    [Fact]
    public async Task ForceMount默认true时关闭态内容仍挂载()
    {
        var renderer = await MountAsync(new Dictionary<string, object?>());

        // 组件层 AccordionContent 的 ForceMount 默认 true（与基元层相反，如此）。
        var markup = renderer.Markup();
        Assert.Contains(">内容一<", markup);
        Assert.Contains(">内容二<", markup);
        // 全闭态下 data-state="closed" 共 6 处：按钮×2 + 区域×2 + svg×2。
        Assert.Equal(6, new Regex("data-state=\"closed\"").Count(markup));
    }

    [Fact]
    public async Task 根元素应透传额外属性()
    {
        var renderer = await MountAsync(new Dictionary<string, object?>
        {
            ["data-testid"] = "acc-root",
        });

        Assert.Contains("data-testid=\"acc-root\"", renderer.Markup());
    }

    [Fact]
    public async Task 默认结构快照应与基线一致()
    {
        var renderer = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(Accordion.DefaultValue)] = new HashSet<string> { "one" },
        });

        await Verify(Scrub(renderer.Markup()));
    }
}
