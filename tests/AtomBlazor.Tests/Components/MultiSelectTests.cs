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
/// MultiSelect 家族测试：受控选择/回填与展示文本缓存、Badge 溢出 "+N more"(More)、
/// SelectAll 只作用过滤后条目、Clear 回填 null、Search 内部过滤与 SearchQueryChanged 旁路、
/// 禁用与必填写入面、复合模式 MultiSelectItem 登记/切换/禁用项拒击与无父级拒绝初始化、
/// multiselect.js 初始化/摘除沿——barrel import 与 multiselect.setupMultiSelectInput 载荷
/// （ElementReference + DotNetObjectReference + "{Id}-search"/"{Id}-listbox" 稳定 id）
/// OnLoadMore 经 elementUtils.observeNearBottom 挂懒加载观察，HandleEscape/DisposeAsync
/// 触发 removeMultiSelectInput（headless 下真实 DOM 键盘沿不可用，由 node 侧
/// multiselect-module.test.mjs 覆盖）。
/// 快照含 multiselect GUID id 与 popover-N 短 id，先经正则归一化。
/// </summary>
public class MultiSelectTests
{
    /// <summary>
    /// 归一化 multiselect GUID 稳定 id 与 popover-N 自增短 id，使快照可确定。
    /// </summary>
    private static string Scrub(string markup) =>
        Regex.Replace(
            Regex.Replace(markup, "multiselect-[0-9a-f]{32}", "multiselect-<id>"),
            "popover-[0-9]+", "popover-<n>");

    /// <summary>Options 模式数据：值小写、文本首字母大写，供过滤与展示文本断言。</summary>
    private static readonly SelectOption<string>[] defaultOptions =
    [
        new("alpha", "Alpha"),
        new("beta", "Beta"),
        new("gamma", "Gamma"),
    ];

    /// <summary>四选项数据：供溢出徽标与快照场景复用。</summary>
    private static readonly SelectOption<string>[] fourOptions =
    [
        new("alpha", "Alpha"),
        new("beta", "Beta"),
        new("gamma", "Gamma"),
        new("delta", "Delta"),
    ];

    /// <summary>单选项数据：模拟异步过滤"换页"后不再包含已选值的情形。</summary>
    private static readonly SelectOption<string>[] onlyAlpha = [new("alpha", "Alpha")];

    // 常用受控值集合（CA1861：常量数组提为静态字段）
    private static readonly string[] fourSelectedValues = ["alpha", "beta", "gamma", "delta"];
    private static readonly string[] twoSelectedValues = ["alpha", "beta"];
    private static readonly string[] oneAlphaValue = ["alpha"];
    private static readonly string[] oneBetaValue = ["beta"];
    private static readonly string[] oneXValue = ["x"];

    /// <summary>
    /// 构造带全套基元服务的渲染器（门户/定位/焦点/本地化器），与 CascaderTests 同构。
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

    /// <summary>强制重渲染门户宿主（根 0），让弹层内容确定性地进入渲染树。</summary>
    private static async Task RerenderPortalAsync(ComponentTestRenderer renderer)
    {
        await SettleAsync(renderer);
        await renderer.Dispatcher.InvokeAsync(() => renderer.RerenderRootAsync(0, new Dictionary<string, object?>()));
        await SettleAsync(renderer);
    }

