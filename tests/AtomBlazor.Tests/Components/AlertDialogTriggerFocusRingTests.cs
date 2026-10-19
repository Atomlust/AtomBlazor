using AtomBlazor.Primitives;
using System.Text.RegularExpressions;

namespace AtomBlazor.Tests.Components;

/// <summary>
/// 回归守卫（自 AlertDialogTriggerFocusRingTests，对应 #459/#508 的“浮层触发器”组）：
/// AlertDialogTrigger 渲染一个原生 &lt;button&gt;（经由它包装的 Dialog 基元），并带标准独立控件
/// 焦点环（outline-none + themed ring），与 DialogTrigger/SheetTrigger/PopoverTrigger 保持一致。
/// </summary>
public class AlertDialogTriggerFocusRingTests
{
    private static readonly Regex standaloneRing = new(
        @"atom:focus-visible:outline-none\s+atom:focus-visible:ring-2\s+atom:focus-visible:ring-ring\s+atom:focus-visible:ring-offset-2",
        RegexOptions.Compiled);

    [Fact]
    public void 组件层应套用独立控件焦点环()
    {
        var path = Path.Combine(RepoRoot(), "src", "AtomBlazor",
            "Components", "AlertDialog", "AlertDialogTrigger.razor");
        Assert.True(File.Exists(path), $"未找到 AlertDialogTrigger.razor：{path}");

        var text = File.ReadAllText(path);
        Assert.True(standaloneRing.IsMatch(text),
            "AlertDialogTrigger 应对其渲染的原生 button 套用库标准独立控件焦点环 " +
            "(atom:focus-visible:outline-none atom:focus-visible:ring-2 atom:focus-visible:ring-ring atom:focus-visible:ring-offset-2)。");
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "src")))
        {
            dir = dir.Parent;
        }

        if (dir is null)
        {
            throw new DirectoryNotFoundException("未能从测试输出目录向上定位到仓库根（含 src/）。");
        }

        return dir.FullName;
    }
}
