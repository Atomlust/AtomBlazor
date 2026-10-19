using AtomBlazor.Primitives;
using System.Text.RegularExpressions;
using AtomBlazor;
using AtomBlazor.Tests.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Xunit;

namespace AtomBlazor.Tests.Components;

/// <summary>
/// InputField 泛型解析与校验测试：默认渲染快照（Scrub 随机 id）、非法文本触发 OnParseError
/// （ErrorKind=Parse）并保留非法文本与 aria-invalid、有效值恢复并触发 OnErrorCleared、
/// ValidationPattern 违例映射 PatternValidation、Validation 函数拒绝映射 ValueValidation、
/// 清空回填 default(TValue)、Format 仅影响显示格式化、无 EditContext 宿主外挂载降级不抛。
/// 泛型组件统一经 MountTypeAsync 挂载。
/// </summary>
public class InputFieldTests
{
    /// <summary>
    /// 归一化组件自身的三种随机短 id 前缀，使快照可确定。
    /// </summary>
    private static string Scrub(string markup) =>
        Regex.Replace(markup, "(input|inputfield|textarea)-[0-9a-f]{8}", "$1-<id>");

    private static ComponentTestRenderer CreateRenderer()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(new NoopJavaScript());
        return new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance);
    }

    /// <summary>让调度器空转两轮，冲刷 OnAfterRender/StateHasChanged 队列。</summary>
    private static async Task FlushAsync(ComponentTestRenderer renderer)
    {
        for (var i = 0; i < 2; i++)
        {
            await renderer.Dispatcher.InvokeAsync(() => { });
        }
    }

    private static async Task<(ComponentTestRenderer Renderer, InputField<TValue> Field)> MountAsync<TValue>(
        Dictionary<string, object?> parameters)
    {
        var renderer = CreateRenderer();
        var component = await renderer.Dispatcher.InvokeAsync(() =>
            renderer.MountTypeAsync(typeof(InputField<TValue>), parameters));
        await FlushAsync(renderer);
        return (renderer, (InputField<TValue>)component);
    }

    /// <summary>失焦沿：直接调用 JS 入口并等待重渲染落定。</summary>
    private static Task ChangeAsync<TValue>(ComponentTestRenderer renderer, InputField<TValue> field, string? value) =>
        renderer.Dispatcher.InvokeAsync(() => field.JsOnChange(value));

    [Fact]
    public async Task 默认渲染快照应与基线一致()
    {
        var (renderer, _) = await MountAsync<int>(new Dictionary<string, object?>
        {
            [nameof(InputField<int>.Value)] = 42,
            [nameof(InputField<int>.Placeholder)] = "请输入数量",
        });

        await Verify(Scrub(renderer.Markup()));
    }

    [Fact]
    public async Task 非法文本应触发解析错误并保留原文与无效态()
    {
        var errors = new List<InputParseException>();
        var onError = EventCallback.Factory.Create<InputParseException>(new object(), e => errors.Add(e));

        var (renderer, field) = await MountAsync<int>(new Dictionary<string, object?>
        {
            [nameof(InputField<int>.Value)] = 1,
            [nameof(InputField<int>.OnParseError)] = onError,
        });

        await ChangeAsync(renderer, field, "abc");
        await FlushAsync(renderer);

        var error = Assert.Single(errors);
        Assert.Equal(InputFieldErrorKind.Parse, error.ErrorKind);
        Assert.Equal("abc", error.RawInput);
        Assert.Equal(typeof(int), error.TargetType);
        Assert.True(field.HasParseError);
        Assert.Equal(InputFieldErrorKind.Parse, field.CurrentErrorKind);

        var markup = renderer.Markup();
        // 保留非法文本与 aria-invalid，值不回填。
        Assert.Contains("value=\"abc\"", markup);
        Assert.Contains("aria-invalid=\"true\"", markup);
    }

    [Fact]
    public async Task 错误后再输入有效值应清除错误并触发OnErrorCleared()
    {
        var errors = 0;
        var cleared = 0;
        var reports = new List<int>();
        var onError = EventCallback.Factory.Create<InputParseException>(new object(), _ => errors++);
        var onCleared = EventCallback.Factory.Create(new object(), () => cleared++);
        var onChanged = EventCallback.Factory.Create<int>(new object(), v => reports.Add(v));

        var (renderer, field) = await MountAsync<int>(new Dictionary<string, object?>
        {
            [nameof(InputField<int>.Value)] = 1,
            [nameof(InputField<int>.OnParseError)] = onError,
            [nameof(InputField<int>.OnErrorCleared)] = onCleared,
            [nameof(InputField<int>.ValueChanged)] = onChanged,
        });

        await ChangeAsync(renderer, field, "abc");
        await FlushAsync(renderer);
        Assert.Equal(1, errors);

        await ChangeAsync(renderer, field, "42");
        await FlushAsync(renderer);

        Assert.Equal(1, cleared);
        Assert.False(field.HasParseError);
        Assert.Null(field.CurrentErrorKind);
        Assert.Equal([42], reports);

        var markup = renderer.Markup();
        Assert.Contains("value=\"42\"", markup);
        Assert.DoesNotContain("aria-invalid", markup);
    }

    [Fact]
    public async Task 违反ValidationPattern应上报模式校验错误()
    {
        var errors = new List<InputParseException>();
        var onError = EventCallback.Factory.Create<InputParseException>(new object(), e => errors.Add(e));

        var (renderer, field) = await MountAsync<int>(new Dictionary<string, object?>
        {
            [nameof(InputField<int>.ValidationPattern)] = @"^\d+$",
            [nameof(InputField<int>.OnParseError)] = onError,
        });

        // "12a" 既不匹配模式也无法解析：模式校验先行。
        await ChangeAsync(renderer, field, "12a");
        await FlushAsync(renderer);

        var error = Assert.Single(errors);
        Assert.Equal(InputFieldErrorKind.PatternValidation, error.ErrorKind);
        Assert.Contains("value=\"12a\"", renderer.Markup());
        Assert.Contains("aria-invalid=\"true\"", renderer.Markup());
    }

    [Fact]
    public async Task Validation函数拒绝应上报值校验错误且不回填()
    {
        var errors = new List<InputParseException>();
        var reports = new List<int>();
        var onError = EventCallback.Factory.Create<InputParseException>(new object(), e => errors.Add(e));
        var onChanged = EventCallback.Factory.Create<int>(new object(), v => reports.Add(v));

        var (renderer, field) = await MountAsync<int>(new Dictionary<string, object?>
        {
            [nameof(InputField<int>.Validation)] = (Func<int, bool>)(v => v >= 18),
            [nameof(InputField<int>.OnParseError)] = onError,
            [nameof(InputField<int>.ValueChanged)] = onChanged,
        });

        await ChangeAsync(renderer, field, "5");
        await FlushAsync(renderer);

        var error = Assert.Single(errors);
        Assert.Equal(InputFieldErrorKind.ValueValidation, error.ErrorKind);
        Assert.Empty(reports);
        Assert.Contains("value=\"5\"", renderer.Markup());
    }

    [Fact]
    public async Task 清空输入应回填默认值并上报()
    {
        var reports = new List<int>();
        var onChanged = EventCallback.Factory.Create<int>(new object(), v => reports.Add(v));

        var (renderer, field) = await MountAsync<int>(new Dictionary<string, object?>
        {
            [nameof(InputField<int>.Value)] = 7,
            [nameof(InputField<int>.ValueChanged)] = onChanged,
        });

        await ChangeAsync(renderer, field, "");
        await FlushAsync(renderer);

        Assert.Equal([0], reports);
        Assert.Equal(0, field.Value);
        Assert.Contains("value=\"0\"", renderer.Markup());
    }

    [Fact]
    public async Task Format应只影响显示格式化而不参与解析()
    {
        var (renderer, _) = await MountAsync<decimal>(new Dictionary<string, object?>
        {
            [nameof(InputField<decimal>.Value)] = 1234.5m,
            [nameof(InputField<decimal>.Format)] = "N2",
            // 固定区域文化的实例转换器，消除宿主文化对快照断言的影响。
            [nameof(InputField<decimal>.Converter)] = new InputConverter<decimal>
            {
                Culture = System.Globalization.CultureInfo.InvariantCulture,
            },
        });

        Assert.Contains("value=\"1,234.50\"", renderer.Markup());
    }

    [Fact]
    public async Task 无EditContext宿主外挂载应降级不抛且不输出name()
    {
        var reports = new List<int>();
        var onChanged = EventCallback.Factory.Create<int>(new object(), v => reports.Add(v));

        var (renderer, field) = await MountAsync<int>(new Dictionary<string, object?>
        {
            [nameof(InputField<int>.ValueChanged)] = onChanged,
        });

        // 没有级联 EditContext：解析沿照常工作，name 不、不抛异常。
        await ChangeAsync(renderer, field, "9");
        await FlushAsync(renderer);

        Assert.Equal([9], reports);
        Assert.Equal(9, field.Value);
        Assert.DoesNotContain("name=", renderer.Markup());
    }
}
