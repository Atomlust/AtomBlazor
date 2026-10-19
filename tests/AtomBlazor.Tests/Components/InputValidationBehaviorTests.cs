using AtomBlazor.Primitives;
using System.Linq.Expressions;
using AtomBlazor;
using Microsoft.AspNetCore.Components.Forms;
using Xunit;

namespace AtomBlazor.Tests.Components;

/// <summary>
/// InputValidationBehavior 校验基建单元测试（internal，经 InternalsVisibleTo 直测）：
/// Update 绑定/缺参降级、NotifyFieldChanged 通知沿与 FieldIdentifier 正确性、
/// IsInvalid 随 ValidationMessageStore 消息翻转、GetEffectiveName 的
/// 显式名/表达式路径/字段名回退链、GetEffectiveAriaInvalid 三源合并表驱动。
/// 该类是 输入控件时机语义（oninput/onblur/onchange/onsubmit）的执行核心，
/// 时机沿本身在 FormFieldBaseTests 表驱动覆盖。
/// </summary>
public class InputValidationBehaviorTests
{
    private sealed class Person
    {
        public string? Username { get; set; }
    }

    // ------------------------------------------------------------------
    // Update 与通知沿
    // ------------------------------------------------------------------

    [Fact]
    public void Update应绑定EditContext并生成FieldIdentifier()
    {
        var person = new Person { Username = "ok" };
        var context = new EditContext(person);
        FieldIdentifier? notified = null;
        context.OnFieldChanged += (_, e) => notified = e.FieldIdentifier;

        var behavior = new InputValidationBehavior();
        Expression<Func<string?>> expression = () => person.Username;
        behavior.Update(context, expression);

        behavior.NotifyFieldChanged();

        Assert.NotNull(notified);
        Assert.Same(person, notified!.Value.Model);
        Assert.Equal(nameof(Person.Username), notified!.Value.FieldName);
    }

    [Fact]
    public void 缺少EditContext或表达式时不应绑定通知应为空操作()
    {
        var person = new Person { Username = "ok" };
        var context = new EditContext(person);
        var fired = false;
        context.OnFieldChanged += (_, _) => fired = true;

        var withoutContext = new InputValidationBehavior();
        withoutContext.Update(null, () => person.Username);
        withoutContext.NotifyFieldChanged();
        Assert.False(fired);
        Assert.False(withoutContext.IsInvalid);

        var withoutExpression = new InputValidationBehavior();
        withoutExpression.Update<string?>(context, null);
        withoutExpression.NotifyFieldChanged();
        Assert.False(fired);
        Assert.False(withoutExpression.IsInvalid);
    }

    [Fact]
    public void IsInvalid应随ValidationMessageStore消息翻转()
    {
        var person = new Person { Username = "ok" };
        var context = new EditContext(person);
        var messages = new ValidationMessageStore(context);
        var field = new FieldIdentifier(person, nameof(Person.Username));

        var behavior = new InputValidationBehavior();
        behavior.Update(context, () => person.Username);
        Assert.False(behavior.IsInvalid);

        messages.Add(field, "无效");
        Assert.True(behavior.IsInvalid);

        messages.Clear();
        Assert.False(behavior.IsInvalid);
    }

    // ------------------------------------------------------------------
    // GetEffectiveName 回退链
    // ------------------------------------------------------------------

    [Fact]
    public void 显式name应直通优先于表达式路径()
    {
        var person = new Person { Username = "ok" };
        var context = new EditContext(person);

        var behavior = new InputValidationBehavior();
        behavior.Update(context, () => person.Username);

        Assert.Equal("custom-name", behavior.GetEffectiveName("custom-name"));
    }

    [Fact]
    public void 绑定EditContext后name应回退为表达式完整路径()
    {
        var person = new Person { Username = "ok" };
        var context = new EditContext(person);

        var behavior = new InputValidationBehavior();
        behavior.Update(context, () => person.Username);

        // 与 InputBase 对齐：SSR 表单回传要求 "person.Username" 形式的完整成员路径，
        // 由 ExpressionPathFormatter 生成。
        Assert.Equal("person.Username", behavior.GetEffectiveName(null));
    }

    [Fact]
    public void 未绑定EditContext时name应回退为空()
    {
        var person = new Person { Username = "ok" };

        var behavior = new InputValidationBehavior();
        behavior.Update(null, () => person.Username);

        Assert.Null(behavior.GetEffectiveName(null));
        Assert.Equal("explicit", behavior.GetEffectiveName("explicit"));
    }

    // ------------------------------------------------------------------
    // GetEffectiveAriaInvalid 三源合并表驱动
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(true, null, false, "true")]   // 手动 AriaInvalid 优先
    [InlineData(false, null, true, "false")]  // 手动显式 false 压过校验错误
    [InlineData(false, true, false, "false")]
    [InlineData(null, true, false, "true")]   // 父级 Field 无效沿
    [InlineData(null, false, true, "true")]   // EditContext 校验错误沿
    [InlineData(null, null, false, null)]     // 三源皆无 → 不写出
    [InlineData(null, false, false, null)]
    public void AriaInvalid应按手动字段级联与上下文三源合并(bool? manual, bool? fieldInvalid, bool contextError, string? expected)
    {
        var person = new Person { Username = "ok" };
        var context = new EditContext(person);
        var field = new FieldIdentifier(person, nameof(Person.Username));

        var behavior = new InputValidationBehavior();
        behavior.Update(context, () => person.Username);

        if (contextError)
        {
            new ValidationMessageStore(context).Add(field, "错误");
        }

        Assert.Equal(expected, behavior.GetEffectiveAriaInvalid(manual, fieldInvalid));
    }

    // ------------------------------------------------------------------
    // 表达式路径格式化（GetEffectiveName 的支撑件）
    // ------------------------------------------------------------------

    [Fact]
    public void 路径格式化应支持嵌套成员并拒绝非成员形态()
    {
        var person = new Person { Username = "ok" };

        Assert.Equal("person.Username", ExpressionPathFormatter.FormatLambda(() => person.Username));
        Assert.Null(ExpressionPathFormatter.FormatLambda(null));

        // 非成员访问形态（方法调用）应返回 null，由调用方回退到字段名。
        Assert.Null(ExpressionPathFormatter.FormatLambda(() => person.Username!.Length > 0));
    }
}
