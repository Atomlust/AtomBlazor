using AtomBlazor.Primitives;
using System.Text.RegularExpressions;
using Xunit;

namespace AtomBlazor.Tests.Conventions;

/// <summary>
/// 间距、边框、圆角与文本对齐一律使用 Tailwind 的 logical 工具类
/// （<c>ms-</c>、<c>pe-</c>、<c>border-s</c>、<c>start-</c>、<c>text-start</c>），
/// 而非其物理方向对应物，使本库在 <c>dir="rtl"</c> 下自动镜像，无需第二份样式表。
/// <para>
/// 物理方向类不是编译错误，在从左到右的浏览器里也看不出任何问题——这恰恰是它以文本方式检查的原因：
/// 缺陷只出现在从右到左阅读的客户那里，且只出现在那个写的时候没考虑方向的组件里。
/// </para>
/// <para>
/// 有些物理方向类是正确的、应当保留。它们分两类，下面都连同理由列出：
/// </para>
/// <list type="bullet">
/// <item>
/// 组件自身的 API 点名了物理方位——<c>SheetSide.Left</c>、<c>ToastPosition.TopRight</c>、
/// <c>BadgeDotPosition.BottomLeft</c>。参数承诺的是物理边，兑现它就是契约。
/// </item>
/// <item>
/// 用像素而非布局来度量的几何。剩下的不多：在自身偏移上居中滑帽、泳道宽度、
/// 与物理调整尺寸光标匹配的角落抓手。这些组件里的拖拽与缩放数学现在自己读取方向，两者是一致的。
/// </item>
/// </list>
/// </summary>
public partial class LogicalPropertyTests
{
    /// <summary>
    /// 允许保留物理方向类的文件清单，每一项都带豁免理由。
    /// 新增条目必须能归入上面两类之一并给出理由。
    /// </summary>
    private static readonly Dictionary<string, string> exempt = new(StringComparer.Ordinal)
    {
        // 组件自身的 API 点名了物理方位。
        ["SheetContent.razor"] = "SheetSide 点名物理方位",
        ["DrawerContent.razor"] = "DrawerDirection 点名物理方位",
        ["Sidebar.razor.cs"] = "SidebarSide 点名物理方位",
        ["SidebarInset.razor.cs"] = "margin 与 SidebarSide 配对",
        ["SidebarRail.razor"] = "rail 依物理侧边停靠",
        ["ToastProvider.razor"] = "ToastPosition 点名物理角落",
        ["Badge.razor.cs"] = "BadgeDotPosition 点名物理角落",
        ["NotificationBadge.razor"] = "BadgeDotPosition 点名物理角落",
        ["TooltipContent.razor"] = "箭头各边来自物理的 PopoverSide",
        ["NavigationMenuIndicator.razor"] = "旋转箭头的圆角是物理的",

        // 以像素或相对左侧的百分比计算的几何。
        ["Scheduler.razor.cs"] = "泳道宽度以像素度量",
        ["Dock.razor"] = "角落抓手与物理调整尺寸光标匹配",
        ["SelectionIndicator.razor"] = "指示器按测量结果定位",
    };

    /// <summary>
    /// 存在 logical 对应物的物理工具类。成对的居中写法
    /// （<c>left-1/2</c> 配 <c>-translate-x-1/2</c>）是对称的，由 <see cref="IsCentring"/> 排除，
    /// 而非按名字排除。
    /// </summary>
    [GeneratedRegex(
        @"atom:(?:[^\s""]*?:)?!?-?(?<token>ml|mr|pl|pr|border-l|border-r|rounded-tl|rounded-tr|rounded-bl|rounded-br|rounded-l|rounded-r|text-left|text-right|left|right)(?<value>-[^\s""]*)?(?=[""\s]|$)",
        RegexOptions.Compiled)]
    private static partial Regex Physical();

    /// <summary>
    /// 与 translate 配对的 <c>left-1/2</c> 和 <c>left-[50%]</c> 是元素的居中方式。
    /// 它们在两个方向指向同一位置，不是方向缺陷。
    /// 取值从匹配而非整行读取，因为长类字符串常常拆成几行，translate 于是落在另一行上。
    /// </summary>
    private static bool IsCentring(Match match) =>
        match.Groups["value"].Value is "-1/2" or "-[50%]";

    [Fact]
    public void ComponentsUseLogicalDirectionClasses()
    {
        var offenders = new List<string>();

        foreach (var file in SourceTree.ComponentSources)
        {
            if (exempt.ContainsKey(file.Name))
            {
                continue;
            }

            var lines = File.ReadAllLines(file.FullName);

            for (var i = 0; i < lines.Length; i++)
            {
                foreach (var match in Physical().Matches(lines[i]).Cast<Match>())
                {
                    if (IsCentring(match))
                    {
                        continue;
                    }

                    offenders.Add($"{SourceTree.RelativePath(file)}:{i + 1} {match.Value}");
                }
            }
        }

        Assert.True(offenders.Count == 0,
            "这些工具类是物理方向的，布局在 dir=\"rtl\" 下不会镜像。请改用 logical 形式——"
            + "ms/me、ps/pe、start/end、border-s/border-e、rounded-s/rounded-e、"
            + "text-start/text-end——或者，如果物理方位确实是对的，把该文件连同理由加入"
            + $"豁免清单：{Environment.NewLine}"
            + string.Join(Environment.NewLine, offenders));
    }

