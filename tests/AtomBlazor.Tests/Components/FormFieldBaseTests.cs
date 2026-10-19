using AtomBlazor.Primitives;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using AtomBlazor;
using AtomBlazor.Tests.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Xunit;

namespace AtomBlazor.Tests.Components;

/// <summary>
/// FormFieldBase 校验沿测试（重点）：绑定级联 EditContext 后由 FieldIdentifier
/// 接入 ValidationMessageStore、无效值触发消息并由 FieldError 呈现、
/// oninput/onblur/onsubmit/onchange 四类时机语义表驱动、ErrorText 优先级、
/// 校验状态变化驱动重渲染、换绑与 Dispose 的订阅清理、无 EditContext 时的降级。
/// 使用测试替身子类最小实现值更新与 ValueChanged，不依赖未交付的 Input。
/// </summary>
public class FormFieldBaseTests
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

    /// <summary>
    /// 排空渲染队列：StateHasChanged 会把渲染批作为后续队列项调度
    /// 再走一轮 Dispatcher 即可保证标记已反映最新状态。
    /// </summary>
    private static Task SettleAsync(ComponentTestRenderer renderer) =>
        renderer.Dispatcher.InvokeAsync(() => Task.CompletedTask);

    // ------------------------------------------------------------------
    // 挂载辅助
    // ------------------------------------------------------------------

    /// <summary>
    /// 在级联 EditContext 的宿主内挂载测试字段并返回渲染器与组件实例。
    /// </summary>
    private static async Task<(ComponentTestRenderer Renderer, TestFormField Field)> MountWithEditContextAsync(
        EditContext editContext,
        Person person,
        string? helperText = null,
        string? errorText = null)
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<EditContextHost>(new Dictionary<string, object?>
        {
            [nameof(EditContextHost.EditContext)] = editContext,
            [nameof(EditContextHost.ChildContent)] = FieldFragment(person, helperText: helperText, errorText: errorText),
        });

        return (renderer, renderer.FindComponent<TestFormField>());
    }

    private static RenderFragment FieldFragment(
        Person person,
        string? helperText = null,
        string? errorText = null) => builder =>
    {
        Expression<Func<string?>> valueExpression = () => person.Username;

        builder.OpenComponent<TestFormField>(0);
        builder.AddAttribute(1, nameof(TestFormField.Value), "ok");
        builder.AddAttribute(2, nameof(TestFormField.ValueExpression), valueExpression);
        builder.AddAttribute(3, nameof(TestFormField.ValueChanged),
            EventCallback.Factory.Create<string?>(person, value => person.Username = value));
        builder.AddAttribute(4, nameof(TestFormField.Label), (string?)"用户名");
        builder.AddAttribute(5, nameof(TestFormField.HelperText), helperText);
        builder.AddAttribute(6, nameof(TestFormField.ErrorText), errorText);
        builder.CloseComponent();
    };

    /// <summary>
    /// 挂载不包 EditContext 宿主的裸测试字段。
    /// </summary>
    private static async Task<(ComponentTestRenderer Renderer, TestFormField Field)> MountPlainAsync(
        string? helperText = null,
        string? errorText = null)
    {
        var renderer = CreateRenderer();

        var parameters = new Dictionary<string, object?>
        {
            [nameof(TestFormField.Value)] = "ok",
            [nameof(TestFormField.Label)] = "用户名",
        };
        if (helperText is not null)
        {
            parameters[nameof(TestFormField.HelperText)] = helperText;
        }
        if (errorText is not null)
        {
            parameters[nameof(TestFormField.ErrorText)] = errorText;
        }

        // 直接挂载时 TestFormField 即根组件，取 MountAsync 返回值而非 FindComponent。
        var field = await renderer.MountAsync<TestFormField>(parameters);
        return (renderer, field);
    }

    /// <summary>
    /// 构造挂接假校验器的 EditContext：语义与真实 DataAnnotationsValidator 一致——
    /// 订阅 OnFieldChanged/OnValidationRequested，用 ValidationMessageStore 写入消息
    /// 最后 NotifyValidationStateChanged 广播。空用户名视为无效。
    /// </summary>
    private static (EditContext Context, ValidationMessageStore Messages) CreateValidatedContext(Person person)
    {
        var context = new EditContext(person);
        var messages = new ValidationMessageStore(context);
        var field = new FieldIdentifier(person, nameof(Person.Username));

        void Validate()
        {
            messages.Clear();
            if (string.IsNullOrEmpty(person.Username))
            {
                messages.Add(field, "用户名不能为空");
            }
            context.NotifyValidationStateChanged();
        }

        context.OnFieldChanged += (_, _) => Validate();
        context.OnValidationRequested += (_, _) => Validate();
        return (context, messages);
    }

    // ------------------------------------------------------------------
    // 结构与降级
    // ------------------------------------------------------------------

    [Fact]
    public async Task 无EditContext时快照应仅有控件与标签及描述()
    {
        var (renderer, _) = await MountPlainAsync(helperText: "至少 3 个字符");

        await Verify(Scrub(renderer.Markup()));
    }

    [Fact]
    public async Task 控件ID族应由ControlId并保持结构()
    {
        var (renderer, field) = await MountPlainAsync(helperText: "帮助");

        var markup = renderer.Markup();
        Assert.Matches(@"id=""formfield-[0-9a-f]{32}""", markup);
        Assert.Matches(@"id=""formfield-[0-9a-f]{32}-description""", markup);
        Assert.Contains($"for=\"{field.PublicControlId}\"", markup);
    }

    [Fact]
    public async Task 帮助文本存在且有效时AriaDescribedby应指向描述元素()
    {
        var (renderer, field) = await MountPlainAsync(helperText: "帮助");

        var markup = renderer.Markup();
        Assert.Contains($"aria-describedby=\"{field.PublicControlId}-description\"", markup);
        Assert.DoesNotContain("aria-invalid", markup);
    }

    [Fact]
    public async Task ErrorText单独存在应进入无效态并优先展示()
    {
        var (renderer, field) = await MountPlainAsync(errorText: "服务端返回的错误");

        var markup = renderer.Markup();
        Assert.Contains("aria-invalid=\"true\"", markup);
        Assert.Contains($"aria-describedby=\"{field.PublicControlId}-error\"", markup);
        Assert.Contains("data-slot=\"field-error\"", markup);
        Assert.Contains("服务端返回的错误", Scrub(markup));
    }

    [Fact]
    public async Task 无EditContext时值变化通知应安全无操作()
    {
        var (renderer, field) = await MountPlainAsync();

        await renderer.Dispatcher.InvokeAsync(() => field.SimulateChangeAsync("changed"));
        await SettleAsync(renderer);

        // 没有 EditContext：不抛异常、不进入无效态。
        Assert.DoesNotContain("aria-invalid", renderer.Markup());
    }

    // ------------------------------------------------------------------
    // EditContext 校验沿
    // ------------------------------------------------------------------

    [Fact]
    public async Task 绑定EditContext后无效值应触发校验消息并由FieldError呈现()
    {
        var person = new Person { Username = "ok" };
        var (context, _) = CreateValidatedContext(person);

        var (renderer, field) = await MountWithEditContextAsync(context, person, helperText: "帮助");

        await renderer.Dispatcher.InvokeAsync(() => field.SimulateChangeAsync(""));
        await SettleAsync(renderer);

        var markup = renderer.Markup();
        Assert.Contains("用户名不能为空", markup);
        Assert.Contains("data-slot=\"field-error\"", markup);
        Assert.Contains("aria-invalid=\"true\"", markup);
        // 无效时 aria-describedby 从描述元素切换为错误元素。
        Assert.Contains($"aria-describedby=\"{field.PublicControlId}-error\"", markup);
        Assert.DoesNotContain($"aria-describedby=\"{field.PublicControlId}-description\"", markup);
    }

    [Fact]
    public async Task ErrorText应优先于EditContext自动校验消息()
    {
        var person = new Person { Username = "ok" };
        var (context, _) = CreateValidatedContext(person);

        var (renderer, field) = await MountWithEditContextAsync(context, person, errorText: "外部错误");

        await renderer.Dispatcher.InvokeAsync(() => field.SimulateChangeAsync(""));
        await SettleAsync(renderer);

        var markup = renderer.Markup();
        Assert.Contains("外部错误", markup);
        Assert.DoesNotContain("用户名不能为空", markup);
    }

    [Fact]
    public async Task 校验状态变化应在无新参数时驱动重渲染()
    {
        var person = new Person { Username = "ok" };
        var (context, messages) = CreateValidatedContext(person);
        var fieldIdentifier = new FieldIdentifier(person, nameof(Person.Username));

        var (renderer, _) = await MountWithEditContextAsync(context, person);

        // 绕开字段值变化沿：直接由外部（如服务端错误注入）改动消息集合并广播。
        await renderer.Dispatcher.InvokeAsync(() =>
        {
            messages.Add(fieldIdentifier, "来自外部的错误");
            context.NotifyValidationStateChanged();
            return Task.CompletedTask;
        });
        await SettleAsync(renderer);

        Assert.Contains("来自外部的错误", renderer.Markup());
    }

    [Fact]
    public async Task 换绑EditContext应退订旧实例并接管新实例()
    {
        var person = new Person { Username = "ok" };
        var (oldContext, oldMessages) = CreateValidatedContext(person);
        var fieldIdentifier = new FieldIdentifier(person, nameof(Person.Username));

        var (renderer, _) = await MountWithEditContextAsync(oldContext, person);

        await renderer.Dispatcher.InvokeAsync(() =>
        {
            oldMessages.Add(fieldIdentifier, "旧上下文错误");
            oldContext.NotifyValidationStateChanged();
            return Task.CompletedTask;
        });
        await SettleAsync(renderer);
        Assert.Contains("旧上下文错误", renderer.Markup());

        var (newContext, _) = CreateValidatedContext(person);

        // 重新渲染根：级联换成新的 EditContext。
        await renderer.RerenderRootAsync(0, new Dictionary<string, object?>
        {
            [nameof(EditContextHost.EditContext)] = newContext,
            [nameof(EditContextHost.ChildContent)] = FieldFragment(person),
        });

        // 旧上下文的消息不再呈现；新上下文无消息即无错误。
        Assert.DoesNotContain("旧上下文错误", renderer.Markup());
        Assert.DoesNotContain("aria-invalid", renderer.Markup());

        // 新上下文生效后广播应驱动重渲染。
        await renderer.Dispatcher.InvokeAsync(() =>
        {
            new ValidationMessageStore(newContext).Add(fieldIdentifier, "新上下文错误");
            newContext.NotifyValidationStateChanged();
            return Task.CompletedTask;
        });
        await SettleAsync(renderer);
        Assert.Contains("新上下文错误", renderer.Markup());
    }

    [Fact]
    public async Task Dispose应退订校验状态变化()
    {
        var person = new Person { Username = "ok" };
        var (context, messages) = CreateValidatedContext(person);
        var fieldIdentifier = new FieldIdentifier(person, nameof(Person.Username));

        var (renderer, field) = await MountWithEditContextAsync(context, person);

        field.Dispose();

        // Dispose 后广播不再驱动重渲染，也不应抛异常。
        await renderer.Dispatcher.InvokeAsync(() =>
        {
            messages.Add(fieldIdentifier, "退订后的错误");
            context.NotifyValidationStateChanged();
            return Task.CompletedTask;
        });
        await SettleAsync(renderer);

        Assert.DoesNotContain("退订后的错误", renderer.Markup());
    }

    [Fact]
    public async Task 缺少值表达式时不应接入EditContext()
    {
        var person = new Person { Username = "ok" };
        var (context, _) = CreateValidatedContext(person);

        var renderer = CreateRenderer();
        var changedFired = false;
        context.OnFieldChanged += (_, _) => changedFired = true;

        var childContent = new RenderFragment(builder =>
        {
            builder.OpenComponent<TestFormField>(0);
            builder.AddAttribute(1, nameof(TestFormField.Value), "ok");
            builder.AddAttribute(2, nameof(TestFormField.ValueChanged),
                EventCallback.Factory.Create<string?>(person, v => person.Username = v));
            builder.CloseComponent();
        });

        await renderer.MountAsync<EditContextHost>(new Dictionary<string, object?>
        {
            [nameof(EditContextHost.EditContext)] = context,
            [nameof(EditContextHost.ChildContent)] = childContent,
        });

        var field = renderer.FindComponent<TestFormField>();
        await renderer.Dispatcher.InvokeAsync(() => field.SimulateChangeAsync(""));
        await SettleAsync(renderer);

        Assert.False(changedFired);
        Assert.DoesNotContain("aria-invalid", renderer.Markup());
    }

    // ------------------------------------------------------------------
    // 时机语义表驱动
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("oninput", "", false)]            // 纯键入沿：只更新值，不触发校验
    [InlineData("oninput", "valid", false)]
    [InlineData("oninput-immediate", "", true)]   // Immediate 键入沿：每次输入即通知
    [InlineData("onchange", "", true)]            // 值落定沿：更新后通知 EditContext
    [InlineData("onchange", "valid", false)]
    [InlineData("onblur", "", true)]              // blur 复用 change 语义
    [InlineData("onsubmit", "", true)]            // 表单提交沿：EditContext.Validate()
    [InlineData("onsubmit", "valid", false)]
    public async Task 各事件时机应按语义表决定是否触发校验(string trigger, string value, bool expectInvalid)
    {
        var person = new Person { Username = "ok" };
        var (context, _) = CreateValidatedContext(person);

        var (renderer, field) = await MountWithEditContextAsync(context, person);

        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            if (trigger == "onsubmit")
            {
                // 提交沿：值先经键入沿更新（表单内不触发字段校验），再由 EditContext.ValidateAsync() 全量校验。
                await field.SimulateInputAsync(value);
                await context.ValidateAsync();
            }
            else
            {
                await field.TriggerAsync(trigger, value);
            }
        });
        await SettleAsync(renderer);

        var markup = renderer.Markup();
        Assert.Equal(expectInvalid, markup.Contains("aria-invalid=\"true\""));
        Assert.Equal(expectInvalid, markup.Contains("用户名不能为空"));
    }

    // ------------------------------------------------------------------
    // 测试替身与宿主
    // ------------------------------------------------------------------

    /// <summary>
    /// 校验沿测试模型：Username 为空即无效（由假校验器执行）。
    /// </summary>
    internal sealed class Person
    {
        public string? Username { get; set; }
    }

    /// <summary>
    /// FormFieldBase 测试替身：以最小控件标记暴露 ControlId/IsInvalid/DescribedById/
    /// EditContextErrors 计算结果，并实现各浏览器事件时机的模拟。
    /// 值落定顺序与 InputBase 一致：赋值 → ValueChanged 回填模型 → NotifyFieldChanged。
    /// </summary>
    internal sealed class TestFormField : FormFieldBase
    {
        [Parameter]
        public string? Value { get; set; }

        [Parameter]
        public EventCallback<string?> ValueChanged { get; set; }

        [Parameter]
        public Expression<Func<string?>>? ValueExpression { get; set; }

        /// <summary>暴露 ControlId 供断言（基类成员为 protected）。</summary>
        public string PublicControlId => ControlId;

        protected override LambdaExpression? GetFieldExpression() => ValueExpression;

        /// <summary>oninput（非 Immediate）：仅更新值与模型，不通知 EditContext。</summary>
        public async Task SimulateInputAsync(string newValue)
        {
            Value = newValue;
            await ValueChanged.InvokeAsync(newValue);
        }

        /// <summary>onchange/onblur：值落定并回填模型后通知 EditContext 触发校验。</summary>
        public async Task SimulateChangeAsync(string newValue)
        {
            await SimulateInputAsync(newValue);
            NotifyFieldChanged();
        }

        /// <summary>模拟表驱动触发沿（onsubmit 由测试端直接调用 EditContext.Validate()）。</summary>
        public async Task TriggerAsync(string trigger, string newValue)
        {
            switch (trigger)
            {
                case "oninput":
                    await SimulateInputAsync(newValue);
                    break;
                case "oninput-immediate":
                    await SimulateChangeAsync(newValue);
                    break;
                case "onchange":
                case "onblur":
                    await SimulateChangeAsync(newValue);
                    break;
            }
        }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            var seq = 0;

            builder.OpenElement(seq++, "input");
            builder.AddAttribute(seq++, "id", ControlId);
            builder.AddAttribute(seq++, "aria-invalid", IsInvalid ? "true" : null);
            builder.AddAttribute(seq++, "aria-describedby", DescribedById);
            builder.AddAttribute(seq++, "value", Value);
            builder.CloseElement();

            builder.OpenElement(seq++, "label");
            builder.AddAttribute(seq++, "for", ControlId);
            builder.AddContent(seq++, Label);
            builder.CloseElement();

            if (!string.IsNullOrEmpty(HelperText))
            {
                builder.OpenElement(seq++, "div");
                builder.AddAttribute(seq++, "id", DescriptionId);
                builder.AddAttribute(seq++, "data-slot", "field-description");
                builder.AddContent(seq++, HelperText);
                builder.CloseElement();
            }

            // ErrorText 优先于 EditContext 自动消息（与基类约定一致）。
            var errors = !string.IsNullOrEmpty(ErrorText)
                ? new[] { ErrorText }
                : EditContextErrors.ToArray();
            if (errors.Length > 0)
            {
                builder.OpenComponent<FieldError>(seq++);
                builder.AddAttribute(seq++, nameof(FieldError.Id), ErrorId);
                builder.AddAttribute(seq++, nameof(FieldError.Errors), errors);
                builder.CloseComponent();
            }
        }
    }

    /// <summary>
    /// 把 EditContext 作为未命名级联参数下发给子内容的宿主（渲染器无 Form 特化）。
    /// </summary>
    internal sealed class EditContextHost : ComponentBase
    {
        [Parameter]
        public EditContext? EditContext { get; set; }

        [Parameter]
        public RenderFragment? ChildContent { get; set; }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenComponent<CascadingValue<EditContext>>(0);
            builder.AddAttribute(1, nameof(CascadingValue<EditContext>.Value), EditContext);
            builder.AddAttribute(2, nameof(CascadingValue<EditContext>.ChildContent), ChildContent);
            builder.CloseComponent();
        }
    }
}
