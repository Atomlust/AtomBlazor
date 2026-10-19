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
/// Collapsible 测试（提前拉取，供 FormSection 折叠特性使用）：
/// 关闭态 data-state/aria-hidden、ForceMount 默认挂载内容、点击触发器切换、
/// DefaultOpen 初始展开、受控 Open/OpenChanged 双向沿、禁用态拒绝切换。
/// 触发器经 AsChild 把行为交给 Button，headless 下直发按钮 onclick 即走通链路。
/// </summary>
public class CollapsibleTests
{
    private static ComponentTestRenderer CreateRenderer()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(new NoopJavaScript());
        services.AddSingleton<ILocalizer, DefaultLocalizer>();
        return new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance);
    }

    /// <summary>基础内容片段：触发器按钮 + 一段可折叠文本。</summary>
    private static RenderFragment Children() => builder =>
    {
        builder.OpenComponent<CollapsibleTrigger>(0);
        builder.AddAttribute(1, nameof(CollapsibleTrigger.ChildContent), (RenderFragment)(tb =>
        {
            tb.OpenComponent<Button>(0);
            tb.AddAttribute(1, nameof(Button.ChildContent), (RenderFragment)(bb => bb.AddContent(0, "切换")));
            tb.CloseComponent();
        }));
        builder.CloseComponent();

        builder.OpenComponent<CollapsibleContent>(10);
        builder.AddAttribute(11, nameof(CollapsibleContent.ChildContent), (RenderFragment)(cb =>
            cb.AddContent(0, "可折叠内容")));
        builder.CloseComponent();
    };

    private static async Task<ComponentTestRenderer> MountAsync(Dictionary<string, object?> parameters)
    {
        var renderer = CreateRenderer();
        parameters[nameof(Collapsible.ChildContent)] = Children();
        await renderer.MountAsync<Collapsible>(parameters);
        await renderer.Dispatcher.InvokeAsync(() => Task.CompletedTask);
        return renderer;
    }

    /// <summary>点击触发器按钮（关闭态唯一 onclick 携带者）。</summary>
    private static async Task ClickTriggerAsync(ComponentTestRenderer renderer)
    {
        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs()));
        await renderer.Dispatcher.InvokeAsync(() => Task.CompletedTask);
    }

    [Fact]
    public async Task 默认关闭态应渲染关闭状态与aria隐藏()
    {
        var renderer = await MountAsync(new Dictionary<string, object?>());

        var markup = renderer.Markup();
        Assert.Contains("data-state=\"closed\"", markup);
        Assert.Contains("aria-hidden=\"true\"", markup);
        Assert.Contains("可折叠内容", markup); // ForceMount 默认 true：内容保持挂载
        Assert.Contains("切换", markup);
    }

    [Fact]
    public async Task 点击触发器应切换为展开状态()
    {
        var renderer = await MountAsync(new Dictionary<string, object?>());

        await ClickTriggerAsync(renderer);

        var markup = renderer.Markup();
        Assert.Contains("data-state=\"open\"", markup);
        Assert.Contains("aria-hidden=\"false\"", markup);

        // 再点一次收起
        await ClickTriggerAsync(renderer);
        Assert.Contains("data-state=\"closed\"", renderer.Markup());
    }

    [Fact]
    public async Task DefaultOpen为真时应初始展开()
    {
        var renderer = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(Collapsible.DefaultOpen)] = true,
        });

        Assert.Contains("data-state=\"open\"", renderer.Markup());
    }

    [Fact]
    public async Task 受控模式应上报OpenChanged并跟随Open参数()
    {
        var reports = new List<bool>();
        var renderer = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(Collapsible.Open)] = false,
            [nameof(Collapsible.OpenChanged)] =
                EventCallback.Factory.Create<bool>(new object(), v => reports.Add(v)),
        });

        await ClickTriggerAsync(renderer);
        Assert.Equal([true], reports);
        // 行为：HandleOpenChanged 先翻转内部 _isOpen
        // 再上报，而 _isOpen 与 Open 的同步只在参数沿到达时（OnParametersSet）发生
        // 故父级未回写前渲染沿已翻为 open，不会被 Open=false 拉回。
        Assert.Contains("data-state=\"open\"", renderer.Markup());

        // 父级未跟进、以 Open=false 重渲染：OnParametersSet 把 _isOpen 拉回 false。
        await renderer.Dispatcher.InvokeAsync(() => renderer.RerenderRootAsync(0, new Dictionary<string, object?>
        {
            [nameof(Collapsible.Open)] = false,
        }));
        await renderer.Dispatcher.InvokeAsync(() => Task.CompletedTask);
        Assert.Contains("data-state=\"closed\"", renderer.Markup());

        // 父级跟进 Open=true 后渲染展开态。
        await renderer.Dispatcher.InvokeAsync(() => renderer.RerenderRootAsync(0, new Dictionary<string, object?>
        {
            [nameof(Collapsible.Open)] = true,
        }));
        await renderer.Dispatcher.InvokeAsync(() => Task.CompletedTask);
        var markup = renderer.Markup();
        Assert.Contains("data-state=\"open\"", markup);
        Assert.Contains("aria-hidden=\"false\"", markup);
    }

    [Fact]
    public async Task 禁用态应拒绝切换()
    {
        var renderer = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(Collapsible.Disabled)] = true,
        });

        await ClickTriggerAsync(renderer);

        Assert.Contains("data-state=\"closed\"", renderer.Markup());
    }

    [Fact]
    public async Task 默认关闭态结构快照应与基线一致()
    {
        var renderer = await MountAsync(new Dictionary<string, object?>());

        await Verify(renderer.Markup());
    }

    [Fact]
    public async Task 展开态结构快照应与基线一致()
    {
        var renderer = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(Collapsible.DefaultOpen)] = true,
        });

        await Verify(renderer.Markup());
    }
}
