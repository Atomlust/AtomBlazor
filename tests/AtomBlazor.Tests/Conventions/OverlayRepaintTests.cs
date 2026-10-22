using AtomBlazor.Primitives;
using System.Text.RegularExpressions;
using Xunit;

namespace AtomBlazor.Tests.Conventions;

/// <summary>
/// 守护浮层根组件，防止 <see href="https://github.com/ui/ui/issues/437">#437</see> 背后的失同步复现。
/// <para>
/// 每个浮层根组件订阅自己的上下文，并在 <c>HandleContextStateChanged</c> 里协调两件事：
/// 告知使用者打开状态变了，以及重绘自己的子树。这是两个不同的决策，而 bug 把它们当成了一个——
/// 单个 <c>if</c> 同时包住了两者，于是跳过通知时重绘也一并被跳过。
/// </para>
/// <para>
/// 错在两个地方。守卫比较的是 <c>_state.Value</c>，在受控模式下它是 <c>ControlledValue</c>——
/// 只在 <c>OnParametersSet</c> 里刷新，因而只有使用者重新渲染时才刷新。<c>DataGrid</c> 通过
/// <c>ShouldRender</c> 抑制了重新渲染，于是该值变旧、比较得出相等、回调与重绘从此双双被跳过：
/// 筛选按钮继续渲染 <c>aria-expanded="true"</c> 且 <c>pointer-events: none</c>，再也无法点击。
/// </para>
/// <para>
/// 之所以做文本断言而非行为断言，理由见 <see cref="SourceTree"/>：故障是大括号放错了位置。
/// 每一行仍然会执行、逻辑仍然通过类型检查——只有嵌套是错的，而在 primitive 层面
/// 没有一个抑制重渲染的使用者，渲染输出表现不出来。
/// </para>
/// </summary>
public partial class OverlayRepaintTests
{
    /// <summary>
    /// 拥有可控打开状态与上下文订阅的浮层根组件。<c>Tooltip</c> 是刻意缺席的——
    /// 它根本不从这条路径重绘。
    /// </summary>
    private static readonly string[] overlayRoots =
    [
        // 扁平化重构后浮层根统一带 Primitive 后缀
        "PopoverPrimitive",
        "DropdownMenuPrimitive",
        "HoverCardPrimitive",
        "DialogPrimitive",
        "SheetPrimitive",
    ];

    [Fact]
    public void OverlayRootsRepaintWheneverContextStateMoves()
    {
        var offenders = new List<string>();

        foreach (var (component, handler) in Handlers())
        {
            // 剥掉带守卫的块；幸存下来的 StateHasChanged 才是无条件执行的那个。
            var outsideGuard = GuardedBlock().Replace(handler, string.Empty);

            if (!outsideGuard.Contains("StateHasChanged()", StringComparison.Ordinal))
            {
                offenders.Add(component);
            }
        }

        Assert.True(offenders.Count == 0,
            $"{string.Join(", ", offenders)}：StateHasChanged() 只在通知守卫内部执行。"
            + "触发器的 aria-expanded 与 pointer-events、内容是否挂载，全部自上下文状态——"
            + "所以子树必须在该状态变化时重绘，包括使用者的受控值没有变的时候。"
            + "请把调用移出 if。见 #437。");
    }

    [Fact]
    public void OverlayRootsDoNotMeasureChangesAgainstTheControlledValue()
    {
        var offenders = new List<string>();

        foreach (var (component, handler) in Handlers())
        {
            if (handler.Contains("_state.Value != newOpenState", StringComparison.Ordinal))
            {
                offenders.Add(component);
            }
        }

        Assert.True(offenders.Count == 0,
            $"{string.Join(", ", offenders)}：通知守卫拿 _state.Value 做比较。"
            + "受控模式下它就是 ControlledValue，只在 OnParametersSet 里刷新——"
            + "任何抑制自身重渲染的使用者（ShouldRender）都会让它变旧，回调于是悄悄不再触发。"
            + "请改为与上一次实际报告的状态比较。见 #437。");
    }

    [FactWhen(Components = new[] { "Popover", "DropdownMenu", "HoverCard", "Dialog", "Sheet" })]
    public void EveryOverlayRootIsActuallyBeingChecked()
    {
 // 优雅跳过：五个浮层根组件的源码尚未全部进入源树（对应组件尚未交付）时
        // 本守卫无法逐项确认，强行运行只会因组件缺失而误报；全部到位后自动生效。
        // 没有本测试，重命名某个组件或它的 handler 会悄悄掏空上面两个测试
        // 让它们在一个组件都没检查的情况下"通过"。
        var found = Handlers().Select(h => h.Component).ToList();

        var missing = overlayRoots.Except(found, StringComparer.Ordinal).ToList();

        Assert.True(missing.Count == 0,
            $"以下组件没有找到 HandleContextStateChanged：{string.Join(", ", missing)}。"
            + "要么组件改了名，要么 handler 改了名——请更新 OverlayRoots，" +
            "否则本类中的检查就在无声地检查着任何东西都不存在。");
    }

    /// <summary>每个浮层根组件的 <c>HandleContextStateChanged</c> 主体。</summary>
    private static List<(string Component, string Handler)> Handlers()
    {
        var results = new List<(string, string)>();

        foreach (var (component, text) in SourceTree.ByComponent())
        {
            if (!overlayRoots.Contains(component, StringComparer.Ordinal))
            {
                continue;
            }

            var match = HandlerBody().Match(text);
            if (match.Success)
            {
                results.Add((component, match.Value));
            }
        }

        return results;
    }

    /// <summary>
    /// 从 handler 签名到方法缩进处的收尾大括号——足以捕获方法体
    /// 而无需做花括号配对。
    /// </summary>
    [GeneratedRegex(@"private void HandleContextStateChanged\(\)\r?\n\s*\{.*?\r?\n    \}",
        RegexOptions.Singleline)]
    private static partial Regex HandlerBody();

    /// <summary>决定是否通知使用者的那个 <c>if</c> 块。</summary>
    [GeneratedRegex(@"if \([^)]*!= newOpenState\)\r?\n\s*\{.*?\r?\n        \}",
        RegexOptions.Singleline)]
    private static partial Regex GuardedBlock();
}
