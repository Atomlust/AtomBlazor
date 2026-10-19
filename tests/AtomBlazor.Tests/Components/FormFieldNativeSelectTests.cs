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
/// FormFieldNativeSelect 组合字段测试：在级联 EditContext 宿主与非宿主两种形态下挂载
/// FormFieldNativeSelect&lt;string&gt; 并绑定 Doc 模型（复用 FormFieldBaseTests 的 EditContextHost）
/// 验证控件 id 与标签 for 接线、select 元素不携带 aria-describedby（ quirk：本家族不把
/// describedby 落控件，与 Textarea 家族不对称）、ErrorText 手动覆写链只落 Field 根 data-invalid
/// 而不写控件 aria-invalid（NativeSelect 包装层本无该面）、onchange 回转经外层三步沿回填模型并
/// 触发 NotifyFieldChanged、Placeholder/Required/Disabled/Size/InputClass/splat 透传面与
/// option 子内容原样落盘。headless 口径沿用 NativeSelectTests（onchange 直发 ChangeEventArgs）。
/// </summary>
public class FormFieldNativeSelectTests
{
    /// <summary>
    /// 归一化 ControlId 中的随机 GUID 为占位符，使快照与断言可确定。
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

    /// <summary>校验沿测试模型：Country 空白即无效（由假校验器执行）。</summary>
    internal sealed class Doc
    {
        public string? Country { get; set; }
    }

    /// <summary>
    /// 构造挂接假校验器的 EditContext：订阅 OnFieldChanged/OnValidationRequested
    /// Country 空白时用 ValidationMessageStore 写入消息。
    /// </summary>
    private static EditContext CreateValidatedContext(Doc doc)
    {
        var context = new EditContext(doc);
        var messages = new ValidationMessageStore(context);
        var field = new FieldIdentifier(doc, nameof(Doc.Country));

        void Validate()
        {
            messages.Clear();
            if (string.IsNullOrWhiteSpace(doc.Country))
            {
                messages.Add(field, "国家不能为空");
            }
            context.NotifyValidationStateChanged();
        }

        context.OnFieldChanged += (_, _) => Validate();
        context.OnValidationRequested += (_, _) => Validate();
        return context;
    }

    /// <summary>两条 option 子内容（NativeSelectTests 先例形态）。</summary>
    private static RenderFragment OptionChildren(params (string Value, string Text)[] options) => builder =>
    {
        var seq = 0;
        foreach (var (value, text) in options)
        {
            builder.OpenElement(seq++, "option");
            builder.AddAttribute(seq++, "value", value);
            builder.AddContent(seq++, text);
            builder.CloseElement();
        }
    };

    private static Dictionary<string, object?> FieldParams(
        string? value = null,
        EventCallback<string?> valueChanged = default,
        Expression<Func<string?>>? valueExpression = null,
        string? placeholder = null,
        bool? disabled = null,
        bool? required = null,
        NativeSelectSize? size = null,
        RenderFragment? childContent = null,
        string? inputClass = null,
        string? label = null,
        string? helperText = null,
        string? errorText = null,
        string? ariaLabel = null,
        FieldOrientation? orientation = null,
        IReadOnlyDictionary<string, object?>? splat = null)
    {
        var parameters = new Dictionary<string, object?>();
        if (value is not null)
        {
            parameters[nameof(FormFieldNativeSelect<string>.Value)] = value;
        }
        if (valueChanged.HasDelegate)
        {
            parameters[nameof(FormFieldNativeSelect<string>.ValueChanged)] = valueChanged;
        }
        if (valueExpression is not null)
        {
            parameters[nameof(FormFieldNativeSelect<string>.ValueExpression)] = valueExpression;
        }
        if (placeholder is not null)
        {
            parameters[nameof(FormFieldNativeSelect<string>.Placeholder)] = placeholder;
        }
        if (disabled is not null)
        {
            parameters[nameof(FormFieldNativeSelect<string>.Disabled)] = disabled.Value;
        }
        if (required is not null)
        {
            parameters[nameof(FormFieldNativeSelect<string>.Required)] = required.Value;
        }
        if (size is not null)
        {
            parameters[nameof(FormFieldNativeSelect<string>.Size)] = size.Value;
        }
        if (childContent is not null)
        {
            parameters[nameof(FormFieldNativeSelect<string>.ChildContent)] = childContent;
        }
        if (inputClass is not null)
        {
            parameters[nameof(FormFieldNativeSelect<string>.InputClass)] = inputClass;
        }
        if (label is not null)
        {
            parameters[nameof(FormFieldNativeSelect<string>.Label)] = label;
        }
        if (helperText is not null)
        {
            parameters[nameof(FormFieldNativeSelect<string>.HelperText)] = helperText;
        }
        if (errorText is not null)
        {
            parameters[nameof(FormFieldNativeSelect<string>.ErrorText)] = errorText;
        }
        if (ariaLabel is not null)
        {
            parameters[nameof(FormFieldNativeSelect<string>.AriaLabel)] = ariaLabel;
        }
        if (orientation is not null)
        {
            parameters[nameof(FormFieldNativeSelect<string>.Orientation)] = orientation.Value;
        }
        if (splat is not null)
        {
            parameters["AdditionalAttributes"] = new Dictionary<string, object?>(splat);
        }

        return parameters;
    }

