using AtomBlazor.Primitives;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using AtomBlazor;
using AtomBlazor.Tests.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Xunit;

namespace AtomBlazor.Tests.Components;

/// <summary>
/// FormFieldTextarea 组合字段测试：在级联 EditContext 宿主与非宿主两种形态下挂载
/// FormFieldTextarea 并绑定 Doc 模型（复用 FormFieldBaseTests 的 EditContextHost），验证
/// 控件 id 与标签 for、aria-describedby 接线，ErrorText 手动覆写优先链，EditContext 校验消息
/// 经 FieldError 呈现，Required/Disabled/MaxLength 等透传参数面落内部 Textarea，以及
/// 输入沿回填模型并触发 NotifyFieldChanged。
/// </summary>
public class FormFieldTextareaTests
{
    /// <summary>
    /// 把 ControlId 中的随机 GUID 归一化为占位符，使快照与断言可确定。
    /// </summary>
    private static string Scrub(string markup) =>
        Regex.Replace(markup, "formfield-[0-9a-f]{32}", "formfield-<id>");

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
    /// 校验沿测试模型：Summary 为空白即无效（由假校验器执行）。
    /// </summary>
    internal sealed class Doc
    {
        public string? Summary { get; set; }
    }

    /// <summary>
    /// 构造挂接假校验器的 EditContext：订阅 OnFieldChanged/OnValidationRequested
    /// Summary 空白时用 ValidationMessageStore 写入消息。
    /// </summary>
    private static EditContext CreateValidatedContext(Doc doc)
    {
        var context = new EditContext(doc);
        var messages = new ValidationMessageStore(context);
        var field = new FieldIdentifier(doc, nameof(Doc.Summary));

        void Validate()
        {
            messages.Clear();
            if (string.IsNullOrWhiteSpace(doc.Summary))
            {
                messages.Add(field, "摘要不能为空");
            }
            context.NotifyValidationStateChanged();
        }

        context.OnFieldChanged += (_, _) => Validate();
        context.OnValidationRequested += (_, _) => Validate();
        return context;
    }

    private static Dictionary<string, object?> FieldParams(
        string? value = null,
        EventCallback<string?> valueChanged = default,
        Expression<Func<string?>>? valueExpression = null,
        string? label = null,
        string? helperText = null,
        string? errorText = null,
        string? name = null,
        string? placeholder = null,
        bool? disabled = null,
        bool? required = null,
        int? maxLength = null,
        bool? showCharacterCount = null,
        UpdateTiming? updateTiming = null,
        int? debounceInterval = null,
        string? inputClass = null,
        FieldOrientation? orientation = null)
    {
        var parameters = new Dictionary<string, object?>();
        if (value is not null)
        {
            parameters[nameof(FormFieldTextarea.Value)] = value;
        }
        if (valueChanged.HasDelegate)
        {
            parameters[nameof(FormFieldTextarea.ValueChanged)] = valueChanged;
        }
        if (valueExpression is not null)
        {
            parameters[nameof(FormFieldTextarea.ValueExpression)] = valueExpression;
        }
        if (label is not null)
        {
            parameters[nameof(FormFieldTextarea.Label)] = label;
        }
        if (helperText is not null)
        {
            parameters[nameof(FormFieldTextarea.HelperText)] = helperText;
        }
        if (errorText is not null)
        {
            parameters[nameof(FormFieldTextarea.ErrorText)] = errorText;
        }
        if (name is not null)
        {
            parameters[nameof(FormFieldTextarea.Name)] = name;
        }
        if (placeholder is not null)
        {
            parameters[nameof(FormFieldTextarea.Placeholder)] = placeholder;
        }
        if (disabled is not null)
        {
            parameters[nameof(FormFieldTextarea.Disabled)] = disabled.Value;
        }
        if (required is not null)
        {
            parameters[nameof(FormFieldTextarea.Required)] = required.Value;
        }
        if (maxLength is not null)
        {
            parameters[nameof(FormFieldTextarea.MaxLength)] = maxLength.Value;
        }
        if (showCharacterCount is not null)
        {
            parameters[nameof(FormFieldTextarea.ShowCharacterCount)] = showCharacterCount.Value;
        }
        if (updateTiming is not null)
        {
            parameters[nameof(FormFieldTextarea.UpdateTiming)] = updateTiming.Value;
        }
        if (debounceInterval is not null)
        {
            parameters[nameof(FormFieldTextarea.DebounceInterval)] = debounceInterval.Value;
        }
        if (inputClass is not null)
        {
            parameters[nameof(FormFieldTextarea.InputClass)] = inputClass;
        }
        if (orientation is not null)
        {
            parameters[nameof(FormFieldTextarea.Orientation)] = orientation.Value;
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

        await renderer.MountAsync<FormFieldTextarea>(FieldParams(
            value: "第一段草稿", label: "摘要", helperText: "最多 200 字", maxLength: 200,
            showCharacterCount: true));
        await SettleAsync(renderer);

        await Verify(Scrub(renderer.Markup()));
    }

    [Fact]
    public async Task 控件ID应与标签和描述接线()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<FormFieldTextarea>(FieldParams(
            value: "正文", label: "摘要", helperText: "帮助文本"));
        await SettleAsync(renderer);

        var markup = renderer.Markup();
        var match = Regex.Match(markup, @"id=""(formfield-[0-9a-f]{32})""");
        Assert.True(match.Success, "文本域应携带 ControlId 作为 id");

        var controlId = match.Groups[1].Value;
        Assert.Contains($"for=\"{controlId}\"", markup);
        Assert.Contains($"aria-describedby=\"{controlId}-description\"", markup);
        Assert.DoesNotContain("aria-invalid", markup);
    }

