using AtomBlazor.Primitives;
using System.Text.RegularExpressions;
using AtomBlazor;
using AtomBlazor.Tests.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Xunit;

namespace AtomBlazor.Tests.Components;

/// <summary>
/// InputGroup 家族结构测试：全家族嵌套快照（InputGroup>Addon+Text+Button+Input）、
/// data-slot 与 role 关键属性、Addon 四向对齐到 data-align 的映射表驱动、
/// InputGroupTextarea 的行内控件标记与默认行数。
/// </summary>
public class InputGroupTests
{
    /// <summary>
    /// 归一化组内输入控件（input/textarea 前缀）的随机短 id，使快照可确定。
    /// </summary>
    private static string Scrub(string markup) =>
        Regex.Replace(
            Regex.Replace(markup, "input-[0-9a-f]{8}", "input-<id>"),
            "textarea-[0-9a-f]{8}", "textarea-<id>");

    private static ComponentTestRenderer CreateRenderer()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(new NoopJavaScript());
        return new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance);
    }

    private static RenderFragment Text(string content) => builder => builder.AddContent(0, content);

    /// <summary>全家族嵌套体：起始附加内容（文本 $）+ 数字输入 + 结束附加内容（按钮）。</summary>
    private static RenderFragment GroupBody() => builder =>
    {
        var seq = 0;

        builder.OpenComponent<InputGroupAddon>(seq++);
        builder.AddAttribute(seq++, nameof(InputGroupAddon.Align), InputGroupAlign.InlineStart);
        builder.AddAttribute(seq++, nameof(InputGroupAddon.ChildContent), (RenderFragment)(inner =>
        {
            inner.OpenComponent<InputGroupText>(0);
            inner.AddAttribute(1, nameof(InputGroupText.ChildContent), Text("$"));
            inner.CloseComponent();
        }));
        builder.CloseComponent();

        builder.OpenComponent<InputGroupInput>(seq++);
        builder.AddAttribute(seq++, nameof(InputGroupInput.Type), InputType.Number);
        builder.AddAttribute(seq++, nameof(InputGroupInput.Placeholder), "0.00");
        builder.CloseComponent();

        builder.OpenComponent<InputGroupAddon>(seq++);
        builder.AddAttribute(seq++, nameof(InputGroupAddon.Align), InputGroupAlign.InlineEnd);
        builder.AddAttribute(seq++, nameof(InputGroupAddon.ChildContent), (RenderFragment)(inner =>
        {
            inner.OpenComponent<InputGroupButton>(0);
            inner.AddAttribute(1, nameof(InputGroupButton.ChildContent), Text("换算"));
            inner.CloseComponent();
        }));
        builder.CloseComponent();
    };

    private static Dictionary<string, object?> GroupParams(RenderFragment childContent) => new()
    {
        [nameof(InputGroup.ChildContent)] = childContent,
    };

    [Fact]
    public async Task 全家族嵌套结构快照应与基线一致()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<InputGroup>(GroupParams(GroupBody()));

        await Verify(Scrub(renderer.Markup()));
    }

    [Fact]
    public async Task 组容器与控件应携带关键数据槽与角色()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<InputGroup>(GroupParams(GroupBody()));

        var markup = renderer.Markup();
        Assert.Contains("data-slot=\"input-group\"", markup);
        Assert.Contains("role=\"group\"", markup);
        Assert.Contains("data-slot=\"input-group-control\"", markup);
        Assert.Contains("data-align=\"inline-start\"", markup);
        Assert.Contains("data-align=\"inline-end\"", markup);
        Assert.Contains("$", markup);
        Assert.Contains("<button", markup);
        Assert.Contains("换算", markup);
        Assert.Contains("type=\"number\"", markup);
    }

    [Theory]
    [InlineData(InputGroupAlign.InlineStart, "inline-start")]
    [InlineData(InputGroupAlign.InlineEnd, "inline-end")]
    [InlineData(InputGroupAlign.BlockStart, "block-start")]
    [InlineData(InputGroupAlign.BlockEnd, "block-end")]
    public async Task 附加内容对齐枚举应映射为dataalign(InputGroupAlign align, string expected)
    {
        var renderer = CreateRenderer();

        var addon = (RenderFragment)(builder =>
        {
            builder.OpenComponent<InputGroupAddon>(0);
            builder.AddAttribute(1, nameof(InputGroupAddon.Align), align);
            builder.AddAttribute(2, nameof(InputGroupAddon.ChildContent), Text("前缀"));
            builder.CloseComponent();
        });

        await renderer.MountAsync<InputGroup>(GroupParams(addon));

        Assert.Contains($"data-align=\"{expected}\"", renderer.Markup());
    }

    [Fact]
    public async Task InputGroupTextarea应渲染行内控件并默认三行()
    {
        var renderer = CreateRenderer();

        var body = (RenderFragment)(builder =>
        {
            builder.OpenComponent<InputGroupTextarea>(0);
            builder.AddAttribute(1, nameof(InputGroupTextarea.Placeholder), "写点什么…");
            builder.CloseComponent();
        });

        await renderer.MountAsync<InputGroup>(GroupParams(body));

        var markup = renderer.Markup();
        Assert.Contains("<textarea", markup);
        Assert.Contains("rows=\"3\"", markup);
        Assert.Contains("data-slot=\"input-group-control\"", markup);
        Assert.Contains("placeholder=\"写点什么…\"", markup);
    }
}
