using AtomBlazor.Primitives;
using System.Text.RegularExpressions;
using AtomBlazor;
using AtomBlazor.Tests.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Xunit;

namespace AtomBlazor.Tests.Components;

/// <summary>
/// Textarea 渲染与字符计数测试：默认渲染快照（Scrub 随机 id）、ShowCharacterCount 与
/// MaxLength 组合的 "n/max" 计数快照、无 MaxLength 时仅显示当前长度、maxlength 属性输出、
/// 首渲染派发 textInput.initialize。
/// </summary>
public class TextareaTests
{
    /// <summary>
    /// 把 EffectiveId 中的随机短 GUID 归一化为占位符，使快照可确定。
    /// </summary>
    private static string Scrub(string markup) =>
        Regex.Replace(markup, "textarea-[0-9a-f]{8}", "textarea-<id>");

    private static ComponentTestRenderer CreateRenderer(NoopJavaScript? js = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(js ?? new NoopJavaScript());
        return new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance);
    }

    /// <summary>让调度器空转两轮，冲刷 OnAfterRender 队列。</summary>
    private static async Task FlushAsync(ComponentTestRenderer renderer)
    {
        for (var i = 0; i < 2; i++)
        {
            await renderer.Dispatcher.InvokeAsync(() => { });
        }
    }

    private static async Task MountAsync(ComponentTestRenderer renderer, Dictionary<string, object?> parameters)
    {
        await renderer.Dispatcher.InvokeAsync(() => renderer.MountAsync<Textarea>(parameters));
        await FlushAsync(renderer);
    }

    [Fact]
    public async Task 默认渲染快照应与基线一致()
    {
        var renderer = CreateRenderer();

        await MountAsync(renderer, new Dictionary<string, object?>
        {
            [nameof(Textarea.Value)] = "多行文本内容",
            [nameof(Textarea.Placeholder)] = "请输入描述",
        });

        await Verify(Scrub(renderer.Markup()));
    }

    [Fact]
    public async Task 显示字符计数且设有上限时快照应输出n斜max()
    {
        var renderer = CreateRenderer();

        await MountAsync(renderer, new Dictionary<string, object?>
        {
            [nameof(Textarea.Value)] = "hello",
            [nameof(Textarea.MaxLength)] = 100,
            [nameof(Textarea.ShowCharacterCount)] = true,
        });

        await Verify(Scrub(renderer.Markup()));
    }

    [Fact]
    public async Task 字符计数应随值长度更新()
    {
        var renderer = CreateRenderer();

        await MountAsync(renderer, new Dictionary<string, object?>
        {
            [nameof(Textarea.Value)] = "hello",
            [nameof(Textarea.MaxLength)] = 10,
            [nameof(Textarea.ShowCharacterCount)] = true,
        });

        Assert.Contains("data-character-count>5/10<", renderer.Markup());

        await renderer.RerenderRootAsync(0, new Dictionary<string, object?>
        {
            [nameof(Textarea.Value)] = "hello world!",
        });
        await FlushAsync(renderer);

        Assert.Contains("data-character-count>12/10<", renderer.Markup());
    }

    [Fact]
    public async Task 无MaxLength时字符计数只显示当前长度()
    {
        var renderer = CreateRenderer();

        await MountAsync(renderer, new Dictionary<string, object?>
        {
            [nameof(Textarea.Value)] = "hello",
            [nameof(Textarea.ShowCharacterCount)] = true,
        });

        var markup = renderer.Markup();
        Assert.Contains("data-character-count>5</p>", markup);
        // 未设置上限时不输出 maxlength 属性，也不出现 "n/max" 形态。
        Assert.DoesNotContain("maxlength=", markup);
        Assert.DoesNotContain("/5<", markup);
    }

    [Fact]
    public async Task MaxLength应输出maxlength属性()
    {
        var renderer = CreateRenderer();

        await MountAsync(renderer, new Dictionary<string, object?>
        {
            [nameof(Textarea.MaxLength)] = 50,
        });

        Assert.Contains("maxlength=\"50\"", renderer.Markup());
    }

    [Fact]
    public async Task 首渲染应派发textInput初始化()
    {
        var js = new NoopJavaScript();
        var renderer = CreateRenderer(js);

        await MountAsync(renderer, new Dictionary<string, object?>
        {
            [nameof(Textarea.Value)] = "hello",
        });

        Assert.Contains(js.Invocations, i => i.Identifier == "import");
        var initialize = Assert.Single(js.Invocations, i => i.Identifier == "textInput.initialize");
        Assert.Equal(4, initialize.Args!.Length);
        Assert.Matches("^[0-9a-f]{32}$", (string)initialize.Args[2]!);
    }
}
