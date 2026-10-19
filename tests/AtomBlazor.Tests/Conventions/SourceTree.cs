using AtomBlazor.Primitives;
using System.Reflection;
using Xunit;

namespace AtomBlazor.Tests.Conventions;

/// <summary>
/// 从测试程序集所在的位置定位仓库的源码目录。
/// <para>
/// 约定测试以文本方式读取组件标记，而不是校验渲染后的输出，因为这类测试守护的缺陷是标记中的遗漏——
/// 一个缺失的 CSS 类、一个未按区域设置格式化的插值——行为测试看不到这些。两种情形下逻辑都可能出错；
/// 而字符串本身不会。
/// </para>
/// </summary>
internal static class SourceTree
{
    private static readonly Lazy<DirectoryInfo> repoRootLazy = new(FindRepoRoot);

    /// <summary>两个组件库中的每一个 <c>.razor</c> 与 <c>.cs</c> 文件。</summary>
    internal static IReadOnlyList<FileInfo> ComponentSources { get; } = EnumerateSources();

    /// <summary>仓库根目录，供需要读取组件目录之外内容的测试使用。</summary>
    internal static DirectoryInfo RepoRoot => repoRootLazy.Value;

    private static DirectoryInfo FindRepoRoot()
    {
        var dir = new DirectoryInfo(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!);

        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "src", "AtomBlazor")))
            {
                return dir;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException(
            "无法通过从测试程序集逐级向上定位仓库根目录。" +
            "这些测试以文本方式读取组件源码，因此需要工作副本——" +
            "它们无法仅针对已打包的程序集运行。");
    }

    private static List<FileInfo> EnumerateSources()
    {
        string[] roots =
        [
            Path.Combine("src", "AtomBlazor", "Components"),
            Path.Combine("src", "AtomBlazor.Primitives", "Primitives"),
        ];

        var files = new List<FileInfo>();

        foreach (var relative in roots)
        {
            var root = new DirectoryInfo(Path.Combine(repoRootLazy.Value.FullName, relative));
            if (!root.Exists)
            {
 // 渐进交付：对应目录尚不存在时直接跳过该根。
                continue;
            }

            files.AddRange(root
                .EnumerateFiles("*", SearchOption.AllDirectories)
                .Where(f => f.Extension is ".razor" or ".cs"));
        }

        return files;
    }

    /// <summary>
    /// 按组件对源文件分组，使拆分为 <c>Foo.razor</c> 与 <c>Foo.razor.cs</c> 两部分的组件被整体评判。
    /// 标记在一个文件里、类字符串在另一个文件里，单独看任何一半都会得出错误结论。
    /// </summary>
    internal static IEnumerable<(string Component, string Text)> ByComponent()
    {
        return ComponentSources
            .GroupBy(f => f.Name.Split('.')[0], StringComparer.Ordinal)
            .Select(g => (g.Key, string.Join("\n", g.Select(f => File.ReadAllText(f.FullName)))));
    }

    /// <summary>相对仓库根的路径，用于可读性更好的断言消息。</summary>
    internal static string RelativePath(FileInfo file) =>
        Path.GetRelativePath(repoRootLazy.Value.FullName, file.FullName);
}

/// <summary>
/// 条件化 Fact 特性：当测试所扫描的目录、文件或组件源码尚未进入源树（对应组件尚未交付）时，
/// 整个测试优雅跳过（Skip），而不是因文件缺失而失败。
/// <para>
/// xunit v2 没有 v3 的 <c>Skip.When</c> 运行时跳过，因此采用「复合 Fact 特性」模式：
/// 在测试发现阶段构造特性时求值条件，并设置 <see cref="FactAttribute.Skip"/> 理由。
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
internal sealed class FactWhenAttribute : FactAttribute
{
    private string? _fileOrDirectory;
    private string[]? _components;

    /// <summary>仓库根相对路径（目录或文件均可，使用正斜杠）；不存在时跳过本测试。</summary>
    public string? FileOrDirectory
    {
        get => _fileOrDirectory;
        set { _fileOrDirectory = value; Evaluate(); }
    }

    /// <summary>组件名列表（不含扩展名）；其中任一组件的源文件尚不存在时跳过本测试。</summary>
    public string[]? Components
    {
        get => _components;
        set { _components = value; Evaluate(); }
    }

    // 命名属性在特性构造函数之后才赋值，因此求值必须挂在属性 set 上，
    // 而不是构造函数里。
    private void Evaluate()
    {
        if (_fileOrDirectory is not null)
        {
            var full = Path.Combine(SourceTree.RepoRoot.FullName,
                _fileOrDirectory.Replace('/', Path.DirectorySeparatorChar));

            if (!Directory.Exists(full) && !File.Exists(full))
            {
                Skip = $"源树尚未包含 {_fileOrDirectory}（对应模块尚未交付），优雅跳过。";
                return;
            }
        }

        if (_components is not null)
        {
            var missing = _components
                .Where(name => !SourceTree.ComponentSources.Any(f => f.Name.Split('.')[0] == name))
                .ToList();

            if (missing.Count > 0)
            {
                Skip = $"这些组件的源码尚未进入源树（对应模块尚未交付）：{string.Join(", ", missing)}；优雅跳过。";
                return;
            }
        }

        // 条件全部满足：确保不残留跳过理由（属性可能被多次求值）
        if (_fileOrDirectory is not null || _components is not null)
        {
            Skip = null;
        }
    }
}