    /// <summary>
    /// 手写规则里的物理方向 CSS 属性。工具类扫描看不到这些——它们不是工具类——
    /// 而单侧声明的规则正是扫描之后最早暴露的若干从右到左缺陷的藏身处：
    /// 列表缩进、blockquote 规则、时间轴对齐。
    /// </summary>
    [GeneratedRegex(
        @"(?<!-)\b(?<prop>margin|padding|border)-(?<side>left|right)\b|\btext-align:\s*(?<align>left|right)\b",
        RegexOptions.Compiled)]
    private static partial Regex PhysicalCssProperty();

    /// <summary>
    /// 当规则在两侧设置同一属性时，该声明是对称的——与方向无关，无需 logical 形式。
    /// </summary>
    private static bool IsSymmetricPair(string[] lines, int index, Match match)
    {
        var prop = match.Groups["prop"].Value;

        if (prop.Length == 0)
        {
            return false;
        }

        var opposite = prop + "-" + (match.Groups["side"].Value == "left" ? "right" : "left");

        // 按惯例配对是相邻书写的；这里改为扫描整个声明块，以免重排令检查失效。
        var start = index;
        while (start > 0 && !lines[start].Contains('{'))
        {
            start--;
        }

        var end = index;
        while (end < lines.Length - 1 && !lines[end].Contains('}'))
        {
            end++;
        }

        return lines[start..(end + 1)].Any(line => line.Contains(opposite, StringComparison.Ordinal));
    }

    /// <summary>
    /// 与组件源码一并检查的手写样式表：本库的 Tailwind 输入文件，
    /// 以及演示站的输入文件——后者就是用户阅读的文档。
    /// </summary>
    private static IEnumerable<FileInfo> Stylesheets()
    {
        string[] relative =
        [
            Path.Combine("src", "AtomBlazor", "wwwroot", "css", "atom-input.css"),
            Path.Combine("demos", "AtomBlazor.Demo.Shared", "wwwroot", "css", "app-input.css"),
        ];

        return relative
            .Select(path => new FileInfo(Path.Combine(SourceTree.RepoRoot.FullName, path)))
            .Where(file => file.Exists);
    }

    [Fact]
    public void HandWrittenCssUsesLogicalProperties()
    {
        var offenders = new List<string>();

        foreach (var file in Stylesheets())
        {
            var lines = File.ReadAllLines(file.FullName);

            for (var i = 0; i < lines.Length; i++)
            {
                // Tailwind 自己生成的工具类名会出现在选择器里（.atom\:pl-4）；
                // 类本身已由上面的源码侧检查覆盖。
                if (lines[i].TrimStart().StartsWith('.') || lines[i].Contains(@"\:", StringComparison.Ordinal))
                {
                    continue;
                }

                foreach (var match in PhysicalCssProperty().Matches(lines[i]).Cast<Match>())
                {
                    if (IsSymmetricPair(lines, i, match))
                    {
                        continue;
                    }

                    offenders.Add($"{SourceTree.RelativePath(file)}:{i + 1} {match.Value.Trim()}");
                }
            }
        }

        Assert.True(offenders.Count == 0,
            "这些手写声明只设置了一侧的物理边，规则在 dir=\"rtl\" 下不会镜像。"
            + "请改用 logical 属性——margin-inline-start、padding-inline-end、"
            + "border-inline-start、text-align: start——或两侧同设，那是对称的、"
            + $"无需改动：{Environment.NewLine}"
            + string.Join(Environment.NewLine, offenders));
    }

    /// <summary>
    /// 一条文件里已不再有物理方向类的豁免就是过期豁免，而过期的豁免会悄悄停止守护它点名的文件。
    /// </summary>
    [FactWhen(Components = new[]
    {
        "SheetContent", "DrawerContent", "Sidebar", "SidebarInset", "SidebarRail",
        "ToastProvider", "Badge", "NotificationBadge", "TooltipContent",
        "NavigationMenuIndicator", "Scheduler", "Dock", "SelectionIndicator",
    })]
    public void EveryExemptionIsStillNeeded()
    {
 // 优雅跳过：豁免清单点名的组件源码尚未全部进入源树（渐进交付）时，
        // 每个条目都会因"没有该文件"被误判为过期；全部到位后本测试自动生效。
        var unused = new List<string>();

        foreach (var (name, reason) in exempt)
        {
            var files = SourceTree.ComponentSources.Where(f => f.Name == name).ToList();

            if (files.Count == 0)
            {
                unused.Add($"{name} — 没有该文件（{reason}）");
                continue;
            }

            var hasPhysical = files.Any(f => File.ReadAllLines(f.FullName)
                .Any(line => Physical().Matches(line).Cast<Match>().Any(m => !IsCentring(m))));

            if (!hasPhysical)
            {
                unused.Add($"{name} — 已无物理方向类（{reason}）");
            }
        }

        Assert.True(unused.Count == 0,
            "这些文件豁免于 logical 属性规则、却已不再需要豁免。"
            + $"请把它们从清单中移除，让规则重新守护它们：{Environment.NewLine}"
            + string.Join(Environment.NewLine, unused));
    }
}
