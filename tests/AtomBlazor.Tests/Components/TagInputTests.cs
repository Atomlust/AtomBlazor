using AtomBlazor.Primitives;
using System.Text.RegularExpressions;
using AtomBlazor;
using AtomBlazor.Tests.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Xunit;

namespace AtomBlazor.Tests.Components;

/// <summary>
/// TagInput 组件测试：默认/含标签/建议展开快照（Scrub 随机 id）、
/// 触发键提交沿（JsTriggerAdd：键入文本/高亮建议）、Backspace 删尾沿、粘贴切分
/// （逗号/分号/空格分隔符随 AddTrigger 配置）、去重（不区分大小写）、
/// MaxTags/MaxTagLength/Validate 拒绝沿与 OnTagRejected、禁用空操作、
/// 建议导航（Next/Prev 环绕、Close）、Blur 触发提交沿、
/// 移除按钮/全部清除按钮点击沿、tagInput.initialize/dispose interop 载荷、
/// 释放后回调空操作。键盘 preventDefault 与容器点击聚焦沿在浏览器侧 tag-input.js
/// 完成，由 node 侧 tag-input-module 测试覆盖；headless 限制先例同
/// NumericInputTests（不断言纯焦点沿）。
/// </summary>
public class TagInputTests
{
    /// <summary>
    /// 归一化组件自生成的 taginput-{guid:N}（32 位十六进制）随机 id，使快照可确定。
    /// </summary>
    private static readonly string[] twoTags = ["alpha", "beta"];
    private static readonly string[] fruitSuggestions = ["apple", "banana", "apricot"];
    private static readonly string[] appleSuggestions = ["apple", "apricot"];
    private static readonly string[] alphaTag = ["alpha"];
    private static readonly string[] aBTags = ["a", "b"];
    private static readonly string[] upperAppleTag = ["APPLE"];

    private static string Scrub(string markup) =>
        Regex.Replace(markup, "taginput-[0-9a-f]{32}", "taginput-<id>");