    /// <summary>截取 select 开标签，限定对该元素的断言范围。</summary>
    private static string SelectTag(string markup) =>
        Regex.Match(markup, @"<select[^>]*>").Value;

    // ------------------------------------------------------------------
    // 快照与接线
    // ------------------------------------------------------------------

    [Fact]
    public async Task 默认结构快照应与基线一致()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<FormFieldNativeSelect<string>>(FieldParams(
            label: "国家", helperText: "选择常住国家", placeholder: "请选择国家",
            childContent: OptionChildren(("us", "United States"), ("uk", "United Kingdom"))));
        await SettleAsync(renderer);

        await Verify(Scrub(renderer.Markup()));
    }

    [Fact]
    public async Task 控件ID应与标签for接线但select不携带describedby()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<FormFieldNativeSelect<string>>(FieldParams(
            label: "国家", helperText: "帮助文本",
            childContent: OptionChildren(("us", "United States"))));
        await SettleAsync(renderer);

        var markup = renderer.Markup();
        var match = Regex.Match(markup, @"id=""(formfield-[0-9a-f]{32})""");
        Assert.True(match.Success, "select 元素应携带 ControlId 作为 id");

        var controlId = match.Groups[1].Value;
        Assert.Contains($"for=\"{controlId}\"", markup);
        Assert.Contains($"id=\"{controlId}-description\"", markup);
        // quirk：本家族不把 DescribedById 传控件（与 Textarea 家族不对称）
        // 帮助/错误元素有 id 却无任何 aria-describedby 引用。
        Assert.DoesNotContain("aria-describedby", markup);
    }

    // ------------------------------------------------------------------
    // 值绑定与 EditContext 沿
    // ------------------------------------------------------------------

    [Fact]
    public async Task onchange应回填模型并触发EditContext消息消长()
    {
        var doc = new Doc { Country = "us" };
        var reports = new List<string?>();
        var renderer = CreateRenderer();
        var context = CreateValidatedContext(doc);

        Expression<Func<string?>> valueExpression = () => doc.Country;
        await renderer.MountAsync<FormFieldBaseTests.EditContextHost>(new Dictionary<string, object?>
        {
            [nameof(FormFieldBaseTests.EditContextHost.EditContext)] = context,
            [nameof(FormFieldBaseTests.EditContextHost.ChildContent)] = (RenderFragment)(builder =>
            {
                builder.OpenComponent<FormFieldNativeSelect<string>>(0);
                builder.AddAttribute(1, nameof(FormFieldNativeSelect<string>.Value), doc.Country);
                builder.AddAttribute(2, nameof(FormFieldNativeSelect<string>.ValueExpression), valueExpression);
                builder.AddAttribute(3, nameof(FormFieldNativeSelect<string>.ValueChanged),
                    EventCallback.Factory.Create<string?>(doc, value =>
                    {
                        doc.Country = value;
                        reports.Add(value);
                    }));
                builder.AddAttribute(4, nameof(FormFieldNativeSelect<string>.Label), (string?)"国家");
                builder.AddAttribute(5, nameof(FormFieldNativeSelect<string>.Placeholder), "请选择");
                builder.AddAttribute(6, nameof(FormFieldNativeSelect<string>.ChildContent),
                    OptionChildren(("us", "United States"), ("uk", "United Kingdom")));
                builder.CloseComponent();
            }),
        });
        await SettleAsync(renderer);

        // EditContext 集成沿：未传 Name 时 select 的 name 回退字段名。
        Assert.Contains("name=\"Country\"", SelectTag(renderer.Markup()));

        // 选中占位项（value="" → 回转 null）→ 外层三步沿 → NotifyFieldChanged → 假校验器写消息。
        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onchange", new ChangeEventArgs { Value = "" }));
        await SettleAsync(renderer);

        Assert.Null(doc.Country);
        Assert.Single(reports);
        var markup = renderer.Markup();
        Assert.Contains("国家不能为空", markup);
        Assert.Contains("data-slot=\"field-error\"", markup);
        Assert.Contains("data-invalid=\"true\"", markup);

        // 再选 "uk" → 消息消解。
        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onchange", new ChangeEventArgs { Value = "uk" }));
        await SettleAsync(renderer);

        Assert.Equal("uk", doc.Country);
        markup = renderer.Markup();
        Assert.DoesNotContain("国家不能为空", markup);
        Assert.DoesNotContain("data-invalid=\"true\"", markup);
    }

    [Fact]
    public async Task 占位项应只在值为空时被标记为selected()
    {
        var renderer = CreateRenderer();
        await renderer.MountAsync<FormFieldNativeSelect<string>>(FieldParams(
            placeholder: "请选择国家", childContent: OptionChildren(("us", "United States"))));
        await SettleAsync(renderer);

        var placeholder = Regex.Match(renderer.Markup(), @"<option[^>]*>[^<]*</option>").Value;
        Assert.Contains("disabled", placeholder);
        Assert.Contains("selected", placeholder);

        var picked = CreateRenderer();
        await picked.MountAsync<FormFieldNativeSelect<string>>(FieldParams(
            value: "us", placeholder: "请选择国家", childContent: OptionChildren(("us", "United States"))));
        await SettleAsync(picked);

        Assert.DoesNotContain("selected", Regex.Match(picked.Markup(), @"<option value="""">[^>]*>").Value);
        Assert.Contains("value=\"us\"", SelectTag(picked.Markup()));
    }

    [Fact]
    public async Task 选项子内容应原样落在select内部()
    {
        var renderer = CreateRenderer();
        await renderer.MountAsync<FormFieldNativeSelect<string>>(FieldParams(
            childContent: OptionChildren(("us", "United States"), ("uk", "United Kingdom"), ("de", "Germany"))));
        await SettleAsync(renderer);

        var markup = renderer.Markup();
        Assert.Equal(3, Regex.Count(markup, "<option"));
        Assert.Contains("<option value=\"us\">United States</option>", markup);
    }

    // ------------------------------------------------------------------
    // 错误覆写与透传参数面
    // ------------------------------------------------------------------

    [Fact]
    public async Task 错误文本覆写应显形且select不落ariaInvalid()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<FormFieldNativeSelect<string>>(FieldParams(
            label: "国家", helperText: "帮助文本", errorText: "服务端驳回：国家不支持",
            childContent: OptionChildren(("us", "United States"))));
        await SettleAsync(renderer);

        var markup = Scrub(renderer.Markup());
        Assert.Contains("服务端驳回：国家不支持", markup);
        Assert.Contains("data-slot=\"field-error\"", markup);
        Assert.Contains("data-invalid=\"true\"", markup);
        Assert.DoesNotContain("帮助文本", markup);
        // quirk：NativeSelect 包装层零 aria-invalid/describedby 面，无效态只落 Field 根。
        Assert.DoesNotContain("aria-invalid", markup);
    }

    [Fact]
    public async Task 必填禁用与尺寸应透传select元素()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<FormFieldNativeSelect<string>>(FieldParams(
            required: true, disabled: true, size: NativeSelectSize.Small,
            childContent: OptionChildren(("us", "United States"))));
        await SettleAsync(renderer);

        var inner = renderer.FindComponent<NativeSelect<string>>();
        Assert.True(inner.Required);
        Assert.True(inner.Disabled);
        Assert.Equal(NativeSelectSize.Small, inner.Size);

        var tag = SelectTag(renderer.Markup());
        Assert.Contains("required", tag);
        Assert.Contains("disabled", tag);
        Assert.Contains("atom:h-8", tag); // Small 档尺寸类（缺省为 atom:h-10）
    }

    [Fact]
    public async Task 无障碍名称输入类与透传应落select元素()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<FormFieldNativeSelect<string>>(FieldParams(
            ariaLabel: "常住国家", inputClass: "atom:custom-select",
            childContent: OptionChildren(("us", "United States")),
            splat: new Dictionary<string, object?> { ["data-testid"] = "field-native-select" }));
        await SettleAsync(renderer);

        var tag = SelectTag(renderer.Markup());
        Assert.Contains("aria-label=\"常住国家\"", tag);
        Assert.Contains("atom:custom-select", tag);
        // splat 直落内部 select 元素而非 Field 根（与 FormFieldInput 家族同沿）。
        Assert.Contains("data-testid=\"field-native-select\"", tag);
    }

    [Fact]
    public async Task 缺省参数面应与一致()
    {
        var renderer = CreateRenderer();

        var field = await renderer.MountAsync<FormFieldNativeSelect<string>>(FieldParams());
        await SettleAsync(renderer);

        // 缺省：Orientation=Vertical（底座）、Size=Default、Required/Disabled false、
        // Placeholder/Value 空；Name 不在本家族参数面（透传清单无该项）。
        Assert.Equal(FieldOrientation.Vertical, field.Orientation);
        Assert.Equal(NativeSelectSize.Default, field.Size);
        Assert.False(field.Required);
        Assert.False(field.Disabled);
        Assert.Null(field.Placeholder);
        Assert.Null(field.Value);
        Assert.Null(field.InputClass);
        Assert.DoesNotContain("placeholder", renderer.Markup());
        Assert.DoesNotContain("name=", renderer.Markup());
    }
}
