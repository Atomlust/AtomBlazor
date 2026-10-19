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

namespace AtomBlazor.Tests.Components;

/// <summary>
/// Combobox 测试：弹层开合与受控回填（点击选项 → ValueChanged、弹层随收、
/// 同项再点切换清空）、Options/复合两模式的 Group/Item 复合渲染（隐藏注册通道回填
/// 触发器标题、SelectedItemText 兜底链）、常量文件语义、键盘沿（借 CommandContext
/// 焦点索引状态断言，不依赖真实 DOM）、搜索查询外部过滤接管（PassthroughFilter）与
/// 关闭复位、禁用/必填/宽度等参数面。
/// 快照含 combobox-/command-/popover- 短 id 与 GUID，先经正则归一化。
/// </summary>
public class ComboboxTests
{
    /// <summary>
    /// 归一化 combobox-search/combobox GUID 稳定 id、command 家族 GUID（上下文、输入、
    /// 分组）与 popover-N 自增短 id，使快照可确定。
    /// </summary>
    private static string Scrub(string markup) =>
        Regex.Replace(
            Regex.Replace(
                Regex.Replace(
                    Regex.Replace(markup, "combobox-search-[0-9a-f]{32}", "combobox-search-<id>"),
                    "combobox-[0-9a-f]{32}", "combobox-<id>"),
                "command-[0-9a-f]{32}", "command-<id>"),
            "popover-[0-9]+", "popover-<n>");

    private static readonly SelectOption<string>[] frameworkOptions =
    [
        new("blazor", "Blazor"),
        new("react", "React"),
    ];

    /// <summary>
    /// 构造带全套基元服务的渲染器（门户/定位/焦点/本地化器），与 TreeSelectTests 同构。
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

    /// <summary>打开后强制重渲染门户宿主（根 0），让弹层内容确定性地进入渲染树。</summary>
    private static async Task OpenAndSettleAsync(ComponentTestRenderer renderer)
    {
        await SettleAsync(renderer);
        await renderer.Dispatcher.InvokeAsync(() => renderer.RerenderRootAsync(0, new Dictionary<string, object?>()));
        await SettleAsync(renderer);
    }

    /// <summary>反射取 Combobox 内基元 Popover 的上下文（先例：TreeSelectTests 的 ContextOf）。</summary>
    private static AtomBlazor.Primitives.PopoverContext ContextOf(ComponentTestRenderer renderer) =>
        (AtomBlazor.Primitives.PopoverContext)typeof(AtomBlazor.Primitives.PopoverPrimitive)
            .GetField("_context", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(renderer.FindComponent<AtomBlazor.Primitives.PopoverPrimitive>())!;

    /// <summary>挂载 PortalHost（根 0）+ Combobox（根 1）。</summary>
    private static async Task<(ComponentTestRenderer Renderer, NoopJavaScript Js)> MountAsync(
        Dictionary<string, object?> parameters)
    {
        var (renderer, js) = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());
        await renderer.MountAsync<Combobox<string>>(parameters);
        await SettleAsync(renderer);
        return (renderer, js);
    }

