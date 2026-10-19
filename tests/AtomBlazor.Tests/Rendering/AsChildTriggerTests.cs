using AtomBlazor.Primitives;
using AtomBlazor;
using AtomBlazor.Primitives.Utilities;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Xunit;
using AtomBlazor.Tests.Rendering;

namespace AtomBlazor.Tests.Rendering;

/// <summary>
/// 带 AsChild 模式的触发器，以及该模式静默失效时该响的告警。
/// </summary>
/// <remarks>
/// <para>开着 AsChild 时——这些触发器多数默认如此——
/// 触发器自身什么都不渲染，而是把行为交给读取它的子元素。
/// 文本、图标或普通元素读不了它，
/// 于是触发器不做事、样式类也无从落地。
/// 这些测试钉住随之而来的三件事：该错误会在 Development 下被报告；
/// 用对子元素时不被报告且工作正常；
/// 而 AsChild="false" 则给出带着类、能工作的真按钮。</para>
/// <para>Development 判定在进程内有缓存，
/// 所以测试直接强制它，而非注册宿主环境。
/// 本类设置它、用完放回，
/// 而 xUnit 对同一个类的测试是逐条顺序跑的。</para>
/// </remarks>
public sealed class AsChildTriggerTests : IDisposable
{
    private const string warningName = "TriggerContextUnconsumed";

    public AsChildTriggerTests()
    {
        DevelopmentEnvironment.OverrideForTests = true;
    }

    public void Dispose() => DevelopmentEnvironment.OverrideForTests = null;

    /// <summary>所有以点击打开、关闭或切换的触发器，附各自的简称。</summary>
    public static TheoryData<string> ClickTriggers =>
    [
        "Collapsible",
        "Popover",
        "DialogTrigger",
        "DialogClose",
        "SheetTrigger",
        "SheetClose",
        "DropdownMenu",
        "AlertDialogTrigger",
        "AlertDialogAction",
        "AlertDialogCancel",
        "DrawerTrigger",
        "DrawerClose",
    ];

    [Theory]
    [MemberData(nameof(ClickTriggers))]
    public async Task APlainTextChildLogsTheWarning(string name)
    {
        var trigger = Case(name);

        var run = await RenderAsync(trigger, Child.Text, asChild: InAsChildMode(trigger));

        var warning = Assert.Single(run.Warnings);
        Assert.Equal(LogLevel.Warning, warning.Level);
        Assert.Contains(trigger.NamedAs, warning.Message, StringComparison.Ordinal);
        Assert.Contains("把 AsChild 设为 \"false\"", warning.Message, StringComparison.Ordinal);
        Assert.Contains("在其内放入 Button", warning.Message, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(ClickTriggers))]
    public async Task ABbButtonChildIsNotReportedAndWorks(string name)
    {
        var trigger = Case(name);

        var run = await RenderAsync(trigger, Child.Button, asChild: InAsChildMode(trigger));
        await run.Renderer.Dispatcher.InvokeAsync(() => run.Renderer.DispatchAsync("onclick", new MouseEventArgs()));

        Assert.Empty(run.Warnings);
        Assert.Equal(new[] { !trigger.StartsOpen }, run.OpenChanges);
    }

    [Theory]
    [MemberData(nameof(ClickTriggers))]
    public async Task AsChildFalseRendersAButtonThatCarriesTheClassAndWorks(string name)
    {
        var trigger = Case(name);

        var run = await RenderAsync(trigger, Child.Text, asChild: false, pageClass: "page-class");
        var markup = await run.Renderer.Dispatcher.InvokeAsync(run.Renderer.Markup);
        await run.Renderer.Dispatcher.InvokeAsync(() => run.Renderer.DispatchAsync("onclick", new MouseEventArgs()));

        Assert.Matches("<button[^>]*class=\"[^\"]*page-class", markup);
        Assert.Empty(run.Warnings);
        Assert.Equal(new[] { !trigger.StartsOpen }, run.OpenChanges);
    }

