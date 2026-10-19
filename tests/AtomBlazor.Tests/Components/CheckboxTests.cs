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
/// Checkbox 家族测试：原语层三态呈现（未选中/选中/不确定）与纯行为无样式约定、
/// 包装层的勾选图标、禁用与必填的 ARIA 落点、点击拨动与不确定态清除的回调沿、
/// 以及 EditContext 内由 CheckedExpression 隐藏 input 的 name 并通知字段变化。
/// </summary>
public class CheckboxTests
{
    /// <summary>
    /// 归一化自动生成的复选框短 id，使快照可确定。
    /// </summary>
    private static string Scrub(string markup) =>
        Regex.Replace(markup, "checkbox-[0-9a-f]{8}", "checkbox-<id>");

    private static ComponentTestRenderer CreateRenderer()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(new NoopJavaScript());
        return new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance);
    }

    /// <summary>排空渲染队列，确保 StateHasChanged 的后续批已落定。</summary>
    private static Task SettleAsync(ComponentTestRenderer renderer) =>
        renderer.Dispatcher.InvokeAsync(() => Task.CompletedTask);

    private static Dictionary<string, object?> Params(
        bool? @checked = null,
        EventCallback<bool> checkedChanged = default,
        bool? indeterminate = null,
        EventCallback<bool> indeterminateChanged = default,
        bool? disabled = null,
        bool? required = null,
        string? id = null,
        string? ariaLabel = null,
        string? ariaDescribedBy = null,
        string? @class = null,
        Dictionary<string, object>? additionalAttributes = null)
    {
        var parameters = new Dictionary<string, object?>();
        if (@checked is not null)
        {
            parameters[nameof(Checkbox.Checked)] = @checked.Value;
        }
        if (checkedChanged.HasDelegate)
        {
            parameters[nameof(Checkbox.CheckedChanged)] = checkedChanged;
        }
        if (indeterminate is not null)
        {
            parameters[nameof(Checkbox.Indeterminate)] = indeterminate.Value;
        }
        if (indeterminateChanged.HasDelegate)
        {
            parameters[nameof(Checkbox.IndeterminateChanged)] = indeterminateChanged;
        }
        if (disabled is not null)
        {
            parameters[nameof(Checkbox.Disabled)] = disabled.Value;
        }
        if (required is not null)
        {
            parameters[nameof(Checkbox.Required)] = required.Value;
        }
        if (id is not null)
        {
            parameters[nameof(Checkbox.Id)] = id;
        }
        if (ariaLabel is not null)
        {
            parameters[nameof(Checkbox.AriaLabel)] = ariaLabel;
        }
        if (ariaDescribedBy is not null)
        {
            parameters[nameof(Checkbox.AriaDescribedBy)] = ariaDescribedBy;
        }
        if (@class is not null)
        {
            parameters[nameof(Checkbox.Class)] = @class;
        }
        if (additionalAttributes is not null)
        {
            parameters[nameof(Checkbox.AdditionalAttributes)] = additionalAttributes;
        }

        return parameters;
    }

    // ------------------------------------------------------------------
    // 呈现快照
    // ------------------------------------------------------------------

    [Fact]
    public async Task 未选中默认渲染快照应与基线一致()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Checkbox>(Params());
        await SettleAsync(renderer);

        await Verify(Scrub(renderer.Markup()));
    }

    [Fact]
    public async Task 选中态快照应与基线一致()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Checkbox>(Params(@checked: true));
        await SettleAsync(renderer);

        await Verify(Scrub(renderer.Markup()));
    }

    [Fact]
    public async Task 不确定态快照应与基线一致()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Checkbox>(Params(indeterminate: true));
        await SettleAsync(renderer);

        await Verify(Scrub(renderer.Markup()));
    }

    [Fact]
    public async Task 未选中应呈现false状态且不渲染图标()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Checkbox>(Params());

        var markup = renderer.Markup();
        Assert.Contains("role=\"checkbox\"", markup);
        Assert.Contains("aria-checked=\"false\"", markup);
        Assert.Contains("data-state=\"unchecked\"", markup);
        Assert.DoesNotContain("<svg", markup);
    }

    [Fact]
    public async Task 选中应呈现勾选折线图标()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Checkbox>(Params(@checked: true));

        var markup = renderer.Markup();
        Assert.Contains("aria-checked=\"true\"", markup);
        Assert.Contains("data-state=\"checked\"", markup);
        Assert.Contains("<polyline", markup);
    }

    [Fact]
    public async Task 不确定态应呈现mixed与横线图标()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Checkbox>(Params(indeterminate: true));

        var markup = renderer.Markup();
        Assert.Contains("aria-checked=\"mixed\"", markup);
        Assert.Contains("data-state=\"indeterminate\"", markup);
        Assert.Contains("<line", markup);
    }

    [Fact]
    public async Task 禁用应播报不可用并把焦点移出tab序列()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Checkbox>(Params(disabled: true));

        var markup = renderer.Markup();
        Assert.Contains("aria-disabled=\"true\"", markup);
        Assert.Contains("tabindex=\"-1\"", markup);
        Assert.Contains("disabled", markup);
    }

    [Fact]
    public async Task 必填应播报必填而未必填时整个属性缺席()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Checkbox>(Params(required: true));
        Assert.Contains("aria-required=\"true\"", renderer.Markup());

        var plain = CreateRenderer();
        await plain.MountAsync<Checkbox>(Params());
        Assert.DoesNotContain("aria-required", plain.Markup());
    }

    // ------------------------------------------------------------------
    // 交互沿
    // ------------------------------------------------------------------

    [Fact]
    public async Task 点击应拨动选中态并上报新值()
    {
        var reports = new List<bool>();
        var callback = EventCallback.Factory.Create<bool>(new object(), value => reports.Add(value));
        var renderer = CreateRenderer();

        await renderer.MountAsync<Checkbox>(Params(@checked: false, checkedChanged: callback));

        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs()));

        Assert.Equal([true], reports);
        Assert.Contains("aria-checked=\"true\"", renderer.Markup());
    }

    [Fact]
    public async Task 点击应先清除不确定态再上报两侧变化()
    {
        var checkedReports = new List<bool>();
        var indeterminateReports = new List<bool>();
        var checkedChanged = EventCallback.Factory.Create<bool>(new object(), value => checkedReports.Add(value));
        var indeterminateChanged = EventCallback.Factory.Create<bool>(new object(), value => indeterminateReports.Add(value));
        var renderer = CreateRenderer();

        await renderer.MountAsync<Checkbox>(Params(
            @checked: false,
            checkedChanged: checkedChanged,
            indeterminate: true,
            indeterminateChanged: indeterminateChanged));

        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs()));

        Assert.Equal([false], indeterminateReports);
        Assert.Equal([true], checkedReports);

        // 不确定态是受控状态：原语点击时已内部清除并上报 false，包装层与 一致
        // 不把清除回写自己的 Indeterminate 参数（真实场景由宿主 @bind-Indeterminate 清零）。
        // 这里模拟宿主应用两侧新值后的一次重渲染，随后应呈现真正的选中态。
        await renderer.RerenderRootAsync(0, Params(
            @checked: true,
            checkedChanged: checkedChanged,
            indeterminate: false,
            indeterminateChanged: indeterminateChanged));
        await SettleAsync(renderer);

        Assert.Contains("aria-checked=\"true\"", renderer.Markup());
        Assert.DoesNotContain("mixed", renderer.Markup());
    }

    [Fact]
    public async Task 禁用态点击不应上报任何变化()
    {
        var reports = new List<bool>();
        var callback = EventCallback.Factory.Create<bool>(new object(), value => reports.Add(value));
        var renderer = CreateRenderer();

        await renderer.MountAsync<Checkbox>(Params(disabled: true, checkedChanged: callback));

        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs()));

        Assert.Empty(reports);
        Assert.Contains("aria-checked=\"false\"", renderer.Markup());
    }

    [Fact]
    public async Task 自定义类应追加在基础类之后()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Checkbox>(Params(@class: "atom:my-checkbox"));

        var classAttribute = Regex.Match(renderer.Markup(), "class=\"([^\"]*)\"");
        Assert.True(classAttribute.Success);
        Assert.EndsWith("atom:my-checkbox", classAttribute.Groups[1].Value);
        Assert.Contains("atom:h-5", classAttribute.Groups[1].Value);
    }

    // ------------------------------------------------------------------
    // 原语层（无样式）
    // ------------------------------------------------------------------

    [Fact]
    public async Task 原语应只呈现行为与无障碍属性而不带样式类()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<AtomBlazor.Primitives.CheckboxPrimitive>(
            new Dictionary<string, object?>
            {
                [nameof(AtomBlazor.Primitives.CheckboxPrimitive.Id)] = "cb-headless",
                [nameof(AtomBlazor.Primitives.CheckboxPrimitive.AriaLabel)] = "同意条款",
            });

        var markup = renderer.Markup();
        Assert.Contains("id=\"cb-headless\"", markup);
        Assert.Contains("aria-label=\"同意条款\"", markup);
        Assert.DoesNotContain("class=", markup);
    }

    [Fact]
    public async Task 原语应把指示器级联给子项并据状态渲染图标()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<AtomBlazor.Primitives.CheckboxPrimitive>(
            new Dictionary<string, object?>
            {
                [nameof(AtomBlazor.Primitives.CheckboxPrimitive.Checked)] = true,
                [nameof(AtomBlazor.Primitives.CheckboxPrimitive.ChildContent)] =
                    (RenderFragment)(builder =>
                    {
                        // 组件帧必须开合成对，未闭合的 Region 帧会让渲染器判定输出非法。
                        builder.OpenComponent<AtomBlazor.Primitives.CheckboxIndicator>(0);
                        builder.CloseComponent();
                    }),
            });

        Assert.Contains("<polyline", renderer.Markup());
    }

    // ------------------------------------------------------------------
    // EditContext 校验沿
    // ------------------------------------------------------------------

    /// <summary>校验沿测试模型：必须勾选记住我。</summary>
    internal sealed class Profile
    {
        public bool RememberMe { get; set; }
    }

    /// <summary>
    /// 构造挂接假校验器的 EditContext：语义与真实 DataAnnotationsValidator 一致——
    /// 未勾选即写入消息；同时记录 OnFieldChanged 次数，供断言字段通知沿。
    /// </summary>
    private static (EditContext Context, List<int> FieldChanges) CreateValidatedContext(Profile profile)
    {
        var context = new EditContext(profile);
        var messages = new ValidationMessageStore(context);
        var field = new FieldIdentifier(profile, nameof(Profile.RememberMe));
        var fieldChanges = new List<int>();

        void Validate()
        {
            messages.Clear();
            if (!profile.RememberMe)
            {
                messages.Add(field, "请勾选记住我");
            }

            context.NotifyValidationStateChanged();
        }

        context.OnFieldChanged += (_, _) =>
        {
            fieldChanges.Add(1);
            Validate();
        };
        context.OnValidationRequested += (_, _) => Validate();

        return (context, fieldChanges);
    }

    private static RenderFragment CheckboxFragment(Profile profile, EventCallback<bool> changed) => builder =>
    {
        Expression<Func<bool>> checkedExpression = () => profile.RememberMe;

        builder.OpenComponent<Checkbox>(0);
        builder.AddAttribute(1, nameof(Checkbox.Checked), profile.RememberMe);
        builder.AddAttribute(2, nameof(Checkbox.CheckedExpression), checkedExpression);
        builder.AddAttribute(3, nameof(Checkbox.CheckedChanged), changed);
        builder.CloseComponent();
    };

    [Fact]
    public async Task 校验沿应在EditContext内隐藏input的name并通知字段变化()
    {
        var profile = new Profile();
        var reports = new List<bool>();
        var changed = EventCallback.Factory.Create<bool>(profile, value =>
        {
            reports.Add(value);
            profile.RememberMe = value;
        });
        var (context, fieldChanges) = CreateValidatedContext(profile);
        var renderer = CreateRenderer();

        // 初始播报依赖已存在的校验消息：挂载前显式请求一次校验（语义同真实验证器）。
        await context.ValidateAsync();

        var hostParams = new Dictionary<string, object?>
        {
            [nameof(FormFieldBaseTests.EditContextHost.EditContext)] = context,
            [nameof(FormFieldBaseTests.EditContextHost.ChildContent)] = CheckboxFragment(profile, changed),
        };
        await renderer.MountAsync<FormFieldBaseTests.EditContextHost>(hostParams);
        await SettleAsync(renderer);

        // 未勾选 → 校验消息存在 → aria-invalid；隐藏 input 携带的 name。
        var markup = renderer.Markup();
        Assert.Contains("aria-invalid=\"true\"", markup);
        Assert.Contains("<input", markup);
        Assert.Contains("type=\"checkbox\"", markup);
        Assert.Contains("name=\"", markup);
        Assert.Contains("atom:sr-only", markup);

        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs()));
        await SettleAsync(renderer);

        Assert.Equal([true], reports);
        Assert.Single(fieldChanges);

        // Checkbox 包装层不订阅 OnValidationStateChanged（与 ）：
        // 真实场景里由 EditForm 在验证态变化后重渲染子树，这里显式重渲染根来模拟。
        await renderer.RerenderRootAsync(0, hostParams);
        await SettleAsync(renderer);

        Assert.DoesNotContain("aria-invalid", renderer.Markup());
    }

    [Fact]
    public async Task 无EditContext宿主时不应渲染隐藏input也不播报无效态()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Checkbox>(new Dictionary<string, object?>
        {
            [nameof(Checkbox.Checked)] = true,
        });

        var markup = Scrub(renderer.Markup());
        Assert.DoesNotContain("<input", markup);
        Assert.DoesNotContain("aria-invalid", markup);
    }

    [Fact]
    public async Task 显式Name应优先于表达式名()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Checkbox>(new Dictionary<string, object?>
        {
            [nameof(Checkbox.Name)] = "remember-me",
        });

        Assert.Contains("name=\"remember-me\"", renderer.Markup());
    }
}
