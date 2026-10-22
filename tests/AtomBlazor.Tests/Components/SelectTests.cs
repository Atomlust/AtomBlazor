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
using PSelect = AtomBlazor.Primitives.SelectPrimitive<string>;
using PSelectContent = AtomBlazor.Primitives.SelectContentPrimitive<string>;
using PSelectContext = AtomBlazor.Primitives.SelectContext<string>;
using PSelectItem = AtomBlazor.Primitives.SelectItemPrimitive<string>;
using PSelectTrigger = AtomBlazor.Primitives.SelectTriggerPrimitive<string>;
using CSelect = AtomBlazor.Select<string>;

namespace AtomBlazor.Tests.Components;

/// <summary>
/// Select 家族测试：基元上下文的开合/选中/键盘导航状态沿、触发器 ARIA 呈现、
/// 打开沿在 overlay.open 一次 interop 里声明列表框键盘与关闭手势（JS 侧据此调用
/// select.setupKeyboardNavigation/openListbox）、释放时 select.cleanupKeyboardNavigation 的
/// 直调、包装层 Options 与组合式两模式的点击选项回填 Value、Group/Label 结构与占位符呈现。
/// 快照含自增短 id 与注册项 GUID 片段，先经正则归一化。
/// </summary>
public class SelectTests
{
    /// <summary>
    /// 归一化上下文自增 id（select-N）与注册项 GUID 片段（-item-xxxxxxxx），使快照可确定。
    /// </summary>
    private static string Scrub(string markup) =>
        Regex.Replace(
            Regex.Replace(markup, "select-[0-9]+", "select-<n>"),
            "-item-[0-9a-f]{8}", "-item-<id>");

    /// <summary>测试复用选项数组（CA1861 先例：常量数组参数首选 static readonly 字段）。</summary>
    private static readonly SelectOption<string>[] singleOption = [new("a", "苹果")];

    /// <summary>测试复用选项数组（CA1861 先例：常量数组参数首选 static readonly 字段）。</summary>
    private static readonly SelectOption<string>[] fruitOptions = [new("a", "苹果"), new("b", "香蕉")];

    /// <summary>组合式家族片段的两条选项值（CA1861 同上）。</summary>
    private static readonly string[] optionValues = ["a", "b"];

