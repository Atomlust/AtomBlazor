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
/// FormFieldCheckbox 组合字段测试：缺省方向 HorizontalEnd 接管沿、
/// EffectiveOrientation 归一 quirk（Field 容器恒收 Vertical，Responsive 除外）、
/// 四种方向的标签落位、控件 id 与标签/描述接线、ErrorText 手动覆写链、
/// 点击回填模型并经 EditContext 播报校验、Indeterminate/Required/Disabled/Name 透传面。
/// </summary>
public class FormFieldCheckboxTests
{
    /// <summary>
    /// 把 ControlId 中的随机 GUID 与项内复选框短 id 归一化为占位符，使快照可确定。
    /// </summary>
    private static string Scrub(string markup) =>
        Regex.Replace(
            Regex.Replace(markup, "formfield-[0-9a-f]{32}", "formfield-<id>"),
            "checkbox-[0-9a-f]{8}", "checkbox-<id>");

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
    /// 校验沿测试模型：Agreed 未勾选即无效（由假校验器执行）。
    /// </summary>
    internal sealed class Terms
    {
        public bool Agreed { get; set; }
    }

    /// <summary>
    /// 构造挂接假校验器的 EditContext：未勾选时写入消息。
    /// </summary>
    private static EditContext CreateValidatedContext(Terms terms)
    {
        var context = new EditContext(terms);
        var messages = new ValidationMessageStore(context);
        var field = new FieldIdentifier(terms, nameof(Terms.Agreed));

        void Validate()
        {
            messages.Clear();
            if (!terms.Agreed)
            {
                messages.Add(field, "必须同意条款");
            }
            context.NotifyValidationStateChanged();
        }

        context.OnFieldChanged += (_, _) => Validate();
        context.OnValidationRequested += (_, _) => Validate();
        return context;
    }

    private static Dictionary<string, object?> FieldParams(
        bool? @checked = null,
        EventCallback<bool> checkedChanged = default,
        Expression<Func<bool>>? checkedExpression = null,
        string? label = null,
        string? helperText = null,
        string? errorText = null,
        string? name = null,
        bool? indeterminate = null,
        bool? disabled = null,
        bool? required = null,
        string? inputClass = null,
        FieldOrientation? orientation = null)
    {
        var parameters = new Dictionary<string, object?>();
        if (@checked is not null)
        {
            parameters[nameof(FormFieldCheckbox.Checked)] = @checked.Value;
        }
        if (checkedChanged.HasDelegate)
        {
            parameters[nameof(FormFieldCheckbox.CheckedChanged)] = checkedChanged;
        }
        if (checkedExpression is not null)
        {
            parameters[nameof(FormFieldCheckbox.CheckedExpression)] = checkedExpression;
        }
        if (label is not null)
        {
            parameters[nameof(FormFieldCheckbox.Label)] = label;
        }
        if (helperText is not null)
        {
            parameters[nameof(FormFieldCheckbox.HelperText)] = helperText;
        }
        if (errorText is not null)
        {
            parameters[nameof(FormFieldCheckbox.ErrorText)] = errorText;
        }
        if (name is not null)
        {
            parameters[nameof(FormFieldCheckbox.Name)] = name;
        }
        if (indeterminate is not null)
        {
            parameters[nameof(FormFieldCheckbox.Indeterminate)] = indeterminate.Value;
        }
        if (disabled is not null)
        {
            parameters[nameof(FormFieldCheckbox.Disabled)] = disabled.Value;
        }
        if (required is not null)
        {
            parameters[nameof(FormFieldCheckbox.Required)] = required.Value;
        }
        if (inputClass is not null)
        {
            parameters[nameof(FormFieldCheckbox.InputClass)] = inputClass;
        }
        if (orientation is not null)
        {
            parameters[nameof(FormFieldCheckbox.Orientation)] = orientation.Value;
        }

        return parameters;
    }

    // ------------------------------------------------------------------
    // 快照与方向沿
    // ------------------------------------------------------------------

    [Fact]
    public async Task 缺省水平末端方向快照应与基线一致()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<FormFieldCheckbox>(FieldParams(
            label: "同意条款", helperText: "勾选后即可提交"));
        await SettleAsync(renderer);

