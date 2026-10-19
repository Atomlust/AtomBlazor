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
/// RadioGroup 家族测试：选中互斥与漫游 tabindex、点击与 Space/Enter 选中上报、
/// 方向键导航（含沿书写方向的左右键与自动选中并聚焦下一项）、组禁用吞掉交互、
/// ItemClass 级联合并，以及 EditContext 内的 aria-invalid 校验沿。
/// </summary>
public class RadioGroupTests
{
    /// <summary>
    /// 归一化单选项自动生成的随机 id，使快照可确定。
    /// </summary>
    private static string Scrub(string markup) =>
        Regex.Replace(markup, "radio-[0-9a-f]{32}", "radio-<id>");

    private static ComponentTestRenderer CreateRenderer()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(new NoopJavaScript());
        return new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance);
    }

    /// <summary>排空渲染队列，确保 StateHasChanged 的后续批已落定。</summary>
    private static Task SettleAsync(ComponentTestRenderer renderer) =>
        renderer.Dispatcher.InvokeAsync(() => Task.CompletedTask);

    private static EventCallback<string> NullValueChanged() =>
        EventCallback.Factory.Create<string>(new object(), _ => { });

    /// <summary>Components 层：带 Label 的单选项，由组件自己接线 for/id。</summary>
    private static RenderFragment Items(params string[] values) => builder =>
    {
        var seq = 0;
        foreach (var value in values)
        {
            builder.OpenComponent<RadioGroupItem<string>>(seq++);
            builder.AddAttribute(seq++, nameof(RadioGroupItem<string>.Value), value);
            builder.AddAttribute(seq++, nameof(RadioGroupItem<string>.Label), $"选项 {value}");
            builder.CloseComponent();
        }
    };

    private static Dictionary<string, object?> GroupParams(
        RenderFragment childContent,
        string? value = null,
        EventCallback<string> valueChanged = default,
        bool? disabled = null,
        bool? required = null,
        string? ariaLabel = null,
        string? @class = null,
        string? ariaDescribedBy = null)
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(RadioGroup<string>.ChildContent)] = childContent,
        };
        if (value is not null)
        {
            parameters[nameof(RadioGroup<string>.Value)] = value;
        }
        if (valueChanged.HasDelegate)
        {
            parameters[nameof(RadioGroup<string>.ValueChanged)] = valueChanged;
        }
        if (disabled is not null)
        {
            parameters[nameof(RadioGroup<string>.Disabled)] = disabled.Value;
        }
        if (required is not null)
        {
            parameters[nameof(RadioGroup<string>.Required)] = required.Value;
        }
        if (ariaLabel is not null)
        {
            parameters[nameof(RadioGroup<string>.AriaLabel)] = ariaLabel;
        }
        if (@class is not null)
        {
            parameters[nameof(RadioGroup<string>.Class)] = @class;
        }
        if (ariaDescribedBy is not null)
        {
            parameters[nameof(RadioGroup<string>.AriaDescribedBy)] = ariaDescribedBy;
        }

        return parameters;
    }

    // ------------------------------------------------------------------
    // 快照与互斥呈现
    // ------------------------------------------------------------------

    [Fact]
    public async Task 默认渲染快照应与基线一致()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<RadioGroup<string>>(GroupParams(
            Items("a", "b"), value: "a", valueChanged: NullValueChanged()));
        await SettleAsync(renderer);

        await Verify(Scrub(renderer.Markup()));
    }

    [Fact]
    public async Task 组根应为radiogroup并携带网格排布类()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<RadioGroup<string>>(GroupParams(Items("a"), ariaLabel: "通知方式"));

        var markup = renderer.Markup();
        Assert.Contains("role=\"radiogroup\"", markup);
        Assert.Contains("aria-label=\"通知方式\"", markup);
        Assert.Contains("atom:grid atom:gap-2", markup);
    }

    [Fact]
    public async Task 同一时刻应只有一个项呈选中并按此分配漫游tabindex()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<RadioGroup<string>>(GroupParams(
            Items("a", "b", "c"), value: "b", valueChanged: NullValueChanged()));
        await SettleAsync(renderer);

        var markup = renderer.Markup();
        // Regex.Count 静态重载是 (input, pattern)：markup 在前、模式在后。
        Assert.Single(Regex.Matches(markup, "aria-checked=\\\"true\\\""));
        Assert.Equal(2, Regex.Count(markup, "aria-checked=\\\"false\\\""));
        Assert.Single(Regex.Matches(markup, "tabindex=\\\"0\\\""));
        Assert.Equal(2, Regex.Count(markup, "tabindex=\\\"-1\\\""));
    }

    [Fact]
    public async Task 选中项应显示内点而未选项缩放为零()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<RadioGroup<string>>(GroupParams(
            Items("a", "b"), value: "a", valueChanged: NullValueChanged()));
        await SettleAsync(renderer);

        var markup = renderer.Markup();
        Assert.Contains("atom:border-primary", markup);
        Assert.Contains("atom:scale-0", markup);
    }

    [Fact]
    public async Task 标签应按for指向项内单选按钮的id()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<RadioGroup<string>>(GroupParams(Items("a"), valueChanged: NullValueChanged()));
        await SettleAsync(renderer);

        var markup = Scrub(renderer.Markup());
        Assert.Contains("id=\"radio-<id>\"", markup);
        Assert.Contains("for=\"radio-<id>\"", markup);
        Assert.Contains("选项 a", markup);
    }

    // ------------------------------------------------------------------
    // 交互沿
    // ------------------------------------------------------------------

    [Fact]
    public async Task 点击项应上报新值并把选中态移到该项()
    {
        var reports = new List<string>();
        var callback = EventCallback.Factory.Create<string>(new object(), value => reports.Add(value));
        var renderer = CreateRenderer();

        await renderer.MountAsync<RadioGroup<string>>(GroupParams(
            Items("a", "b"), value: "a", valueChanged: callback));
        await SettleAsync(renderer);

        // occurrence 0/1 是两个项内 button 的 onclick。
        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs(), occurrence: 1));

        Assert.Equal(["b"], reports);
        Assert.Contains("aria-checked=\"true\"", renderer.Markup());
    }

    [Fact]
    public async Task 项上按Space应选中该项()
    {
        var reports = new List<string>();
        var callback = EventCallback.Factory.Create<string>(new object(), value => reports.Add(value));
        var renderer = CreateRenderer();

        await renderer.MountAsync<RadioGroup<string>>(GroupParams(
            Items("a", "b"), value: "a", valueChanged: callback));
        await SettleAsync(renderer);

        // occurrence 0 是组容器的 onkeydown，1/2 才是各项自己的 onkeydown。
        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onkeydown", new KeyboardEventArgs { Key = " " }, occurrence: 2));

        Assert.Equal(["b"], reports);
    }

    [Fact]
    public async Task 方向键应选中并聚焦下一项()
    {
        var reports = new List<string>();
        var callback = EventCallback.Factory.Create<string>(new object(), value => reports.Add(value));
        var renderer = CreateRenderer();

        var group = await renderer.MountAsync<RadioGroup<string>>(GroupParams(
            Items("a", "b"), value: "a", valueChanged: callback));
        await SettleAsync(renderer);

        // headless 批渲染器的已知限制：ElementReference 从不配置，导航项选中完成后的
        // RadioGroupItem.FocusAsync 会抛 InvalidOperationException（原语 174–179 行
        // 只按写法兜 JS 断连类异常，不为此加捕获，也不改组件）。
        // 选择与上报都发生在聚焦之前，故此处吞掉该异常并断言实例状态而非 markup 聚焦效果。
        try
        {
            await renderer.Dispatcher.InvokeAsync(async () =>
                await renderer.DispatchAsync("onkeydown", new KeyboardEventArgs { Key = "ArrowDown" }));
        }
        catch (InvalidOperationException)
        {
            // headless 聚焦限制，见上。
        }

        // 导航到的项被自动选中（聚焦本身由 JS 互操作承担，headless 不可观测）。
        Assert.Equal(["b"], reports);
        Assert.Equal("b", group.Value);
        Assert.Equal("b", renderer.FindComponent<AtomBlazor.Primitives.RadioGroupPrimitive<string>>().Value);
    }

    [Fact]
    public async Task 方向键到底应回绕到首项()
    {
        var reports = new List<string>();
        var callback = EventCallback.Factory.Create<string>(new object(), value => reports.Add(value));
        var renderer = CreateRenderer();

        var group = await renderer.MountAsync<RadioGroup<string>>(GroupParams(
            Items("a", "b"), value: "b", valueChanged: callback));
        await SettleAsync(renderer);

        // 同上：headless 聚焦限制，选择与上报先于聚焦完成。
        try
        {
            await renderer.Dispatcher.InvokeAsync(async () =>
                await renderer.DispatchAsync("onkeydown", new KeyboardEventArgs { Key = "ArrowDown" }));
        }
        catch (InvalidOperationException)
        {
            // headless 聚焦限制。
        }

        Assert.Equal(["a"], reports);
        Assert.Equal("a", group.Value);
        Assert.Equal("a", renderer.FindComponent<AtomBlazor.Primitives.RadioGroupPrimitive<string>>().Value);
    }

    [Fact]
    public async Task 组禁用应吞掉点击与方向键()
    {
        var reports = new List<string>();
        var callback = EventCallback.Factory.Create<string>(new object(), value => reports.Add(value));
        var renderer = CreateRenderer();

        await renderer.MountAsync<RadioGroup<string>>(GroupParams(
            Items("a", "b"), value: "a", valueChanged: callback, disabled: true));
        await SettleAsync(renderer);

        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs(), occurrence: 1));
        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onkeydown", new KeyboardEventArgs { Key = "ArrowDown" }));

        Assert.Empty(reports);
        Assert.Contains("aria-disabled=\"true\"", renderer.Markup());
    }

    [Fact]
    public async Task 必填应播报必填状态()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<RadioGroup<string>>(GroupParams(Items("a"), required: true));

        Assert.Contains("aria-required=\"true\"", renderer.Markup());
    }

    // ------------------------------------------------------------------
    // 原语层
    // ------------------------------------------------------------------

    private static RenderFragment PrimitiveItems(params string[] values) => builder =>
    {
        var seq = 0;
        foreach (var value in values)
        {
            builder.OpenComponent<AtomBlazor.Primitives.RadioGroupItemPrimitive<string>>(seq++);
            builder.AddAttribute(seq++, "Value", value);
            builder.AddAttribute(seq++, "Id", $"r-{value}");
            builder.CloseComponent();
        }
    };

    [Fact]
    public async Task 原语结构快照应与基线一致()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<AtomBlazor.Primitives.RadioGroupPrimitive<string>>(
            new Dictionary<string, object?>
            {
                [nameof(AtomBlazor.Primitives.RadioGroupPrimitive<string>.Value)] = "a",
                [nameof(AtomBlazor.Primitives.RadioGroupPrimitive<string>.AriaLabel)] = "通知方式",
                [nameof(AtomBlazor.Primitives.RadioGroupPrimitive<string>.ChildContent)] = PrimitiveItems("a", "b"),
            });
        await SettleAsync(renderer);

        await Verify(renderer.Markup());
    }

    [Fact]
    public async Task 原语ItemClass应与项自身类合并而不产出样式包装()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<AtomBlazor.Primitives.RadioGroupPrimitive<string>>(
            new Dictionary<string, object?>
            {
                [nameof(AtomBlazor.Primitives.RadioGroupPrimitive<string>.ItemClass)] = "atom:primitive-item",
                [nameof(AtomBlazor.Primitives.RadioGroupPrimitive<string>.ChildContent)] = PrimitiveItems("a"),
            });

        var markup = renderer.Markup();
        Assert.Contains("class=\"atom:primitive-item\"", markup);
        Assert.DoesNotContain("atom:grid", markup);
    }

    // ------------------------------------------------------------------
    // EditContext 校验沿
    // ------------------------------------------------------------------

    /// <summary>校验沿测试模型：许可证号必填。</summary>
    internal sealed class Applicant
    {
        public string? License { get; set; }
    }

    /// <summary>构造挂接假校验器的 EditContext：空值即写入消息，并记录字段变化次数。</summary>
    private static (EditContext Context, List<int> FieldChanges) CreateValidatedContext(Applicant applicant)
    {
        var context = new EditContext(applicant);
        var messages = new ValidationMessageStore(context);
        var field = new FieldIdentifier(applicant, nameof(Applicant.License));
        var fieldChanges = new List<int>();

        void Validate()
        {
            messages.Clear();
            if (string.IsNullOrEmpty(applicant.License))
            {
                messages.Add(field, "请选择许可证类型");
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

    private static RenderFragment GroupFragment(Applicant applicant) => builder =>
    {
        Expression<Func<string?>> valueExpression = () => applicant.License;

        builder.OpenComponent<RadioGroup<string?>>(0);
        builder.AddAttribute(1, nameof(RadioGroup<string?>.Value), applicant.License);
        builder.AddAttribute(2, nameof(RadioGroup<string?>.ValueExpression), valueExpression);
        builder.AddAttribute(3, nameof(RadioGroup<string?>.ValueChanged),
            EventCallback.Factory.Create<string?>(applicant, value => applicant.License = value));
        builder.AddAttribute(4, nameof(RadioGroup<string?>.ChildContent), Items("a", "b"));
        builder.CloseComponent();
    };

    [Fact]
    public async Task 校验沿应在选择后清除无效态播报并通知字段变化()
    {
        var applicant = new Applicant();
        var (context, fieldChanges) = CreateValidatedContext(applicant);
        var renderer = CreateRenderer();

        // 初始播报依赖已存在的校验消息：挂载前显式请求一次校验（语义同真实验证器）。
        await context.ValidateAsync();

        var hostParams = new Dictionary<string, object?>
        {
            [nameof(FormFieldBaseTests.EditContextHost.EditContext)] = context,
            [nameof(FormFieldBaseTests.EditContextHost.ChildContent)] = GroupFragment(applicant),
        };
        await renderer.MountAsync<FormFieldBaseTests.EditContextHost>(hostParams);
        await SettleAsync(renderer);

        Assert.Contains("aria-invalid=\"true\"", renderer.Markup());

        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs(), occurrence: 0));
        await SettleAsync(renderer);

        Assert.Equal("a", applicant.License);
        Assert.Single(fieldChanges);

        // RadioGroup 包装层不订阅 OnValidationStateChanged（与 ）：
        // 真实场景里由 EditForm 在验证态变化后重渲染子树，这里显式重渲染根来模拟。
        await renderer.RerenderRootAsync(0, hostParams);
        await SettleAsync(renderer);

        Assert.DoesNotContain("aria-invalid", renderer.Markup());
    }
}
