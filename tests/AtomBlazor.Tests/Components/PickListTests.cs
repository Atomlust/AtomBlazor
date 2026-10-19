using AtomBlazor.Primitives;
using System.Text.RegularExpressions;
using AtomBlazor;
using AtomBlazor.Primitives.Extensions;
using AtomBlazor.Tests.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Xunit;

namespace AtomBlazor.Tests.Components;

/// <summary>
/// PickList 组件层行为测试：两栏分派（源=未命中 Values 的 Options 按 Options 序，目标=命中项
/// 按 Values/moved 序）、无匹配选项的值被丢弃、四个移动按钮的本地化 aria-label 与禁用条件
/// （无选择禁用 MoveSelected/RemoveSelected，目标空禁用 RemoveAll）、ShowMoveAll=false 只剩两个
/// 按钮、经按钮 OnClick 事件沿触发移动并断言 OnMove（PickListMoveEventArgs：Values 与 ToTarget）
/// 与 ValuesChanged 新集合、搜索词收窄后 MoveAll 只迁移可见项、Disabled 传播两栏列表并令移动失效。
/// JS 经 NoopJavaScript 静默（仿 /c 口径）；按钮触达沿用 MultiSelectTests 的 onclick
/// occurrence 先例，选中标记经根重渲染同步（照 CheckboxTests 受控沿先例）。
/// </summary>
public class PickListTests
{
    private static readonly SelectOption<string>[] defaultOptions =
    [
        new("alpha", "Alpha"),
        new("beta", "Beta"),
        new("gamma", "Gamma"),
    ];

