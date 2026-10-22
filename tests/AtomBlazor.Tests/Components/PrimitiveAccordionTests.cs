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
// 双层同名家族（照 Collapsible 先例）：本文件测基元层，别名钉死到 Primitives 侧，
// 避免与 AtomBlazor.Components 同名类型（ILocalizer 所在的 using）发生 CS0104 歧义。
using PAccordion = AtomBlazor.Primitives.AccordionPrimitive;
using PAccordionContent = AtomBlazor.Primitives.AccordionContentPrimitive;
using PAccordionItem = AtomBlazor.Primitives.AccordionItemPrimitive;
using PAccordionTrigger = AtomBlazor.Primitives.AccordionTriggerPrimitive;

namespace AtomBlazor.Tests.Components;

/// <summary>
/// Accordion 基元层测试：单开/多开互斥切换、Collapsible 收起沿、受控/非受控值沿、
/// Disabled 沿、Trigger 点击展开上报、aria-expanded/data-state 属性面、
/// ForceMount 挂载沿、As 标题标签沿、Context 缺失异常沿与默认结构快照。
/// 基元层零 JS；键盘导航的焦点链在 headless 下不可见，按状态沿覆盖。
/// </summary>
public class PrimitiveAccordionTests
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

    /// <summary>单个基元条目片段：触发器标题 + 可展开内容。</summary>
    private static RenderFragment ItemFragment(string value, string heading, string body, bool disabled = false) => builder =>
    {
        builder.OpenComponent<PAccordionItem>(0);
        builder.AddAttribute(1, nameof(PAccordionItem.Value), value);
        builder.AddAttribute(2, nameof(PAccordionItem.Disabled), disabled);
        builder.AddAttribute(3, nameof(PAccordionItem.ChildContent), (RenderFragment)(ib =>
        {
            ib.OpenComponent<PAccordionTrigger>(0);
            ib.AddAttribute(1, nameof(PAccordionTrigger.ChildContent), (RenderFragment)(tb => tb.AddContent(0, heading)));
            ib.CloseComponent();

            ib.OpenComponent<PAccordionContent>(10);
            ib.AddAttribute(11, nameof(PAccordionContent.ChildContent), (RenderFragment)(cb => cb.AddContent(0, body)));
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
    public async Task 默认单开未操作时全部条目收起()
    {
        var renderer = await MountAsync(new Dictionary<string, object?>());

        var markup = renderer.Markup();
        Assert.Equal(2, new Regex("aria-expanded=\"false\"").Count(markup));
        Assert.DoesNotContain("aria-expanded=\"true\"", markup);
        Assert.DoesNotContain(">内容一<", markup);
        Assert.DoesNotContain(">内容二<", markup);
    }

    [Fact]
    public async Task 点击第一个触发器应展开对应条目并渲染region与aria关系()
    {
        var renderer = await MountAsync(new Dictionary<string, object?>());

        await ClickTriggerAsync(renderer);

        var markup = renderer.Markup();
        Assert.Contains("aria-expanded=\"true\"", markup);
        Assert.Contains("data-state=\"open\"", markup);
        Assert.Contains(">内容一<", markup);
        Assert.Contains("role=\"region\"", markup);

        // region 的 aria-labelledby 指向触发器 id，触发器 aria-controls 指回 region id。
        var triggerId = Regex.Match(markup, "id=\"(accordion-\\d+-trigger-one)\"");
        Assert.True(triggerId.Success);
        var contentId = triggerId.Groups[1].Value.Replace("trigger-one", "content-one");
        Assert.Contains($"aria-controls=\"{contentId}\"", markup);
        Assert.Contains($"aria-labelledby=\"{triggerId.Groups[1].Value}\"", markup);
    }

    [Fact]
    public async Task 单开模式点击另一条目应互斥切换()
    {
        var renderer = await MountAsync(new Dictionary<string, object?>());

        await ClickTriggerAsync(renderer, 0);
        await ClickTriggerAsync(renderer, 1);

        var markup = renderer.Markup();
        Assert.Contains("标题二", markup);
        Assert.DoesNotContain(">内容一<", markup); // item1 收起后内容卸载（ForceMount 默认 false）
        Assert.Contains(">内容二<", markup);
        Assert.Equal(1, new Regex("aria-expanded=\"true\"").Count(markup));
    }

    [Fact]
    public async Task 单开且Collapsible为false时点击已展开条目应保持不变()
    {
        var renderer = await MountAsync(new Dictionary<string, object?>());

        await ClickTriggerAsync(renderer, 0);
        // 行为：单开且 Collapsible=false 时，
        // 点击当前已展开条目既不收起也不改变集合——必须始终有一个条目保持展开。
        await ClickTriggerAsync(renderer, 0);

        var markup = renderer.Markup();
        Assert.Contains(">内容一<", markup);
        Assert.Contains("aria-expanded=\"true\"", markup);
    }

    [Fact]
    public async Task 单开且Collapsible为true时点击已展开条目应可收起()
    {
        var renderer = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(Accordion.Collapsible)] = true,
        });

        await ClickTriggerAsync(renderer, 0);
        Assert.Contains(">内容一<", renderer.Markup());

        await ClickTriggerAsync(renderer, 0);
        var markup = renderer.Markup();
        Assert.DoesNotContain(">内容一<", markup);
        Assert.DoesNotContain("aria-expanded=\"true\"", markup);
    }

    [Fact]
    public async Task 多开模式应允许多条目同时展开并可独立收起()
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
        Assert.Equal(2, new Regex("aria-expanded=\"true\"").Count(markup));

        // 独立收起 item1，item2 不受影响。
        await ClickTriggerAsync(renderer, 0);
        markup = renderer.Markup();
        Assert.DoesNotContain(">内容一<", markup);
        Assert.Contains(">内容二<", markup);
    }

    [Fact]
    public async Task DefaultValue应在非受控模式初始展开()
    {
        var renderer = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(Accordion.DefaultValue)] = new HashSet<string> { "two" },
        });

        var markup = renderer.Markup();
        Assert.Contains(">内容二<", markup);
        Assert.DoesNotContain(">内容一<", markup);
    }

    [Fact]
    public async Task 非受控点击应上报OnValueChange()
    {
        var reports = new List<HashSet<string>>();
        var renderer = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(Accordion.OnValueChange)] =
                EventCallback.Factory.Create<HashSet<string>>(new object(), v => reports.Add(v)),
        });

        await ClickTriggerAsync(renderer, 0);

        Assert.Single(reports);
        Assert.Equal(new HashSet<string> { "one" }, reports[0]);
    }

    [Fact]
    public async Task 受控模式点击应上报ValueChanged且渲染沿不被旧Value拉回()
    {
        var reports = new List<HashSet<string>>();
        var renderer = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(Accordion.Value)] = new HashSet<string> { "one" },
            [nameof(Accordion.ValueChanged)] =
                EventCallback.Factory.Create<HashSet<string>>(new object(), v => reports.Add(v)),
        });

        // 初始受控值 one 展开。
        Assert.Contains(">内容一<", renderer.Markup());

        await ClickTriggerAsync(renderer, 1);
        Assert.Single(reports);
        Assert.Equal(new HashSet<string> { "two" }, reports[0]);

        // 行为：受控模式下
        // SetValueAsync 只上报、不写回 ControlledValue，父级未跟进时上下文已翻为 two，
        // 渲染沿保持 two，不会被旧 Value=one 拉回。
        var markup = renderer.Markup();
        Assert.Contains(">内容二<", markup);
        Assert.DoesNotContain(">内容一<", markup);
    }

    [Fact]
    public async Task 受控模式父级回写新Value应沿更新展开集合并绕过单开约束()
    {
        var reports = new List<HashSet<string>>();
        var renderer = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(Accordion.Value)] = new HashSet<string> { "one" },
            [nameof(Accordion.ValueChanged)] =
                EventCallback.Factory.Create<HashSet<string>>(new object(), v => reports.Add(v)),
        });

        // 父级跟进并给出 {one,two}：OnParametersSet 直接覆写 OpenValues——
        // 行为：该沿不经过 ToggleItem，
        // 单开模式不在受控写入路径上强制，两个条目可同时展开。
        await renderer.Dispatcher.InvokeAsync(() => renderer.RerenderRootAsync(0, new Dictionary<string, object?>
        {
            [nameof(Accordion.Value)] = new HashSet<string> { "one", "two" },
            [nameof(Accordion.ValueChanged)] =
                EventCallback.Factory.Create<HashSet<string>>(new object(), v => reports.Add(v)),
            [nameof(Accordion.ChildContent)] = TwoItems(),
        }));
        await renderer.Dispatcher.InvokeAsync(() => Task.CompletedTask);

        var markup = renderer.Markup();
        Assert.Contains(">内容一<", markup);
        Assert.Contains(">内容二<", markup);
    }

    [Fact]
    public async Task 仅有Value而无ValueChanged时按非受控处理()
    {
        // 行为：IsControlled 仅以
        // ValueChanged.HasDelegate 判定，只传 Value 不传回调时状态仍由内部维护，
        // 受控写入沿不生效——初始展开集合来自 DefaultValue 兜底为空。
        var renderer = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(Accordion.Value)] = new HashSet<string> { "one" },
        });

        Assert.DoesNotContain(">内容一<", renderer.Markup());

        await ClickTriggerAsync(renderer, 0);
        Assert.Contains(">内容一<", renderer.Markup());
    }

    [Fact]
    public async Task Disabled条目应渲染disabled并拒绝点击切换()
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

        var markup = renderer.Markup();
        Assert.Contains("<button", markup);
        Assert.Contains(" disabled>", markup); // bool true 在渲染树中序列化为裸属性名

        // occurrence 0 即禁用条目的按钮：点击被 HandleClick 拦截。
        await ClickTriggerAsync(renderer, 0);
        Assert.DoesNotContain(">内容一<", renderer.Markup());
    }

    [Fact]
    public async Task 键盘导航事件应不改变展开状态()
    {
        // headless 下焦点链不可达（FocusAsync 经 interop 静默失败并被基元 catch 吞掉），
        // 照 SelectTests 先例以状态沿覆盖：keydown 只走导航分支，不触碰展开集合。
        var renderer = await MountAsync(new Dictionary<string, object?>());

        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onkeydown", new KeyboardEventArgs { Key = "ArrowDown" }, 0));
        await renderer.Dispatcher.InvokeAsync(() => Task.CompletedTask);

        var markup = renderer.Markup();
        Assert.DoesNotContain("aria-expanded=\"true\"", markup);
        Assert.DoesNotContain(">内容一<", markup);
    }

    [Fact]
    public async Task AccordionItem脱离Accordion应抛异常()
    {
        var renderer = CreateRenderer();
        var mount = renderer.MountAsync<PAccordionItem>(new Dictionary<string, object?>
        {
            [nameof(PAccordionItem.Value)] = "one",
        });
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () => await mount);
        Assert.Contains("AccordionItemPrimitive 必须在 Accordion 组件内使用", ex.Message);
    }

    [Fact]
    public async Task AccordionTrigger脱离AccordionItem应抛异常()
    {
        var renderer = CreateRenderer();
        var mount = renderer.MountAsync<PAccordionTrigger>(new Dictionary<string, object?>());
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () => await mount);
        Assert.Contains("AccordionTriggerPrimitive 必须在 AccordionItemPrimitive 组件内使用", ex.Message);
    }

    [Fact]
    public async Task AccordionContent脱离AccordionItem应抛异常()
    {
        var renderer = CreateRenderer();
        var mount = renderer.MountAsync<PAccordionContent>(new Dictionary<string, object?>());
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () => await mount);
        Assert.Contains("AccordionContentPrimitive 必须在 AccordionItemPrimitive 组件内使用", ex.Message);
    }

    [Fact]
    public async Task ForceMount为true时关闭态内容区域仍挂载()
    {
        var renderer = CreateRenderer();
        await renderer.MountAsync<Accordion>(new Dictionary<string, object?>
        {
            [nameof(Accordion.ChildContent)] = (RenderFragment)(builder =>
            {
                builder.OpenComponent<PAccordionItem>(0);
                builder.AddAttribute(1, nameof(PAccordionItem.Value), "one");
                builder.AddAttribute(2, nameof(PAccordionItem.ChildContent), (RenderFragment)(ib =>
                {
                    ib.OpenComponent<PAccordionTrigger>(0);
                    ib.AddAttribute(1, nameof(PAccordionTrigger.ChildContent), (RenderFragment)(tb => tb.AddContent(0, "标题一")));
                    ib.CloseComponent();

                    ib.OpenComponent<PAccordionContent>(10);
                    ib.AddAttribute(11, nameof(PAccordionContent.ForceMount), true);
                    ib.AddAttribute(12, nameof(PAccordionContent.ChildContent), (RenderFragment)(cb => cb.AddContent(0, "内容一")));
                    ib.CloseComponent();
                }));
                builder.CloseComponent();
            }),
        });

        var markup = renderer.Markup();
        Assert.Contains(">内容一<", markup); // 关闭态仍挂载
        Assert.Contains("data-state=\"closed\"", markup);
        Assert.Contains("role=\"region\"", markup);
    }

    [Fact]
    public async Task As参数应更换标题层标签并携带headerId()
    {
        var renderer = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(Accordion.ChildContent)] = (RenderFragment)(builder =>
            {
                builder.OpenComponent<PAccordionItem>(0);
                builder.AddAttribute(1, nameof(PAccordionItem.Value), "one");
                builder.AddAttribute(2, nameof(PAccordionItem.ChildContent), (RenderFragment)(ib =>
                {
                    ib.OpenComponent<PAccordionTrigger>(0);
                    ib.AddAttribute(1, nameof(PAccordionTrigger.As), "h2");
                    ib.AddAttribute(2, nameof(PAccordionTrigger.ChildContent), (RenderFragment)(tb => tb.AddContent(0, "标题一")));
                    ib.CloseComponent();
                }));
                builder.CloseComponent();
            }),
        });

        var markup = renderer.Markup();
        Assert.Contains("<h2 id=", markup);
        Assert.DoesNotContain("<h3", markup);
        Assert.Matches("id=\"accordion-\\d+-header-one\"", markup);
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
