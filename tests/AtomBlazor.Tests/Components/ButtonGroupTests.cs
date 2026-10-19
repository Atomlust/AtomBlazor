using AtomBlazor.Primitives;
using AtomBlazor;
using AtomBlazor.Tests.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Xunit;

namespace AtomBlazor.Tests.Components;

/// <summary>
/// ButtonGroup 家族渲染测试：组结构（data-slot/role/aria-label）、
/// 水平与垂直方向类映射、分隔符外层与内层 Separator 渲染、文本槽样式。
/// </summary>
public class ButtonGroupTests
{
    private static ComponentTestRenderer CreateRenderer()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(new NoopJavaScript());
        return new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance);
    }

    private static RenderFragment Text(string content) => builder => builder.AddContent(0, content);

    [Fact]
    public async Task 全家族结构快照应与基线一致()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<ButtonGroup>(new Dictionary<string, object?>
        {
            [nameof(ButtonGroup.AriaLabel)] = "Actions",
            [nameof(ButtonGroup.ChildContent)] = (RenderFragment)(inner =>
            {
                var i = 0;
                i = AddButton(inner, i, "Edit");
                i = AddSeparator(inner, i);
                i = AddText(inner, i, "or");
                i = AddButton(inner, i, "Delete");
            }),
        });

        await Verify(renderer.Markup());
    }

    private static int AddButton(RenderTreeBuilder b, int i, string label)
    {
        b.OpenComponent<Button>(i++);
        b.AddAttribute(i++, nameof(Button.ChildContent), (RenderFragment)(inner => inner.AddContent(0, label)));
        b.CloseComponent();
        return i;
    }

    private static int AddSeparator(RenderTreeBuilder b, int i)
    {
        b.OpenComponent<ButtonGroupSeparator>(i++);
        b.CloseComponent();
        return i;
    }

    private static int AddText(RenderTreeBuilder b, int i, string text)
    {
        b.OpenComponent<ButtonGroupText>(i++);
        b.AddAttribute(i++, nameof(ButtonGroupText.ChildContent), (RenderFragment)(inner => inner.AddContent(0, text)));
        b.CloseComponent();
        return i;
    }

    [Fact]
    public async Task 组根应携带group角色与数据槽标记()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<ButtonGroup>(new Dictionary<string, object?>
        {
            [nameof(ButtonGroup.AriaLabel)] = "Mail",
        });

        var markup = renderer.Markup();
        Assert.Contains("data-slot=\"button-group\"", markup);
        Assert.Contains("role=\"group\"", markup);
        Assert.Contains("aria-label=\"Mail\"", markup);
        Assert.Contains("atom:inline-flex", markup);
        Assert.Contains("atom:isolate", markup);
    }

    [Fact]
    public async Task 水平方向应使用逻辑边类连接按钮()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<ButtonGroup>(new Dictionary<string, object?>
        {
            [nameof(ButtonGroup.Orientation)] = ButtonGroupOrientation.Horizontal,
        });

        var markup = renderer.Markup();
        Assert.Contains("atom:[&>button:not(:first-child)]:border-s-0", markup);
        Assert.DoesNotContain("atom:flex-col", markup);
    }

    [Fact]
    public async Task 垂直方向应切换为纵向排列类()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<ButtonGroup>(new Dictionary<string, object?>
        {
            [nameof(ButtonGroup.Orientation)] = ButtonGroupOrientation.Vertical,
        });

        var markup = renderer.Markup();
        Assert.Contains("atom:flex-col", markup);
        Assert.Contains("atom:[&>button:not(:first-child)]:border-t-0", markup);
    }

    [Fact]
    public async Task 组上的未匹配属性应展开到容器()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<ButtonGroup>(new Dictionary<string, object?>
        {
            [nameof(ButtonGroup.AdditionalAttributes)] = new Dictionary<string, object> { ["data-x"] = "1" },
        });

        Assert.Contains("data-x=\"1\"", renderer.Markup());
    }

    // ------------------------------------------------------------------
    // ButtonGroupSeparator
    // ------------------------------------------------------------------

    [Fact]
    public async Task 分隔符默认垂直方向并以内层分隔线呈现()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<ButtonGroupSeparator>(new Dictionary<string, object?>());

        var markup = renderer.Markup();
        Assert.Contains("data-slot=\"separator\"", markup);
        Assert.Contains("atom:mx-1", markup);
        // 内层 Separator 默认装饰模式。
        Assert.Contains("role=\"none\"", markup);
        Assert.Contains("data-orientation=\"vertical\"", markup);
    }

    [Fact]
    public async Task 分隔符水平方向应改用纵向边距()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<ButtonGroupSeparator>(new Dictionary<string, object?>
        {
            [nameof(ButtonGroupSeparator.Orientation)] = SeparatorOrientation.Horizontal,
        });

        var markup = renderer.Markup();
        Assert.Contains("atom:my-1", markup);
        Assert.DoesNotContain("atom:mx-1", markup);
    }

    [Fact]
    public async Task 分隔符语义模式应向下传递装饰参数()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<ButtonGroupSeparator>(new Dictionary<string, object?>
        {
            [nameof(ButtonGroupSeparator.Decorative)] = false,
        });

        var markup = renderer.Markup();
        Assert.Contains("role=\"separator\"", markup);
    }

    [Fact]
    public async Task 分隔符外层应捕获并展开额外属性()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<ButtonGroupSeparator>(new Dictionary<string, object?>
        {
            [nameof(ButtonGroupSeparator.AdditionalAttributes)] = new Dictionary<string, object> { ["id"] = "sep" },
        });

        Assert.Contains("id=\"sep\"", renderer.Markup());
    }

    // ------------------------------------------------------------------
    // ButtonGroupText
    // ------------------------------------------------------------------

    [Fact]
    public async Task 文本槽应携带数据槽与柔和背景样式()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<ButtonGroupText>(new Dictionary<string, object?>
        {
            [nameof(ButtonGroupText.ChildContent)] = Text("Actions:"),
        });

        var markup = renderer.Markup();
        Assert.Contains("data-slot=\"button-group-text\"", markup);
        Assert.Contains("atom:bg-muted", markup);
        Assert.Contains("atom:text-muted-foreground", markup);
        Assert.Contains("atom:px-3", markup);
        Assert.Contains("Actions:", markup);
    }

    [Fact]
    public async Task 文本槽应捕获额外属性并合并自定义类()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<ButtonGroupText>(new Dictionary<string, object?>
        {
            [nameof(ButtonGroupText.Class)] = "atom:custom",
            [nameof(ButtonGroupText.AdditionalAttributes)] = new Dictionary<string, object> { ["title"] = "t" },
        });

        var markup = renderer.Markup();
        Assert.Contains("atom:custom", markup);
        Assert.Contains("title=\"t\"", markup);
    }
}
