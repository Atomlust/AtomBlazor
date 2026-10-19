using AtomBlazor.Primitives;
using System.Text.RegularExpressions;
using AtomBlazor;
using AtomBlazor.Tests.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Xunit;

namespace AtomBlazor.Tests.Components;

/// <summary>
/// 自 Rendering/CalendarGridAlignmentTests.cs（七规则机械改名：
/// →AtomBlazor、→Calendar、bb:→atom:）。
/// <para>
/// Calendar 的日格在表格内居中而非左对齐：七枚日单元各固定 <c>w-9</c>（252px）
/// 而表格随上方月份/年份下拉一样宽（更宽）。左对齐会把差值留成网格右侧的死白。
/// 它只在年份下拉由 80px 拓宽到 100px 后才显形。
/// </para>
/// <para>
/// 断言落在渲染出的行上而非源串，故表头行与周行皆被覆盖，后续新增行也无法漏网。
/// </para>
/// </summary>
public class CalendarGridAlignmentTests
{
    [Fact]
    public async Task 日格每一行都应满宽且居中()
    {
        var markup = await RenderAsync();
        var rows = Regex.Matches(markup, @"<tr class=""(?<cls>[^""]*)""")
            .Select(m => m.Groups["cls"].Value)
            .ToList();

        // 一行日名表头，加上每个显示周各一行。
        Assert.True(rows.Count >= 5, $"期望日历渲染表头行与若干周行；实得 {rows.Count} 行。");

        foreach (var cls in rows)
        {
            Assert.True(
                cls.Contains("atom:justify-center", StringComparison.Ordinal),
                $"某条日历行缺少 atom:justify-center，其日单元会贴左、在表格满宽右侧留下死白。行类：'{cls}'");

            Assert.True(
                cls.Contains("atom:w-full", StringComparison.Ordinal),
                $"某条日历行缺少 atom:w-full，会收缩到单元宽度而无居中余量。行类：'{cls}'");
        }
    }

    [Fact]
    public async Task 日名表头应与日格使用同一单元宽度()
    {
        // 只有表头与日格同为固定宽度时，居中才能让日名对齐各自的列。
        var markup = await RenderAsync();

        var headerCells = Regex.Matches(markup, @"<th scope=""col"" class=""(?<cls>[^""]*)""")
            .Select(m => m.Groups["cls"].Value)
            .ToList();

        Assert.Equal(7, headerCells.Count);
        Assert.All(headerCells, cls => Assert.Contains("atom:w-9", cls, StringComparison.Ordinal));
        Assert.Contains("atom:h-9 atom:w-9", markup, StringComparison.Ordinal);
    }

    private static async Task<string> RenderAsync()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime, NoopJavaScript>();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddAtomBlazorComponents();

        await using var provider = services.BuildServiceProvider();
        await using var renderer = new ComponentTestRenderer(provider, NullLoggerFactory.Instance);

        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            await renderer.MountAsync<Calendar>(new()
            {
                [nameof(Calendar.Selected)] = new DateTime(2026, 9, 19),
                [nameof(Calendar.FirstDayOfWeek)] = DayOfWeek.Sunday,
            });

            return renderer.Markup();
        });
    }
}