    /// <summary>反射取 MultiSelect 内基元 Popover 的上下文（先例：CascaderTests 的 ContextOf）。</summary>
    private static AtomBlazor.Primitives.PopoverContext ContextOf(ComponentTestRenderer renderer) =>
        (AtomBlazor.Primitives.PopoverContext)typeof(AtomBlazor.Primitives.PopoverPrimitive)
            .GetField("_context", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(renderer.FindComponent<AtomBlazor.Primitives.PopoverPrimitive>())!;

    /// <summary>挂载 PortalHost（根 0）+ MultiSelect&lt;string&gt;（根 1），返回实例与参数字典。</summary>
    private static async Task<(
        ComponentTestRenderer Renderer,
        NoopJavaScript Js,
        MultiSelect<string> MultiSelect,
        Dictionary<string, object?> Params)> MountAsync(Dictionary<string, object?> extra)
    {
        var (renderer, js) = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        var dict = new Dictionary<string, object?>
        {
            [nameof(MultiSelect<string>.Options)] = defaultOptions,
        };
        foreach (var (key, value) in extra)
        {
            dict[key] = value;
        }

        var multiSelect = await renderer.MountAsync<MultiSelect<string>>(dict);
        await SettleAsync(renderer);
        return (renderer, js, multiSelect, dict);
    }

    /// <summary>点击触发器打开弹层（关闭态下唯一 onclick 携带者），并让 MultiSelect 自身
    /// 再渲染一轮，触发 OnAfterRenderAsync 的 setup 沿。</summary>
    private static async Task OpenAsync(
        ComponentTestRenderer renderer, Dictionary<string, object?> dict)
    {
        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs()));
        await RerenderPortalAsync(renderer);
        // Popover 子树重渲染不带 MultiSelect 的 OnAfterRender——按 CascaderTests 先例
        // 显式重渲染根 1，让 setup 沿在此落定。
        await renderer.Dispatcher.InvokeAsync(() => renderer.RerenderRootAsync(1, dict));
        await RerenderPortalAsync(renderer);
    }

    // ------------------------------------------------------------------
    // 关闭态触发器与 Badge
    // ------------------------------------------------------------------

    [Fact]
    public async Task 无选中值应显示本地化占位符与combobox无障碍面()
    {
        var (renderer, _, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(MultiSelect<string>.AriaLabel)] = "标签",
        });

        var markup = renderer.Markup();
        Assert.Contains("Select items...", markup); // Localizer["MultiSelect.Placeholder"]
        Assert.Contains("role=\"combobox\"", markup);
        Assert.Contains("aria-expanded=\"false\"", markup);
        Assert.Contains("aria-multiselectable=\"true\"", markup);
        // aria-haspopup/aria-expanded/aria-controls 由基元 PopoverTrigger 自有声明压过
        // 触发器透传值——渲染面以基元为准（同构行为）。
        Assert.Contains("aria-haspopup", markup);
        Assert.Contains("aria-label=\"标签\"", markup);
        Assert.Contains("aria-controls", markup);
    }

    [Fact]
    public async Task 选中值应渲染徽标溢出与本地化删除及清除按钮()
    {
        var (renderer, _, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(MultiSelect<string>.Values)] = fourSelectedValues,
            [nameof(MultiSelect<string>.Options)] = fourOptions,
        });

        var markup = renderer.Markup();
        // MaxDisplayTags 默认 3：第 4 个值折进 "+1 more"（Localizer["MultiSelect.More", 1]）
        Assert.Contains("Alpha", markup);
        Assert.Contains("Beta", markup);
        Assert.Contains("Gamma", markup);
        Assert.DoesNotContain(">Delta<", markup);
        Assert.Contains("+1 more", markup);
        Assert.Contains("aria-label=\"Remove Alpha\"", markup); // Localizer["MultiSelect.Remove", …]
        Assert.Contains("aria-label=\"Clear\"", markup); // 触发器区清除按钮
    }

    [Fact]
    public async Task 点击徽标删除按钮应回填剔除该项的列表()
    {
        var reports = new List<IEnumerable<string>?>();
        var (renderer, _, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(MultiSelect<string>.Values)] = twoSelectedValues,
            [nameof(MultiSelect<string>.ValuesChanged)] =
                EventCallback.Factory.Create<IEnumerable<string>?>(new object(), v => reports.Add(v)),
        });

        // 关闭态 onclick 次序：触发器(0)、徽标 Alpha 删除(1)、徽标 Beta 删除(2)、清除(3)
        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs(), occurrence: 1));
        await SettleAsync(renderer);

        Assert.Equal(["beta"], reports.Select(r => string.Join(",", r ?? Enumerable.Empty<string>())));
    }

    [Fact]
    public async Task 点击触发器清除按钮应回填null()
    {
        var reports = new List<IEnumerable<string>?>();
        var (renderer, _, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(MultiSelect<string>.Values)] = oneAlphaValue,
            [nameof(MultiSelect<string>.ValuesChanged)] =
                EventCallback.Factory.Create<IEnumerable<string>?>(new object(), v => reports.Add(v)),
        });

        // 关闭态 onclick 次序：触发器(0)、徽标删除(1)、清除(2)
        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs(), occurrence: 2));
        await SettleAsync(renderer);

        var cleared = Assert.Single(reports);
        Assert.Null(cleared);
    }

    [Fact]
    public async Task 必填与禁用应落无障碍及参数面()
    {
        var (renderer, _, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(MultiSelect<string>.Required)] = true,
            [nameof(MultiSelect<string>.Disabled)] = true,
        });

        var markup = renderer.Markup();
        Assert.Contains("aria-required=\"true\"", markup); // 触发器 combobox
        Assert.Contains("disabled", markup); // 触发按钮 disabled 属性
    }

    // ------------------------------------------------------------------
    // 打开态：搜索框 / 列表 / SelectAll / 页脚
    // ------------------------------------------------------------------

    [Fact]
    public async Task 打开后应挂载搜索框列表框SelectAll与页脚()
    {
        var (renderer, _, _, dict) = await MountAsync(new Dictionary<string, object?>());
        await OpenAsync(renderer, dict);

        var markup = renderer.Markup();
        Assert.Matches(@"id=""multiselect-[0-9a-f]{32}-search""", markup);
        Assert.Contains("Search...", markup); // Localizer["MultiSelect.SearchPlaceholder"]
        Assert.Contains("role=\"listbox\"", markup);
        Assert.Contains("aria-multiselectable=\"true\"", markup);
        Assert.Contains("Select All", markup); // Localizer["MultiSelect.SelectAll"]
        Assert.Contains("aria-selected=\"false\"", markup); // 未选中项
        Assert.Contains("aria-expanded=\"true\"", markup);
    }

    [Fact]
    public async Task 点击选项应切换回填并缓存展示文本供换页后徽标使用()
    {
        var reports = new List<IEnumerable<string>?>();
        var (renderer, _, _, dict) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(MultiSelect<string>.ShowSelectAll)] = false,
            [nameof(MultiSelect<string>.ValuesChanged)] =
                EventCallback.Factory.Create<IEnumerable<string>?>(new object(), v => reports.Add(v)),
        });
        await OpenAsync(renderer, dict);

        // 打开态 onclick 次序：选项 Alpha(0)、其复选框(1)、Beta(2)、其复选框(3)、
        // Gamma(4)、其复选框(5)、页脚 Clear(6)、页脚 Close(7)、触发器(8)
        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs(), occurrence: 2));
        await SettleAsync(renderer);

        Assert.Equal(["beta"], reports.Select(r => string.Join(",", r ?? Enumerable.Empty<string>())));

        // 模拟异步过滤换页：Options 变成不含 beta 的新数组，徽标文本走 _displayTextCache
        dict[nameof(MultiSelect<string>.Values)] = oneBetaValue;
        dict[nameof(MultiSelect<string>.Options)] = onlyAlpha;
        await renderer.Dispatcher.InvokeAsync(() => renderer.RerenderRootAsync(1, dict));
        await SettleAsync(renderer);

        Assert.Contains("Beta", renderer.Markup()); // 缓存兜底，而非回退 ToString() 的 "beta"
    }

    [Fact]
    public async Task SelectAll应整体勾选与取消且只作用可见项()
    {
        var reports = new List<IEnumerable<string>?>();
        var (renderer, _, _, dict) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(MultiSelect<string>.ValuesChanged)] =
                EventCallback.Factory.Create<IEnumerable<string>?>(new object(), v => reports.Add(v)),
        });
        await OpenAsync(renderer, dict);

        // occurrence 0 = Select All 行（门户内容最先渲染，其后才是根 1 触发器）
        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs(), occurrence: 0));
        await SettleAsync(renderer);

        var all = Assert.Single(reports);
        Assert.Equal(["alpha", "beta", "gamma"], all);

        // 再击：全部取消 → 回填 null
        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs(), occurrence: 0));
        await SettleAsync(renderer);

        Assert.Equal(2, reports.Count);
        Assert.Null(reports[1]);
    }

    [Fact]
    public async Task 键入搜索应在内部过滤选项文本()
    {
        var (renderer, _, _, dict) = await MountAsync(new Dictionary<string, object?>());
        await OpenAsync(renderer, dict);

        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("oninput", new ChangeEventArgs { Value = "ga" }));
        await RerenderPortalAsync(renderer);

        var markup = renderer.Markup();
        Assert.Contains("Gamma", markup);
        Assert.DoesNotContain(">Alpha<", markup);
        Assert.DoesNotContain(">Beta<", markup);
    }

    [Fact]
    public async Task SearchQueryChanged应旁路内部过滤并上报搜索词()
    {
        var queries = new List<string>();
        var (renderer, _, _, dict) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(MultiSelect<string>.SearchQueryChanged)] =
                EventCallback.Factory.Create<string>(new object(), q => queries.Add(q)),
        });
        await OpenAsync(renderer, dict);

        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("oninput", new ChangeEventArgs { Value = "zzz" }));
        await RerenderPortalAsync(renderer);

        Assert.Equal(["zzz"], queries);
        var markup = renderer.Markup();
        Assert.Contains("Alpha", markup); // 外部过滤生效：条目列表照单全收
        Assert.Contains("Gamma", markup);
    }

    // ------------------------------------------------------------------
    // 复合模式：MultiSelectItem
    // ------------------------------------------------------------------

    /// <summary>ChildContent：一个可用项 x + 一个禁用项 y。</summary>
    private static RenderFragment CompositionalItems => builder =>
    {
        builder.OpenComponent<MultiSelectItem<string>>(0);
        builder.AddAttribute(1, nameof(MultiSelectItem<string>.Value), "x");
        builder.AddAttribute(2, nameof(MultiSelectItem<string>.Text), "甲项");
        builder.CloseComponent();

        builder.OpenComponent<MultiSelectItem<string>>(3);
        builder.AddAttribute(4, nameof(MultiSelectItem<string>.Value), "y");
        builder.AddAttribute(5, nameof(MultiSelectItem<string>.Text), "乙项");
        builder.AddAttribute(6, nameof(MultiSelectItem<string>.Disabled), true);
        builder.CloseComponent();
    };

    [Fact]
    public async Task 复合模式应登记条目文本点击切换且禁用项拒绝点击()
    {
        var reports = new List<IEnumerable<string>?>();
        var (renderer, _, _, dict) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(MultiSelect<string>.Options)] = null,
            [nameof(MultiSelect<string>.ChildContent)] = CompositionalItems,
            [nameof(MultiSelect<string>.ValuesChanged)] =
                EventCallback.Factory.Create<IEnumerable<string>?>(new object(), v => reports.Add(v)),
        });
        await OpenAsync(renderer, dict);

        var markup = renderer.Markup();
        Assert.Contains("甲项", markup);
        Assert.Contains("aria-disabled=\"true\"", markup); // 乙项禁用面

        // 打开态 onclick 次序：SelectAll 行(0)、其复选框(1)、甲项行(2)、其复选框(3)、
        // 乙项行(4)、其复选框(5)、页脚 Clear(6)、Close(7)、触发器(8)
        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs(), occurrence: 2));
        await SettleAsync(renderer);
        Assert.Equal(["x"], reports.Select(r => string.Join(",", r ?? Enumerable.Empty<string>())));

        // 禁用项：点击行不回填
        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs(), occurrence: 4));
        await SettleAsync(renderer);
        Assert.Single(reports);

 // 关闭态徽标文本取自登记表的注册值
        dict[nameof(MultiSelect<string>.Values)] = oneXValue;
        dict[nameof(MultiSelect<string>.Options)] = null;
        await renderer.Dispatcher.InvokeAsync(() => renderer.RerenderRootAsync(1, dict));
        await SettleAsync(renderer);
        Assert.Contains("甲项", renderer.Markup());
    }

    [Fact]
    public void MultiSelectItem缺少父级应在初始化时拒绝并给出中文提示()
    {
        // BL0005：孤立实例不经渲染器挂载，参数只能按普通属性直赋——以反射绕行
        // 本测试守护的正是"无父级时 OnInitialized 拒绝初始化"这条约束。
        var item = new MultiSelectItem<string>();
        typeof(MultiSelectItem<string>).GetProperty(nameof(MultiSelectItem<string>.Value))!.SetValue(item, "x");
        typeof(MultiSelectItem<string>).GetProperty(nameof(MultiSelectItem<string>.Text))!.SetValue(item, "孤项");
        var onInitialized = typeof(ComponentBase)
            .GetMethod("OnInitialized", BindingFlags.Instance | BindingFlags.NonPublic)!;

        var ex = Assert.Throws<TargetInvocationException>(() => onInitialized.Invoke(item, null));
        var invalid = Assert.IsType<InvalidOperationException>(ex.InnerException);
        Assert.Contains("必须用在", invalid.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(MultiSelect<string>), invalid.Message, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------
    // multiselect.js interop 沿（barrel 门面 + JSInvokable 直调）
    // ------------------------------------------------------------------

    [Fact]
    public async Task 打开后应导入共享聚合模块并声明multiselect初始化载荷()
    {
        var (renderer, js, _, dict) = await MountAsync(new Dictionary<string, object?>());
        await OpenAsync(renderer, dict);

 // 门面沿：加载的是 components-core.js barrel，而非 multiselect.js 原路径（偏差登记）。
        // 基元服务（门户/定位）也各自 import 自己的模块路径，故谓词限定到 barrel 一枚。
        var import = Assert.Single(js.Invocations,
            i => i.Identifier == "import" && ((string?)i.Args?[0])?.Contains("components-core.js") == true);
        Assert.Contains("components-core.js", Assert.IsType<string>(import.Args![0]));

        var (_, args) = Assert.Single(js.Invocations, i => i.Identifier == "multiselect.setupMultiSelectInput");
        Assert.IsType<ElementReference>(args![0]);
        Assert.IsType<DotNetObjectReference<MultiSelect<string>>>(args[1]);
        var id = Assert.IsType<string>(args[2]);
        Assert.Matches(@"^multiselect-[0-9a-f]{32}-search$", id);
        Assert.Equal(id.Replace("-search", "-listbox", StringComparison.Ordinal), Assert.IsType<string>(args[3]));
    }

    [Fact]
    public async Task OnLoadMore应挂近底观察且JsOnNearBottom受IsLoading闸控()
    {
        var loadMoreCount = 0;
        var (renderer, js, multiSelect, dict) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(MultiSelect<string>.OnLoadMore)] =
                EventCallback.Factory.Create(new object(), () =>
                {
                    loadMoreCount++;
                    return Task.CompletedTask;
                }),
        });
        await OpenAsync(renderer, dict);

        // elementUtils.observeNearBottom 已在 primitives barrel 就位（直连名，不经 multiselect 门面）
        var (_, observeArgs) = Assert.Single(js.Invocations,
            i => i.Identifier == "elementUtils.observeNearBottom");
        Assert.IsType<ElementReference>(observeArgs![0]);
        Assert.Equal(nameof(MultiSelect<string>.JsOnNearBottom), Assert.IsType<string>(observeArgs[2]));
        Assert.Equal(80.0, Assert.IsType<double>(observeArgs[3]));

        await renderer.Dispatcher.InvokeAsync(async () => await multiSelect.JsOnNearBottom());
        Assert.Equal(1, loadMoreCount);

        // IsLoading 期间抑制追加（BL0005：经属性写入等价于参数面，走反射）
        typeof(MultiSelect<string>).GetProperty(nameof(MultiSelect<string>.IsLoading))!
            .SetValue(multiSelect, true);
        await renderer.Dispatcher.InvokeAsync(async () => await multiSelect.JsOnNearBottom());
        Assert.Equal(1, loadMoreCount);
    }

    [Fact]
    public async Task Escape关闭应摘除JS监听并携带搜索框稳定id()
    {
        var (renderer, js, multiSelect, dict) = await MountAsync(new Dictionary<string, object?>());
        await OpenAsync(renderer, dict);

        var (_, setupArgs) = Assert.Single(js.Invocations, i => i.Identifier == "multiselect.setupMultiSelectInput");
        var searchId = Assert.IsType<string>(setupArgs![2]);

        // HandleEscape 是 JS 键盘沿的 C# 对端（headless 无真实键盘，直调入口）
        await renderer.Dispatcher.InvokeAsync(async () => await multiSelect.HandleEscape());
        await SettleAsync(renderer);
        await renderer.Dispatcher.InvokeAsync(() => renderer.RerenderRootAsync(1, dict));
        await SettleAsync(renderer);

        var (_, removeArgs) = Assert.Single(js.Invocations, i => i.Identifier == "multiselect.removeMultiSelectInput");
        Assert.Equal(searchId, Assert.IsType<string>(removeArgs![0]));
        Assert.False(ContextOf(renderer).IsOpen);
    }

    [Fact]
    public async Task 释放应兜底摘除并对已断电路幂等()
    {
        var (renderer, js, multiSelect, dict) = await MountAsync(new Dictionary<string, object?>());
        await OpenAsync(renderer, dict);

        // 关闭弹层走一次常规清理
        await renderer.Dispatcher.InvokeAsync(async () => await multiSelect.HandleEscape());
        await SettleAsync(renderer);
        await renderer.Dispatcher.InvokeAsync(() => renderer.RerenderRootAsync(1, dict));
        await SettleAsync(renderer);
        Assert.Single(js.Invocations, i => i.Identifier == "multiselect.removeMultiSelectInput");

        // DisposeAsync 的 CleanupJsAsync 兜底再摘一次；Noop 桩永不抛错
        // 真实断线由 catch 谓词（JSDisconnectedException 等）兜住。
        await ((IAsyncDisposable)multiSelect).DisposeAsync();
        Assert.Equal(2, js.Invocations.Count(i => i.Identifier == "multiselect.removeMultiSelectInput"));
    }

    // ------------------------------------------------------------------
    // 快照
    // ------------------------------------------------------------------

    [Fact]
    public async Task 关闭态徽标溢出与清除结构快照应与基线一致()
    {
        var (renderer, _, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(MultiSelect<string>.Values)] = fourSelectedValues,
            [nameof(MultiSelect<string>.Options)] = fourOptions,
            [nameof(MultiSelect<string>.AriaLabel)] = "snap",
        });

        await Verify(Scrub(renderer.Markup()));
    }

    [Fact]
    public async Task 打开态搜索列表与页脚结构快照应与基线一致()
    {
        var (renderer, _, _, dict) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(MultiSelect<string>.Values)] = oneBetaValue,
        });
        await OpenAsync(renderer, dict);

        await Verify(Scrub(renderer.Markup()));
    }
}