        await Verify(Scrub(renderer.Markup()));
    }

    [Fact]
    public async Task 未指定方向时参数缺省应接管为水平末端()
    {
        var renderer = CreateRenderer();

        var field = await renderer.MountAsync<FormFieldCheckbox>(FieldParams(label: "同意条款"));
        await SettleAsync(renderer);

        // SetParametersAsync 覆写：未显式传 Orientation 时接管为 HorizontalEnd。
        Assert.Equal(FieldOrientation.HorizontalEnd, field.Orientation);

        // EffectiveOrientation quirk：HorizontalEnd 归一为 Vertical 交给 Field 容器
        // 标签与控件的水平并排由 FieldContent 内层 flex div 承担。
        var markup = renderer.Markup();
        Assert.Contains("data-orientation=\"vertical\"", markup);
        Assert.Contains("atom:flex atom:items-center atom:gap-2", markup);
    }

    [Fact]
    public async Task 水平与垂直末端方向也应归一为垂直容器()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<FormFieldCheckbox>(FieldParams(
            label: "同意条款", orientation: FieldOrientation.Horizontal));
        await SettleAsync(renderer);

        Assert.Contains("data-orientation=\"vertical\"", renderer.Markup());

        await renderer.RerenderRootAsync(0, FieldParams(
            label: "同意条款", orientation: FieldOrientation.VerticalEnd));
        await SettleAsync(renderer);

        Assert.Contains("data-orientation=\"vertical\"", renderer.Markup());
    }

    [Fact]
    public async Task 响应式方向不应被归一()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<FormFieldCheckbox>(FieldParams(
            label: "同意条款", orientation: FieldOrientation.Responsive));
        await SettleAsync(renderer);

        // Responsive 不在 EffectiveOrientation 归一集合内，原样交给 Field。
        Assert.Contains("data-orientation=\"responsive\"", renderer.Markup());
    }

    [Fact]
    public async Task 垂直方向标签应置于控件上方()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<FormFieldCheckbox>(FieldParams(
            label: "同意条款", orientation: FieldOrientation.Vertical));
        await SettleAsync(renderer);

        var markup = renderer.Markup();
        var labelIndex = markup.IndexOf("data-slot=\"field-label\"", StringComparison.Ordinal);
        var contentIndex = markup.IndexOf("data-slot=\"field-content\"", StringComparison.Ordinal);
        var checkboxIndex = markup.IndexOf("role=\"checkbox\"", StringComparison.Ordinal);
        Assert.True(labelIndex >= 0 && labelIndex < contentIndex && contentIndex < checkboxIndex,
            "Vertical 沿：标签先于 FieldContent，控件在内容区内");

        // 不在内层 flex 包裹 div 中。
        Assert.DoesNotContain("atom:items-center atom:gap-2", markup);
    }

    [Fact]
    public async Task 水平方向标签应在控件之前成对出现且仅一枚()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<FormFieldCheckbox>(FieldParams(
            label: "同意条款", orientation: FieldOrientation.Horizontal));
        await SettleAsync(renderer);

        var markup = renderer.Markup();
        Assert.Single(Regex.Matches(markup, "data-slot=\"field-label\""));

        var labelIndex = markup.IndexOf("data-slot=\"field-label\"", StringComparison.Ordinal);
        var checkboxIndex = markup.IndexOf("role=\"checkbox\"", StringComparison.Ordinal);
        Assert.True(labelIndex > markup.IndexOf("data-slot=\"field-content\"", StringComparison.Ordinal));
        Assert.True(labelIndex < checkboxIndex, "Horizontal 沿：标签在包裹 div 内、控件之前");
    }

    // ------------------------------------------------------------------
    // 接线与错误链
    // ------------------------------------------------------------------

    [Fact]
    public async Task 控件ID应与标签和描述接线()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<FormFieldCheckbox>(FieldParams(
            label: "同意条款", helperText: "勾选后即可提交"));
        await SettleAsync(renderer);

        var markup = renderer.Markup();
        var match = Regex.Match(markup, @"id=""(formfield-[0-9a-f]{32})""");
        Assert.True(match.Success, "复选框应携带 ControlId 作为 id");

        var controlId = match.Groups[1].Value;
        Assert.Contains($"for=\"{controlId}\"", markup);
        Assert.Contains($"aria-describedby=\"{controlId}-description\"", markup);
    }

    [Fact]
    public async Task 错误文本手动覆写应显形并切换describedby()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<FormFieldCheckbox>(FieldParams(
            label: "同意条款", helperText: "勾选后即可提交", errorText: "请先阅读条款"));
        await SettleAsync(renderer);

        var markup = Scrub(renderer.Markup());
        Assert.Contains("请先阅读条款", markup);
        Assert.Contains("data-slot=\"field-error\"", markup);

        // 错误态下帮助文本隐藏，describedby 切换到错误元素；Field 容器进入无效态。
        // 注记（实据）：包装层 Checkbox 的 aria-invalid 仅在自身 EditContext 校验沿
        // 或 FieldIsInvalid 级联命中时写出，ErrorText 手动覆写沿不落该属性（与
        // FormFieldTextarea 的 AriaInvalid 显式透传不同），断言以 Field 根 data-invalid 为据。
        Assert.DoesNotContain("勾选后即可提交", markup);
        Assert.Contains("aria-describedby=\"formfield-<id>-error\"", markup);
        Assert.Contains("data-invalid=\"true\"", markup);
    }

    [Fact]
    public async Task 点击应回填模型并经EditContext播报校验()
    {
        var terms = new Terms();
        var reports = new List<bool>();
        var renderer = CreateRenderer();
        var context = CreateValidatedContext(terms);

        Expression<Func<bool>> checkedExpression = () => terms.Agreed;
        await renderer.MountAsync<FormFieldBaseTests.EditContextHost>(new Dictionary<string, object?>
        {
            [nameof(FormFieldBaseTests.EditContextHost.EditContext)] = context,
            [nameof(FormFieldBaseTests.EditContextHost.ChildContent)] = (RenderFragment)(builder =>
            {
                builder.OpenComponent<FormFieldCheckbox>(0);
                builder.AddAttribute(1, nameof(FormFieldCheckbox.CheckedExpression), checkedExpression);
                builder.AddAttribute(2, nameof(FormFieldCheckbox.CheckedChanged),
                    EventCallback.Factory.Create<bool>(terms, value =>
                    {
                        terms.Agreed = value;
                        reports.Add(value);
                    }));
                builder.AddAttribute(3, nameof(FormFieldCheckbox.Label), (string?)"同意条款");
                builder.CloseComponent();
            }),
        });
        await SettleAsync(renderer);

        // 树内唯一 onclick 携带者是原语复选框按钮（occurrence 0）。
        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs()));
        await SettleAsync(renderer);

        Assert.True(terms.Agreed);
        Assert.Single(reports);
        Assert.DoesNotContain("必须同意条款", renderer.Markup());

        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs()));
        await SettleAsync(renderer);

        Assert.False(terms.Agreed);
        Assert.Contains("必须同意条款", renderer.Markup());
        Assert.Contains("data-slot=\"field-error\"", renderer.Markup());
    }

    // ------------------------------------------------------------------
    // 透传参数面
    // ------------------------------------------------------------------

    [Fact]
    public async Task 必填与禁用应透传原语复选框()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<FormFieldCheckbox>(FieldParams(
            label: "同意条款", required: true, disabled: true));
        await SettleAsync(renderer);

        var inner = renderer.FindComponent<Checkbox>();
        Assert.True(inner.Required);
        Assert.True(inner.Disabled);

        var markup = renderer.Markup();
        Assert.Contains("aria-required=\"true\"", markup);
        Assert.Contains("disabled", markup);
    }

    [Fact]
    public async Task 不确定态应透传呈现mixed并落名称隐藏input()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<FormFieldCheckbox>(FieldParams(
            label: "全选", indeterminate: true, name: "select-all"));
        await SettleAsync(renderer);

        var inner = renderer.FindComponent<Checkbox>();
        Assert.True(inner.Indeterminate);
        Assert.Equal("select-all", inner.Name);

        var markup = renderer.Markup();
        Assert.Contains("aria-checked=\"mixed\"", markup);
        Assert.Contains("type=\"checkbox\"", markup);
        Assert.Contains("name=\"select-all\"", markup);
    }

    [Fact]
    public async Task 输入类应落内部复选框()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<FormFieldCheckbox>(FieldParams(
            label: "同意条款", inputClass: "atom:custom-check"));
        await SettleAsync(renderer);

        Assert.Equal("atom:custom-check", renderer.FindComponent<Checkbox>().Class);
        Assert.Contains("atom:custom-check", renderer.Markup());
    }
}