    private static ComponentTestRenderer CreateRenderer()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(new NoopJavaScript());
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddAtomBlazorPrimitives();
        services.AddAtomBlazorComponents();
        return new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance);
    }

    /// <summary>排空渲染队列，确保事件沿触发的后续批已落定。</summary>
    private static Task SettleAsync(ComponentTestRenderer renderer) =>
        renderer.Dispatcher.InvokeAsync(() => Task.CompletedTask);

    private static Task ClickAsync(ComponentTestRenderer renderer, int occurrence = 0) =>
        renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs(), occurrence));

    private static Task InputAsync(ComponentTestRenderer renderer, string value, int occurrence = 0) =>
        renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("oninput", new ChangeEventArgs { Value = value }, occurrence));

    /// <summary>
    /// 挂载 PickList&lt;string&gt;：ValuesChanged 记录上报并镜像回参数字典（模拟 @bind-Values
    /// 回填，供显式重渲染沿用），OnMove 记录事件参量。
    /// </summary>
    private static async Task<(
        ComponentTestRenderer Renderer,
        Dictionary<string, object?> Dict,
        List<IEnumerable<string>?> ValueReports,
        List<PickListMoveEventArgs<string>> Moves)> MountAsync(
        IEnumerable<string>? values = null,
        bool showSearch = false,
        bool showSelectAll = false,
        bool showMoveAll = true,
        bool disabled = false)
    {
        var renderer = CreateRenderer();
        var reports = new List<IEnumerable<string>?>();
        var moves = new List<PickListMoveEventArgs<string>>();
        var dict = new Dictionary<string, object?>
        {
            [nameof(PickList<string>.Options)] = defaultOptions,
            [nameof(PickList<string>.Values)] = values,
            [nameof(PickList<string>.ShowSearch)] = showSearch,
            [nameof(PickList<string>.ShowSelectAll)] = showSelectAll,
            [nameof(PickList<string>.ShowMoveAll)] = showMoveAll,
            [nameof(PickList<string>.Disabled)] = disabled,
        };

        // 回调在字典建好后挂接：ValuesChanged 记录上报并镜像回字典（模拟 @bind-Values 回填）
        dict[nameof(PickList<string>.ValuesChanged)] =
            EventCallback.Factory.Create<IEnumerable<string>?>(new object(), v =>
            {
                reports.Add(v);
                dict[nameof(PickList<string>.Values)] = v;
            });
        dict[nameof(PickList<string>.OnMove)] =
            EventCallback.Factory.Create<PickListMoveEventArgs<string>>(new object(), e => moves.Add(e));

        await renderer.MountAsync<PickList<string>>(dict);
        await SettleAsync(renderer);
        return (renderer, dict, reports, moves);
    }

    private static Task RerenderRootAsync(
        ComponentTestRenderer renderer, Dictionary<string, object?> dict) =>
        renderer.RerenderRootAsync(0, dict);

    private static int Count(string haystack, string needle) => haystack.Split(needle).Length - 1;

    // ------------------------------------------------------------------
    // 两栏分派
    // ------------------------------------------------------------------

    [Fact]
    public async Task 两栏应分派未选与已选且目标栏保持Values序()
    {
        var (renderer, _, _, _) = await MountAsync(values: ["gamma", "alpha"]);
        var markup = renderer.Markup();

        // 两栏各一个 listbox，标签为本地化缺省文案
        Assert.Equal(2, Count(markup, "role=\"listbox\""));
        Assert.Contains("Available", markup); // Localizer["PickList.Available"]
        Assert.Contains("Selected", markup);  // Localizer["PickList.Selected"]

        // DFS 序：源栏(Beta) → 按钮列 → 目标栏(Gamma、Alpha 按 Values 序)
        var beta = markup.IndexOf("Beta", StringComparison.Ordinal);
        var gamma = markup.IndexOf("Gamma", StringComparison.Ordinal);
        var alpha = markup.IndexOf("Alpha", StringComparison.Ordinal);
        Assert.True(beta < gamma, "未选的 Beta 应在源栏（先渲染）");
        Assert.True(gamma < alpha, "目标栏应按 Values 序：gamma 先于 alpha");
        Assert.Equal(3, Count(markup, "role=\"option\""));
    }

    [Fact]
    public async Task 无匹配选项的Values项应被丢弃不呈现()
    {
        // zzz 不在 Options 中：不可见亦不可移动，直接丢弃
        var (renderer, _, _, _) = await MountAsync(values: ["zzz", "beta"]);
        var markup = renderer.Markup();

        Assert.DoesNotContain("zzz", markup);
        Assert.Equal(3, Count(markup, "role=\"option\""));
        // 源栏剩 Alpha、Gamma（Options 序），目标栏为 Beta
        Assert.True(
            markup.IndexOf("Gamma", StringComparison.Ordinal)
            < markup.IndexOf("Beta", StringComparison.Ordinal));
    }

    // ------------------------------------------------------------------
    // 按钮面
    // ------------------------------------------------------------------

    [Fact]
    public async Task 四个移动按钮应携带本地化无障碍标签且无选择时禁用两选中按钮()
    {
        // Values=["beta"] 使目标栏非空：两个 MoveAll 可用，而两栏均无选中
        var (renderer, _, _, _) = await MountAsync(values: ["beta"], showSelectAll: true);
        var markup = renderer.Markup();

        Assert.Contains("aria-label=\"Move selected\"", markup);   // PickList.MoveSelectedToTarget
        Assert.Contains("aria-label=\"Move all\"", markup);        // PickList.MoveAllToTarget
        Assert.Contains("aria-label=\"Remove selected\"", markup); // PickList.MoveSelectedToSource
        Assert.Contains("aria-label=\"Remove all\"", markup);      // PickList.MoveAllToSource

        // 禁用面：恰好两个「按选中移动」按钮 disabled（全选复选框写 aria-disabled="false"，
        // 不计入）；两个「移动全部」按钮因两栏皆有可见项而可用
        Assert.Equal(2, Count(markup, "aria-disabled=\"true\""));
        Assert.Equal(6, Count(markup, "<button")); // 4 移动钮 + 2 栏全选复选框
    }

    [Fact]
    public async Task ShowMoveAll关闭后应只剩两个选中移动按钮()
    {
        var (renderer, _, _, _) = await MountAsync(values: ["beta"], showMoveAll: false);
        var markup = renderer.Markup();

        Assert.Equal(2, Count(markup, "<button"));
        Assert.DoesNotContain("Move all", markup);
        Assert.DoesNotContain("Remove all", markup);
        Assert.Contains("aria-label=\"Move selected\"", markup);
        Assert.Contains("aria-label=\"Remove selected\"", markup);
    }

    // ------------------------------------------------------------------
    // 移动沿
    // ------------------------------------------------------------------

    [Fact]
    public async Task 源栏选中后经MoveSelected应迁入目标栏并上报ValuesChanged与OnMove()
    {
        var (renderer, dict, reports, moves) = await MountAsync();

        // ShowSelectAll=false 时 onclick 次序：源项 Alpha(0)、Beta(1)、Gamma(2)，
        // 其后按钮列 MoveSelected→(3)、MoveAll→Target(4)、MoveSelected→Source(5)、MoveAll→Source(6)
        await ClickAsync(renderer, occurrence: 0); // 勾选 Alpha（写入 sourceSelection）
        await SettleAsync(renderer);

        // PickList 不随子组件事件自渲染：显式重渲染根，使按钮禁用面反映选中态
        await RerenderRootAsync(renderer, dict);
        await SettleAsync(renderer);
        Assert.Equal(2, Count(renderer.Markup(), "aria-disabled=\"true\"")); // 仅剩两个向源按钮禁用

        await ClickAsync(renderer, occurrence: 3); // MoveSelectedToTarget
        await SettleAsync(renderer);

        var moved = Assert.Single(reports);
        Assert.Equal(["alpha"], moved);
        var move = Assert.Single(moves);
        Assert.True(move.ToTarget);
        Assert.Equal(["alpha"], move.Values);

        // 回填 Values 后重渲染：Alpha 落目标栏（在两个向源按钮之后）
        await RerenderRootAsync(renderer, dict);
        var markup = renderer.Markup();
        Assert.True(
            markup.IndexOf("Gamma", StringComparison.Ordinal)
            < markup.IndexOf("Alpha", StringComparison.Ordinal),
            "Alpha 应已迁入后渲染的目标栏");
    }

    [Fact]
    public async Task MoveAll应迁入全部可见源项并上报ToTarget真()
    {
        var (renderer, _, reports, moves) = await MountAsync();

        // 按钮列次序见 MountAsync 注释：MoveAll→Target 为 occurrence 4
        await ClickAsync(renderer, occurrence: 4);
        await SettleAsync(renderer);

        var all = Assert.Single(reports);
        Assert.Equal(["alpha", "beta", "gamma"], all);
        var move = Assert.Single(moves);
        Assert.True(move.ToTarget);
        Assert.Equal(3, move.Values.Count);
    }

    [Fact]
    public async Task 搜索词生效后MoveAll应只迁移可见项()
    {
        var (renderer, dict, reports, moves) = await MountAsync(showSearch: true);

        // oninput 次序：源栏搜索框(0)、目标栏搜索框(1)；键入后源栏仅余 Alpha
        await InputAsync(renderer, "al", occurrence: 0);
        await SettleAsync(renderer);

        // 重渲染根使两栏参数面同步：此后 onclick 次序为源项 Alpha(0)、
        // 按钮 MoveSelected→Target(1)、MoveAll→Target(2)、MoveSelected→Source(3)、MoveAll→Source(4)
        await RerenderRootAsync(renderer, dict);
        await SettleAsync(renderer);
        await ClickAsync(renderer, occurrence: 2);
        await SettleAsync(renderer);

        var only = Assert.Single(reports);
        Assert.Equal(["alpha"], only);
        var move = Assert.Single(moves);
        Assert.Equal(["alpha"], move.Values);
        Assert.True(move.ToTarget);
    }

    [Fact]
    public async Task 目标栏选中后移回应上报ToTarget假且清空回填null()
    {
        var (renderer, dict, reports, moves) = await MountAsync(values: ["beta"]);

        // onclick 次序：源项 Alpha(0)、Gamma(1)，按钮(2..5)，目标项 Beta(6)
        await ClickAsync(renderer, occurrence: 6); // 勾选目标栏 Beta
        await SettleAsync(renderer);
        await RerenderRootAsync(renderer, dict);
        await SettleAsync(renderer);

        await ClickAsync(renderer, occurrence: 4); // MoveSelectedToSource
        await SettleAsync(renderer);

        var cleared = Assert.Single(reports);
        Assert.Null(cleared);
        var move = Assert.Single(moves);
        Assert.False(move.ToTarget);
        Assert.Equal(["beta"], move.Values);

        // 回填后重渲染：目标栏空态文案
        await RerenderRootAsync(renderer, dict);
        Assert.Contains("No options", renderer.Markup());
    }

    [Fact]
    public async Task 整表禁用应传播两栏列表并令移动失效()
    {
        var (renderer, _, reports, moves) = await MountAsync(disabled: true);

        var markup = renderer.Markup();
        Assert.Equal(2, Regex.Count(markup, "<ul[^>]*aria-disabled=\"true\""));
        Assert.Equal(2, Regex.Count(markup, "<ul[^>]*tabindex=\"-1\""));

        // MoveAll→Target（occurrence 4）：按钮已 disabled，点击沿被 Button.HandleClick 挡下
        await ClickAsync(renderer, occurrence: 4);
        await SettleAsync(renderer);
        Assert.Empty(reports);
        Assert.Empty(moves);
    }
}
