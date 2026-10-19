using AtomBlazor.Primitives;
using System.Text.RegularExpressions;
using Xunit;

namespace AtomBlazor.Tests.Conventions;

/// <summary>
/// 把 <c>@attributes</c> 展开（splat）到一个没有声明任何 <c>CaptureUnmatchedValues</c> 参数的组件上
/// 会抛出 <see cref="InvalidOperationException"/>——但只在字典里确实有条目时才会。
/// 这样写出来的包装组件渲染完全正常，直到使用者加上 <c>data-testid</c> 或 <c>title</c>，
/// 然后它就崩溃了。编译器看不见这个问题，也没有演示页面会触发它，所以这里以文本方式检查。
/// </summary>
public partial class AttributeSplatTargetTests
{
    [Fact]
    public void EverySplatTargetCapturesUnmatchedValues()
    {
        var components = Components();
        var offenders = new List<string>();

        foreach (var file in SourceTree.ComponentSources.Where(f => f.Extension == ".razor"))
        {
            var text = File.ReadAllText(file.FullName);

            foreach (Match match in ComponentWithSplat().Matches(text))
            {
                var tag = match.Groups["tag"].Value;

                if (!Resolve(components, file.FullName, tag, out var target) || Captures(components, target))
                {
                    continue;
                }

                var line = text.Take(match.Index).Count(c => c == '\n') + 1;
                offenders.Add($"{SourceTree.RelativePath(file)}:{line} 将属性展开到 {tag}");
            }
        }

        Assert.True(offenders.Count == 0,
            "这些组件把捕获到的属性转发给一个无法接收它们的组件，于是任何多余的 HTML 属性都会"
            + "在渲染时抛异常。要么给目标组件加一个 CaptureUnmatchedValues 参数，要么把属性"
            + $"展开到该组件实际渲染的元素上：{Environment.NewLine}{string.Join(Environment.NewLine, offenders)}");
    }

    /// <summary>组件名 → 其完整源码文本（含 code-behind）。</summary>
    private static Dictionary<string, List<(string Path, string Text)>> Components()
    {
        var map = new Dictionary<string, List<(string, string)>>(StringComparer.Ordinal);

        foreach (var file in SourceTree.ComponentSources.Where(f => f.Extension == ".razor"))
        {
            var text = File.ReadAllText(file.FullName);
            var behind = new FileInfo(file.FullName + ".cs");

            if (behind.Exists)
            {
                text += "\n" + File.ReadAllText(behind.FullName);
            }

            var name = Path.GetFileNameWithoutExtension(file.Name);

            if (!map.TryGetValue(name, out var list))
            {
                map[name] = list = [];
            }

            list.Add((file.FullName, text));
        }

        return map;
    }

    /// <summary>
    /// 把标签解析到一个源文件。限定标签带出命名空间，命名空间对应到目录；
    /// 裸标签先在本库内解析，与 Razor 通过 <c>_Imports.razor</c> 的解析方式一致。
    /// </summary>
    private static bool Resolve(
        Dictionary<string, List<(string Path, string Text)>> components,
        string fromPath,
        string tag,
        out string target)
    {
        target = string.Empty;
        var name = tag.Split('.')[^1];

        if (!components.TryGetValue(name, out var candidates))
        {
            // 不是我们自己的组件——例如框架组件 NavLink。
            return false;
        }

        if (tag.Contains('.', StringComparison.Ordinal))
        {
            var folder = tag.Split('.')[^2];
            var match = candidates.Find(c => c.Path.Contains(Path.DirectorySeparatorChar + folder + Path.DirectorySeparatorChar, StringComparison.Ordinal));

            if (match.Path is null)
            {
                return false;
            }

            target = match.Path;
            return true;
        }

        var library = fromPath.Contains("AtomBlazor.Primitives", StringComparison.Ordinal)
            ? "AtomBlazor.Primitives"
            : "AtomBlazor";
        var sameLibrary = candidates.FindAll(c => c.Path.Contains(library, StringComparison.Ordinal));
        var pick = sameLibrary.Count > 0 ? sameLibrary : candidates;

        if (pick.Count != 1)
        {
            return false;
        }

        target = pick[0].Path;
        return true;
    }

    /// <summary>
    /// 该组件是否捕获未匹配属性——直接声明，或通过 <c>@inherits</c> 声明的基类间接拥有。
    /// </summary>
    private static bool Captures(Dictionary<string, List<(string Path, string Text)>> components, string path)
    {
        var text = components.Values.SelectMany(v => v).First(c => c.Path == path).Text;

        if (text.Contains("CaptureUnmatchedValues", StringComparison.Ordinal))
        {
            return true;
        }

        var inherits = Inherits().Match(text);

        if (!inherits.Success)
        {
            return false;
        }

        var baseName = inherits.Groups["base"].Value.Split('.')[^1].Split('<')[0];
        var baseFile = BaseSource(baseName);

        return baseFile is not null && baseFile.Contains("CaptureUnmatchedValues", StringComparison.Ordinal);
    }

    private static string? BaseSource(string name)
    {
        var file = SourceTree.ComponentSources.FirstOrDefault(f => f.Name == name + ".cs" || f.Name == name + ".razor")
            ?? new DirectoryInfo(Path.Combine(SourceTree.RepoRoot.FullName, "src"))
                .EnumerateFiles(name + ".cs", SearchOption.AllDirectories)
                .FirstOrDefault();

        return file is null ? null : File.ReadAllText(file.FullName);
    }

    [GeneratedRegex(@"<(?<tag>[A-Z][\w.]*)(?<attrs>[^<>]*?@attributes[^<>]*?)/?>", RegexOptions.Singleline)]
    private static partial Regex ComponentWithSplat();

    [GeneratedRegex(@"^@inherits\s+(?<base>\S+)", RegexOptions.Multiline)]
    private static partial Regex Inherits();
}