    private static (ComponentTestRenderer Renderer, NoopJavaScript Js) CreateRenderer()
    {
        var js = new NoopJavaScript();
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(js);
        services.AddSingleton<ILocalizer>(new DefaultLocalizer());
        return (new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance), js);
    }

    /// <summary>让调度器空转两轮，冲刷 OnAfterRender/StateHasChanged 队列。</summary>
    private static async Task FlushAsync(ComponentTestRenderer renderer)
    {
        for (var i = 0; i < 2; i++)
        {
            await renderer.Dispatcher.InvokeAsync(() => { });
        }
    }

    private static async Task<(ComponentTestRenderer Renderer, NoopJavaScript Js, TagInput Input)> MountAsync(
        Dictionary<string, object?>? parameters = null)
    {
        var (renderer, js) = CreateRenderer();
        var component = await renderer.Dispatcher.InvokeAsync(() =>
            renderer.MountTypeAsync(typeof(TagInput), parameters ?? []));
        await FlushAsync(renderer);
        return (renderer, js, (TagInput)component);
    }

    /// <summary>模拟键入：派发 oninput 更新输入文本。</summary>
    private static Task TypeAsync(ComponentTestRenderer renderer, string text) =>
        renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("oninput", new ChangeEventArgs { Value = text }));

    /// <summary>JS 回调沿：直接在 Dispatcher 上调用 [JSInvokable] 入口并等待落定。</summary>
    private static Task InvokeAsync(ComponentTestRenderer renderer, TagInput input, Func<TagInput, Task> callback) =>
        renderer.Dispatcher.InvokeAsync(() => callback(input));

    private static (string Identifier, object?[]? Args) Single(NoopJavaScript js, string identifier) =>
        Assert.Single(js.Invocations, i => i.Identifier == identifier);

    // ------------------------------------------------------------------
    // 快照
    // ------------------------------------------------------------------

    [Fact]
    public async Task 默认渲染快照应与基线一致()
    {
        var (renderer, _, _) = await MountAsync();
        await Verify(Scrub(renderer.Markup()));
    }

    [Fact]
    public async Task 含标签与清除按钮渲染快照应与基线一致()
    {
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(TagInput.Tags)] = twoTags,
            [nameof(TagInput.Clearable)] = true,
        });
        await Verify(Scrub(renderer.Markup()));
    }

    [Fact]
    public async Task 建议展开渲染快照应与基线一致()
    {
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(TagInput.Suggestions)] = appleSuggestions,
        });

        await TypeAsync(renderer, "ap");
        await FlushAsync(renderer);

        await Verify(Scrub(renderer.Markup()));
    }

    // ------------------------------------------------------------------
    // 结构与 aria
    // ------------------------------------------------------------------

    [Fact]
    public async Task 容器与输入框应携带aria标签与占位符()
    {
        // 默认：容器无标签，输入框回退为 "Add tag"
        var (renderer, _, _) = await MountAsync();

        var markup = renderer.Markup();
        Assert.Contains("data-tag-input-container", markup);
        Assert.Contains("aria-label=\"Add tag\"", markup);
        Assert.Contains("placeholder=\"Add tag...\"", markup);

        // 设置 AriaLabel 后容器与输入框共用同一标签
        var (renderer2, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(TagInput.AriaLabel)] = "技能标签",
        });
        Assert.Equal(2, Regex.Count(renderer2.Markup(), "aria-label=\"技能标签\""));
    }

    [Fact]
    public async Task 标签徽章应携带移除按钮与本地化标签()
    {
        var (renderer, _, _) = (await MountAsync(new Dictionary<string, object?>
        {
            [nameof(TagInput.Tags)] = alphaTag,
        }));

        var markup = renderer.Markup();
        Assert.Contains("alpha", markup);
        Assert.Contains("aria-label=\"Remove alpha\"", markup);
    }

    [Fact]
    public async Task 禁用态应携带aria禁用且无移除按钮()
    {
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(TagInput.Tags)] = alphaTag,
            [nameof(TagInput.Disabled)] = true,
        });

        var markup = renderer.Markup();
        Assert.Contains("aria-disabled=\"true\"", markup);
        Assert.DoesNotContain("aria-label=\"Remove alpha\"", markup);
        Assert.Contains("disabled", markup);
    }

    // ------------------------------------------------------------------
    // 触发键提交沿
    // ------------------------------------------------------------------

    [Fact]
    public async Task 触发键应把键入文本提交为标签()
    {
        var reports = new List<IReadOnlyList<string>?>();
        var tagsChanged = EventCallback.Factory.Create<IReadOnlyList<string>?>(new object(), v => reports.Add(v));

        var (renderer, _, input) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(TagInput.TagsChanged)] = tagsChanged,
        });

        await TypeAsync(renderer, "  alpha  ");
        await InvokeAsync(renderer, input, static i => i.JsTriggerAdd());
        await FlushAsync(renderer);

        Assert.Equal([alphaTag], reports);
        Assert.Equal(["alpha"], input.Tags);
        // 提交后输入文本清空
        Assert.Contains("value=\"\"", renderer.Markup());
    }

    [Fact]
    public async Task 高亮中的建议应优先于键入文本被提交()
    {
        var (renderer, _, input) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(TagInput.Suggestions)] = appleSuggestions,
        });

        await TypeAsync(renderer, "ap");
        await FlushAsync(renderer);

        // 下移两次：-1 → 0（apple）→ 1（apricot）
        await InvokeAsync(renderer, input, static i => { i.JsSuggestionNext(); return Task.CompletedTask; });
        await InvokeAsync(renderer, input, static i => { i.JsSuggestionNext(); return Task.CompletedTask; });
        await InvokeAsync(renderer, input, static i => i.JsTriggerAdd());
        await FlushAsync(renderer);

        Assert.Equal(["apricot"], input.Tags);
    }

    [Fact]
    public async Task Backspace应在空输入框上移除最后一个标签()
    {
        var reports = new List<IReadOnlyList<string>?>();
        var tagsChanged = EventCallback.Factory.Create<IReadOnlyList<string>?>(new object(), v => reports.Add(v));

        var (renderer, _, input) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(TagInput.Tags)] = twoTags,
            [nameof(TagInput.TagsChanged)] = tagsChanged,
        });

        await InvokeAsync(renderer, input, static i => i.JsBackspace());
        await FlushAsync(renderer);

        Assert.Equal([alphaTag], reports);
        Assert.Equal(["alpha"], input.Tags);
    }

    [Fact]
    public async Task 无标签时Backspace应为空操作()
    {
        var (renderer, _, input) = await MountAsync();
        await InvokeAsync(renderer, input, static i => i.JsBackspace());
        Assert.Null(input.Tags);
    }

    // ------------------------------------------------------------------
    // 粘贴切分
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(TagInputTrigger.Enter | TagInputTrigger.Comma, "a,b,c", new string[] { "a", "b", "c" })]
    [InlineData(TagInputTrigger.Enter | TagInputTrigger.Semicolon, "a;b", new string[] { "a", "b" })]
    [InlineData(TagInputTrigger.Enter | TagInputTrigger.Space, "a b  c", new string[] { "a", "b", "c" })]
    [InlineData(TagInputTrigger.Enter, "a,b", new string[] { "a,b" })]
    public async Task 粘贴文本应按已配置分隔符切分提交(TagInputTrigger triggers, string pasted, string[] expected)
    {
        var (renderer, _, input) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(TagInput.AddTrigger)] = triggers,
        });

        await InvokeAsync(renderer, input, i => i.JsPasteText(pasted));
        await FlushAsync(renderer);

        Assert.Equal(expected, input.Tags);
    }

    // ------------------------------------------------------------------
    // 拒绝沿
    // ------------------------------------------------------------------

    [Fact]
    public async Task 重复标签应被拒绝且不区分大小写()
    {
        var rejected = new List<string>();
        var onRejected = EventCallback.Factory.Create<string>(new object(), v => rejected.Add(v));

        var (renderer, _, input) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(TagInput.Tags)] = alphaTag,
            [nameof(TagInput.OnTagRejected)] = onRejected,
        });

        await TypeAsync(renderer, "ALPHA");
        await InvokeAsync(renderer, input, static i => i.JsTriggerAdd());
        await FlushAsync(renderer);

        Assert.Equal(["ALPHA"], rejected);
        Assert.Equal(["alpha"], input.Tags);
    }

    [Fact]
    public async Task 达到MaxTags与MaxTagLength时应拒绝()
    {
        var rejected = new List<string>();
        var onRejected = EventCallback.Factory.Create<string>(new object(), v => rejected.Add(v));

        var (renderer, _, input) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(TagInput.Tags)] = aBTags,
            [nameof(TagInput.MaxTags)] = 2,
            [nameof(TagInput.MaxTagLength)] = 3,
            [nameof(TagInput.OnTagRejected)] = onRejected,
        });

        await TypeAsync(renderer, "c");
        await InvokeAsync(renderer, input, static i => i.JsTriggerAdd());
        await TypeAsync(renderer, "toolong");
        await InvokeAsync(renderer, input, static i => i.JsTriggerAdd());
        await FlushAsync(renderer);

        Assert.Equal(["c", "toolong"], rejected);
        Assert.Equal(["a", "b"], input.Tags);
    }

    [Fact]
    public async Task 自定义校验失败时应拒绝()
    {
        var rejected = new List<string>();
        var onRejected = EventCallback.Factory.Create<string>(new object(), v => rejected.Add(v));

        var (renderer, _, input) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(TagInput.Validate)] = (Func<string, bool>)(t => t.StartsWith('v')),
            [nameof(TagInput.OnTagRejected)] = onRejected,
        });

        await TypeAsync(renderer, "invalid");
        await InvokeAsync(renderer, input, static i => i.JsTriggerAdd());
        await TypeAsync(renderer, "valid");
        await InvokeAsync(renderer, input, static i => i.JsTriggerAdd());
        await FlushAsync(renderer);

        Assert.Equal(["invalid"], rejected);
        Assert.Equal(["valid"], input.Tags);
    }

    [Fact]
    public async Task 禁用时JS回调应为空操作()
    {
        var (renderer, _, input) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(TagInput.Disabled)] = true,
        });

        await TypeAsync(renderer, "alpha");
        await InvokeAsync(renderer, input, static i => i.JsTriggerAdd());
        await InvokeAsync(renderer, input, i => i.JsPasteText("a,b"));
        await InvokeAsync(renderer, input, static i => i.JsBackspace());
        await FlushAsync(renderer);

        Assert.Null(input.Tags);
    }

    // ------------------------------------------------------------------
    // 建议列表
    // ------------------------------------------------------------------

    [Fact]
    public async Task 键入应过滤静态建议并展开下拉()
    {
        var (renderer, _, input) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(TagInput.Suggestions)] = fruitSuggestions,
        });

        await TypeAsync(renderer, "ap");
        await FlushAsync(renderer);

        var markup = renderer.Markup();
        Assert.Contains("role=\"listbox\"", markup);
        Assert.Contains("role=\"option\"", markup);
        Assert.Contains("apple", markup);
        Assert.Contains("apricot", markup);
        Assert.DoesNotContain("banana", markup);
        // aria 沿：展开态携带 expanded/controls
        Assert.Contains("aria-expanded=\"true\"", markup);
        Assert.Contains("aria-autocomplete=\"list\"", markup);
    }

    [Fact]
    public async Task 建议导航应环绕且aria选中态跟随()
    {
        var (renderer, _, input) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(TagInput.Suggestions)] = appleSuggestions,
        });

        await TypeAsync(renderer, "ap");
        await FlushAsync(renderer);

        await InvokeAsync(renderer, input, static i => { i.JsSuggestionNext(); return Task.CompletedTask; });
        await FlushAsync(renderer);
        Assert.Contains("aria-selected=\"true\"", renderer.Markup());
        Assert.Contains("aria-activedescendant=", renderer.Markup());

        // 从第 0 条上移环绕到末条
        await InvokeAsync(renderer, input, static i => { i.JsSuggestionPrev(); return Task.CompletedTask; });
        await InvokeAsync(renderer, input, static i => { i.JsSuggestionPrev(); return Task.CompletedTask; });
        await FlushAsync(renderer);
        Assert.Contains("aria-activedescendant=", renderer.Markup());

        // Escape 关闭后下拉消失
        await InvokeAsync(renderer, input, static i => { i.JsSuggestionClose(); return Task.CompletedTask; });
        await FlushAsync(renderer);
        Assert.DoesNotContain("role=\"listbox\"", renderer.Markup());
    }

    [Fact]
    public async Task 选中建议应清空建议列表并提交该建议()
    {
        var (renderer, _, input) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(TagInput.Suggestions)] = appleSuggestions,
        });

        await TypeAsync(renderer, "ap");
        await FlushAsync(renderer);

        // mousedown 在建议项上（occurrence 0 = 第一条）
        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onmousedown", new MouseEventArgs(), occurrence: 0));
        await FlushAsync(renderer);

        Assert.Equal(["apple"], input.Tags);
        Assert.DoesNotContain("role=\"listbox\"", renderer.Markup());
    }

    [Fact]
    public async Task 已存在的标签不应重复出现在建议里()
    {
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(TagInput.Suggestions)] = appleSuggestions,
            [nameof(TagInput.Tags)] = upperAppleTag,
        });

        await TypeAsync(renderer, "ap");
        await FlushAsync(renderer);

        var markup = renderer.Markup();
        Assert.Contains("apricot", markup);
        Assert.DoesNotContain(">apple<", markup);
    }

    // ------------------------------------------------------------------
    // Blur 触发提交沿
    // ------------------------------------------------------------------

    [Fact]
    public async Task 配置Blur触发时失焦应提交键入文本()
    {
        var (renderer, _, input) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(TagInput.AddTrigger)] = TagInputTrigger.Enter | TagInputTrigger.Blur,
        });

        await TypeAsync(renderer, "alpha");
        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onblur", new FocusEventArgs()));
        // HandleBlur 延迟 150ms 后提交
        await Task.Delay(400);
        await FlushAsync(renderer);

        Assert.Equal(["alpha"], input.Tags);
    }

    [Fact]
    public async Task 未配置Blur触发时失焦不应提交()
    {
        var (renderer, _, input) = await MountAsync();

        await TypeAsync(renderer, "alpha");
        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onblur", new FocusEventArgs()));
        await Task.Delay(400);
        await FlushAsync(renderer);

        Assert.Null(input.Tags);
    }

    // ------------------------------------------------------------------
    // 移除与清除沿
    // ------------------------------------------------------------------

    [Fact]
    public async Task 移除按钮点击应移除对应标签()
    {
        var reports = new List<IReadOnlyList<string>?>();
        var tagsChanged = EventCallback.Factory.Create<IReadOnlyList<string>?>(new object(), v => reports.Add(v));

        var (renderer, _, input) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(TagInput.Tags)] = twoTags,
            [nameof(TagInput.TagsChanged)] = tagsChanged,
        });

        // 第二个移除按钮（beta）
        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs(), occurrence: 1));
        await FlushAsync(renderer);

        Assert.Equal(["alpha"], input.Tags);
        Assert.Equal([alphaTag], reports);
    }

    [Fact]
    public async Task 清除按钮应清空全部标签()
    {
        var reports = new List<IReadOnlyList<string>?>();
        var tagsChanged = EventCallback.Factory.Create<IReadOnlyList<string>?>(new object(), v => reports.Add(v));

        var (renderer, _, input) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(TagInput.Tags)] = twoTags,
            [nameof(TagInput.Clearable)] = true,
            [nameof(TagInput.TagsChanged)] = tagsChanged,
        });

        // onclick occurrence：两个移除按钮之后是清除按钮
        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs(), occurrence: 2));
        await FlushAsync(renderer);

        Assert.Null(input.Tags);
        Assert.Equal([null], reports);
        Assert.Contains("aria-label=\"Clear all tags\"", renderer.Markup());
    }

    // ------------------------------------------------------------------
    // interop
    // ------------------------------------------------------------------

    [Fact]
    public async Task 挂载应经共享core包初始化并在释放时派发dispose()
    {
        var (renderer, js, input) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(TagInput.AddTrigger)] = TagInputTrigger.Enter | TagInputTrigger.Comma,
        });

        var import = Assert.Single(js.Invocations, i => i.Identifier == "import");
        Assert.Contains("components-core.js", (string)import.Args![0]!);

        var initialize = Single(js, "tagInput.initialize");
        Assert.Equal(5, initialize.Args!.Length);
        Assert.NotNull(initialize.Args![0]);   // 容器元素引用
        Assert.NotNull(initialize.Args![1]);   // 输入框元素引用
        var instanceId = Assert.IsType<string>(initialize.Args![3]);
        Assert.Matches("^taginput-[0-9a-f]{32}$", instanceId);
        var dotNetRef = Assert.IsType<DotNetObjectReference<TagInput>>(initialize.Args[2]!);
        Assert.Same(input, dotNetRef.Value);

        // triggers 以标志位掩码下发
        var config = initialize.Args[4]!;
        Assert.Equal((int)(TagInputTrigger.Enter | TagInputTrigger.Comma), (int)config.GetType().GetProperty("triggers")!.GetValue(config)!);

        await renderer.Dispatcher.InvokeAsync(input.DisposeAsync);
        await FlushAsync(renderer);

        var dispose = Single(js, "tagInput.dispose");
        Assert.Equal(instanceId, dispose.Args![0]);
    }

    [Fact]
    public async Task 释放后JS回调应为空操作()
    {
        var (renderer, _, input) = await MountAsync();
        await TypeAsync(renderer, "alpha");

        await renderer.Dispatcher.InvokeAsync(input.DisposeAsync);
        await InvokeAsync(renderer, input, static i => i.JsTriggerAdd());

        Assert.Null(input.Tags);
    }
}
