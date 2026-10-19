using AtomBlazor.Primitives;
using AtomBlazor;
using AtomBlazor.Tests.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Xunit;

namespace AtomBlazor.Tests.Components;

/// <summary>
/// Highlighter 渲染测试：匹配片段的 mark 包裹、大小写敏感开关、全词匹配、
/// 多词与重叠区间合并、空文本与无命中的退化路径、自定义 mark 类。
/// 文本永远按 Text 帧渲染而非标记，注入用例如源所述保持字面。
/// </summary>
public class HighlighterTests
{
    private static ComponentTestRenderer CreateRenderer()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(new NoopJavaScript());
        return new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance);
    }

    private static Dictionary<string, object?> Params(
        string? text,
        string? query = null,
        IReadOnlyList<string>? queries = null,
        bool caseSensitive = false,
        bool wholeWord = false,
        string? markClass = null)
    {
        var parameters = new Dictionary<string, object?> { [nameof(Highlighter.Text)] = text };
        if (query is not null)
        {
            parameters[nameof(Highlighter.Query)] = query;
        }
        if (queries is not null)
        {
            parameters[nameof(Highlighter.Queries)] = queries;
        }
        if (caseSensitive)
        {
            parameters[nameof(Highlighter.CaseSensitive)] = true;
        }
        if (wholeWord)
        {
            parameters[nameof(Highlighter.WholeWord)] = true;
        }
        if (markClass is not null)
        {
            parameters[nameof(Highlighter.MarkClass)] = markClass;
        }
        return parameters;
    }

    private static int MarkCount(string markup)
    {
        var count = 0;
        for (var index = markup.IndexOf("<mark", StringComparison.Ordinal);
             index >= 0;
             index = markup.IndexOf("<mark", index + 1, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }

    [Fact]
    public async Task 默认渲染快照应与基线一致()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Highlighter>(Params("A grid of gridlines in the grid", query: "grid"));

        await Verify(renderer.Markup());
    }

    [Fact]
    public async Task 空文本应渲染为空()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Highlighter>(Params(string.Empty));

        Assert.Equal(string.Empty, renderer.Markup());
    }

    [Fact]
    public async Task 无搜索词时应只输出纯文本不产生mark()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Highlighter>(Params("Hello world"));

        var markup = renderer.Markup();
        Assert.DoesNotContain("<mark", markup);
        Assert.Equal("Hello world", markup);
    }

    [Fact]
    public async Task 空串搜索词应被过滤不产生mark()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Highlighter>(Params("Hello", query: ""));

        Assert.DoesNotContain("<mark", renderer.Markup());
    }

    [Fact]
    public async Task 单命中应包裹mark且前后文本保持原样()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Highlighter>(Params("Dashboard Grid widgets", query: "Grid"));

        var markup = renderer.Markup();
        Assert.StartsWith("Dashboard <mark", markup);
        Assert.Contains(">Grid</mark> widgets", markup);
        Assert.Contains("atom:bg-primary/20", markup);
        Assert.Equal(1, MarkCount(markup));
    }

    [Fact]
    public async Task 默认匹配应大小写不敏感()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Highlighter>(Params("Grid, grid and GRID", query: "grid"));

        Assert.Equal(3, MarkCount(renderer.Markup()));
    }

    [Fact]
    public async Task 大小写敏感时只命中完全一致的词()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Highlighter>(Params("Grid, grid and GRID", query: "Grid", caseSensitive: true));

        var markup = renderer.Markup();
        Assert.Equal(1, MarkCount(markup));
        Assert.Contains(">Grid</mark>", markup);
    }

    [Fact]
    public async Task 全词匹配应排除单词内部片段()
    {
        var renderer = CreateRenderer();

        // 源的语义是"词只在单词开头才算命中"：Engrid 里的 grid 前是字母，被排除；
        // 独立出现的 grid 才产出 mark。
        await renderer.MountAsync<Highlighter>(Params("Engrid and grid", query: "grid", wholeWord: true));

        var markup = renderer.Markup();
        Assert.Equal(1, MarkCount(markup));
        Assert.Contains(">grid</mark>", markup);
        Assert.StartsWith("Engrid and <mark", markup);
    }

    [Fact]
    public async Task 非全词模式应命中单词内部片段()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Highlighter>(Params("Engrid and grid", query: "grid"));

        Assert.Equal(2, MarkCount(renderer.Markup()));
    }

    [Fact]
    public async Task 句首即视为词首()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Highlighter>(Params("gridlines and grid", query: "grid", wholeWord: true));

        // 句首位置 0 的 grid 算词首（它同时是 gridlines 的前缀），加尾部独立 grid 共两个 mark。
        Assert.Equal(2, MarkCount(renderer.Markup()));
    }

    [Fact]
    public async Task Queries优先于Query且多词各自命中()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Highlighter>(Params(
            "The data grid renders a column per field, and the dashboard grid places widgets.",
            query: "ignored",
            queries: ["grid", "column"]));

        var markup = renderer.Markup();
        Assert.Equal(3, MarkCount(markup));
        Assert.DoesNotContain("ignored", markup);
    }

    [Fact]
    public async Task 重叠匹配应合并为一段而非嵌套mark()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Highlighter>(Params(
            "Data grid columns",
            queries: ["grid col", "columns"]));

        var markup = renderer.Markup();
        Assert.Equal(1, MarkCount(markup));
        Assert.Contains(">grid columns</mark>", markup);
    }

    [Fact]
    public async Task 相邻区间应扩展合并右端点()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Highlighter>(Params("abcdef", queries: ["abcd", "cdef"]));

        var markup = renderer.Markup();
        Assert.Equal(1, MarkCount(markup));
        Assert.Contains(">abcdef</mark>", markup);
    }

    [Fact]
    public async Task 无命中时应输出整段原文()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Highlighter>(Params("Hello world", query: "zzz"));

        Assert.Equal("Hello world", renderer.Markup());
    }

    [Fact]
    public async Task 搜索词只按文本渲染不产生元素()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Highlighter>(Params("Angle brackets like <b>this</b> stay literal", query: "<b>"));

        var markup = renderer.Markup();
        Assert.Equal(1, MarkCount(markup));
        // 命中的 "<b>" 走 Text 帧：它被 mark 包裹成字面文本，而没有被渲染成真的 b 元素。
        Assert.Contains("Angle brackets like <mark", markup);
        Assert.Contains("</mark>this</b> stay literal", markup);
    }

    [Fact]
    public async Task 自定义MarkClass应并入mark类串并消解背景冲突()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Highlighter>(Params("A grid", query: "grid", markClass: "atom:bg-amber-200"));

        var markup = renderer.Markup();
        Assert.Contains("atom:bg-amber-200", markup);
        // cn 的 Tailwind 冲突消解让调用方的背景类覆盖内置高亮底色（与 Badge 自定义类行为一致）。
        Assert.DoesNotContain("atom:bg-primary/20", markup);
        Assert.Contains("atom:rounded-sm", markup);
    }
}
