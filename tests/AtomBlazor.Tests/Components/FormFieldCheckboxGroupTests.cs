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
/// FormFieldCheckboxGroup 组合字段测试：在级联 EditContext 宿主与非宿主两种形态下
/// 挂载 FormFieldCheckboxGroup&lt;string&gt;（复用 FormFieldBaseTests 的 EditContextHost）
/// 验证集合值绑定回填与 NotifyFieldChanged 校验沿、标签不带 for 的组沿接线
/// （组件无单控件可指）、ShowSelectAll/SelectAllLabel/Disabled/InputClass 透传面、
/// ErrorText 手动覆写与 EditContext 消息双源、附加属性落内部组元素而非 Field 根。
/// </summary>
public class FormFieldCheckboxGroupTests
{
    /// <summary>
    /// 归一化 ControlId、组内项 id 与项内复选框短 id，使快照与断言可确定。
    /// </summary>
    private static string Scrub(string markup) =>
        Regex.Replace(
            Regex.Replace(
                Regex.Replace(markup, "formfield-[0-9a-f]{32}", "formfield-<id>"),
                "cbg-[0-9a-f]{32}", "cbg-<id>"),
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
    /// 校验沿测试模型：Tags 为空集合即无效（由假校验器执行）。
    /// </summary>
    internal sealed class Profile
    {
        public IReadOnlyCollection<string> Tags { get; set; } = Array.Empty<string>();
    }

    private static EditContext CreateValidatedContext(Profile profile)
    {
        var context = new EditContext(profile);
        var messages = new ValidationMessageStore(context);
        var field = new FieldIdentifier(profile, nameof(Profile.Tags));

        void Validate()
        {
            messages.Clear();
            if (profile.Tags.Count == 0)
            {
                messages.Add(field, "至少选择一个标签");
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
            builder.OpenComponent<CheckboxGroupItem<string>>(seq++);
            builder.AddAttribute(seq++, nameof(CheckboxGroupItem<string>.Value), value);
            if (label is not null)
            {
                builder.AddAttribute(seq++, nameof(CheckboxGroupItem<string>.Label), label);
            }

            builder.CloseComponent();
        }
    };

    private static Dictionary<string, object?> FieldParams(
        RenderFragment childContent,
        IReadOnlyCollection<string>? values = null,
        EventCallback<IReadOnlyCollection<string>> valuesChanged = default,
        Expression<Func<IReadOnlyCollection<string>>>? valuesExpression = null,
        string? label = null,
        string? helperText = null,
        string? errorText = null,
        bool? showSelectAll = null,
        string? selectAllLabel = null,
        bool? disabled = null,
        string? ariaLabel = null,
        string? inputClass = null,
        Dictionary<string, object>? additionalAttributes = null)
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(FormFieldCheckboxGroup<string>.ChildContent)] = childContent,
        };
        if (values is not null)
        {
            parameters[nameof(FormFieldCheckboxGroup<string>.Values)] = values;
        }
        if (valuesChanged.HasDelegate)
        {
            parameters[nameof(FormFieldCheckboxGroup<string>.ValuesChanged)] = valuesChanged;
        }
        if (valuesExpression is not null)
        {
            parameters[nameof(FormFieldCheckboxGroup<string>.ValuesExpression)] = valuesExpression;
        }
        if (label is not null)
        {
            parameters[nameof(FormFieldCheckboxGroup<string>.Label)] = label;
        }
        if (helperText is not null)
        {
            parameters[nameof(FormFieldCheckboxGroup<string>.HelperText)] = helperText;
        }
        if (errorText is not null)
        {
            parameters[nameof(FormFieldCheckboxGroup<string>.ErrorText)] = errorText;
        }
        if (showSelectAll is not null)
        {
            parameters[nameof(FormFieldCheckboxGroup<string>.ShowSelectAll)] = showSelectAll.Value;
        }
        if (selectAllLabel is not null)
        {
            parameters[nameof(FormFieldCheckboxGroup<string>.SelectAllLabel)] = selectAllLabel;
        }
        if (disabled is not null)
        {
            parameters[nameof(FormFieldCheckboxGroup<string>.Disabled)] = disabled.Value;
        }
        if (ariaLabel is not null)
        {
            parameters[nameof(FormFieldCheckboxGroup<string>.AriaLabel)] = ariaLabel;
        }
        if (inputClass is not null)
        {
            parameters[nameof(FormFieldCheckboxGroup<string>.InputClass)] = inputClass;
        }
        if (additionalAttributes is not null)
        {
            parameters[nameof(FormFieldCheckboxGroup<string>.AdditionalAttributes)] = additionalAttributes;
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

        await renderer.MountAsync<FormFieldCheckboxGroup<string>>(FieldParams(
            Items(("a", "选项 A"), ("b", "选项 B")),
            values: ["a"],
            label: "兴趣标签",
            helperText: "可多选"));
        await SettleAsync(renderer);

        await Verify(Scrub(renderer.Markup()));
    }