    /// <summary>
    /// 构造带全套基元服务的渲染器：门户/定位/焦点/下拉管理器/本地化器 + 记录 interop 的 NoopJavaScript。
    /// </summary>
    private static (ComponentTestRenderer Renderer, NoopJavaScript Js) CreateRenderer()
    {
        var js = new NoopJavaScript();
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(js);
        services.AddSingleton<IPortalService>(new PortalService(NullLogger<PortalService>.Instance));
        services.AddSingleton<IPositioningService>(new PositioningService(js));
        services.AddSingleton<IFocusManager>(new FocusManager(js));
        services.AddSingleton<DropdownManagerService>();
        services.AddSingleton<ILocalizer>(new DefaultLocalizer());
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

        return (new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance), js);
    }

    /// <summary>排空渲染队列，确保 StateHasChanged 的后续批已落定。</summary>
    private static Task SettleAsync(ComponentTestRenderer renderer) =>
        renderer.Dispatcher.InvokeAsync(() => Task.CompletedTask);

    /// <summary>
    /// 打开后强制重渲染门户宿主（根 0），让列表框内容确定性地进入渲染树。
    /// 宿主自身的类别变更通知已会排队，这里的显式一轮渲染只是把时序钉死，避免脆测试。
    /// </summary>
    private static async Task OpenAndSettleAsync(ComponentTestRenderer renderer)
    {
        await SettleAsync(renderer);
        await renderer.Dispatcher.InvokeAsync(() => renderer.RerenderRootAsync(0, new Dictionary<string, object?>()));
        await SettleAsync(renderer);
    }

    /// <summary>反射读取 interop 记录里匿名选项对象的属性（含 DotNetObjectReference 的对象无法直接 JSON 化）。</summary>
    private static object? Prop(object? target, string name) =>
        target?.GetType().GetProperty(name)?.GetValue(target);

    /// <summary>经反射取基元 Select 的私有 _context 字段。</summary>
    private static PSelectContext ContextOf(PSelect select) =>
        (PSelectContext)typeof(PSelect)
            .GetField("_context", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(select)!;

    // ------------------------------------------------------------------
    // 基元上下文（纯状态沿，无渲染）
    // ------------------------------------------------------------------

    [Fact]
    public void 开合切换沿只在未禁用时生效并重置聚焦索引()
    {
        var context = new PSelectContext();

        context.Open();
        Assert.True(context.IsOpen);
        Assert.Equal(-1, context.FocusedIndex);

        context.SetFocusedIndex(2);
        context.Close();
        Assert.False(context.IsOpen);
        Assert.Equal(-1, context.FocusedIndex);

        context.Toggle();
        Assert.True(context.IsOpen);

        context.SetDisabled(true);
        Assert.False(context.IsOpen); // 禁用时关闭已开的状态

        context.Toggle();
        Assert.False(context.IsOpen); // 禁用吞掉切换
        context.Open();
        Assert.False(context.IsOpen); // 禁用吞掉打开
    }

    [Fact]
    public void 选中值沿应回填值与展示文本并关闭且回调()
    {
        var reports = new List<string?>();
        var context = new PSelectContext();
        context.OnValueChange = v => reports.Add(v);
        context.Open();

        context.SelectValue("b", "选项 B");

        Assert.Equal("b", context.Value);
        Assert.Equal("选项 B", context.DisplayText);
        Assert.False(context.IsOpen);                    // 选中即关闭
        Assert.True(context.State.RestoreFocusOnClose);  // 有意关闭，焦点归还触发器
        Assert.Equal(["b"], reports);
    }

    [Fact]
    public void 注册项命中当前值时应回填展示文本()
    {
        var context = new PSelectContext();
        context.State.Value = "b";

        context.RegisterItem("b", false, "选项 B");

        Assert.Equal("选项 B", context.DisplayText);
    }

    [Fact]
    public void 移动焦点应跳过禁用项并在两端回绕()
    {
        var context = new PSelectContext();
        context.RegisterItem("a", false);
        context.RegisterItem("b", true);
        context.RegisterItem("c", false);

        context.SetFocusedIndex(0);
        context.MoveFocus(1);
        Assert.Equal(2, context.FocusedIndex); // 跳过禁用的 b

        context.MoveFocus(1);
        Assert.Equal(0, context.FocusedIndex); // 末位回绕到首位

        context.MoveFocus(-1);
        Assert.Equal(2, context.FocusedIndex); // 首位回绕到末位
    }

    [Fact]
    public void 聚焦首末与选中项沿分别落在正确的可用项上()
    {
        var context = new PSelectContext();
        context.RegisterItem("a", true);
        context.RegisterItem("b", false);
        context.RegisterItem("c", false);

        context.FocusFirst();
        Assert.Equal(1, context.FocusedIndex); // a 禁用，落到 b

        context.FocusLast();
        Assert.Equal(2, context.FocusedIndex);

        context.State.Value = "c";
        context.FocusSelectedOrFirst();
        Assert.Equal(2, context.FocusedIndex); // 选中项 c

        context.State.Value = "a";             // a 禁用
        context.FocusSelectedOrFirst();
        Assert.Equal(1, context.FocusedIndex); // 回落到第一个可用项 b
    }

    [Fact]
    public void 确认聚焦项应跳过禁用项的选中()
    {
        var context = new PSelectContext();
        context.RegisterItem("a", true, "选项 A");
        context.RegisterItem("b", false, "选项 B");

        context.SetFocusedIndex(0);
        context.SelectFocusedItem();
        Assert.Null(context.Value); // 禁用项不选中

        context.SetFocusedIndex(1);
        context.SelectFocusedItem();
        Assert.Equal("b", context.Value);
        Assert.Equal("选项 B", context.DisplayText);
    }

    [Fact]
    public void 查询与覆盖标识沿应保持一致性()
    {
        var context = new PSelectContext();
        var idA = context.RegisterItem("a", false, "选项 A");

        Assert.Equal(0, context.GetItemIndex(idA));
        Assert.Equal("选项 A", context.GetDisplayTextForValue("a"));
        Assert.Null(context.GetDisplayTextForValue(null));
        Assert.Null(context.GetFocusedItemId());

        context.SetFocusedIndex(0);
        Assert.Equal(idA, context.GetFocusedItemId());

        context.UnregisterItem(idA);
        Assert.Equal(-1, context.GetItemIndex(idA));

        var originalTriggerId = context.TriggerId;
        context.SetTriggerId("custom-trigger");
        Assert.Equal("custom-trigger", context.TriggerId);
        Assert.NotEqual(originalTriggerId, context.TriggerId);
    }

    // ------------------------------------------------------------------
    // 基元层渲染
    // ------------------------------------------------------------------

    private static RenderFragment PrimitiveFamily(params string[] values) => builder =>
    {
        builder.OpenComponent<PSelectTrigger>(0);
        builder.CloseComponent();
        builder.OpenComponent<PSelectContent>(1);
        builder.AddAttribute(2, "ChildContent", (RenderFragment)(inner =>
        {
            var seq = 0;
            foreach (var value in values)
            {
                inner.OpenComponent<PSelectItem>(seq++);
                inner.AddAttribute(seq++, "Value", value);
                inner.AddAttribute(seq++, "Text", $"选项 {value}");
                inner.CloseComponent();
            }
        }));
        builder.CloseComponent();
    };

    [Fact]
    public async Task 基元触发器关闭态快照应与基线一致()
    {
        var (renderer, _) = CreateRenderer();

        await renderer.MountAsync<PSelect>(new Dictionary<string, object?>
        {
            [nameof(PSelect.ChildContent)] = PrimitiveFamily("a", "b"),
        });
        await SettleAsync(renderer);

        await Verify(Scrub(renderer.Markup()));
    }

    [Fact]
    public async Task 触发器ARIA应随开合沿切换()
    {
        var (renderer, _) = CreateRenderer();
        await renderer.MountAsync<PSelect>(new Dictionary<string, object?>
        {
            [nameof(PSelect.ChildContent)] = PrimitiveFamily("a"),
        });
        await SettleAsync(renderer);

        Assert.Contains("role=\"combobox\"", renderer.Markup());
        Assert.Contains("aria-haspopup=\"listbox\"", renderer.Markup());
        Assert.Contains("aria-expanded=\"false\"", renderer.Markup());
        Assert.Contains("data-state=\"closed\"", renderer.Markup());

        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs()));
        await SettleAsync(renderer);

        Assert.Contains("aria-expanded=\"true\"", renderer.Markup());
        Assert.Contains("data-state=\"open\"", renderer.Markup());
    }

    [Fact]
    public async Task 键盘激活不应重复关闭刚被自己打开的列表()
    {
        // Selection/SelectTriggerLifecycleTests 的 1:1 对应：
        // Enter 触发 HandleKeyDown 打开后，浏览器还会补一次 Detail=0 的原生 click，
        // 该重复激活必须被吞掉；随后的指针点击（Detail=1）才执行关闭。
        var (renderer, _) = CreateRenderer();
        var select = await renderer.MountAsync<PSelect>(new Dictionary<string, object?>
        {
            [nameof(PSelect.ChildContent)] = PrimitiveFamily("a"),
        });
        await SettleAsync(renderer);
        var context = ContextOf(select);

        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onkeydown", new KeyboardEventArgs { Key = "Enter" }));
        Assert.True(context.IsOpen);

        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs { Detail = 0 }));
        Assert.True(context.IsOpen);

        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs { Detail = 1 }));
        Assert.False(context.IsOpen);
    }

    [Fact]
    public async Task 打开沿应在overlay点open一次interop里声明列表框键盘与关闭手势()
    {
        // headless 渲染从不配置 @ref，所以经上下文显式 Open 一个手工构造的锚元素引用，
        // 让 FloatingPortal 走完整打开沿。JS 侧（overlay.open → select.openListbox →
        // select.setupKeyboardNavigation）据这里的 keyboard/dismiss 声明完成实际接线。
        var (renderer, js) = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());
        var select = await renderer.MountAsync<PSelect>(new Dictionary<string, object?>
        {
            [nameof(PSelect.ChildContent)] = PrimitiveFamily("a", "b"),
        });
        await SettleAsync(renderer); // 预热基元 bundle（FloatingPortal firstRender）

        var context = ContextOf(select);
        await renderer.Dispatcher.InvokeAsync(() => context.Open(new ElementReference("anchor-1")));
        await OpenAndSettleAsync(renderer);

        var openCall = js.Invocations.Single(i => i.Identifier == "overlay.open");
        var openOptions = openCall.Args![2]!;

        var keyboard = Prop(openOptions, "keyboard");
        Assert.Equal("Listbox", Prop(keyboard, "kind"));
        Assert.Equal(context.ContentId, Prop(keyboard, "contentId"));
        Assert.NotNull(Prop(keyboard, "callbackRef")); // 回指内容组件的 DotNetObjectReference

        var dismiss = Prop(openOptions, "dismiss");
        Assert.Equal(context.ContentId, Prop(dismiss, "contentId"));
        Assert.Equal(context.TriggerId, Prop(dismiss, "triggerId"));
        Assert.Equal(true, Prop(dismiss, "onOutsideInteraction"));
        Assert.Equal("select-portal-" + context.ContentId, openCall.Args![0]);

        await renderer.Dispatcher.InvokeAsync(() => context.Close(restoreFocus: true));
        await OpenAndSettleAsync(renderer);

        var closeCall = js.Invocations.Single(i => i.Identifier == "overlay.close");
        var closeOptions = closeCall.Args![1]!;
        Assert.Equal(context.TriggerId, Prop(closeOptions, "restoreFocusToId"));
        // 关闭沿应同批声明释放列表框键盘处理器（JS 在 overlay.close 里调用 cleanup）。
        Assert.Equal(context.ContentId, Prop(Prop(closeOptions, "keyboard"), "contentId"));
    }

    [Fact]
    public async Task 释放内容组件应直调select点cleanupKeyboardNavigation()
    {
        var (renderer, js) = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());
        var select = await renderer.MountAsync<PSelect>(new Dictionary<string, object?>
        {
            [nameof(PSelect.ChildContent)] = PrimitiveFamily("a"),
        });
        await SettleAsync(renderer); // OnAfterRender 里拿到 _jsModule

        var content = renderer.FindComponent<PSelectContent>();
        var context = ContextOf(select);

        await renderer.Dispatcher.InvokeAsync(() => content.DisposeAsync().AsTask());

        var cleanup = js.Invocations.Single(i => i.Identifier == "select.cleanupKeyboardNavigation");
        Assert.Equal(context.ContentId, cleanup.Args![0]);
    }

    [Fact]
    public async Task 点击选项应选中该项并关闭列表()
    {
        var (renderer, _) = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());
        var select = await renderer.MountAsync<PSelect>(new Dictionary<string, object?>
        {
            [nameof(PSelect.ChildContent)] = PrimitiveFamily("a", "b"),
        });
        await SettleAsync(renderer);

        var context = ContextOf(select);

        // 关闭态下唯一 onclick 是触发按钮。
        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs()));
        await OpenAndSettleAsync(renderer);
        Assert.True(context.IsOpen);

        // 打开后根 0（门户宿主）先渲染两个选项 div（0/1），其后才是根 1 的触发按钮（2）。
        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs { Detail = 1 }, occurrence: 1));
        await SettleAsync(renderer);

        Assert.Equal("b", context.Value);
        Assert.Equal("选项 b", context.DisplayText);
        Assert.False(context.IsOpen);

        var markup = Scrub(renderer.Markup());
        Assert.Contains("aria-expanded=\"false\"", markup);                 // 触发器随关闭复位
        Assert.Single(Regex.Matches(markup, "aria-selected=\\\"true\\\"")); // 只有 b 被标记选中
        Assert.Equal(2, Regex.Count(markup, "role=\\\"option\\\""));
    }

    // ------------------------------------------------------------------
    // 包装层（Components）
    // ------------------------------------------------------------------

    private static RenderFragment StyledFamily(bool withGroup = false) => builder =>    {
        builder.OpenComponent<SelectTrigger<string>>(0);
        builder.AddAttribute(1, "ChildContent", (RenderFragment)(inner =>
        {
            // 包装层 SelectValue 一致：非泛型，经 ISelectDisplayContext 级联取值文本。
            inner.OpenComponent<SelectValue>(0);
            inner.AddAttribute(1, nameof(SelectValue.Placeholder), "请选择");
            inner.CloseComponent();
        }));
        builder.CloseComponent();

        builder.OpenComponent<SelectContent<string>>(2);
        builder.AddAttribute(3, "ChildContent", (RenderFragment)(inner =>
        {
            var seq = 0;
            if (withGroup)
            {
                inner.OpenComponent<SelectGroup>(seq++);
                inner.AddAttribute(seq++, nameof(SelectGroup.ChildContent), (RenderFragment)(group =>
                {
                    var gseq = 0;
                    group.OpenComponent<SelectLabel>(gseq++);
                    group.AddAttribute(gseq++, nameof(SelectLabel.ChildContent),
                        (RenderFragment)(label => label.AddContent(0, "分组一")));
                    group.CloseComponent();
                    foreach (var value in optionValues)
                    {
                        group.OpenComponent<SelectItem<string>>(gseq);
                        group.AddAttribute(gseq + 1, nameof(SelectItem<string>.Value), value);
                        group.AddAttribute(gseq + 2, nameof(SelectItem<string>.Text), $"选项 {value}");
                        group.CloseComponent();
                        gseq += 3;
                    }
                }));
                inner.CloseComponent();
            }
            else
            {
                foreach (var value in optionValues)
                {
                    inner.OpenComponent<SelectItem<string>>(seq);
                    inner.AddAttribute(seq + 1, nameof(SelectItem<string>.Value), value);
                    inner.AddAttribute(seq + 2, nameof(SelectItem<string>.Text), $"选项 {value}");
                    inner.CloseComponent();
                    seq += 3;
                }
            }
        }));
        builder.CloseComponent();
    };

    [Fact]
    public async Task 包装层关闭态快照应与基线一致()
    {
        var (renderer, _) = CreateRenderer();

        await renderer.MountAsync<CSelect>(new Dictionary<string, object?>
        {
            [nameof(CSelect.ValueChanged)] = EventCallback.Factory.Create<string?>(new object(), _ => { }),
            [nameof(CSelect.ChildContent)] = StyledFamily(withGroup: true),
        });
        await SettleAsync(renderer);

        await Verify(Scrub(renderer.Markup()));
    }

    [Fact]
    public async Task 包装层打开态全家族结构快照应与基线一致()
    {
        var (renderer, _) = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());
        await renderer.MountAsync<CSelect>(new Dictionary<string, object?>
        {
            [nameof(CSelect.ValueChanged)] = EventCallback.Factory.Create<string?>(new object(), _ => { }),
            [nameof(CSelect.ChildContent)] = StyledFamily(withGroup: true),
        });
        await SettleAsync(renderer);

        var context = ContextOf(renderer.FindComponent<PSelect>());
        await renderer.Dispatcher.InvokeAsync(() => context.Open(new ElementReference("anchor-1")));
        await OpenAndSettleAsync(renderer);

        await Verify(Scrub(renderer.Markup()));
    }

    /// <summary>
    /// 断言：BottomSheet 呈现走 Sheet 模态（data-side=bottom）而非浮动面板，
    /// 抽屉内的列表框包裹件仍经 aria-labelledby 挂回触发器，标注链与 Popover 形态同形。
    /// </summary>
    [Fact]
    public async Task 底部抽屉呈现应走Sheet模态并保留触发器标注链()
    {
        var (renderer, _) = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());
        await renderer.MountAsync<CSelect>(new Dictionary<string, object?>
        {
            [nameof(CSelect.ValueChanged)] = EventCallback.Factory.Create<string?>(new object(), _ => { }),
            [nameof(CSelect.Options)] = fruitOptions,
            [nameof(CSelect.Presentation)] = SelectPresentation.BottomSheet,
        });
        await SettleAsync(renderer);

        var context = ContextOf(renderer.FindComponent<PSelect>());
        await renderer.Dispatcher.InvokeAsync(() => context.Open(new ElementReference("anchor-1")));
        await OpenAndSettleAsync(renderer);

        var markup = renderer.Markup();
        Assert.Contains("data-side=\"bottom\"", markup);
        Assert.Contains("data-select-content", markup);
        Assert.DoesNotContain("data-radix-popper", markup);

        var triggerId = Regex.Match(markup, "id=\"(select-[0-9]+-trigger)\"").Groups[1].Value;
        Assert.NotEmpty(triggerId);
        Assert.Contains($"aria-labelledby=\"{triggerId}\"", markup);
    }

    [Fact]
    public async Task 选项模式点击应回填Value且触发器显示选项文本()
    {
        var reports = new List<string?>();
        var callback = EventCallback.Factory.Create<string?>(new object(), v => reports.Add(v));
        var (renderer, _) = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        await renderer.MountAsync<CSelect>(new Dictionary<string, object?>
        {
            [nameof(CSelect.ValueChanged)] = callback,
            [nameof(CSelect.Options)] = fruitOptions,
            [nameof(CSelect.Placeholder)] = "选一个",
        });
        await SettleAsync(renderer);

        // 关闭态：占位符呈现，aria-expanded 为 false。
        Assert.Contains("选一个", renderer.Markup());
        Assert.Contains("aria-expanded=\"false\"", renderer.Markup());

        // 点击触发（关闭态下根 0 无门户内容，触发按钮是唯一 onclick）。
        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs()));
        await OpenAndSettleAsync(renderer);

        // 打开后：根 0 门户里两个选项（0/1），根 1 触发按钮（2）。点第二项"香蕉"。
        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs { Detail = 1 }, occurrence: 1));
        await SettleAsync(renderer);

        Assert.Equal(["b"], reports);
        var markup = renderer.Markup();
        Assert.Contains("香蕉", markup);                     // 触发器回填展示文本
        Assert.DoesNotContain("选一个", markup);             // 占位符让位
        Assert.Contains("aria-expanded=\"false\"", markup);
    }

    [Fact]
    public async Task DisplayTextSelector应让预选值直接显示文本而不必打开列表()
    {
        var (renderer, _) = CreateRenderer();

        await renderer.MountAsync<CSelect>(new Dictionary<string, object?>
        {
            [nameof(CSelect.Value)] = "b",
            [nameof(CSelect.ValueChanged)] = EventCallback.Factory.Create<string?>(new object(), _ => { }),
            [nameof(CSelect.DisplayTextSelector)] = new Func<string, string>(v => v == "b" ? "香蕉" : "苹果"),
            [nameof(CSelect.ChildContent)] = StyledFamily(),
        });
        await SettleAsync(renderer);

        Assert.Contains("香蕉", renderer.Markup());
        Assert.DoesNotContain("请选择", renderer.Markup());
    }

    [Fact]
    public async Task 受控父级把Value置空应回到占位符()
    {
        var (renderer, _) = CreateRenderer();
        var callback = EventCallback.Factory.Create<string?>(new object(), _ => { });
        Func<string, string> displayText = value => value == "b" ? "香蕉" : "苹果";

        await renderer.MountAsync<CSelect>(new Dictionary<string, object?>
        {
            [nameof(CSelect.Value)] = "b",
            [nameof(CSelect.ValueChanged)] = callback,
            [nameof(CSelect.DisplayTextSelector)] = displayText,
            [nameof(CSelect.ChildContent)] = StyledFamily(),
        });
        await SettleAsync(renderer);
        Assert.Contains("香蕉", renderer.Markup());

        await renderer.Dispatcher.InvokeAsync(() => renderer.RerenderRootAsync(0, new Dictionary<string, object?>
        {
            [nameof(CSelect.Value)] = null,
            [nameof(CSelect.ValueChanged)] = callback,
            [nameof(CSelect.DisplayTextSelector)] = displayText,
            [nameof(CSelect.ChildContent)] = StyledFamily(),
        }));
        await SettleAsync(renderer);

        var markup = renderer.Markup();
        Assert.Contains("请选择", markup);
        Assert.DoesNotContain("香蕉", markup);
    }

    [Fact]
    public async Task 禁用态应吞掉触发点击并播报aria禁用()
    {
        var (renderer, _) = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());
        await renderer.MountAsync<CSelect>(new Dictionary<string, object?>
        {
            [nameof(CSelect.Disabled)] = true,
            [nameof(CSelect.ValueChanged)] = EventCallback.Factory.Create<string?>(new object(), _ => { }),
            [nameof(CSelect.Options)] = singleOption,
        });
        await SettleAsync(renderer);

        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs()));
        await OpenAndSettleAsync(renderer);

        var context = ContextOf(renderer.FindComponent<PSelect>());
        Assert.False(context.IsOpen);
        Assert.True(context.Disabled);
        Assert.Contains("aria-disabled=\"true\"", renderer.Markup());
    }

    [Fact]
    public async Task 列表底部状态应随加载与到底消息切换()
    {
        var (renderer, _) = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        await renderer.MountAsync<CSelect>(new Dictionary<string, object?>
        {
            [nameof(CSelect.ValueChanged)] = EventCallback.Factory.Create<string?>(new object(), _ => { }),
            [nameof(CSelect.Options)] = singleOption,
            [nameof(CSelect.EndOfListMessage)] = "已加载全部",
        });
        await SettleAsync(renderer);

        var context = ContextOf(renderer.FindComponent<PSelect>());
        await renderer.Dispatcher.InvokeAsync(() => context.Open(new ElementReference("anchor-1")));
        await OpenAndSettleAsync(renderer);

        Assert.Contains("已加载全部", renderer.Markup());

        // 转入加载态：消息让位于加载指示（根 1 是包装层 Select）。
        await renderer.Dispatcher.InvokeAsync(() => renderer.RerenderRootAsync(1, new Dictionary<string, object?>
        {
            [nameof(CSelect.ValueChanged)] = EventCallback.Factory.Create<string?>(new object(), _ => { }),
            [nameof(CSelect.Options)] = singleOption,
            [nameof(CSelect.EndOfListMessage)] = "已加载全部",
            [nameof(CSelect.IsLoading)] = true,
        }));
        await OpenAndSettleAsync(renderer);

        Assert.DoesNotContain("已加载全部", renderer.Markup());
        Assert.Contains("animate-spin", renderer.Markup()); // Spinner 的旋转类
    }

    [Fact]
    public async Task 必填与无障碍名称应落到触发按钮上()
    {
        var (renderer, _) = CreateRenderer();

        await renderer.MountAsync<CSelect>(new Dictionary<string, object?>
        {
            [nameof(CSelect.Required)] = true,
            [nameof(CSelect.AriaLabel)] = "通知方式",
            [nameof(CSelect.ValueChanged)] = EventCallback.Factory.Create<string?>(new object(), _ => { }),
            [nameof(CSelect.Options)] = singleOption,
        });
        await SettleAsync(renderer);

        var markup = renderer.Markup();
        Assert.Contains("aria-required=\"true\"", markup);
        Assert.Contains("aria-label=\"通知方式\"", markup);
    }
}
