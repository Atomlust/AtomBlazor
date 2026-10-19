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
/// Input 渲染与 JS 化事件测试：默认渲染快照（Scrub 随机 id）、InputType 到 HTML type 的映射表驱动、
/// File 类型不输出 value、首渲染后 textInput.initialize 的 interop 契约（identifier、instanceId 与配置内容）、
/// UpdateTiming/DebounceInterval 变化驱动 textInput.updateConfig、JsOnInput/JsOnChange 直接调用回填值
/// 与空白规整为 null、释放时派发 textInput.dispose。
/// </summary>
public class InputTests
{
    /// <summary>
    /// 把 EffectiveId 中的随机短 GUID 归一化为占位符，使快照可确定。
    /// </summary>
    private static string Scrub(string markup) =>
        Regex.Replace(markup, "input-[0-9a-f]{8}", "input-<id>");

    private static ComponentTestRenderer CreateRenderer(NoopJavaScript? js = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(js ?? new NoopJavaScript());
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

    private static async Task<Input> MountAsync(ComponentTestRenderer renderer, Dictionary<string, object?> parameters)
    {
        var input = await renderer.Dispatcher.InvokeAsync(() => renderer.MountAsync<Input>(parameters));
        await FlushAsync(renderer);
        return input;
    }

    /// <summary>读取发给 JS 的匿名配置对象（匿名类型不可 ToString 断言，按属性反射读取）。</summary>
    private static (string Mode, int DebounceMs) ReadConfig(object config)
    {
        var type = config.GetType();
        return ((string)type.GetProperty("mode")!.GetValue(config)!,
                (int)type.GetProperty("debounceMs")!.GetValue(config)!);
    }

    [Fact]
    public async Task 默认渲染快照应与基线一致()
    {
        var renderer = CreateRenderer();

        await MountAsync(renderer, new Dictionary<string, object?>
        {
            [nameof(Input.Value)] = "hello",
            [nameof(Input.Placeholder)] = "请输入内容",
        });

        await Verify(Scrub(renderer.Markup()));
    }

    [Theory]
    [InlineData(InputType.Email, "email")]
    [InlineData(InputType.Number, "number")]
    [InlineData(InputType.Date, "date")]
    [InlineData(InputType.Password, "password")]
    [InlineData(InputType.File, "file")]
    public async Task 输入类型枚举应映射为同名HTMLtype属性(InputType type, string expected)
    {
        var renderer = CreateRenderer();

        await MountAsync(renderer, new Dictionary<string, object?>
        {
            [nameof(Input.Type)] = type,
        });

        Assert.Contains($"type=\"{expected}\"", renderer.Markup());
    }

    [Fact]
    public async Task File类型不应输出value属性()
    {
        var renderer = CreateRenderer();

        await MountAsync(renderer, new Dictionary<string, object?>
        {
            [nameof(Input.Type)] = InputType.File,
            [nameof(Input.Value)] = "不应出现",
        });

        Assert.DoesNotContain("value=", renderer.Markup());
    }

    [Fact]
    public async Task 首渲染应派发textInput初始化并携带标识符与配置()
    {
        var js = new NoopJavaScript();
        var renderer = CreateRenderer(js);

        await MountAsync(renderer, new Dictionary<string, object?>
        {
            [nameof(Input.Value)] = "hello",
        });

        // 核心 bundle 只导入一次，且为 components-core.js 带版本查询串。
        var import = Assert.Single(js.Invocations, i => i.Identifier == "import");
        Assert.Contains("./_content/AtomBlazor/js/components-core.js", import.Args![0]!.ToString()!, StringComparison.Ordinal);

        var initialize = Assert.Single(js.Invocations, i => i.Identifier == "textInput.initialize");
        Assert.Equal(4, initialize.Args!.Length);
        Assert.IsType<ElementReference>(initialize.Args[0]);
        Assert.IsType<DotNetObjectReference<Input>>(initialize.Args[1]);
        Assert.Matches("^[0-9a-f]{32}$", (string)initialize.Args[2]!);

        var (mode, debounceMs) = ReadConfig(initialize.Args[3]!);
        Assert.Equal("onchange", mode);
        Assert.Equal(500, debounceMs);
    }

    [Fact]
    public async Task 更换更新时机应派发textInput配置更新()
    {
        var js = new NoopJavaScript();
        var renderer = CreateRenderer(js);

        await MountAsync(renderer, new Dictionary<string, object?>
        {
            [nameof(Input.UpdateTiming)] = UpdateTiming.OnChange,
        });

        await renderer.RerenderRootAsync(0, new Dictionary<string, object?>
        {
            [nameof(Input.UpdateTiming)] = UpdateTiming.Debounced,
            [nameof(Input.DebounceInterval)] = 150,
        });
        await FlushAsync(renderer);

        var update = Assert.Single(js.Invocations, i => i.Identifier == "textInput.updateConfig");
        Assert.Equal(2, update.Args!.Length);

        // 第二参为配置对象，首参须与 initialize 使用的 instanceId 同值。
        var initialize = js.Invocations.First(i => i.Identifier == "textInput.initialize");
        Assert.Equal(initialize.Args![2], update.Args[0]);

        var (mode, debounceMs) = ReadConfig(update.Args[1]!);
        Assert.Equal("debounced", mode);
        Assert.Equal(150, debounceMs);
    }

    [Fact]
    public async Task JS回调入口应回填值并把空白规整为null()
    {
        var reports = new List<string?>();
        var changed = EventCallback.Factory.Create<string?>(new object(), v => reports.Add(v));
        var renderer = CreateRenderer();

        var input = await MountAsync(renderer, new Dictionary<string, object?>
        {
            [nameof(Input.ValueChanged)] = changed,
        });

        await renderer.Dispatcher.InvokeAsync(() => input.JsOnInput("abc"));
        await FlushAsync(renderer);

        Assert.Equal("abc", input.Value);
        Assert.Contains("value=\"abc\"", renderer.Markup());

        await renderer.Dispatcher.InvokeAsync(() => input.JsOnChange("   "));
        await FlushAsync(renderer);

        // 空白输入规整为 null：回调上报 null 且标记不再输出 value。
        Assert.Equal(["abc", null], reports);
        Assert.Null(input.Value);
        Assert.DoesNotContain("value=", renderer.Markup());
    }

    [Fact]
    public async Task 释放应派发textInput销毁并回收浏览器端状态()
    {
        var js = new NoopJavaScript();
        var renderer = CreateRenderer(js);

        var input = await MountAsync(renderer, new Dictionary<string, object?>
        {
            [nameof(Input.Value)] = "hello",
        });

        await renderer.Dispatcher.InvokeAsync(async () => await input.DisposeAsync());

        var dispose = Assert.Single(js.Invocations, i => i.Identifier == "textInput.dispose");
        var initialize = js.Invocations.First(i => i.Identifier == "textInput.initialize");
        Assert.Equal(initialize.Args![2], dispose.Args![0]);
    }
}