    [Fact]
    public async Task 组标签应不带for指向单控件()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<FormFieldCheckboxGroup<string>>(FieldParams(
            Items(("a", "选项 A")),
            values: [],
            label: "兴趣标签",
            helperText: "可多选"));
        await SettleAsync(renderer);

        var markup = renderer.Markup();
        Assert.Contains("data-slot=\"field-label\"", markup);

        // 组件形态：FieldLabel 不传 For（组无单一控件可指），标签元素不应有 for 属性。
        var labelTag = Regex.Match(markup, "<label[^>]*data-slot=\"field-label\"");
        Assert.True(labelTag.Success);
        Assert.DoesNotContain("for=", labelTag.Value);

        // 帮助文本仍经描述元素与 describedby 链接到组（CheckboxGroup 本体无该参数
        // 仅渲染描述元素，不做控件级链接）。
        Assert.Contains("data-slot=\"field-description\"", markup);
        Assert.Contains("可多选", markup);
    }

    // ------------------------------------------------------------------
    // 集合值绑定沿
    // ------------------------------------------------------------------

    [Fact]
    public async Task 勾选条目应回填集合并经EditContext播报校验()
    {
        var profile = new Profile();
        var reports = new List<IReadOnlyCollection<string>>();
        var renderer = CreateRenderer();
        var context = CreateValidatedContext(profile);

        Expression<Func<IReadOnlyCollection<string>>> valuesExpression = () => profile.Tags;
        await renderer.MountAsync<FormFieldBaseTests.EditContextHost>(new Dictionary<string, object?>
        {
            [nameof(FormFieldBaseTests.EditContextHost.EditContext)] = context,
            [nameof(FormFieldBaseTests.EditContextHost.ChildContent)] = (RenderFragment)(builder =>
            {
                builder.OpenComponent<FormFieldCheckboxGroup<string>>(0);
                builder.AddAttribute(1, nameof(FormFieldCheckboxGroup<string>.Values), profile.Tags);
                builder.AddAttribute(2, nameof(FormFieldCheckboxGroup<string>.ValuesExpression), valuesExpression);
                builder.AddAttribute(3, nameof(FormFieldCheckboxGroup<string>.ValuesChanged),
                    EventCallback.Factory.Create<IReadOnlyCollection<string>>(profile, values =>
                    {
                        profile.Tags = values;
                        reports.Add(values);
                    }));
                builder.AddAttribute(4, nameof(FormFieldCheckboxGroup<string>.Label), (string?)"兴趣标签");
                builder.AddAttribute(5, nameof(FormFieldCheckboxGroup<string>.ChildContent), Items(("a", "选项 A"), ("b", "选项 B")));
                builder.CloseComponent();
            }),
        });
        await SettleAsync(renderer);

        // 组内两条目的 onclick 序 0/1；点第二条加入 "b"。
        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs(), occurrence: 1));
        await SettleAsync(renderer);

        Assert.Single(reports);
        Assert.Equal(["b"], profile.Tags);
        Assert.DoesNotContain("至少选择一个标签", renderer.Markup());

        // 取消该条目 → 空集合 → 校验消息显形。
        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs(), occurrence: 1));
        await SettleAsync(renderer);

        Assert.Empty(profile.Tags);
        Assert.Contains("至少选择一个标签", renderer.Markup());
        Assert.Contains("data-slot=\"field-error\"", renderer.Markup());
    }

    [Fact]
    public async Task 宿主回写集合后对应条目应呈现选中()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<FormFieldCheckboxGroup<string>>(FieldParams(
            Items(("a", "选项 A"), ("b", "选项 B")),
            values: ["a"],
            label: "兴趣标签"));
        await SettleAsync(renderer);

        var markup = renderer.Markup();
        Assert.Contains("aria-checked=\"true\"", markup);
        Assert.Contains("aria-checked=\"false\"", markup);

        await renderer.RerenderRootAsync(0, FieldParams(
            Items(("a", "选项 A"), ("b", "选项 B")),
            values: ["a", "b"],
            label: "兴趣标签"));
        await SettleAsync(renderer);

        // Regex.Count 静态重载是 (input, pattern)：markup 在前、模式在后。
        Assert.Equal(2, Regex.Count(renderer.Markup(), "aria-checked=\"true\""));
    }

    // ------------------------------------------------------------------
    // 错误链与透传面
    // ------------------------------------------------------------------

    [Fact]
    public async Task 错误文本手动覆写应显形并隐藏帮助文本()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<FormFieldCheckboxGroup<string>>(FieldParams(
            Items(("a", "选项 A")),
            values: [],
            label: "兴趣标签",
            helperText: "可多选",
            errorText: "标签选择不符合要求"));
        await SettleAsync(renderer);

        var markup = renderer.Markup();
        Assert.Contains("标签选择不符合要求", markup);
        Assert.Contains("data-slot=\"field-error\"", markup);
        Assert.DoesNotContain("可多选", markup);

        // Field 容器进入无效态。
        Assert.Contains("data-invalid=\"true\"", markup);
    }

    [Fact]
    public async Task 全选开关与文案应透传内部组()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<FormFieldCheckboxGroup<string>>(FieldParams(
            Items(("a", "选项 A"), ("b", "选项 B")),
            values: [],
            label: "兴趣标签",
            showSelectAll: true,
            selectAllLabel: "全部选中"));
        await SettleAsync(renderer);

        var inner = renderer.FindComponent<CheckboxGroup<string>>();
        Assert.True(inner.ShowSelectAll);
        Assert.Equal("全部选中", inner.SelectAllLabel);
        Assert.Contains("全部选中", renderer.Markup());

        // 缺省全选文案照为 "Select all"。
        var plain = CreateRenderer();
        await plain.MountAsync<FormFieldCheckboxGroup<string>>(FieldParams(
            Items(("a", "选项 A")), values: [], showSelectAll: true));
        Assert.Equal("Select all", plain.FindComponent<CheckboxGroup<string>>().SelectAllLabel);
    }

    [Fact]
    public async Task 禁用与输入类应透传内部组()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<FormFieldCheckboxGroup<string>>(FieldParams(
            Items(("a", "选项 A"), ("b", "选项 B")),
            values: ["a"],
            label: "兴趣标签",
            disabled: true,
            inputClass: "atom:custom-group"));
        await SettleAsync(renderer);

        var inner = renderer.FindComponent<CheckboxGroup<string>>();
        Assert.True(inner.Disabled);

        var markup = renderer.Markup();
        Assert.Contains("atom:custom-group", markup);

        // 组禁用级联到条目：原语复选框落 aria-disabled 面（照 CheckboxGroupTests 口径）。
        Assert.Equal(2, Regex.Count(markup, "aria-disabled=\"true\""));
    }

    [Fact]
    public async Task 无障碍标签与附加属性应落内部组元素()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<FormFieldCheckboxGroup<string>>(FieldParams(
            Items(("a", "选项 A")),
            values: [],
            label: "兴趣标签",
            ariaLabel: "选择兴趣",
            additionalAttributes: new Dictionary<string, object> { ["data-testid"] = "tags-group" }));
        await SettleAsync(renderer);

        Assert.Equal("选择兴趣", renderer.FindComponent<CheckboxGroup<string>>().AriaLabel);

        var markup = renderer.Markup();

        // 形态：splat 直落内部 CheckboxGroup（Field 根同样是 role="group"，锚定 data-testid
        // 所在 div 以区分二者）。
        var groupTag = Regex.Match(markup, "<div[^>]*data-testid=\"tags-group\"[^>]*>");
        Assert.True(groupTag.Success);
        Assert.Contains("role=\"group\"", groupTag.Value);
        Assert.Contains("aria-label=\"选择兴趣\"", groupTag.Value);
    }
}