    /// <summary>点击触发器打开弹层（关闭态下唯一 onclick 携带者）并让门户把内容画进渲染树。</summary>
    private static async Task OpenPopupAsync(ComponentTestRenderer renderer)
    {
        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs()));
        await OpenAndSettleAsync(renderer);
    }

    /// <summary>复合模式子内容：一个 ComboboxGroup 包两个 ComboboxItem。</summary>
    private static RenderFragment CompositeContent(bool withGroup = true) => builder =>
    {
        RenderFragment items = inner =>
        {
            var seq = 0;
            foreach (var (value, text) in new[] { ("blazor", "Blazor"), ("react", "React") })
            {
                inner.OpenComponent<ComboboxItem<string>>(seq++);
                inner.AddAttribute(seq++, nameof(ComboboxItem<string>.Value), value);
                inner.AddAttribute(seq++, nameof(ComboboxItem<string>.Text), text);
                inner.CloseComponent();
            }
        };

        if (withGroup)
        {
            builder.OpenComponent<ComboboxGroup>(0);
            builder.AddAttribute(1, nameof(ComboboxGroup.Label), "框架");
            builder.AddAttribute(2, nameof(ComboboxGroup.ChildContent), items);
            builder.CloseComponent();
        }
        else
        {
            items(builder);
        }
    };

    // ------------------------------------------------------------------
    // 弹层开合与受控回填
    // ------------------------------------------------------------------

    [Fact]
    public async Task 关闭态应呈现combobox语义与占位文本且弹层内容未挂载()
    {
        var (renderer, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(Combobox<string>.Options)] = frameworkOptions,
            [nameof(Combobox<string>.AriaLabel)] = "选择框架",
        });

        var markup = renderer.Markup();
        Assert.Contains("role=\"combobox\"", markup);
        // 基元触发器的显式 aria-haspopup="true" 按位置规则压过包装层透传的 "listbox"（先例：TreeSelectTests）。
        Assert.Contains("aria-haspopup=\"true\"", markup);
        Assert.Contains("aria-expanded=\"false\"", markup);
        Assert.Contains("aria-label=\"选择框架\"", markup);
        // 基元触发器的显式 aria-controls（指向浮层内容）按位置规则覆盖包装层透传的 "{Id}-listbox"（先例：TreeSelectTests）。
        Assert.Contains($"aria-controls=\"{ContextOf(renderer).ContentId}\"", markup);
        Assert.Contains("Select an option...", markup); // DefaultLocalizer 占位符
        Assert.DoesNotContain("role=\"listbox\"", markup); // 弹层内容未挂载
        Assert.DoesNotContain("Blazor", markup);
    }

    [Fact]
    public async Task 打开弹层应呈现搜索框选项与选中标记()
    {
        var (renderer, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(Combobox<string>.Options)] = frameworkOptions,
            [nameof(Combobox<string>.Value)] = "react",
        });

        await OpenPopupAsync(renderer);
        var markup = renderer.Markup();
        Assert.Contains("Search...", markup); // 搜索框占位符
        Assert.Contains("Blazor", markup);
        Assert.Contains("React", markup);
        Assert.Contains("data-atom-current=\"true\"", markup); // 选中项自带标记
        Assert.Contains("aria-expanded=\"true\"", renderer.Markup());
        Assert.True(ContextOf(renderer).IsOpen);
    }

    [Fact]
    public async Task 点击选项应回填ValueChanged并随受控沿收起弹层()
    {
        var reports = new List<string?>();
        var (renderer, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(Combobox<string>.Options)] = frameworkOptions,
            [nameof(Combobox<string>.ValueChanged)] =
                EventCallback.Factory.Create<string?>(new object(), v => reports.Add(v)),
        });

        await OpenPopupAsync(renderer);
        var context = renderer.FindComponent<CommandList>().Context!;
        await renderer.Dispatcher.InvokeAsync(() => context.SelectItemByValueAsync("blazor"));
        await SettleAsync(renderer);

        Assert.Equal(["blazor"], reports);
        await renderer.Dispatcher.InvokeAsync(() => renderer.RerenderRootAsync(1, new Dictionary<string, object?>
        {
            [nameof(Combobox<string>.Options)] = frameworkOptions,
            [nameof(Combobox<string>.Value)] = "blazor",
            [nameof(Combobox<string>.ValueChanged)] =
                EventCallback.Factory.Create<string?>(new object(), v => reports.Add(v)),
        }));
        await SettleAsync(renderer);
        Assert.False(ContextOf(renderer).IsOpen); // _isOpen=false 经重渲染同步回上下文
        Assert.Contains("Blazor", renderer.Markup()); // 触发器标题回填
        Assert.Contains("aria-expanded=\"false\"", renderer.Markup());
    }

    [Fact]
    public async Task 同项再点应切换清空为default()
    {
        var reports = new List<string?>();
        var (renderer, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(Combobox<string>.Options)] = frameworkOptions,
            [nameof(Combobox<string>.Value)] = "blazor",
            [nameof(Combobox<string>.ValueChanged)] =
                EventCallback.Factory.Create<string?>(new object(), v => reports.Add(v)),
        });

        await OpenPopupAsync(renderer);
        var context = renderer.FindComponent<CommandList>().Context!;
        await renderer.Dispatcher.InvokeAsync(() => context.SelectItemByValueAsync("blazor"));
        await SettleAsync(renderer);

        // 切换行为：已选中项再次被选 → 回落 default（string 即 null）
        Assert.Equal([null], reports);
    }

    // ------------------------------------------------------------------
    // 常量与复合模式
    // ------------------------------------------------------------------

    [Fact]
    // internal 常量（InternalsVisibleTo 先例）：Combobox 与 ComboboxItem 靠它对齐级联名
    public void 常量文件应提供注册通道级联名() =>
        Assert.Equal("ComboboxRegistrationOnly", ComboboxConstants.RegistrationScopeName);

    [Fact]
    public async Task 复合模式应经隐藏注册通道回填触发器标题()
    {
        var (renderer, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(Combobox<string>.ChildContent)] = CompositeContent(),
            [nameof(Combobox<string>.Value)] = "blazor",
        });

        var markup = renderer.Markup();
        // 隐藏注册通道：display:none + aria-hidden，组渲染但项不产出可交互 DOM
        Assert.Contains("display:none", markup);
        Assert.Contains("aria-hidden=\"true\"", markup);
        Assert.Contains(">框架<", markup);
        // 注册文本抵达触发器（_triggerTextDirty 旁路 ShouldRender 闸门）
        Assert.Contains("Blazor", markup);
        Assert.DoesNotContain("Select an option...", markup);
    }

    [Fact]
    public async Task 复合模式弹层内点击项应回填并关闭且选中项带标记()
    {
        var reports = new List<string?>();
        var (renderer, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(Combobox<string>.ChildContent)] = CompositeContent(withGroup: false),
            [nameof(Combobox<string>.ValueChanged)] =
                EventCallback.Factory.Create<string?>(new object(), v => reports.Add(v)),
        });

        await OpenPopupAsync(renderer);
        Assert.Contains("Search...", renderer.Markup());

        var context = renderer.FindComponent<CommandList>().Context!;
        await renderer.Dispatcher.InvokeAsync(() => context.SelectItemByValueAsync("react"));
        await SettleAsync(renderer);

        Assert.Equal(["react"], reports);
        await renderer.Dispatcher.InvokeAsync(() => renderer.RerenderRootAsync(1, new Dictionary<string, object?>
        {
            [nameof(Combobox<string>.ChildContent)] = CompositeContent(withGroup: false),
            [nameof(Combobox<string>.Value)] = "react",
        }));
        await SettleAsync(renderer);
        Assert.False(ContextOf(renderer).IsOpen);
    }

    [Fact]
    public async Task SelectedItemText应为无匹配注册项时的兜底标题()
    {
        var (renderer, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(Combobox<string>.ChildContent)] = CompositeContent(withGroup: false),
            [nameof(Combobox<string>.Value)] = "async-later",
            [nameof(Combobox<string>.SelectedItemText)] = "异步加载的选中项",
        });

        // 解析链：Options→注册表→SelectedItemText——未注册值显示调用方提示
        Assert.Contains("异步加载的选中项", renderer.Markup());
    }

    // ------------------------------------------------------------------
    // 键盘沿（CommandContext 状态断言）与搜索过滤
    // ------------------------------------------------------------------

    [Fact]
    public async Task 方向键应移动上下文焦点且回车选中回填并收起()
    {
        var reports = new List<string?>();
        var (renderer, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(Combobox<string>.Options)] = frameworkOptions,
            [nameof(Combobox<string>.ValueChanged)] =
                EventCallback.Factory.Create<string?>(new object(), v => reports.Add(v)),
        });

        await OpenPopupAsync(renderer);
        var context = renderer.FindComponent<CommandList>().Context!;

        // occurrence 0 是基元 PopoverContent 容器的 Escape 沿，occurrence 1 才是搜索框的
        // @onkeydown（Command 键盘导航的挂点）。
        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onkeydown", new KeyboardEventArgs { Key = "ArrowDown" }, occurrence: 1));
        await SettleAsync(renderer);
        Assert.Equal(0, context.FocusedIndex);
        Assert.True(context.IsKeyboardNavigating);

        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onkeydown", new KeyboardEventArgs { Key = "Enter" }, occurrence: 1));
        await SettleAsync(renderer);

        Assert.Equal(["blazor"], reports); // 回车选中聚焦项 → HandleSelect 回填
        await renderer.Dispatcher.InvokeAsync(() => renderer.RerenderRootAsync(1, new Dictionary<string, object?>
        {
            [nameof(Combobox<string>.Options)] = frameworkOptions,
        }));
        await SettleAsync(renderer);
        Assert.False(ContextOf(renderer).IsOpen);
    }

    [Fact]
    public async Task SearchQuery参数应下传过滤并在无匹配时显形空消息()
    {
        var (renderer, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(Combobox<string>.Options)] = frameworkOptions,
            [nameof(Combobox<string>.SearchQuery)] = "zzz",
            [nameof(Combobox<string>.EmptyMessage)] = "没有框架了。",
        });

        await OpenPopupAsync(renderer);
        var context = renderer.FindComponent<CommandList>().Context!;
        Assert.Equal("zzz", context.SearchQuery); // 参数下传 Command 上下文

        var markup = renderer.Markup();
        Assert.DoesNotContain(">Blazor<", markup); // 全部滤出
        Assert.Contains("没有框架了。", markup);   // CommandEmpty 携 EffectiveEmptyMessage
    }

    [Fact]
    public async Task 提供SearchQueryChanged时外部过滤应以直通谓词接管()
    {
        var (renderer, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(Combobox<string>.Options)] = frameworkOptions,
            [nameof(Combobox<string>.SearchQueryChanged)] =
                EventCallback.Factory.Create<string>(new object(), _ => { }),
        });

        // Command 只在弹层内容挂载后存在（门户按需渲染），先打开再取
        await OpenPopupAsync(renderer);
        // 双滤波竞态守卫：Combobox 把 Command 内部过滤换成恒真直通谓词
        var filter = renderer.FindComponent<Command>().FilterFunction;
        Assert.NotNull(filter);
        Assert.True(filter!(new CommandItemMetadata { Value = "x", SearchText = "任何文本" }, "不匹配串"));

        // 未提供回调时谓词保持 null——Command 走内置 Contains 过滤
        var (plain, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(Combobox<string>.Options)] = frameworkOptions,
        });
        await OpenPopupAsync(plain);
        Assert.Null(plain.FindComponent<Command>().FilterFunction);
    }

    [Fact]
    public async Task 关闭弹层应仅在有搜索时复位查询并通知消费方()
    {
        var reports = new List<string>();
        var (renderer, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(Combobox<string>.Options)] = frameworkOptions,
            [nameof(Combobox<string>.SearchQuery)] = "blaz",
            [nameof(Combobox<string>.SearchQueryChanged)] =
                EventCallback.Factory.Create<string>(new object(), reports.Add),
        });

        // 打开后沿基元上下文 Close() 收起（与点击外部/选项同路：Popover 的
        // HandleContextStateChanged 会把变化播给消费者的 OpenChanged）。
        // 关闭沿把非空查询复位为空串并回播。
        await OpenPopupAsync(renderer);
        await renderer.Dispatcher.InvokeAsync(() => ContextOf(renderer).Close());
        await SettleAsync(renderer);

        Assert.Equal([""], reports);
        Assert.False(ContextOf(renderer).IsOpen);

        // 无搜索的关闭沿不回播（七页分页选择回归守卫）：重开再关，报告不再增加
        await renderer.Dispatcher.InvokeAsync(() => renderer.RerenderRootAsync(1, new Dictionary<string, object?>
        {
            [nameof(Combobox<string>.Options)] = frameworkOptions,
            [nameof(Combobox<string>.SearchQuery)] = string.Empty,
            [nameof(Combobox<string>.SearchQueryChanged)] =
                EventCallback.Factory.Create<string>(new object(), reports.Add),
        }));
        await SettleAsync(renderer);
        await OpenPopupAsync(renderer);
        await renderer.Dispatcher.InvokeAsync(() => ContextOf(renderer).Close());
        await SettleAsync(renderer);
        Assert.Equal([""], reports);
    }

    // ------------------------------------------------------------------
    // 参数面：禁用/必填/宽度/透传
    // ------------------------------------------------------------------

    [Fact]
    public async Task 禁用与必填及附加属性应落触发器参数面()
    {
        var (renderer, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(Combobox<string>.Options)] = frameworkOptions,
            [nameof(Combobox<string>.Disabled)] = true,
            [nameof(Combobox<string>.Required)] = true,
            [nameof(Combobox<string>.AriaDescribedBy)] = "hint-1",
            [nameof(Combobox<string>.Class)] = "atom:w-[300px]",
            ["data-testid"] = "cb",
        });

        var markup = renderer.Markup();
        Assert.Contains("disabled", markup);
        Assert.Contains("aria-required=\"true\"", markup);
        Assert.Contains("aria-describedby=\"hint-1\"", markup);
        Assert.Contains("data-testid=\"cb\"", markup); // 未匹配属性落根 div
        Assert.Contains("atom:w-[300px]", markup);    // Class 压过默认 PopoverWidth
        Assert.DoesNotContain("atom:w-[200px]", markup);
    }

    [Fact]
    public async Task MatchTriggerWidth应改走触发宽度且内容免横向滚动类()
    {
        var (renderer, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(Combobox<string>.Options)] = frameworkOptions,
            [nameof(Combobox<string>.MatchTriggerWidth)] = true,
        });

        await OpenPopupAsync(renderer);
        var markup = renderer.Markup();
        // MatchTriggerWidth=true 时内容类只剩 !p-0（PopoverWidth 从内容侧撤下）
        // 触发器按钮在 Class 为空时仍带 PopoverWidth—— ButtonCssClass 原样，勿动。
        Assert.Contains("atom:!p-0", markup);
        Assert.Contains("atom:w-full", markup); // PopoverContent 的宽度对齐沿
    }

    [Fact]
    public async Task 加载沿应透传给命令列表并显形指示器()
    {
        var (renderer, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(Combobox<string>.Options)] = frameworkOptions,
            [nameof(Combobox<string>.IsLoading)] = true,
            [nameof(Combobox<string>.EndOfListMessage)] = "已全部加载",
        });

        await OpenPopupAsync(renderer);
        var markup = renderer.Markup();
        Assert.Contains("role=\"status\"", markup); // 加载指示器压过尾部消息
        Assert.DoesNotContain("已全部加载", markup);
    }

    // ------------------------------------------------------------------
    // 快照
    // ------------------------------------------------------------------

    [Fact]
    public async Task 关闭态触发器与隐藏注册通道快照应与基线一致()
    {
        var (renderer, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(Combobox<string>.ChildContent)] = CompositeContent(),
            [nameof(Combobox<string>.Value)] = "blazor",
        });

        await Verify(Scrub(renderer.Markup()));
    }

    [Fact]
    public async Task 打开态弹层结构快照应与基线一致()
    {
        var (renderer, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(Combobox<string>.Options)] = frameworkOptions,
            [nameof(Combobox<string>.Value)] = "react",
        });

        await OpenPopupAsync(renderer);

        await Verify(Scrub(renderer.Markup()));
    }
}
