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
/// FormFieldInput 组合字段测试：在级联 EditContext 宿主内挂载 FormFieldInput&lt;int&gt;
/// 并绑定 Person 模型（复用 FormFieldBaseTests 的 EditContextHost），验证正常值快照、
/// 控件 id 与标签 for 及 aria-describedby 的接线、底层 InputField 解析错误经 FieldError
/// 呈现自动生成的消息、清空回填默认值后经 EditContext 校验沿呈现消息。
/// </summary>
public class FormFieldInputTests
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
    /// 校验沿测试模型：Age 非正即无效（由假校验器执行）。
    /// </summary>
    internal sealed class Person
    {
        public int Age { get; set; }
    }

    /// <summary>
    /// 构造挂接假校验器的 EditContext：与真实 DataAnnotationsValidator 语义一致——
    /// 订阅 OnFieldChanged/OnValidationRequested，用 ValidationMessageStore 写入消息。
    /// </summary>
    private static EditContext CreateValidatedContext(Person person)
    {
        var context = new EditContext(person);
        var messages = new ValidationMessageStore(context);
        var field = new FieldIdentifier(person, nameof(Person.Age));

        void Validate()
        {
            messages.Clear();
            if (person.Age <= 0)
            {
                messages.Add(field, "年龄必须为正整数");
            }
            context.NotifyValidationStateChanged();
        }

        context.OnFieldChanged += (_, _) => Validate();
        context.OnValidationRequested += (_, _) => Validate();
        return context;
    }

    private static RenderFragment FieldFragment(Person person) => builder =>
    {
        // 无约束 TValue? 在 TValue=int 时即 int：参数类型须逐一对应。
        Expression<Func<int>> valueExpression = () => person.Age;

        builder.OpenComponent<FormFieldInput<int>>(0);
        builder.AddAttribute(1, nameof(FormFieldInput<int>.Value), person.Age);
        builder.AddAttribute(2, nameof(FormFieldInput<int>.ValueExpression), valueExpression);
        builder.AddAttribute(3, nameof(FormFieldInput<int>.ValueChanged),
            EventCallback.Factory.Create<int>(person, value => person.Age = value));
        builder.AddAttribute(4, nameof(FormFieldInput<int>.Label), (string?)"年龄");
        builder.AddAttribute(5, nameof(FormFieldInput<int>.HelperText), (string?)"请输入真实年龄");
        builder.CloseComponent();
    };

    private static async Task<(ComponentTestRenderer Renderer, FormFieldInput<int> Field)> MountWithEditContextAsync(
        Person person)
    {
        var renderer = CreateRenderer();
        var context = CreateValidatedContext(person);

        await renderer.MountAsync<FormFieldBaseTests.EditContextHost>(new Dictionary<string, object?>
        {
            [nameof(FormFieldBaseTests.EditContextHost.EditContext)] = context,
            [nameof(FormFieldBaseTests.EditContextHost.ChildContent)] = FieldFragment(person),
        });

        return (renderer, renderer.FindComponent<FormFieldInput<int>>());
    }

    [Fact]
    public async Task 正常值快照应与基线一致()
    {
        var person = new Person { Age = 30 };

        var (renderer, _) = await MountWithEditContextAsync(person);
        await SettleAsync(renderer);

        await Verify(Scrub(renderer.Markup()));
    }

    [Fact]
    public async Task 控件ID应由字段持有并与标签和描述接线()
    {
        var person = new Person { Age = 30 };

        var (renderer, _) = await MountWithEditContextAsync(person);
        await SettleAsync(renderer);

        var markup = renderer.Markup();
        var match = Regex.Match(markup, @"id=""(formfield-[0-9a-f]{32})""");
        Assert.True(match.Success, "输入控件应携带 ControlId 作为 id");

        var controlId = match.Groups[1].Value;
        // label for 指向控件；有效态下 describedby 指向描述元素。
        Assert.Contains($"for=\"{controlId}\"", markup);
        Assert.Contains($"aria-describedby=\"{controlId}-description\"", markup);
        Assert.DoesNotContain("aria-invalid", markup);
    }

    [Fact]
    public async Task 非法解析应经FieldError呈现自动消息并切换describedby()
    {
        var person = new Person { Age = 30 };

        var (renderer, field) = await MountWithEditContextAsync(person);
        var inner = renderer.FindComponent<InputField<int>>();

        await renderer.Dispatcher.InvokeAsync(() => inner.JsOnChange("abc"));
        await SettleAsync(renderer);

        Assert.True(field.HasError);
        var markup = renderer.Markup();
        Assert.Contains("'abc' is not a valid whole number.", markup);
        Assert.Contains("data-slot=\"field-error\"", markup);
        Assert.Contains("aria-invalid=\"true\"", markup);

        // 错误态下帮助文本隐藏，describedby 切换到错误元素。
        Assert.DoesNotContain("请输入真实年龄", markup);
        var match = Regex.Match(markup, @"id=""(formfield-[0-9a-f]{32})""");
        Assert.Contains($"aria-describedby=\"{match.Groups[1].Value}-error\"", markup);
    }

    [Fact]
    public async Task 清空回填默认值应触发EditContext校验消息()
    {
        var person = new Person { Age = 30 };

        var (renderer, field) = await MountWithEditContextAsync(person);
        var inner = renderer.FindComponent<InputField<int>>();

        await renderer.Dispatcher.InvokeAsync(() => inner.JsOnChange(""));
        await SettleAsync(renderer);

        // 值沿：清空 → default(int)=0 → 回填模型 → NotifyFieldChanged → 假校验器写入消息。
        Assert.Equal(0, person.Age);
        Assert.False(field.HasError); // 非解析错误，走 EditContext 校验沿。

        var markup = renderer.Markup();
        Assert.Contains("年龄必须为正整数", markup);
        Assert.Contains("data-slot=\"field-error\"", markup);
        Assert.Contains("aria-invalid=\"true\"", markup);
    }
}
