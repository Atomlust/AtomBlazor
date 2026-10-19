using AtomBlazor.Primitives;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using AtomBlazor;
using AtomBlazor.Tests.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Xunit;

namespace AtomBlazor.Tests.Components;

/// <summary>
/// FormFieldRadioGroup 组合字段测试：在级联 EditContext 宿主与非宿主两种形态下
/// 挂载 FormFieldRadioGroup&lt;string&gt;（复用 FormFieldBaseTests 的 EditContextHost）
/// 验证单选值绑定回填与 NotifyFieldChanged 校验沿、组标签不带 for 的组沿接线、
/// AriaLabel 显式优先与缺省回退 Label 的沿、aria-describedby 经描述/错误元素的双态切换、
/// Required/Disabled/InputClass/splat 透传面与 ErrorText 手动覆写链。
/// </summary>
public class FormFieldRadioGroupTests
{
    /// <summary>
    /// 归一化 ControlId 与项内随机 id，使快照与断言可确定。
    /// </summary>
    private static string Scrub(string markup) =>
        Regex.Replace(
            Regex.Replace(markup, "formfield-[0-9a-f]{32}", "formfield-<id>"),
            "radio-[0-9a-f]{32}", "radio-<id>");

    private static ComponentTestRenderer CreateRenderer()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(new NoopJavaScript());
        return new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance);
    }

    /// <summary>排空渲染队列，确保 StateHasChanged 的后续批已落定。</summary>
    private static Task SettleAsync(ComponentTestRenderer renderer) =>
        renderer.Dispatcher.InvokeAsync(() => Task.CompletedTask);

    /// <summary>
    /// 校验沿测试模型：Priority 非 "high" 即无效（由假校验器执行）。
    /// </summary>
    internal sealed class Ticket
    {
        public string? Priority { get; set; }
    }

    private static EditContext CreateValidatedContext(Ticket ticket)
    {
        var context = new EditContext(ticket);
        var messages = new ValidationMessageStore(context);
        var field = new FieldIdentifier(ticket, nameof(Ticket.Priority));

        void Validate()
        {
            messages.Clear();
            if (ticket.Priority != "high")
            {
                messages.Add(field, "必须选择高优先级");
            }
            context.NotifyValidationStateChanged();
        }

        context.OnFieldChanged += (_, _) => Validate();
        context.OnValidationRequested += (_, _) => Validate();
        return context;
    }

    private static RenderFragment Items(params (string Value, string? Label)[] items) => builder =>
    {
        var seq = 0;
        foreach (var (value, label) in items)
        {
            builder.OpenComponent<RadioGroupItem<string>>(seq++);
            builder.AddAttribute(seq++, nameof(RadioGroupItem<string>.Value), value);
            if (label is not null)
            {
                builder.AddAttribute(seq++, nameof(RadioGroupItem<string>.Label), label);
            }

            builder.CloseComponent();
        }
    };

    private static Dictionary<string, object?> FieldParams(
        RenderFragment childContent,
        string? value = null,
        EventCallback<string?> valueChanged = default,
        Expression<Func<string?>>? valueExpression = null,
        string? label = null,
        string? helperText = null,
        string? errorText = null,
        bool? disabled = null,
        bool? required = null,
        string? ariaLabel = null,
        string? inputClass = null,
        Dictionary<string, object>? additionalAttributes = null)
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(FormFieldRadioGroup<string>.ChildContent)] = childContent,
        };
        if (value is not null)
        {
            parameters[nameof(FormFieldRadioGroup<string>.Value)] = value;
        }
        if (valueChanged.HasDelegate)
        {
            parameters[nameof(FormFieldRadioGroup<string>.ValueChanged)] = valueChanged;
        }
        if (valueExpression is not null)
        {
            parameters[nameof(FormFieldRadioGroup<string>.ValueExpression)] = valueExpression;
        }
        if (label is not null)
        {
            parameters[nameof(FormFieldRadioGroup<string>.Label)] = label;
        }
        if (helperText is not null)
        {
            parameters[nameof(FormFieldRadioGroup<string>.HelperText)] = helperText;
        }
        if (errorText is not null)
        {
            parameters[nameof(FormFieldRadioGroup<string>.ErrorText)] = errorText;
        }
        if (disabled is not null)
        {
            parameters[nameof(FormFieldRadioGroup<string>.Disabled)] = disabled.Value;
        }
        if (required is not null)
        {
            parameters[nameof(FormFieldRadioGroup<string>.Required)] = required.Value;
        }
        if (ariaLabel is not null)
        {
            parameters[nameof(FormFieldRadioGroup<string>.AriaLabel)] = ariaLabel;
        }
        if (inputClass is not null)
        {
            parameters[nameof(FormFieldRadioGroup<string>.InputClass)] = inputClass;
        }
        if (additionalAttributes is not null)
        {
            parameters[nameof(FormFieldRadioGroup<string>.AdditionalAttributes)] = additionalAttributes;
        }

        return parameters;
    }

    // ------------------------------------------------------------------
    // 快照与接线
    // ------------------------------------------------------------------

    [Fact]
    public async Task 默认结构快照应与基线一致()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<FormFieldRadioGroup<string>>(FieldParams(
            Items(("high", "高"), ("low", "低")),
            value: "high",
            label: "优先级",
            helperText: "影响处理时效"));
        await SettleAsync(renderer);

        await Verify(Scrub(renderer.Markup()));
    }

    [Fact]
    public async Task 组标签应不带for且无障碍名缺省回退标签()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<FormFieldRadioGroup<string>>(FieldParams(
            Items(("high", "高")),
            value: "high",
            label: "优先级",
            helperText: "影响处理时效"));
        await SettleAsync(renderer);

        var markup = renderer.Markup();
        Assert.Contains("data-slot=\"field-label\"", markup);

        // 组件形态：FieldLabel 不传 For（组无单一控件可指）。
        var labelTag = Regex.Match(markup, "<label[^>]*data-slot=\"field-label\"");
        Assert.True(labelTag.Success);
        Assert.DoesNotContain("for=", labelTag.Value);

        // 沿：内部 RadioGroup 的 AriaLabel 取 AriaLabel ?? Label。
        var groupTag = Regex.Match(markup, "<div[^>]*role=\"radiogroup\"[^>]*>");
        Assert.True(groupTag.Success);
        Assert.Contains("aria-label=\"优先级\"", groupTag.Value);

        // 有效态下 describedby 指向描述元素。
        // 注记：本组件不向内部 RadioGroup 传 Id（包装层无该参数）
        // ControlId 仅出现在描述/错误元素 id 与 describedby 链接上。
        var id = Regex.Match(markup, "id=\"(formfield-[0-9a-f]{32})-description\"");
        Assert.True(id.Success, "描述元素应携带 ControlId-description");
        Assert.Contains($"aria-describedby=\"{id.Groups[1].Value}-description\"", markup);
    }

    [Fact]
    public async Task 显式无障碍标签应压过文本标签()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<FormFieldRadioGroup<string>>(FieldParams(
            Items(("high", "高")),
            value: "high",
            label: "优先级",
            ariaLabel: "工单优先级选择"));
        await SettleAsync(renderer);

        var markup = renderer.Markup();
        var groupTag = Regex.Match(markup, "<div[^>]*role=\"radiogroup\"[^>]*>");
        Assert.Contains("aria-label=\"工单优先级选择\"", groupTag.Value);

        // 文本标签仍照常渲染。
        Assert.Contains("优先级", markup);
    }

    // ------------------------------------------------------------------
    // 值绑定沿
    // ------------------------------------------------------------------

    [Fact]
    public async Task 点击条目应回填模型并经EditContext播报校验()
    {
        var ticket = new Ticket();
        var reports = new List<string?>();
        var renderer = CreateRenderer();
        var context = CreateValidatedContext(ticket);

        Expression<Func<string?>> valueExpression = () => ticket.Priority;
        await renderer.MountAsync<FormFieldBaseTests.EditContextHost>(new Dictionary<string, object?>
        {
            [nameof(FormFieldBaseTests.EditContextHost.EditContext)] = context,
            [nameof(FormFieldBaseTests.EditContextHost.ChildContent)] = (RenderFragment)(builder =>
            {
                builder.OpenComponent<FormFieldRadioGroup<string>>(0);
                builder.AddAttribute(1, nameof(FormFieldRadioGroup<string>.ValueExpression), valueExpression);
                builder.AddAttribute(2, nameof(FormFieldRadioGroup<string>.ValueChanged),
                    EventCallback.Factory.Create<string?>(ticket, value =>
                    {
                        ticket.Priority = value;
                        reports.Add(value);
                    }));
                builder.AddAttribute(3, nameof(FormFieldRadioGroup<string>.Label), (string?)"优先级");
                builder.AddAttribute(4, nameof(FormFieldRadioGroup<string>.ChildContent), Items(("high", "高"), ("low", "低")));
                builder.CloseComponent();
            }),
        });
        await SettleAsync(renderer);

        // occurrence 0/1 是两个项内按钮的 onclick（照 RadioGroupTests 口径）。
        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs(), occurrence: 0));
        await SettleAsync(renderer);

        Assert.Equal("high", ticket.Priority);
        Assert.Single(reports);
        Assert.DoesNotContain("必须选择高优先级", renderer.Markup());

        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs(), occurrence: 1));
        await SettleAsync(renderer);

        Assert.Equal("low", ticket.Priority);
        Assert.Contains("必须选择高优先级", renderer.Markup());
        Assert.Contains("data-slot=\"field-error\"", renderer.Markup());
    }

    [Fact]
    public async Task 宿主回写值后对应条目应呈现选中()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<FormFieldRadioGroup<string>>(FieldParams(
            Items(("high", "高"), ("low", "低")),
            value: "high",
            label: "优先级"));
        await SettleAsync(renderer);

        Assert.Equal(1, Regex.Count(renderer.Markup(), "aria-checked=\"true\""));

        await renderer.RerenderRootAsync(0, FieldParams(
            Items(("high", "高"), ("low", "低")),
            value: "low",
            label: "优先级"));
        await SettleAsync(renderer);

        var markup = renderer.Markup();
        Assert.Contains("aria-checked=\"true\"", markup);
        Assert.Contains("aria-checked=\"false\"", markup);

        // 无效态经 Field 级联：EditContext 缺席时不写 aria-invalid。
        Assert.DoesNotContain("aria-invalid=\"true\"", markup);
    }

    // ------------------------------------------------------------------
    // 错误链与透传面
    // ------------------------------------------------------------------

    [Fact]
    public async Task 错误文本手动覆写应显形并切换describedby()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<FormFieldRadioGroup<string>>(FieldParams(
            Items(("high", "高")),
            value: "high",
            label: "优先级",
            helperText: "影响处理时效",
            errorText: "当前等级不可用"));
        await SettleAsync(renderer);

        var markup = Scrub(renderer.Markup());
        Assert.Contains("当前等级不可用", markup);
        Assert.Contains("data-slot=\"field-error\"", markup);
        Assert.DoesNotContain("影响处理时效", markup);
        Assert.Contains("aria-describedby=\"formfield-<id>-error\"", markup);
        Assert.Contains("data-invalid=\"true\"", markup);
    }

    [Fact]
    public async Task 必填与禁用应透传内部单选组()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<FormFieldRadioGroup<string>>(FieldParams(
            Items(("high", "高")),
            label: "优先级",
            required: true,
            disabled: true));
        await SettleAsync(renderer);

        var inner = renderer.FindComponent<RadioGroup<string>>();
        Assert.True(inner.Required);
        Assert.True(inner.Disabled);

        var markup = renderer.Markup();
        Assert.Contains("aria-required=\"true\"", markup);
        Assert.Contains("aria-disabled=\"true\"", markup);
    }

    [Fact]
    public async Task 输入类与附加属性应落内部单选组元素()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<FormFieldRadioGroup<string>>(FieldParams(
            Items(("high", "高")),
            value: "high",
            label: "优先级",
            inputClass: "atom:custom-radio-group",
            additionalAttributes: new Dictionary<string, object> { ["data-testid"] = "priority-group" }));
        await SettleAsync(renderer);

        var markup = renderer.Markup();

        // splat 直落内部 RadioGroup（原语根 div 携带 role="radiogroup"）。
        var groupTag = Regex.Match(markup, "<div[^>]*data-testid=\"priority-group\"[^>]*>");
        Assert.True(groupTag.Success);
        Assert.Contains("role=\"radiogroup\"", groupTag.Value);
        Assert.Contains("atom:custom-radio-group", groupTag.Value);
    }
}
