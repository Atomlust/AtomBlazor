using AtomBlazor.Primitives;
using System.Globalization;
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
/// NativeSelect 家族测试：原生 select 与 option 子内容的直通渲染、
/// Value 到 <c>value</c> 特性的格式化回填（含枚举、Guid、不变文化数值）、
/// onchange 的字符串回转与失败回退、占位项的 selected 态
/// 以及 EditContext 内由 ValueExpression name 并通知字段变化。
/// </summary>
public class NativeSelectTests
{
    /// <summary>币种选项，用于验证枚举特判（<c>Convert.ChangeType</c> 并不支持枚举）。</summary>
    internal enum Currency
    {
        Cny,
        Usd,
        Eur,
    }

    /// <summary>
    /// 归一化自动生成的选择框短 id，使快照可确定。
    /// </summary>
    private static string Scrub(string markup) =>
        Regex.Replace(markup, "select-[0-9a-f]{8}", "select-<id>");

    private static ComponentTestRenderer CreateRenderer()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(new NoopJavaScript());
        return new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance);
    }

    /// <summary>排空渲染队列，确保 StateHasChanged 的后续批已落定。</summary>
    private static Task SettleAsync(ComponentTestRenderer renderer) =>
        renderer.Dispatcher.InvokeAsync(() => Task.CompletedTask);

    /// <summary>两个 option 子内容，字符串值。</summary>
    private static RenderFragment Options(params string[] values) => builder =>
    {
        var seq = 0;
        foreach (var value in values)
        {
            builder.OpenElement(seq++, "option");
            builder.AddAttribute(seq++, "value", value);
            builder.AddContent(seq++, $"选项 {value}");
            builder.CloseElement();
        }
    };

    private static Dictionary<string, object?> Params(
        RenderFragment? childContent = null,
        string? value = null,
        EventCallback<string?> valueChanged = default,
        string? name = null,
        string? placeholder = null,
        bool? disabled = null,
        bool? required = null,
        string? @class = null,
        NativeSelectSize? size = null,
        string? id = null,
        string? ariaLabel = null)
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(NativeSelect<string>.ChildContent)] = childContent ?? Options("cny", "usd"),
        };
        if (value is not null)
        {
            parameters[nameof(NativeSelect<string>.Value)] = value;
        }
        if (valueChanged.HasDelegate)
        {
            parameters[nameof(NativeSelect<string>.ValueChanged)] = valueChanged;
        }
        if (name is not null)
        {
            parameters[nameof(NativeSelect<string>.Name)] = name;
        }
        if (placeholder is not null)
        {
            parameters[nameof(NativeSelect<string>.Placeholder)] = placeholder;
        }
        if (disabled is not null)
        {
            parameters[nameof(NativeSelect<string>.Disabled)] = disabled.Value;
        }
        if (required is not null)
        {
            parameters[nameof(NativeSelect<string>.Required)] = required.Value;
        }
        if (@class is not null)
        {
            parameters[nameof(NativeSelect<string>.Class)] = @class;
        }
        if (size is not null)
        {
            parameters[nameof(NativeSelect<string>.Size)] = size.Value;
        }
        if (id is not null)
        {
            parameters[nameof(NativeSelect<string>.Id)] = id;
        }
        if (ariaLabel is not null)
        {
            parameters[nameof(NativeSelect<string>.AriaLabel)] = ariaLabel;
        }

        return parameters;
    }

    /// <summary>取 select 元素的开标签。</summary>
    private static string SelectTag(string markup) =>
        Regex.Match(markup, @"<select[^>]*>").Value;

    // ------------------------------------------------------------------
    // 快照与结构
    // ------------------------------------------------------------------

    [Fact]
    public async Task 默认渲染快照应与基线一致()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<NativeSelect<string>>(Params(ariaLabel: "结算币种"));
        await SettleAsync(renderer);

        await Verify(Scrub(renderer.Markup()));
    }

    [Fact]
    public async Task 含占位项与必填禁用的结构快照应与基线一致()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<NativeSelect<string>>(new Dictionary<string, object?>
        {
            [nameof(NativeSelect<string>.ChildContent)] = Options("cny", "usd"),
            [nameof(NativeSelect<string>.Placeholder)] = "请选择币种",
            [nameof(NativeSelect<string>.Required)] = true,
            [nameof(NativeSelect<string>.Disabled)] = true,
            [nameof(NativeSelect<string>.Size)] = NativeSelectSize.Small,
            [nameof(NativeSelect<string>.Name)] = "currency",
            [nameof(NativeSelect<string>.Id)] = "currency-select",
        });
        await SettleAsync(renderer);

        await Verify(renderer.Markup());
    }

    [Fact]
    public async Task option子内容应原样落在select内部()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<NativeSelect<string>>(Params(Options("cny", "usd", "eur")));

        var markup = renderer.Markup();
        // Regex.Count 静态重载是 (input, pattern)：markup 在前、模式在后。
        Assert.Equal(3, Regex.Count(markup, "<option"));
        Assert.Contains("<option value=\"eur\">选项 eur</option>", markup);
        Assert.Contains("data-atom-native-select", SelectTag(markup));
    }

    [Fact]
    public async Task 未选中值时不应输出name与selected占位项()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<NativeSelect<string>>(Params());

        var tag = SelectTag(renderer.Markup());
        Assert.DoesNotContain("name=", tag);
        Assert.DoesNotContain("aria-invalid", tag);
    }

    // ------------------------------------------------------------------
    // 值回填与格式化
    // ------------------------------------------------------------------

    [Fact]
    public async Task 字符串值应写回select的value特性()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<NativeSelect<string>>(Params(@class: "atom:my-select", value: "usd"));

        Assert.Contains("value=\"usd\"", SelectTag(renderer.Markup()));
    }

    [Theory]
    [InlineData(NativeSelectSize.Small, "atom:h-8 atom:text-xs atom:ps-2")]
    [InlineData(NativeSelectSize.Default, "atom:h-10 atom:text-sm atom:ps-3")]
    [InlineData(NativeSelectSize.Large, "atom:h-12 atom:text-base atom:ps-4")]
    public async Task 尺寸变体应切换到对应的高度内边距与字号(NativeSelectSize size, string expected)
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<NativeSelect<string>>(Params(size: size));

        Assert.Contains(expected, SelectTag(renderer.Markup()));
        Assert.Contains("atom:appearance-none atom:pe-8", SelectTag(renderer.Markup()));
    }

    [Fact]
    public async Task 自定义类应追加在基础类之后()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<NativeSelect<string>>(Params(@class: "atom:my-select"));

        var classAttribute = Regex.Match(SelectTag(renderer.Markup()), @"class=""([^""]*)""");
        Assert.True(classAttribute.Success);
        Assert.EndsWith("atom:my-select", classAttribute.Groups[1].Value);
        Assert.Contains("atom:flex atom:w-full", classAttribute.Groups[1].Value);
    }

    [Fact]
    public async Task 显式Id与Name应优先于自动值()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<NativeSelect<string>>(Params(id: "currency-select", name: "currency"));

        var tag = SelectTag(renderer.Markup());
        Assert.Contains("id=\"currency-select\"", tag);
        Assert.Contains("name=\"currency\"", tag);
    }

    [Fact]
    public async Task 未提供Id时应自动生成长度稳定的短id()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<NativeSelect<string>>(Params());

        Assert.Contains("id=\"select-", Scrub(SelectTag(renderer.Markup())));
        Assert.Matches(@"id=""select-[0-9a-f]{8}""", SelectTag(renderer.Markup()));
    }

    // ------------------------------------------------------------------
    // onchange 回转
    // ------------------------------------------------------------------

    [Fact]
    public async Task 选择字符串选项应原样上报()
    {
        var reports = new List<string?>();
        var callback = EventCallback.Factory.Create<string?>(new object(), value => reports.Add(value));
        var renderer = CreateRenderer();

        await renderer.MountAsync<NativeSelect<string>>(Params(valueChanged: callback));

        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onchange", new ChangeEventArgs { Value = "usd" }));

        Assert.Equal(["usd"], reports);
    }

    [Fact]
    public async Task 选择整数选项应按不变文化回转并上报()
    {
        var reports = new List<int>();
        var callback = EventCallback.Factory.Create<int>(new object(), value => reports.Add(value));
        var renderer = CreateRenderer();

        await renderer.MountAsync<NativeSelect<int>>(new Dictionary<string, object?>
        {
            [nameof(NativeSelect<int>.ChildContent)] = Options("1", "42"),
            [nameof(NativeSelect<int>.ValueChanged)] = callback,
        });

        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onchange", new ChangeEventArgs { Value = "42" }));

        Assert.Equal([42], reports);
        Assert.Contains("value=\"42\"", SelectTag(renderer.Markup()));
    }

    [Fact]
    public async Task 选择枚举选项应走特判而不是ConvertChangeType()
    {
        var reports = new List<Currency>();
        var callback = EventCallback.Factory.Create<Currency>(new object(), value => reports.Add(value));
        var renderer = CreateRenderer();

        await renderer.MountAsync<NativeSelect<Currency>>(new Dictionary<string, object?>
        {
            [nameof(NativeSelect<Currency>.Value)] = Currency.Cny,
            [nameof(NativeSelect<Currency>.ChildContent)] = Options("Cny", "Usd"),
            [nameof(NativeSelect<Currency>.ValueChanged)] = callback,
        });

        // 枚举值写回时按 option 自身的方式呈现（名称而非数字）。
        Assert.Contains("value=\"Cny\"", SelectTag(renderer.Markup()));

        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onchange", new ChangeEventArgs { Value = "Usd" }));

        Assert.Equal([Currency.Usd], reports);
    }

    [Fact]
    public async Task 选择Guid选项应解析回转()
    {
        var id = Guid.Parse("1f2c3d4e-5678-90ab-cdef-1234567890ab");
        var reports = new List<Guid>();
        var callback = EventCallback.Factory.Create<Guid>(new object(), value => reports.Add(value));
        var renderer = CreateRenderer();

        await renderer.MountAsync<NativeSelect<Guid>>(new Dictionary<string, object?>
        {
            [nameof(NativeSelect<Guid>.ChildContent)] = Options(id.ToString("D", CultureInfo.InvariantCulture)),
            [nameof(NativeSelect<Guid>.ValueChanged)] = callback,
        });

        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onchange", new ChangeEventArgs { Value = id.ToString("D") }));

        Assert.Equal([id], reports);
    }

    [Fact]
    public async Task 选中占位项应回退到默认值并上报()
    {
        var reports = new List<string?>();
        var callback = EventCallback.Factory.Create<string?>(new object(), value => reports.Add(value));
        var renderer = CreateRenderer();

        await renderer.MountAsync<NativeSelect<string>>(new Dictionary<string, object?>
        {
            [nameof(NativeSelect<string>.Value)] = "usd",
            [nameof(NativeSelect<string>.Placeholder)] = "请选择币种",
            [nameof(NativeSelect<string>.ChildContent)] = Options("cny", "usd"),
            [nameof(NativeSelect<string>.ValueChanged)] = callback,
        });

        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onchange", new ChangeEventArgs { Value = "" }));

        Assert.Null(reports.Single());
        Assert.Contains("value=\"\"", SelectTag(renderer.Markup()));
    }

    [Fact]
    public async Task 无法解析的字符串应回退到默认值而不是抛出()
    {
        var reports = new List<int>();
        var callback = EventCallback.Factory.Create<int>(new object(), value => reports.Add(value));
        var renderer = CreateRenderer();

        await renderer.MountAsync<NativeSelect<int>>(new Dictionary<string, object?>
        {
            [nameof(NativeSelect<int>.Value)] = 7,
            [nameof(NativeSelect<int>.ChildContent)] = Options("7"),
            [nameof(NativeSelect<int>.ValueChanged)] = callback,
        });

        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onchange", new ChangeEventArgs { Value = "不是数字" }));

        Assert.Equal([0], reports);
    }

    [Fact]
    public async Task 数值与可空数值应按不变文化写入value()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<NativeSelect<decimal>>(new Dictionary<string, object?>
        {
            [nameof(NativeSelect<decimal>.Value)] = 1234.56m,
            [nameof(NativeSelect<decimal>.ChildContent)] = Options("1234.56"),
        });
        Assert.Contains("value=\"1234.56\"", SelectTag(renderer.Markup()));

        var nullable = CreateRenderer();
        await nullable.MountAsync<NativeSelect<int?>>(new Dictionary<string, object?>
        {
            [nameof(NativeSelect<int?>.Value)] = 8,
            [nameof(NativeSelect<int?>.ChildContent)] = Options("8"),
        });
        Assert.Contains("value=\"8\"", SelectTag(nullable.Markup()));
    }

    [Fact]
    public async Task 占位项应只在值为空时被标记为selected()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<NativeSelect<string>>(new Dictionary<string, object?>
        {
            [nameof(NativeSelect<string>.Placeholder)] = "请选择币种",
            [nameof(NativeSelect<string>.ChildContent)] = Options("cny", "usd"),
        });

        var placeholder = Regex.Match(renderer.Markup(), @"<option[^>]*>[^<]*</option>").Value;
        Assert.Contains("disabled", placeholder);
        Assert.Contains("selected", placeholder);

        var picked = CreateRenderer();
        await picked.MountAsync<NativeSelect<string>>(new Dictionary<string, object?>
        {
            [nameof(NativeSelect<string>.Value)] = "cny",
            [nameof(NativeSelect<string>.Placeholder)] = "请选择币种",
            [nameof(NativeSelect<string>.ChildContent)] = Options("cny", "usd"),
        });
        Assert.DoesNotContain("selected", Regex.Match(picked.Markup(), @"<option value="""">[^>]*>").Value);
    }

    [Fact]
    public async Task 禁用与必填应作为HTML布尔属性落在select上()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<NativeSelect<string>>(Params(disabled: true, required: true));

        var tag = SelectTag(renderer.Markup());
        Assert.Contains("disabled", tag);
        Assert.Contains("required", tag);
    }

    // ------------------------------------------------------------------
    // EditContext 校验沿
    // ------------------------------------------------------------------

    /// <summary>校验沿测试模型：币种必填。</summary>
    internal sealed class Order
    {
        public string? Currency { get; set; }
    }

    /// <summary>
    /// 构造挂接假校验器的 EditContext：语义与真实 DataAnnotationsValidator 一致——
    /// 未选择即写入消息；同时记录 OnFieldChanged 次数，供断言字段通知沿。
    /// </summary>
    private static (EditContext Context, List<int> FieldChanges) CreateValidatedContext(Order order)
    {
        var context = new EditContext(order);
        var messages = new ValidationMessageStore(context);
        var field = new FieldIdentifier(order, nameof(Order.Currency));
        var fieldChanges = new List<int>();

        void Validate()
        {
            messages.Clear();
            if (string.IsNullOrEmpty(order.Currency))
            {
                messages.Add(field, "请选择币种");
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

    private static RenderFragment SelectFragment(Order order) => builder =>
    {
        Expression<Func<string?>> valueExpression = () => order.Currency;

        builder.OpenComponent<NativeSelect<string?>>(0);
        builder.AddAttribute(1, nameof(NativeSelect<string?>.Value), order.Currency);
        builder.AddAttribute(2, nameof(NativeSelect<string?>.ValueExpression), valueExpression);
        builder.AddAttribute(3, nameof(NativeSelect<string?>.ValueChanged),
            EventCallback.Factory.Create<string?>(order, value => order.Currency = value));
        builder.AddAttribute(4, nameof(NativeSelect<string?>.ChildContent), Options("cny", "usd"));
        builder.CloseComponent();
    };

    [Fact]
    public async Task 校验沿应从表达式name并在选择后通知字段变化()
    {
        var order = new Order();
        var (context, fieldChanges) = CreateValidatedContext(order);
        var renderer = CreateRenderer();

        await renderer.MountAsync<FormFieldBaseTests.EditContextHost>(new Dictionary<string, object?>
        {
            [nameof(FormFieldBaseTests.EditContextHost.EditContext)] = context,
            [nameof(FormFieldBaseTests.EditContextHost.ChildContent)] = SelectFragment(order),
        });
        await SettleAsync(renderer);

        // name 由 FieldIdentifier ，SSR/增强表单回传才带得上这个控件。
        Assert.Contains($"name=\"{nameof(Order.Currency)}\"", SelectTag(renderer.Markup()));

        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onchange", new ChangeEventArgs { Value = "usd" }));
        await SettleAsync(renderer);

        Assert.Equal("usd", order.Currency);
        Assert.Single(fieldChanges);
        Assert.Contains("value=\"usd\"", SelectTag(renderer.Markup()));
    }
}
