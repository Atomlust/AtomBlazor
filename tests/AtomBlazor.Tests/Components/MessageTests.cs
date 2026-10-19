using AtomBlazor.Primitives;
using AtomBlazor;
using AtomBlazor.Tests.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Xunit;

namespace AtomBlazor.Tests.Components;

/// <summary>
/// Message 家族渲染测试：分组/消息行/头像/内容/头部/尾部全结构快照、
/// 对齐驱动 data-align 与行内方向、各插槽 data-slot 语义与默认类串、
/// 属性展开与自定义类并入。
/// </summary>
public class MessageTests
{
    private static ComponentTestRenderer CreateRenderer()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(new NoopJavaScript());
        services.AddSingleton<ILocalizer, DefaultLocalizer>();
        return new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance);
    }

    private static RenderFragment Text(string content) => builder => builder.AddContent(0, content);

    private static Dictionary<string, object?> MessageParams(
        MessageAlign? align = null,
        string? @class = null,
        RenderFragment? childContent = null,
        Dictionary<string, object>? additionalAttributes = null)
    {
        var parameters = new Dictionary<string, object?>();
        if (align is not null)
        {
            parameters[nameof(Message.Align)] = align.Value;
        }
        if (@class is not null)
        {
            parameters[nameof(Message.Class)] = @class;
        }
        parameters[nameof(Message.ChildContent)] = childContent ?? Text("消息");
        if (additionalAttributes is not null)
        {
            parameters[nameof(Message.AdditionalAttributes)] = additionalAttributes;
        }
        return parameters;
    }

    private static RenderFragment Slot<T>(string text) where T : IComponent => builder =>
    {
        builder.OpenComponent<T>(0);
        builder.AddAttribute(1, nameof(Message.ChildContent), Text(text));
        builder.CloseComponent();
    };

    [Fact]
    public async Task 全家族结构快照应与基线一致()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<MessageGroup>(new Dictionary<string, object?>
        {
            [nameof(MessageGroup.ChildContent)] = (RenderFragment)(builder =>
            {
                var seq = 0;

                // 起始对齐完整行：头像 + 内容（头部、正文、尾部）。
                builder.OpenComponent<Message>(seq++);
                builder.AddAttribute(seq++, nameof(Message.ChildContent), (RenderFragment)(inner =>
                {
                    inner.OpenComponent<MessageAvatar>(0);
                    inner.AddAttribute(1, nameof(MessageAvatar.ChildContent), Text("A"));
                    inner.CloseComponent();

                    inner.OpenComponent<MessageContent>(2);
                    inner.AddAttribute(3, nameof(MessageContent.ChildContent), (RenderFragment)(core =>
                    {
                        core.OpenComponent<MessageHeader>(0);
                        core.AddAttribute(1, nameof(MessageHeader.ChildContent), Text("小明"));
                        core.CloseComponent();
                        core.AddContent(2, "你好，这是一条消息。");
                        core.OpenComponent<MessageFooter>(3);
                        core.AddAttribute(4, nameof(MessageFooter.ChildContent), Text("10:24"));
                        core.CloseComponent();
                    }));
                    inner.CloseComponent();
                }));
                builder.CloseComponent();

                // 结束对齐的简行：仅内容。
                builder.OpenComponent<Message>(seq++);
                builder.AddAttribute(seq++, nameof(Message.Align), MessageAlign.End);
                builder.AddAttribute(seq++, nameof(Message.ChildContent), Slot<MessageContent>("收到！"));
                builder.CloseComponent();
            }),
        });

        await Verify(renderer.Markup());
    }

    [Fact]
    public async Task 默认应渲染起始对齐的消息行()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Message>(MessageParams());

        var markup = renderer.Markup();
        Assert.Contains("data-slot=\"message\"", markup);
        Assert.Contains("data-align=\"start\"", markup);
        Assert.Contains("atom:group/message", markup);
        Assert.Contains("atom:data-[align=end]:flex-row-reverse", markup);
        Assert.Contains("atom:justify-start", markup);
        Assert.Contains("消息", markup);
    }

    [Fact]
    public async Task 结束对齐应写入dataalign并保留行反转类()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Message>(MessageParams(align: MessageAlign.End));

        var markup = renderer.Markup();
        Assert.Contains("data-align=\"end\"", markup);
        Assert.Contains("atom:data-[align=end]:flex-row-reverse", markup);
    }

    [Fact]
    public async Task 头像插槽应为贴底圆形容器并渲染内容()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<MessageAvatar>(new Dictionary<string, object?>
        {
            [nameof(MessageAvatar.ChildContent)] = Text("A"),
        });

        var markup = renderer.Markup();
        Assert.Contains("data-slot=\"message-avatar\"", markup);
        Assert.Contains("atom:self-end", markup);
        Assert.Contains("atom:rounded-full atom:bg-muted", markup);
        Assert.Contains("atom:group-has-data-[slot=message-footer]/message:-translate-y-8", markup);
        Assert.Contains("A", markup);
    }

    [Fact]
    public async Task 内容包裹层应纵向排布并响应结束对齐()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<MessageContent>(new Dictionary<string, object?>
        {
            [nameof(MessageContent.ChildContent)] = Text("正文"),
        });

        var markup = renderer.Markup();
        Assert.Contains("data-slot=\"message-content\"", markup);
        Assert.Contains("atom:flex-col atom:gap-2.5 atom:wrap-break-word", markup);
        Assert.Contains("atom:group-data-[align=end]/message:*:data-slot:self-end", markup);
    }

    [Fact]
    public async Task 头部与尾部插槽应为弱化小字容器()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<MessageHeader>(new Dictionary<string, object?>
        {
            [nameof(MessageHeader.ChildContent)] = Text("小明"),
        });
        Assert.Contains("data-slot=\"message-header\"", renderer.Markup());
        Assert.Contains("atom:text-xs atom:font-medium atom:text-muted-foreground", renderer.Markup());
        Assert.Contains("atom:group-has-data-[variant=ghost]/message:px-0", renderer.Markup());

        var footer = CreateRenderer();
        await footer.MountAsync<MessageFooter>(new Dictionary<string, object?>
        {
            [nameof(MessageFooter.ChildContent)] = Text("10:24"),
        });
        Assert.Contains("data-slot=\"message-footer\"", footer.Markup());
        Assert.Contains("atom:group-data-[align=end]/message:justify-end", footer.Markup());
        Assert.Contains("atom:group-data-[align=end]/message:text-end", footer.Markup());
    }

    [Fact]
    public async Task 分组容器应为纵向弹性布局间隙排布()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<MessageGroup>(new Dictionary<string, object?>
        {
            [nameof(MessageGroup.ChildContent)] = Text("g"),
        });

        var markup = renderer.Markup();
        Assert.Contains("data-slot=\"message-group\"", markup);
        Assert.Contains("atom:flex atom:min-w-0 atom:flex-col atom:gap-2", markup);
    }

    [Theory]
    [InlineData(MessageAlign.Start, "start")]
    [InlineData(MessageAlign.End, "end")]
    public async Task 对齐枚举应小写写入dataalign属性(MessageAlign align, string expected)
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Message>(MessageParams(align: align));

        Assert.Contains($"data-align=\"{expected}\"", renderer.Markup());
    }

    [Fact]
    public async Task 额外属性应展开到根元素且自定义类应并入()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Message>(MessageParams(
            @class: "my-message",
            additionalAttributes: new Dictionary<string, object> { ["data-testid"] = "message" }));

        var markup = renderer.Markup();
        Assert.Contains("data-testid=\"message\"", markup);
        Assert.Contains("my-message", markup);
    }
}
