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
/// Button 渲染测试：6 变体×6 尺寸×3 类型表驱动映射、禁用与加载态、
/// 锚模式（href/target/rel）、图标位置、事件回调与自定义类合并。
/// </summary>
public class ButtonTests
{
    private static ComponentTestRenderer CreateRenderer()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(new NoopJavaScript());
        return new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance);
    }

    private static RenderFragment Text(string content) => builder => builder.AddContent(0, content);

    private static Dictionary<string, object?> Params(
        RenderFragment? childContent = null,
        ButtonVariant? variant = null,
        ButtonSize? size = null,
        ButtonType? type = null,
        bool? disabled = null,
        string? @class = null,
        EventCallback<MouseEventArgs> onClick = default,
        string? ariaLabel = null,
        RenderFragment? icon = null,
        IconPosition? iconPosition = null,
        bool? loading = null,
        string? loadingText = null,
        RenderFragment? loadingTemplate = null,
        string? href = null,
        string? target = null,
        Dictionary<string, object>? additionalAttributes = null)
    {
        var parameters = new Dictionary<string, object?>();
        if (childContent is not null)
        {
            parameters[nameof(Button.ChildContent)] = childContent;
        }
        if (variant is not null)
        {
            parameters[nameof(Button.Variant)] = variant.Value;
        }
        if (size is not null)
        {
            parameters[nameof(Button.Size)] = size.Value;
        }
        if (type is not null)
        {
            parameters[nameof(Button.Type)] = type.Value;
        }
        if (disabled is not null)
        {
            parameters[nameof(Button.Disabled)] = disabled.Value;
        }
        if (@class is not null)
        {
            parameters[nameof(Button.Class)] = @class;
        }
        if (onClick.HasDelegate)
        {
            parameters[nameof(Button.OnClick)] = onClick;
        }
        if (ariaLabel is not null)
        {
            parameters[nameof(Button.AriaLabel)] = ariaLabel;
        }
        if (icon is not null)
        {
            parameters[nameof(Button.Icon)] = icon;
        }
        if (iconPosition is not null)
        {
            parameters[nameof(Button.IconPosition)] = iconPosition.Value;
        }
        if (loading is not null)
        {
            parameters[nameof(Button.Loading)] = loading.Value;
        }
        if (loadingText is not null)
        {
            parameters[nameof(Button.LoadingText)] = loadingText;
        }
        if (loadingTemplate is not null)
        {
            parameters[nameof(Button.LoadingTemplate)] = loadingTemplate;
        }
        if (href is not null)
        {
            parameters[nameof(Button.Href)] = href;
        }
        if (target is not null)
        {
            parameters[nameof(Button.Target)] = target;
        }
        if (additionalAttributes is not null)
        {
            parameters[nameof(Button.AdditionalAttributes)] = additionalAttributes;
        }
        return parameters;
    }

    [Fact]
    public async Task 默认变体渲染快照应与基线一致()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Button>(Params(childContent: Text("Click me")));

        await Verify(renderer.Markup());
    }

    // ------------------------------------------------------------------
    // 变体表驱动
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(ButtonVariant.Default, "atom:bg-primary")]
    [InlineData(ButtonVariant.Destructive, "atom:bg-destructive")]
    [InlineData(ButtonVariant.Outline, "atom:border-input")]
    [InlineData(ButtonVariant.Secondary, "atom:bg-secondary")]
    [InlineData(ButtonVariant.Ghost, "atom:hover:bg-accent")]
    [InlineData(ButtonVariant.Link, "atom:hover:underline")]
    public async Task 每个变体应映射专属样式类(ButtonVariant variant, string expectedClass)
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Button>(Params(variant: variant));

        Assert.Contains(expectedClass, renderer.Markup());
    }

    [Fact]
    public async Task 幽灵与链接变体不应携带实底背景()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Button>(Params(variant: ButtonVariant.Ghost));
        Assert.DoesNotContain("atom:bg-primary", renderer.Markup());

        await renderer.MountAsync<Button>(Params(variant: ButtonVariant.Link));
        Assert.DoesNotContain("atom:bg-primary", renderer.Markup());
    }

    // ------------------------------------------------------------------
    // 尺寸表驱动
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(ButtonSize.Small, "atom:h-9", "atom:px-3")]
    [InlineData(ButtonSize.Default, "atom:h-10", "atom:px-4")]
    [InlineData(ButtonSize.Large, "atom:h-11", "atom:px-8")]
    public async Task 文本尺寸应映射高度与内边距(ButtonSize size, string heightClass, string paddingClass)
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Button>(Params(size: size));

        var markup = renderer.Markup();
        Assert.Contains(heightClass, markup);
        Assert.Contains(paddingClass, markup);
    }

    [Theory]
    [InlineData(ButtonSize.Icon, "atom:h-10", "atom:w-10")]
    [InlineData(ButtonSize.IconSmall, "atom:h-9", "atom:w-9")]
    [InlineData(ButtonSize.IconLarge, "atom:h-11", "atom:w-11")]
    public async Task 图标尺寸应映射正方形宽高(ButtonSize size, string heightClass, string widthClass)
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Button>(Params(size: size, ariaLabel: "gear"));

        var markup = renderer.Markup();
        Assert.Contains(heightClass, markup);
        Assert.Contains(widthClass, markup);
        Assert.Contains("aria-label=\"gear\"", markup);
    }

    // ------------------------------------------------------------------
    // HTML type
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(ButtonType.Submit, "submit")]
    [InlineData(ButtonType.Reset, "reset")]
    [InlineData(ButtonType.Button, "button")]
    public async Task 类型参数应映射到HtmlType特性(ButtonType type, string expected)
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Button>(Params(type: type));

        Assert.Contains($"type=\"{expected}\"", renderer.Markup());
    }

    [Fact]
    public async Task 默认类型应为button防止误提交()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Button>(Params());

        Assert.Contains("type=\"button\"", renderer.Markup());
    }

    // ------------------------------------------------------------------
    // 禁用与加载
    // ------------------------------------------------------------------

    [Fact]
    public async Task 禁用应输出disabled特性与aria禁用并吞掉点击()
    {
        var clicks = 0;
        var callback = EventCallback.Factory.Create<MouseEventArgs>(new object(), _ => clicks++);
        var renderer = CreateRenderer();

        await renderer.MountAsync<Button>(Params(disabled: true, onClick: callback));

        var markup = renderer.Markup();
        Assert.Contains("disabled", markup);
        Assert.Contains("aria-disabled=\"true\"", markup);
        Assert.Contains("tabindex=\"-1\"", markup);

        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs()));

        Assert.Equal(0, clicks);
    }

    [Fact]
    public async Task 启用按钮点击应触发回调()
    {
        var clicks = 0;
        var callback = EventCallback.Factory.Create<MouseEventArgs>(new object(), _ => clicks++);
        var renderer = CreateRenderer();

        await renderer.MountAsync<Button>(Params(onClick: callback));

        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs()));

        Assert.Equal(1, clicks);
    }

    [Fact]
    public async Task 加载态应禁用并渲染加载指示器与加载文本()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Button>(Params(
            childContent: Text("Save"),
            loading: true,
            loadingText: "Saving…"));

        var markup = renderer.Markup();
        Assert.Contains("disabled", markup);
        Assert.Contains("atom:animate-spin", markup);
        Assert.Contains("Saving…", markup);
        Assert.DoesNotContain(">Save<", markup);
    }

    [Fact]
    public async Task 加载态自定义模板应替换默认加载指示器()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Button>(Params(
            loading: true,
            loadingTemplate: Text("L")));

        var markup = renderer.Markup();
        Assert.Contains("atom:me-2", markup);
        Assert.DoesNotContain("atom:animate-spin", markup);
    }

    // ------------------------------------------------------------------
    // 锚模式
    // ------------------------------------------------------------------

    [Fact]
    public async Task 设置Href应渲染锚元素()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Button>(Params(href: "https://example.com", childContent: Text("Go")));

        var markup = renderer.Markup();
        Assert.Contains("<a href=\"https://example.com\"", markup);
        Assert.DoesNotContain("<button", markup);
    }

    [Fact]
    public async Task 新窗口打开应携带安全rel属性()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Button>(Params(href: "/x", target: "_blank"));

        Assert.Contains("rel=\"noopener noreferrer\"", renderer.Markup());
    }

    [Fact]
    public async Task 禁用锚应去href并加不可点击样式()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Button>(Params(href: "/x", disabled: true));

        var markup = renderer.Markup();
        Assert.DoesNotContain("href=", markup);
        Assert.Contains("aria-disabled=\"true\"", markup);
        Assert.Contains("tabindex=\"-1\"", markup);
        Assert.Contains("atom:pointer-events-none", markup);
        Assert.Contains("atom:opacity-50", markup);
    }

    // ------------------------------------------------------------------
    // 图标位置
    // ------------------------------------------------------------------

    [Fact]
    public async Task 起始图标应排在文本之前()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Button>(Params(
            childContent: Text("Label"),
            icon: Text("I"),
            iconPosition: IconPosition.Start));

        var markup = renderer.Markup();
        Assert.Contains("<span class=\"atom:me-2\">I</span>", markup);
        Assert.True(markup.IndexOf("atom:me-2", StringComparison.Ordinal) < markup.IndexOf("Label", StringComparison.Ordinal));
    }

    [Fact]
    public async Task 结尾图标应排在文本之后()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Button>(Params(
            childContent: Text("Label"),
            icon: Text("I"),
            iconPosition: IconPosition.End));

        var markup = renderer.Markup();
        Assert.Contains("<span class=\"atom:ms-2\">I</span>", markup);
        Assert.True(markup.IndexOf("Label", StringComparison.Ordinal) < markup.IndexOf("atom:ms-2", StringComparison.Ordinal));
    }

    // ------------------------------------------------------------------
    // 属性与类合并
    // ------------------------------------------------------------------

    [Fact]
    public async Task 自定义类应追加在基础类之后()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Button>(Params(@class: "atom:my-custom"));

        Assert.Contains("atom:my-custom", renderer.Markup());
    }

    [Fact]
    public async Task 未匹配属性应展开到按钮元素()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Button>(Params(
            additionalAttributes: new Dictionary<string, object>
            {
                ["title"] = "hint",
                ["data-testid"] = "btn",
            }));

        var markup = renderer.Markup();
        Assert.Contains("title=\"hint\"", markup);
        Assert.Contains("data-testid=\"btn\"", markup);
    }
}
