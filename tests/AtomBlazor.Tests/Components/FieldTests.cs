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
/// Field 家族渲染测试：全家族结构快照、五向 Orientation 与 FieldGroup 三向表驱动、
/// data-slot/data-orientation/data-invalid 语义、FieldIsInvalid 级联下发、
/// FieldError 双模式（Errors 优先于 ChildContent）与 FieldLegend 双变体。
/// </summary>
public class FieldTests
{
    // CA1861：常量数组参数提取为静态字段复用。
    private static readonly string[] snapshotErrors = ["邮箱格式不正确", "长度超出限制"];
    private static readonly string[] singleRequiredError = ["必填字段"];
    private static readonly string[] collectionOnlyError = ["来自集合"];

    private static ComponentTestRenderer CreateRenderer()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(new NoopJavaScript());
        return new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance);
    }

    private static RenderFragment Text(string content) => builder => builder.AddContent(0, content);

    private static RenderFragment Child(params (Type Type, Dictionary<string, object?> Parameters)[] children) =>
        builder =>
        {
            var seq = 0;
            foreach (var (type, parameters) in children)
            {
                builder.OpenComponent(seq++, type);
                foreach (var (key, value) in parameters)
                {
                    builder.AddAttribute(seq++, key, value);
                }
                builder.CloseComponent();
            }
        };

    private static Dictionary<string, object?> P(string key, object value) => new() { [key] = value };

    private static Dictionary<string, object?> P(string key, object value, string key2, object value2) =>
        new() { [key] = value, [key2] = value2 };

    private static Dictionary<string, object?> Params(
        FieldOrientation? orientation = null,
        bool? isInvalid = null,
        string? @class = null,
        RenderFragment? childContent = null,
        Dictionary<string, object>? additionalAttributes = null)
    {
        var parameters = new Dictionary<string, object?>();
        if (orientation is not null)
        {
            parameters[nameof(Field.Orientation)] = orientation.Value;
        }
        if (isInvalid is not null)
        {
            parameters[nameof(Field.IsInvalid)] = isInvalid.Value;
        }
        if (@class is not null)
        {
            parameters[nameof(Field.Class)] = @class;
        }
        if (childContent is not null)
        {
            parameters[nameof(Field.ChildContent)] = childContent;
        }
        if (additionalAttributes is not null)
        {
            parameters[nameof(Field.AdditionalAttributes)] = additionalAttributes;
        }
        return parameters;
    }

    // ------------------------------------------------------------------
    // 全家族结构快照
    // ------------------------------------------------------------------

    [Fact]
    public async Task 全家族结构快照应与基线一致()
    {
        var renderer = CreateRenderer();

        var field = Child(
            (typeof(FieldLabel), P(nameof(FieldLabel.ChildContent), (RenderFragment)Text("Email"))),
            (typeof(FieldContent), P(nameof(FieldContent.ChildContent), (RenderFragment)Child(
                (typeof(FieldTitle), P(nameof(FieldTitle.ChildContent), (RenderFragment)Text("通知偏好"))),
                (typeof(FieldDescription), new Dictionary<string, object?>
                {
                    [nameof(FieldDescription.Id)] = "email-description",
                    [nameof(FieldDescription.ChildContent)] = Text("我们不会分享你的邮箱。"),
                }),
                (typeof(FieldError), P(nameof(FieldError.Errors), snapshotErrors))))));

        var group = Child(
            (typeof(FieldLegend), P(nameof(FieldLegend.ChildContent), (RenderFragment)Text("收货地址"))),
            (typeof(Field), P(nameof(Field.Orientation), FieldOrientation.Horizontal,
                nameof(Field.ChildContent), (RenderFragment)field)),
            (typeof(FieldSeparator), new Dictionary<string, object?>()),
            (typeof(Field), P(nameof(Field.IsInvalid), true,
                nameof(Field.ChildContent), (RenderFragment)field)));

        await renderer.MountAsync<FieldSet>(P(nameof(FieldSet.ChildContent), group));

        await Verify(renderer.Markup());
    }

    // ------------------------------------------------------------------
    // Orientation 表驱动
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(FieldOrientation.Vertical, "vertical", "atom:flex-col atom:w-full")]
    [InlineData(FieldOrientation.Horizontal, "horizontal", "atom:flex-row atom:items-start atom:w-full")]
    [InlineData(FieldOrientation.Responsive, "responsive", "atom:@md:flex-row atom:@md:items-start")]
    [InlineData(FieldOrientation.HorizontalEnd, "horizontalend", "atom:flex-row-reverse atom:items-start")]
    [InlineData(FieldOrientation.VerticalEnd, "verticalend", "atom:flex-col-reverse atom:w-full")]
    public async Task 每个方向应映射data方向与专属样式类(FieldOrientation orientation, string dataOrientation, string expectedClass)
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Field>(Params(orientation: orientation));

        var markup = renderer.Markup();
        Assert.Contains($"data-orientation=\"{dataOrientation}\"", markup);
        Assert.Contains(expectedClass, markup);
        Assert.Contains("role=\"group\"", markup);
        Assert.Contains("data-slot=\"field\"", markup);
    }

    [Theory]
    [InlineData(FieldGroupOrientation.Vertical, "vertical", "atom:flex-col")]
    [InlineData(FieldGroupOrientation.Horizontal, "horizontal", "atom:flex-row atom:flex-wrap")]
    [InlineData(FieldGroupOrientation.Responsive, "responsive", "atom:@container/field-group")]
    public async Task 字段组方向应映射data方向与样式类(FieldGroupOrientation orientation, string dataOrientation, string expectedClass)
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<FieldGroup>(new Dictionary<string, object?>
        {
            [nameof(FieldGroup.Orientation)] = orientation,
        });

        var markup = renderer.Markup();
        Assert.Contains($"data-orientation=\"{dataOrientation}\"", markup);
        Assert.Contains(expectedClass, markup);
        Assert.Contains("data-slot=\"field-group\"", markup);
    }

    // ------------------------------------------------------------------
    // 无效态与级联
    // ------------------------------------------------------------------

    [Fact]
    public async Task 无效态应写出invalid特性并级联FieldIsInvalid()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Field>(Params(
            isInvalid: true,
            childContent: builder =>
            {
                builder.OpenComponent<FieldIsInvalidProbe>(0);
                builder.CloseComponent();
            }));

        var markup = renderer.Markup();
        Assert.Contains("data-invalid=\"true\"", markup);
        Assert.Contains("data-cascade=\"true\"", markup);
    }

    [Fact]
    public async Task 有效态不应写出invalid特性且级联值为false()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Field>(Params(
            childContent: builder =>
            {
                builder.OpenComponent<FieldIsInvalidProbe>(0);
                builder.CloseComponent();
            }));

        var markup = renderer.Markup();
        Assert.DoesNotContain("data-invalid", markup);
        Assert.Contains("data-cascade=\"false\"", markup);
    }

    // ------------------------------------------------------------------
    // FieldError 双模式
    // ------------------------------------------------------------------

    [Fact]
    public async Task FieldError应以项目符号渲染错误集合()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<FieldError>(new Dictionary<string, object?>
        {
            [nameof(FieldError.Id)] = "email-error",
            [nameof(FieldError.Errors)] = singleRequiredError,
        });

        var markup = renderer.Markup();
        Assert.Contains("id=\"email-error\"", markup);
        Assert.Contains("data-slot=\"field-error\"", markup);
        Assert.Contains("atom:flex atom:items-start atom:gap-1.5", markup);
        Assert.Contains("<span class=\"atom:mt-0.5\">•</span>", markup);
        Assert.Contains("必填字段", markup);
    }

    [Fact]
    public async Task 错误集合存在时ChildContent不应抢占()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<FieldError>(new Dictionary<string, object?>
        {
            [nameof(FieldError.Errors)] = collectionOnlyError,
            [nameof(FieldError.ChildContent)] = Text("来自插槽"),
        });

        var markup = renderer.Markup();
        Assert.Contains("来自集合", markup);
        Assert.DoesNotContain("来自插槽", markup);
    }

    [Fact]
    public async Task 无错误但有插槽内容时应渲染自定义内容()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<FieldError>(P(nameof(FieldError.ChildContent), (RenderFragment)Text("自定义错误")));

        var markup = renderer.Markup();
        Assert.Contains("data-slot=\"field-error\"", markup);
        Assert.Contains("自定义错误", markup);
    }

    [Fact]
    public async Task 无错误且无内容时FieldError应整体不渲染()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<FieldError>(new Dictionary<string, object?>());

        Assert.Equal(string.Empty, renderer.Markup());
    }

    // ------------------------------------------------------------------
    // FieldLegend 双变体
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(FieldLegendVariant.Legend, "<legend", 0)]
    [InlineData(FieldLegendVariant.Label, "<div", 1)]
    public async Task 图例变体应切换元素形态(FieldLegendVariant variant, string expectedElement, int expectRoleGroup)
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<FieldLegend>(new Dictionary<string, object?>
        {
            [nameof(FieldLegend.Variant)] = variant,
            [nameof(FieldLegend.AriaLabel)] = "分组名称",
            [nameof(FieldLegend.ChildContent)] = Text("收货地址"),
        });

        var markup = renderer.Markup();
        Assert.Contains(expectedElement, markup);
        Assert.Contains("data-slot=\"field-legend\"", markup);
        Assert.Equal(expectRoleGroup == 1, markup.Contains("role=\"group\""));
    }

    // ------------------------------------------------------------------
    // 其余成员的 data-slot 与类串
    // ------------------------------------------------------------------

    [Fact]
    public async Task 各成员应输出自身slot标记与默认类()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<FieldContent>(P(nameof(FieldContent.ChildContent), (RenderFragment)Text("x")));
        Assert.Contains("data-slot=\"field-content\"", renderer.Markup());
        Assert.Contains("atom:flex-col", renderer.Markup());
        Assert.Contains("atom:min-w-0", renderer.Markup());

        await renderer.MountAsync<FieldDescription>(new Dictionary<string, object?>());
        Assert.Contains("data-slot=\"field-description\"", renderer.Markup());
        Assert.Contains("atom:text-balance", renderer.Markup());
        Assert.Contains("atom:text-muted-foreground", renderer.Markup());

        await renderer.MountAsync<FieldLabel>(P(nameof(FieldLabel.For), "email-input"));
        Assert.Contains("data-slot=\"field-label\"", renderer.Markup());
        Assert.Contains("for=\"email-input\"", renderer.Markup());
        Assert.Contains("atom:peer-aria-[invalid=true]:text-destructive", renderer.Markup());

        await renderer.MountAsync<FieldSeparator>(new Dictionary<string, object?>());
        Assert.Contains("data-slot=\"field-separator\"", renderer.Markup());
        Assert.Contains("atom:border-border", renderer.Markup());
        Assert.Contains("atom:my-2", renderer.Markup());

        await renderer.MountAsync<FieldSet>(new Dictionary<string, object?>());
        Assert.Contains("<fieldset", renderer.Markup());
        Assert.Contains("data-slot=\"fieldset\"", renderer.Markup());

        await renderer.MountAsync<FieldTitle>(new Dictionary<string, object?>());
        Assert.Contains("data-slot=\"field-title\"", renderer.Markup());
    }

    [Fact]
    public async Task 属性展开与自定义类应并入根元素()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Field>(Params(
            @class: "my-field",
            additionalAttributes: new Dictionary<string, object> { ["data-testid"] = "field-root" }));

        var markup = renderer.Markup();
        Assert.Contains("data-testid=\"field-root\"", markup);
        Assert.Contains("my-field", markup);
    }

    // ------------------------------------------------------------------
    // 级联探针（测试专用，读取 FieldIsInvalid）
    // ------------------------------------------------------------------

    /// <summary>
    /// 读取 Field 以 Name="FieldIsInvalid" 下发的级联值并渲染到标记中，
 /// 用于守护级联沿（真实消费方为 的输入控件）。
    /// </summary>
    internal sealed class FieldIsInvalidProbe : ComponentBase
    {
        [CascadingParameter(Name = "FieldIsInvalid")]
        public bool? FieldIsInvalid { get; set; }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "span");
            builder.AddAttribute(1, "data-cascade", FieldIsInvalid switch
            {
                true => "true",
                false => "false",
                null => "null",
            });
            builder.CloseElement();
        }
    }
}