    [Fact]
    public async Task 错误文本手动覆写应显形并切换describedby()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<FormFieldTextarea>(FieldParams(
            value: "正文", label: "摘要", helperText: "帮助文本", errorText: "服务端驳回：内容过长"));
        await SettleAsync(renderer);

        var markup = Scrub(renderer.Markup());
        Assert.Contains("服务端驳回：内容过长", markup);
        Assert.Contains("data-slot=\"field-error\"", markup);
        Assert.Contains("aria-invalid=\"true\"", markup);

        // 错误态下帮助文本隐藏，describedby 切换到错误元素。
        Assert.DoesNotContain("帮助文本", markup);
        Assert.Contains("aria-describedby=\"formfield-<id>-error\"", markup);
        Assert.Equal(true, renderer.FindComponent<Textarea>().AriaInvalid);
    }

    // ------------------------------------------------------------------
    // EditContext 校验沿
    // ------------------------------------------------------------------

    [Fact]
    public async Task 清空值应经EditContext呈现校验消息()
    {
        var doc = new Doc { Summary = "ok" };
        var reports = new List<string?>();
        var renderer = CreateRenderer();
        var context = CreateValidatedContext(doc);

        Expression<Func<string?>> valueExpression = () => doc.Summary;
        await renderer.MountAsync<FormFieldBaseTests.EditContextHost>(new Dictionary<string, object?>
        {
            [nameof(FormFieldBaseTests.EditContextHost.EditContext)] = context,
            [nameof(FormFieldBaseTests.EditContextHost.ChildContent)] = (RenderFragment)(builder =>
            {
                builder.OpenComponent<FormFieldTextarea>(0);
                builder.AddAttribute(1, nameof(FormFieldTextarea.Value), doc.Summary);
                builder.AddAttribute(2, nameof(FormFieldTextarea.ValueExpression), valueExpression);
                builder.AddAttribute(3, nameof(FormFieldTextarea.ValueChanged),
                    EventCallback.Factory.Create<string?>(doc, value =>
                    {
                        doc.Summary = value;
                        reports.Add(value);
                    }));
                builder.AddAttribute(4, nameof(FormFieldTextarea.Label), (string?)"摘要");
                builder.CloseComponent();
            }),
        });
        await SettleAsync(renderer);

        var field = renderer.FindComponent<FormFieldTextarea>();
        var inner = renderer.FindComponent<Textarea>();

        // 输入沿：内部 Textarea 失焦回调 → 外层回填模型并 NotifyFieldChanged → 假校验器写入消息。
        await renderer.Dispatcher.InvokeAsync(() => inner.JsOnChange("   "));
        await SettleAsync(renderer);

        Assert.Null(doc.Summary);
        Assert.Single(reports);
        var markup = renderer.Markup();
        Assert.Contains("摘要不能为空", markup);
        Assert.Contains("data-slot=\"field-error\"", markup);
        Assert.Contains("aria-invalid=\"true\"", markup);
        Assert.True(field.TextareaRef != null);
    }

    // ------------------------------------------------------------------
    // 透传参数面
    // ------------------------------------------------------------------

    [Fact]
    public async Task 必填与禁用应透传内部文本域()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<FormFieldTextarea>(FieldParams(
            label: "摘要", required: true, disabled: true));
        await SettleAsync(renderer);

        var inner = renderer.FindComponent<Textarea>();
        Assert.True(inner.Required);
        Assert.True(inner.Disabled);

        var markup = renderer.Markup();
        Assert.Contains("required", markup);
        Assert.Contains("disabled", markup);
    }

    [Fact]
    public async Task 最大长度与字符计数应透传并落DOM()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<FormFieldTextarea>(FieldParams(
            value: "abc", maxLength: 500, showCharacterCount: true));
        await SettleAsync(renderer);

        var inner = renderer.FindComponent<Textarea>();
        Assert.Equal(500, inner.MaxLength);
        Assert.True(inner.ShowCharacterCount);

        var markup = renderer.Markup();
        Assert.Contains("maxlength=\"500\"", markup);
        Assert.Contains("data-character-count", markup);
        Assert.Contains("3/500", markup);
    }

    [Fact]
    public async Task 更新时机与防抖间隔应透传内部文本域()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<FormFieldTextarea>(FieldParams(
            updateTiming: UpdateTiming.Debounced, debounceInterval: 300));
        await SettleAsync(renderer);

        var inner = renderer.FindComponent<Textarea>();
        Assert.Equal(UpdateTiming.Debounced, inner.UpdateTiming);
        Assert.Equal(300, inner.DebounceInterval);
    }

    [Fact]
    public async Task 名称占位与输入类应透传()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<FormFieldTextarea>(FieldParams(
            name: "summary-field", placeholder: "写点什么…", inputClass: "atom:custom-input"));
        await SettleAsync(renderer);

        var inner = renderer.FindComponent<Textarea>();
        Assert.Equal("summary-field", inner.Name);
        Assert.Equal("写点什么…", inner.Placeholder);

        var markup = renderer.Markup();
        Assert.Contains("name=\"summary-field\"", markup);
        Assert.Contains("placeholder=\"写点什么…\"", markup);
        Assert.Contains("atom:custom-input", markup);
    }

    [Fact]
    public async Task 文本域引用应指向内部组件且外层参数默认值照()
    {
        var renderer = CreateRenderer();

        var field = await renderer.MountAsync<FormFieldTextarea>(FieldParams());
        await SettleAsync(renderer);

        var inner = renderer.FindComponent<Textarea>();

        Assert.NotNull(field.TextareaRef);
        Assert.Same(inner, field.TextareaRef);

        // 默认值：UpdateTiming.OnChange / DebounceInterval=500；Orientation 缺省 Vertical。
        Assert.Equal(UpdateTiming.OnChange, field.UpdateTiming);
        Assert.Equal(500, field.DebounceInterval);
        Assert.Equal(FieldOrientation.Vertical, field.Orientation);

        // Id 直写 ControlId（透传链）：文本域 id 即外层 ControlId。
        Assert.Equal(inner.Id, field.TextareaRef!.Id);
    }
}
