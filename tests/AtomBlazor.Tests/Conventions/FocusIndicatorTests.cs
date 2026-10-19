using AtomBlazor.Primitives;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Xunit;

namespace AtomBlazor.Tests.Conventions;

/// <summary>
/// 防止组件移除浏览器的焦点轮廓（outline）却不画出替代品。
/// <para>
/// <c>outline-none</c> 会压制用户代理自带的焦点环。没有东西顶上它的话，聚焦控件与未聚焦控件
/// 就无法区分，这违反 <see href="https://www.w3.org/WAI/WCAG21/Understanding/focus-visible.html">WCAG 2.4.7</see>。
/// 整个文本输入家族直到 #457 之前都是这样发布的——问题讨论 #355
/// 其中三个人表示这一点决定了他们放弃本库选择别的库。
/// </para>
/// <para>
/// 本规则刻意接受任何被认可的示证方式，而不只是焦点环：菜单项用背景变化标示焦点
/// 这正是漫游焦点（roving-focus）列表的正确模式。
/// </para>
/// </summary>
public class FocusIndicatorTests
{
    private static readonly Regex removesOutline =
        new(@"(?:focus(?:-visible)?:)?outline-none", RegexOptions.Compiled);

    /// <summary>
    /// 焦点环、ring 色的边框，或聚焦态／列表项高亮（选中）态下的背景/文字变化。
    /// </summary>
    private static readonly Regex hasIndicator = new(
        @"focus(?:-visible|-within)?:(?:ring-[1-9]|border-ring|bg-|text-)"
        + @"|data-\[(?:focused|highlighted)[^\]]*\]:(?:bg-|text-|ring-)",
        RegexOptions.Compiled);

    /// <summary>
    /// listbox 的选项自己驱动选中样式，因此 <c>aria-selected</c> 本身就是存在示证手段的证据。
    /// </summary>
    private static readonly Regex managesSelection =
        new(@"aria-selected|data-\[state=selected\]", RegexOptions.Compiled);

    /// <summary>
    /// 浮层面板在打开时是被程序化聚焦的。每次打开都在整个面板外面画一圈焦点环是错的
    /// 所以对这些后缀而言 <c>outline-none</c> 是正确的。
    /// </summary>
    private static readonly string[] containerSuffixes =
        ["Content", "Overlay", "Portal", "Provider", "Host"];

    /// <summary>
    /// 按组件名豁免的清单，每一项都要写明理由。保持这份清单短小——没人信服的豁免名单
    /// 就是等着被静音的豁免名单。标注 #459 的条目是等待该议题修复的真实缺口
    /// 与其悄悄跳过，不如列出来，让删除它们成为一次看得见的编辑。
    /// </summary>
    private static readonly Dictionary<string, string> allowed = new(StringComparer.Ordinal)
    {
        ["InputGroup"] = "包装元素；焦点环由内部的 input 承担。",
        ["SidebarInset"] = "页面内容的布局容器，不是控件。",
        ["DashboardWidget"] = "部件外壳；其内部的可聚焦控件各自带焦点环。",
    };

    [Fact]
    public void ComponentsThatRemoveTheOutlineProvideAFocusIndicator()
    {
        var violations = new List<string>();

        foreach (var (component, text) in SourceTree.ByComponent())
        {
            if (!removesOutline.IsMatch(text))
            {
                continue;
            }

            if (hasIndicator.IsMatch(text) || managesSelection.IsMatch(text))
            {
                continue;
            }

            if (containerSuffixes.Any(s => component.EndsWith(s, StringComparison.Ordinal)))
            {
                continue;
            }

            if (allowed.ContainsKey(component))
            {
                continue;
            }

            violations.Add(component);
        }

        Assert.True(violations.Count == 0, BuildMessage(violations));
    }

    /// <summary>
    /// 一条已经不再适用的豁免比没有更糟：它把回归藏到了一条过期的豁免背后。
    /// 如果这里的某个组件已经获得了示证手段，就删掉它的条目。
    /// </summary>
    [FactWhen(Components = new[] { "InputGroup", "SidebarInset", "DashboardWidget" })]
    public void AllowlistHasNoStaleEntries()
    {
 // 优雅跳过：被豁免的三个组件的源码尚未全部进入源树（对应组件尚未交付）时
        // 每个条目都会因"组件不存在"被误判为过期；全部到位后本测试自动生效。
        var byComponent = SourceTree.ByComponent()
            .ToDictionary(x => x.Component, x => x.Text, StringComparer.Ordinal);

        var stale = allowed.Keys
            .Where(c => !byComponent.TryGetValue(c, out var text)
                        || !removesOutline.IsMatch(text)
                        || hasIndicator.IsMatch(text)
                        || managesSelection.IsMatch(text))
            .ToList();

        Assert.True(stale.Count == 0,
            "这些豁免条目已经不再需要豁免——组件要么已获得焦点示证，要么不再移除轮廓，"
            + "要么已不存在。请把它们从 " + $"{nameof(allowed)} 中移除：{Environment.NewLine}  "
            + string.Join($"{Environment.NewLine}  ", stale));
    }

    private static string BuildMessage(List<string> violations)
    {
        var message = new StringBuilder()
            .AppendLine(CultureInfo.InvariantCulture,
                $"{violations.Count} 个组件移除了焦点轮廓却没有提供替代品。")
            .AppendLine()
            .AppendLine("聚焦控件与未聚焦控件看上去一模一样，这违反 WCAG 2.4.7。")
            .AppendLine()
            .AppendLine("请加上本库的标准焦点环：")
            .AppendLine("  atom:focus-visible:outline-none atom:focus-visible:ring-2 atom:focus-visible:ring-ring atom:focus-visible:ring-offset-2")
            .AppendLine()
            .AppendLine("紧贴标签摆放的控件请去掉 ring-offset-2——表单行留有 3px 间距，")
            .AppendLine("而带 offset 的环向外延 4px，会盖到标签上。")
            .AppendLine()
            .AppendLine("聚焦时的背景变化同样有效，也是菜单项的正确模式。")
            .AppendLine("如果组件确实不需要示证手段，请把它加入豁免清单并写明理由。")
            .AppendLine();

        foreach (var violation in violations)
        {
            message.AppendLine("  " + violation);
        }

        return message.ToString();
    }
}