    [Fact]
    public async Task TheWarningNamesTheClassThePageSet()
    {
        // 样式类是这个错误里读者看得见的那一半：页面上一个被定了样
        // 却毫无样式的触发器。告警必须说清它去了哪里。
        var run = await RenderAsync(Case("Collapsible"), Child.Text, pageClass: "flex items-center");

        var warning = Assert.Single(run.Warnings);
        Assert.Contains("它还被赋予了 class。", warning.Message, StringComparison.Ordinal);
        Assert.Contains("它，因此被忽略。", warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheDrawerNamesTheClassThePageSetToo()
    {
        // 抽屉把 Class 作为自有参数而非属性接收，所以它得主动
        // 把 Class 加进上报内容，否则将什么都不上报。
        var run = await RenderAsync(Case("DrawerTrigger"), Child.Text, asChild: true, pageClass: "w-full");

        var warning = Assert.Single(run.Warnings);
        Assert.Contains("它还被赋予了 class。", warning.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Collapsible")]
    [InlineData("Popover")]
    [InlineData("DropdownMenu")]
    [InlineData("DrawerTrigger")]
    public async Task TheWarningDoesNotBlameTheTriggersOwnStyling(string name)
    {
        // 带样式的触发器自带聚焦环类。若把这些也写进告警，
        // 读起来会像是页面自己做的事。
        var trigger = Case(name);
        var run = await RenderAsync(trigger, Child.Text, asChild: InAsChildMode(trigger));

        var warning = Assert.Single(run.Warnings);
        Assert.DoesNotContain("它还被赋予了", warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NothingIsLoggedOutsideDevelopment()
    {
        DevelopmentEnvironment.OverrideForTests = false;

        var run = await RenderAsync(Case("Collapsible"), Child.Text);

        Assert.Empty(run.Warnings);
    }

    [Theory]
    [InlineData("Tooltip")]
    [InlineData("HoverCard")]
    public async Task TheHoverTriggersUseTheSameWarning(string name)
    {
        var trigger = Case(name);

        var plain = await RenderAsync(trigger, Child.Text, asChild: true);
        var button = await RenderAsync(trigger, Child.Button, asChild: true);

        var warning = Assert.Single(plain.Warnings);
        Assert.Contains(trigger.NamedAs, warning.Message, StringComparison.Ordinal);
        Assert.Empty(button.Warnings);
    }

    [Fact]
    public void OnlyAttributesWithAValueAreNamed()
    {
        Assert.Equal(string.Empty, AsChildDiagnostics.Ignored(null));
        Assert.Equal(string.Empty, AsChildDiagnostics.Ignored(new Dictionary<string, object> { ["class"] = null!, ["id"] = "" }));

        var two = AsChildDiagnostics.Ignored(new Dictionary<string, object> { ["class"] = "x", ["data-test"] = "y" });
        Assert.Contains("它还被赋予了 class, data-test。", two, StringComparison.Ordinal);
        Assert.Contains("它们，因此被忽略。", two, StringComparison.Ordinal);
    }

    private enum Child
    {
        Text,
        Button,
    }

    /// <summary>一个触发器与其父件，以及点击它应有的效果。</summary>
    /// <param name="Parent">拥有开合状态的组件。</param>
    /// <param name="Trigger">触发器。</param>
    /// <param name="NamedAs">告警中对它的称呼。</param>
    /// <param name="StartsOpen">父件是否以打开态起步——关闭用例需要。</param>
    /// <param name="ClassParameter">触发器接收样式类的方式：自有参数还是属性。</param>
    /// <param name="AsChildByDefault">触发器是否默认处于 AsChild 模式。</param>
    private sealed record TriggerCase(Type Parent, Type Trigger, string NamedAs, bool StartsOpen, string ClassParameter, bool AsChildByDefault = true);

    /// <summary>默认即 AsChild 的触发器不加干预；不默认的则显式开启。</summary>
    private static bool? InAsChildMode(TriggerCase trigger) => trigger.AsChildByDefault ? null : true;

    private static TriggerCase Case(string name) => name switch
    {
        "Collapsible" => new(typeof(Collapsible), typeof(CollapsibleTrigger), "CollapsibleTrigger", false, "Class"),
        "Popover" => new(typeof(Popover), typeof(PopoverTrigger), "PopoverTrigger", false, "class"),
        "DialogTrigger" => new(typeof(Dialog), typeof(DialogTrigger), "DialogTrigger", false, "class"),
        "DialogClose" => new(typeof(Dialog), typeof(DialogClose), "DialogClose", true, "class"),
        "SheetTrigger" => new(typeof(Sheet), typeof(SheetTrigger), "SheetTrigger", false, "class"),
        "SheetClose" => new(typeof(Sheet), typeof(SheetClose), "SheetClose", true, "class"),
        "DropdownMenu" => new(typeof(DropdownMenu), typeof(DropdownMenuTrigger), "DropdownMenuTrigger", false, "Class"),
        "AlertDialogTrigger" => new(typeof(AlertDialog), typeof(AlertDialogTrigger), "AlertDialogTrigger", false, "class"),
        "AlertDialogAction" => new(typeof(AlertDialog), typeof(AlertDialogAction), "AlertDialogAction", true, "class"),
        "AlertDialogCancel" => new(typeof(AlertDialog), typeof(AlertDialogCancel), "AlertDialogCancel", true, "class"),
        "DrawerTrigger" => new(typeof(Drawer), typeof(DrawerTrigger), "DrawerTrigger", false, "Class", AsChildByDefault: false),
        "DrawerClose" => new(typeof(Drawer), typeof(DrawerClose), "DrawerClose", true, "Class", AsChildByDefault: false),
        "Tooltip" => new(typeof(Tooltip), typeof(TooltipTrigger), "TooltipTrigger", false, "class", AsChildByDefault: false),
        "HoverCard" => new(typeof(HoverCard), typeof(HoverCardTrigger), "HoverCardTrigger", false, "Class"),
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, null),
    };

    private sealed record Run(ComponentTestRenderer Renderer, List<bool> OpenChanges, IReadOnlyList<LogEntry> Warnings);

    private static async Task<Run> RenderAsync(TriggerCase trigger, Child child, bool? asChild = null, string? pageClass = null)
    {
        var logs = new CapturingLoggerFactory();
        var services = new ServiceCollection().AddAtomBlazorTestDefaults();
        services.AddSingleton<ILoggerFactory>(logs);
        services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        services.AddSingleton<IJSRuntime, NoopJavaScript>();
        services.AddAtomBlazorComponents();

        var provider = services.BuildServiceProvider();
        var renderer = new ComponentTestRenderer(provider, NullLoggerFactory.Instance);
        var openChanges = new List<bool>();

        var parameters = new Dictionary<string, object?>
        {
            ["OpenChanged"] = EventCallback.Factory.Create<bool>(new object(), openChanges.Add),
            ["ChildContent"] = (RenderFragment)(builder => BuildTrigger(builder, trigger, child, asChild, pageClass)),
        };

        if (trigger.StartsOpen)
        {
            parameters["DefaultOpen"] = true;
        }

        await renderer.Dispatcher.InvokeAsync(() => MountAsync(renderer, trigger.Parent, parameters));

        var warnings = logs.Entries.Where(e => e.Id.Name == warningName).ToList();
        return new Run(renderer, openChanges, warnings);
    }

    private static Task MountAsync(ComponentTestRenderer renderer, Type parent, Dictionary<string, object?> parameters)
    {
        var mount = typeof(ComponentTestRenderer).GetMethod(nameof(ComponentTestRenderer.MountAsync), System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        return (Task)mount.MakeGenericMethod(parent).Invoke(renderer, [parameters])!;
    }

    private static void BuildTrigger(RenderTreeBuilder builder, TriggerCase trigger, Child child, bool? asChild, string? pageClass)
    {
        builder.OpenComponent(0, trigger.Trigger);

        if (asChild is { } value)
        {
            builder.AddComponentParameter(1, "AsChild", value);
        }

        if (pageClass is not null)
        {
            builder.AddComponentParameter(2, trigger.ClassParameter, pageClass);
        }

        builder.AddComponentParameter(3, "ChildContent", (RenderFragment)(inner =>
        {
            if (child == Child.Button)
            {
                inner.OpenComponent<Button>(0);
                inner.AddComponentParameter(1, nameof(Button.ChildContent), (RenderFragment)(text => text.AddContent(0, "Go")));
                inner.CloseComponent();
            }
            else
            {
                inner.AddContent(2, "Go");
            }
        }));

        builder.CloseComponent();
    }

    private sealed record LogEntry(string Category, LogLevel Level, EventId Id, string Message);

    private sealed class CapturingLoggerFactory : ILoggerFactory
    {
        public List<LogEntry> Entries { get; } = [];

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(this, categoryName);

        public void AddProvider(ILoggerProvider provider)
        {
        }

        public void Dispose()
        {
        }

        private sealed class CapturingLogger(CapturingLoggerFactory owner, string category) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                lock (owner.Entries)
                {
                    owner.Entries.Add(new LogEntry(category, logLevel, eventId, formatter(state, exception)));
                }
            }
        }
    }
}
