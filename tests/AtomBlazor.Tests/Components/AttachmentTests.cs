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
/// Attachment 家族渲染测试：全家族结构快照、状态/尺寸/方向表驱动映射、
/// 媒体变体类与 data-variant、触发遮罩的 button/anchor 解析与 href 自动切换、
/// aria-label 本地化回退、操作按钮复用 Button、data-slot 语义与属性展开。
/// </summary>
public class AttachmentTests
{
    private static ComponentTestRenderer CreateRenderer()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(new NoopJavaScript());
        services.AddSingleton<ILocalizer, DefaultLocalizer>();
        return new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance);
    }

    private static RenderFragment Text(string content) => builder => builder.AddContent(0, content);

    private static Dictionary<string, object?> AttachmentParams(
        AttachmentState? state = null,
        AttachmentSize? size = null,
        AttachmentOrientation? orientation = null,
        string? @class = null,
        RenderFragment? childContent = null,
        Dictionary<string, object>? additionalAttributes = null)
    {
        var parameters = new Dictionary<string, object?>();
        if (state is not null)
        {
            parameters[nameof(Attachment.State)] = state.Value;
        }
        if (size is not null)
        {
            parameters[nameof(Attachment.Size)] = size.Value;
        }
        if (orientation is not null)
        {
            parameters[nameof(Attachment.Orientation)] = orientation.Value;
        }
        if (@class is not null)
        {
            parameters[nameof(Attachment.Class)] = @class;
        }
        parameters[nameof(Attachment.ChildContent)] = childContent ?? Text("file.png");
        if (additionalAttributes is not null)
        {
            parameters[nameof(Attachment.AdditionalAttributes)] = additionalAttributes;
        }
        return parameters;
    }

    // <AttachmentGroup><Attachment…(Media/Content/Title/Description/Actions/Action/Trigger)/></Attachment…></AttachmentGroup> 全结构。
    private static RenderFragment CardBody(int index) => builder =>
    {
        var s = 0;

        builder.OpenComponent<AttachmentMedia>(s++);
        builder.AddAttribute(s++, nameof(AttachmentMedia.Variant), index == 0 ? AttachmentMediaVariant.Icon : AttachmentMediaVariant.Image);
        builder.AddAttribute(s++, nameof(AttachmentMedia.ChildContent), Text(index == 0 ? "[icon]" : "[img]"));
        builder.CloseComponent();

        builder.OpenComponent<AttachmentContent>(s++);
        builder.AddAttribute(s++, nameof(AttachmentContent.ChildContent), (RenderFragment)(cb =>
        {
            cb.OpenComponent<AttachmentTitle>(0);
            cb.AddAttribute(1, nameof(AttachmentTitle.ChildContent), Text($"report-{index}.pdf"));
            cb.CloseComponent();
            cb.OpenComponent<AttachmentDescription>(2);
            cb.AddAttribute(3, nameof(AttachmentDescription.ChildContent), Text("2.4 MB"));
            cb.CloseComponent();
        }));
        builder.CloseComponent();

        builder.OpenComponent<AttachmentActions>(s++);
        builder.AddAttribute(s++, nameof(AttachmentActions.ChildContent), (RenderFragment)(ab =>
        {
            ab.OpenComponent<AttachmentAction>(0);
            ab.AddAttribute(1, nameof(AttachmentAction.ChildContent), Text("x"));
            ab.CloseComponent();
        }));
        builder.CloseComponent();

        builder.OpenComponent<AttachmentTrigger>(s++);
        builder.AddAttribute(s++, nameof(AttachmentTrigger.Href), $"https://example.com/file-{index}");
        builder.CloseComponent();
    };

    [Fact]
    public async Task 全家族结构快照应与基线一致()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<AttachmentGroup>(new Dictionary<string, object?>
        {
            [nameof(AttachmentGroup.ChildContent)] = (RenderFragment)(builder =>
            {
                for (var i = 0; i < 2; i++)
                {
                    builder.OpenComponent<Attachment>(i * 2);
                    builder.AddAttribute((i * 2) + 1, nameof(Attachment.ChildContent), CardBody(i));
                    builder.CloseComponent();
                }
            }),
        });

        await Verify(renderer.Markup());
    }

    [Fact]
    public async Task 默认应渲染完成状态水平默认尺寸的附件卡片()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Attachment>(AttachmentParams());

        var markup = renderer.Markup();
        Assert.Contains("data-slot=\"attachment\"", markup);
        Assert.Contains("data-state=\"done\"", markup);
        Assert.Contains("data-size=\"default\"", markup);
        Assert.Contains("data-orientation=\"horizontal\"", markup);
        Assert.Contains("atom:group/attachment", markup);
        Assert.Contains("atom:min-w-40 atom:items-center", markup);
    }

    [Theory]
    [InlineData(AttachmentState.Idle, null)]
    [InlineData(AttachmentState.Uploading, "atom:border-primary/40 atom:bg-primary/5")]
    [InlineData(AttachmentState.Processing, "atom:border-primary/40 atom:bg-primary/5")]
    [InlineData(AttachmentState.Error, "atom:border-destructive/40 atom:bg-destructive/5")]
    [InlineData(AttachmentState.Done, null)]
    public async Task 状态应映射datastate与状态配色(AttachmentState state, string? expectedClasses)
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Attachment>(AttachmentParams(state: state));

        var markup = renderer.Markup();
        Assert.Contains($"data-state=\"{state.ToString().ToLowerInvariant()}\"", markup);
        if (expectedClasses is not null)
        {
            Assert.Contains(expectedClasses, markup);
        }
        else
        {
            Assert.DoesNotContain("atom:border-primary/40", markup);
            Assert.DoesNotContain("atom:border-destructive/40", markup);
        }
    }

    [Theory]
    [InlineData(AttachmentSize.Default, "atom:gap-2 atom:text-sm")]
    [InlineData(AttachmentSize.Sm, "atom:gap-2.5 atom:text-xs")]
    [InlineData(AttachmentSize.Xs, "atom:gap-1.5 atom:rounded-xl")]
    public async Task 尺寸应映射对应间距与字号(AttachmentSize size, string expectedClasses)
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Attachment>(AttachmentParams(size: size));

        var markup = renderer.Markup();
        Assert.Contains($"data-size=\"{size.ToString().ToLowerInvariant()}\"", markup);
        Assert.Contains(expectedClasses, markup);
    }

    [Fact]
    public async Task 垂直方向应堆叠布局并收窄宽度()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Attachment>(AttachmentParams(orientation: AttachmentOrientation.Vertical));

        var markup = renderer.Markup();
        Assert.Contains("data-orientation=\"vertical\"", markup);
        Assert.Contains("atom:w-24 atom:flex-col", markup);
        Assert.DoesNotContain("atom:min-w-40", markup);
    }

    [Theory]
    [InlineData(AttachmentMediaVariant.Icon, "atom:opacity-60", "atom:*:[img]:aspect-square")]
    [InlineData(AttachmentMediaVariant.Image, "atom:*:[img]:aspect-square", "atom:opacity-60")]
    public async Task 媒体变体应映射容器类与datavariant(AttachmentMediaVariant variant, string expected, string unexpected)
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<AttachmentMedia>(new Dictionary<string, object?>
        {
            [nameof(AttachmentMedia.Variant)] = variant,
            [nameof(AttachmentMedia.ChildContent)] = Text("M"),
        });

        var markup = renderer.Markup();
        Assert.Contains("data-slot=\"attachment-media\"", markup);
        Assert.Contains($"data-variant=\"{variant.ToString().ToLowerInvariant()}\"", markup);
        Assert.Contains(expected, markup);
        Assert.DoesNotContain(unexpected, markup);
    }

    [Fact]
    public async Task 标题描述与内容区应各自携带数据槽与排版类()
    {
        var title = CreateRenderer();
        await title.MountAsync<AttachmentTitle>(new Dictionary<string, object?>
        {
            [nameof(AttachmentTitle.ChildContent)] = Text("T"),
        });
        var titleMarkup = title.Markup();
        Assert.Contains("data-slot=\"attachment-title\"", titleMarkup);
        Assert.Contains("atom:truncate atom:font-medium", titleMarkup);
        // 上传/处理中的 shimmer 扫掠由父级 group-data 驱动，写在标题类串里。
        Assert.Contains("atom:group-data-[state=uploading]/attachment:shimmer", titleMarkup);

        var desc = CreateRenderer();
        await desc.MountAsync<AttachmentDescription>(new Dictionary<string, object?>
        {
            [nameof(AttachmentDescription.ChildContent)] = Text("D"),
        });
        var descMarkup = desc.Markup();
        Assert.Contains("data-slot=\"attachment-description\"", descMarkup);
        Assert.Contains("atom:text-xs atom:text-muted-foreground", descMarkup);

        var content = CreateRenderer();
        await content.MountAsync<AttachmentContent>(new Dictionary<string, object?>
        {
            [nameof(AttachmentContent.ChildContent)] = Text("C"),
        });
        var contentMarkup = content.Markup();
        Assert.Contains("data-slot=\"attachment-content\"", contentMarkup);
        Assert.Contains("atom:flex-1 atom:leading-tight", contentMarkup);
    }

    [Fact]
    public async Task 操作容器应声明纵向绝对定位变体()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<AttachmentActions>(new Dictionary<string, object?>
        {
            [nameof(AttachmentActions.ChildContent)] = Text("A"),
        });

        var markup = renderer.Markup();
        Assert.Contains("data-slot=\"attachment-actions\"", markup);
        Assert.Contains("atom:relative atom:z-20 atom:flex atom:shrink-0 atom:items-center", markup);
        Assert.Contains("atom:group-data-[orientation=vertical]/attachment:absolute", markup);
    }

    [Fact]
    public async Task 操作按钮应复用Button并携带数据槽与默认幽灵图标尺寸()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<AttachmentAction>(new Dictionary<string, object?>
        {
            [nameof(AttachmentAction.ChildContent)] = Text("x"),
        });

        var markup = renderer.Markup();
        Assert.Contains("<button", markup);
        Assert.Contains("data-slot=\"attachment-action\"", markup);
        Assert.Contains("type=\"button\"", markup);
    }

    [Fact]
    public async Task 分组容器应为横向滚动吸附行()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<AttachmentGroup>(new Dictionary<string, object?>
        {
            [nameof(AttachmentGroup.ChildContent)] = Text("i"),
        });

        var markup = renderer.Markup();
        Assert.Contains("data-slot=\"attachment-group\"", markup);
        Assert.Contains("atom:snap-x atom:snap-mandatory", markup);
        Assert.Contains("atom:overflow-x-auto atom:overscroll-x-contain", markup);
        Assert.Contains("atom:scroll-fade-x", markup);
    }

    [Fact]
    public async Task 触发遮罩默认应渲染button并回退本地化标签()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<AttachmentTrigger>(new Dictionary<string, object?>());

        var markup = renderer.Markup();
        Assert.Contains("<button", markup);
        Assert.Contains("data-slot=\"attachment-trigger\"", markup);
        Assert.Contains("type=\"button\"", markup);
        // DefaultLocalizer 的 "Attachment.Open" 键值。
        Assert.Contains("aria-label=\"Open attachment\"", markup);
        Assert.Contains("atom:rounded-[inherit] atom:focus-visible:ring-2", markup);
    }

    [Fact]
    public async Task 提供Href应自动切换为锚点元素()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<AttachmentTrigger>(new Dictionary<string, object?>
        {
            [nameof(AttachmentTrigger.Href)] = "https://example.com/a",
        });

        var markup = renderer.Markup();
        Assert.Contains("<a", markup);
        Assert.Contains("href=\"https://example.com/a\"", markup);
        Assert.DoesNotContain("<button", markup);
    }

    [Fact]
    public async Task AsChild显式Anchor应渲染锚点()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<AttachmentTrigger>(new Dictionary<string, object?>
        {
            [nameof(AttachmentTrigger.AsChild)] = AttachmentTriggerElement.Anchor,
        });

        Assert.Contains("<a", renderer.Markup());
    }

    [Fact]
    public async Task 显式AriaLabel应覆盖本地化默认值()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<AttachmentTrigger>(new Dictionary<string, object?>
        {
            [nameof(AttachmentTrigger.AriaLabel)] = "查看合同",
        });

        var markup = renderer.Markup();
        Assert.Contains("aria-label=\"查看合同\"", markup);
        Assert.DoesNotContain("Open attachment", markup);
    }

    [Fact]
    public async Task 额外属性应展开到对应元素()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Attachment>(AttachmentParams(
            additionalAttributes: new Dictionary<string, object> { ["data-testid"] = "attachment" }));

        Assert.Contains("data-testid=\"attachment\"", renderer.Markup());

        var trigger = CreateRenderer();
        await trigger.MountAsync<AttachmentTrigger>(new Dictionary<string, object?>
        {
            [nameof(AttachmentTrigger.AdditionalAttributes)] = new Dictionary<string, object> { ["data-testid"] = "trigger" },
        });
        Assert.Contains("data-testid=\"trigger\"", trigger.Markup());
    }

    [Fact]
    public async Task 自定义Class应并入根元素类串()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Attachment>(AttachmentParams(@class: "my-card"));

        Assert.Contains("my-card", renderer.Markup());
    }
}
