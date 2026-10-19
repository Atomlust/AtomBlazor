using AtomBlazor.Primitives;
using AtomBlazor;
using AtomBlazor.Tests.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Xunit;

namespace AtomBlazor.Tests.Theme;

/// <summary>
/// ThemeScope 渲染测试：data-atom-* 作用域属性、--radius 内联样式、
/// 前缀类合并与参数校验的失败沿。属性名必须与 Primitives 的 theme-scope.js
/// 及 theme.js 守卫所观察的名称一致，否则局部主题会安静失效。
/// </summary>
public class ThemeScopeTests
{
    private static ComponentTestRenderer CreateRenderer()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(new NoopJavaScript());
        return new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance);
    }

    private static Dictionary<string, object?> Params(
        ThemeDesign? design = null,
        double? radius = null,
        string? @class = null,
        RenderFragment? childContent = null,
        Dictionary<string, object>? additionalAttributes = null)
    {
        var parameters = new Dictionary<string, object?>();
        if (design is not null)
        {
            parameters[nameof(ThemeScope.Design)] = design;
        }
        if (radius is not null)
        {
            parameters[nameof(ThemeScope.Radius)] = radius;
        }
        if (@class is not null)
        {
            parameters[nameof(ThemeScope.Class)] = @class;
        }
        if (childContent is not null)
        {
            parameters[nameof(ThemeScope.ChildContent)] = childContent;
        }
        if (additionalAttributes is not null)
        {
            parameters[nameof(ThemeScope.AdditionalAttributes)] = additionalAttributes;
        }
        return parameters;
    }

    [Fact]
    public async Task 默认设计应写出全套作用域data属性与小写取值()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<ThemeScope>(Params());

        var markup = renderer.Markup();
        Assert.Contains("data-atom-theme-scope", markup);
        Assert.Contains("data-atom-density=\"standard\"", markup);
        Assert.Contains("data-atom-font=\"system\"", markup);
        Assert.Contains("data-atom-surface=\"standard\"", markup);
        Assert.Contains("data-atom-menu-color=\"default\"", markup);
        Assert.Contains("data-atom-menu-accent=\"subtle\"", markup);
        Assert.Contains("atom:font-sans", markup);
        Assert.DoesNotContain("--radius", markup);
    }

    [Fact]
    public async Task 指定Radius应写出rem单位的内联样式且按不变文化格式化()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<ThemeScope>(Params(radius: 0.75));

        Assert.Contains("--radius: 0.75rem", renderer.Markup());
    }

    [Fact]
    public async Task 非默认设计枚举值应逐一映射为小写属性()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<ThemeScope>(Params(design: new ThemeDesign
        {
            Density = ThemeDensity.Dense,
            Font = ThemeFont.NunitoSans,
            Surface = ThemeSurface.Glass,
            MenuColor = ThemeMenuColor.Inverse,
            MenuAccent = ThemeMenuAccent.Primary,
        }));

        var markup = renderer.Markup();
        Assert.Contains("data-atom-density=\"dense\"", markup);
        Assert.Contains("data-atom-font=\"nunitosans\"", markup);
        Assert.Contains("data-atom-surface=\"glass\"", markup);
        Assert.Contains("data-atom-menu-color=\"inverse\"", markup);
        Assert.Contains("data-atom-menu-accent=\"primary\"", markup);
    }

    [Fact]
    public async Task 使用者的Class参数应与库前缀类合并进同一class属性()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<ThemeScope>(Params(@class: "p-6"));

        Assert.Contains("class=\"atom:font-sans p-6\"", renderer.Markup());
    }

    [Fact]
    public async Task 未匹配参数应经AdditionalAttributes展开到作用域div上()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<ThemeScope>(Params(
            additionalAttributes: new Dictionary<string, object> { ["id"] = "scope-1" }));

        Assert.Contains("id=\"scope-1\"", renderer.Markup());
    }

    [Fact]
    public async Task ChildContent应渲染在作用域div内部()
    {
        var renderer = CreateRenderer();

        static void Child(RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "span");
            builder.AddContent(1, "内容");
            builder.CloseElement();
        }

        await renderer.MountAsync<ThemeScope>(Params(childContent: Child));

        var markup = renderer.Markup();
        Assert.Contains("<span>内容</span>", markup);
    }

    [Fact]
    public async Task 半径越界应在参数设置沿抛出并中止渲染()
    {
        var renderer = CreateRenderer();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => renderer.MountAsync<ThemeScope>(Params(radius: 5)));
    }

    [Fact]
    public async Task 非法设计枚举值应在参数设置沿抛出()
    {
        var renderer = CreateRenderer();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => renderer.MountAsync<ThemeScope>(Params(design: new ThemeDesign { Density = (ThemeDensity)99 })));
    }
}
