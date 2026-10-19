using AtomBlazor.Primitives;
using System.Text.RegularExpressions;
using Xunit;

namespace AtomBlazor.Tests.Conventions;

/// <summary>
/// 守护加载 primitive JavaScript 所允许的唯一一次往返开销。
/// <para>
/// 在 Blazor Server 中，从 C# 发出的 <c>import(...)</c> 是一条发往浏览器、随后由服务端等待的指令，
/// 所以每个懒加载的模块都要付出一次完整的 SignalR 电路往返——按模块、按页面加载计，无论有没有缓存。
/// 因此 primitives 以单一 bundle 交付，每个调用点都用 <c>namespace.function</c> 标识符寻址它。
/// </para>
/// <para>
/// 编译器看不见 JavaScript 标识符，API 面快照也看不见。错误的标识符只会在运行时、在浏览器里失败，
/// 报 <c>Could not find 'x.y'</c>——而且只在用到它的那条代码路径上失败；对浮层来说，那可能是一段
/// 没人会用键盘走一遍的交互。这些测试把标识符从 C# 里读出来，并与模块逐一核对。
/// </para>
/// </summary>
public class PrimitiveJsModuleTests
{
    private static readonly string primitiveJsRoot = Path.Combine(
        SourceTree.RepoRoot.FullName,
        "src", "AtomBlazor.Primitives", "wwwroot", "js", "primitives");

    private const string bundleFileName = "primitives.js";

    /// <summary>
    /// Components 层只打包那些几乎每个页面都会加载的模块——实测为 theme、sidebar、sidebar-inset、
    /// text-input 与 composition-guard。其余刻意保持懒加载：把三十个模块全部打包，
    /// 对一个只渲染一个输入框的应用就是 56 KB（gzip 后）。
    /// </summary>
    private static readonly string componentsJsRoot = Path.Combine(
        SourceTree.RepoRoot.FullName,
        "src", "AtomBlazor", "wwwroot", "js");

    private const string componentsBundleFileName = "components-core.js";

    /// <summary>两个库所有 bundle 中「命名空间 → 模块文件」的映射。</summary>
    private static Dictionary<string, string> AllBundledModules()
    {
        var all = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var (root, bundle) in new[]
                 {
                     (primitiveJsRoot, bundleFileName),
                     (componentsJsRoot, componentsBundleFileName),
                 })
        {
            var bundlePath = Path.Combine(root, bundle);

 // 优雅处理渐进交付：对应模块尚未产出该 bundle 时按空集对待，
            // 已有的 bundle 照常参与核对。
            if (!File.Exists(bundlePath))
            {
                continue;
            }

            foreach (Match m in reExport.Matches(File.ReadAllText(bundlePath)))
            {
                all[m.Groups["ns"].Value] = Path.Combine(root, m.Groups["file"].Value + ".js");
            }
        }

        return all;
    }

    /// <summary>
    /// <c>import * as clickOutside from './click-outside.js';</c>——bundle 把每个模块导入为一个命名空间，
    /// 校验它不是过期的缓存副本，然后导出该命名空间。
    /// 较旧的 <c>export * as …</c> 形式与缓存版本查询串也一并接受。
    /// </summary>
    private static readonly Regex reExport = new(
        @"^(?:export|import) \* as (?<ns>\w+) from '\./(?<file>[\w-]+)\.js(?:\?[^']+)?';",
        RegexOptions.Multiline | RegexOptions.Compiled);

    /// <summary><c>export function foo</c>、<c>export async function foo</c>、<c>export const foo</c>。</summary>
    private static readonly Regex moduleExport = new(
        @"^export\s+(?:async\s+)?(?:function|const|let|class)\s+(?<name>\w+)",
        RegexOptions.Multiline | RegexOptions.Compiled);

    /// <summary>点名 bundle 内某个函数的 interop 调用：<c>InvokeVoidAsync("portal.lockBodyScroll", …)</c>。</summary>
    private static readonly Regex dottedInvoke = new(
        @"Invoke(?:Void)?Async(?:<[^>]*>)?\(\s*\n?\s*""(?<ns>[a-z][\w]*)\.(?<fn>[A-Za-z]\w*)""",
        RegexOptions.Compiled);

    /// <summary><c>document.addEventListener('keydown', …)</c>，无论写法如何。</summary>
    private static readonly Regex documentKeydownListener = new(
        @"document\s*\.\s*addEventListener\s*\(\s*['""]keydown['""]",
        RegexOptions.Compiled);

    /// <summary>C# 直接 <c>import</c> 本库模块（而不是带缓存的那种）。</summary>
    private static readonly Regex directImport = new(
        @"""import"",\s*\n?\s*""(?<path>\./_content/Atom\.BlazorUI\.[\w.]+/js/[\w/-]+\.js)""",
        RegexOptions.Compiled);

    [FactWhen(FileOrDirectory = "src/AtomBlazor.Primitives/wwwroot/js/primitives/primitives.js")]
    public void TheBundleReExportsEveryPrimitiveModule()
    {
 // 优雅跳过：primitives bundle（起）尚未交付时本测试没有可扫描的对象。
        var bundled = reExport
            .Matches(File.ReadAllText(Path.Combine(primitiveJsRoot, bundleFileName)))
            .Select(m => m.Groups["file"].Value)
            .ToHashSet(StringComparer.Ordinal);

        var onDisk = Directory
            .EnumerateFiles(primitiveJsRoot, "*.js")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(name => name != Path.GetFileNameWithoutExtension(bundleFileName))
            .ToList();

        var unbundled = onDisk.Where(name => !bundled.Contains(name!)).ToList();

        Assert.True(
            unbundled.Count == 0,
            $"这些 primitive 模块没有被 {bundleFileName} 重新导出，于是每次页面加载访问它们都要" +
            $"多付一次电路往返：{string.Join(", ", unbundled)}。" +
            "请为每个模块添加一行 `import * as <camelCase> from './<file>.js';`、一次新鲜性校验，" +
            "并把命名空间加入导出列表。");
    }

    [Fact]
    public void TheBundleNamespaceIsTheFileNameInCamelCase()
    {
        var wrong = AllBundledModules()
            .Where(pair => pair.Key != ToCamelCase(Path.GetFileNameWithoutExtension(pair.Value)))
            .Select(pair => $"{pair.Key} 应为 {ToCamelCase(Path.GetFileNameWithoutExtension(pair.Value))}")
            .ToList();

        Assert.True(
            wrong.Count == 0,
            "命名空间必须是模块文件名的 camelCase 形式——这条规则是不打开 bundle 也能预测调用点写法" +
            $"的唯一依据：{string.Join("; ", wrong)}");
    }

    [Fact]
    public void EveryInteropIdentifierResolvesToAnExportedFunction()
    {
        var namespaces = AllBundledModules();

        var exportsByNamespace = namespaces.ToDictionary(
            pair => pair.Key,
            pair => moduleExport
                .Matches(File.ReadAllText(pair.Value))
                .Select(m => m.Groups["name"].Value)
                .ToHashSet(StringComparer.Ordinal),
            StringComparer.Ordinal);

        var unresolved = new List<string>();

        foreach (var file in InteropSources())
        {
            foreach (Match match in dottedInvoke.Matches(File.ReadAllText(file.FullName)))
            {
                var ns = match.Groups["ns"].Value;
                var fn = match.Groups["fn"].Value;

                // 作用于 JS 返回的清理句柄上的标识符（"dispose"、"apply"）不带命名空间，
                // 根本不会走到这里。前缀不是某个 bundle 命名空间的，属于别的库的模块，
                // 不归我们检查。
                if (!exportsByNamespace.TryGetValue(ns, out var exports))
                {
                    continue;
                }

                if (!exports.Contains(fn))
                {
                    unresolved.Add($"{SourceTree.RelativePath(file)}: \"{ns}.{fn}\" — " +
                                   $"{Path.GetFileName(namespaces[ns])} 并未导出 {fn}");
                }
            }
        }

        Assert.True(
            unresolved.Count == 0,
            "这些 interop 标识符会在运行时以 \"Could not find\" 失败：\n  " +
            string.Join("\n  ", unresolved));
    }

    [Fact]
    public void NothingImportsAModuleDirectly()
    {
        // 组件自行 import 自己的模块，代价是按组件*实例*每次一趟电路往返，
        // 因为引用缓存在实例字段里：一个有十三个文本输入框的表单曾把同一个文件 import 了十三次。
        // JsModules 按电路缓存。
        var offenders = InteropSources()
            .Where(f => f.Name is not ("PrimitiveModules.cs" or "JsModules.cs"))
            .SelectMany(f => directImport
                .Matches(File.ReadAllText(f.FullName))
                .Select(m => $"{SourceTree.RelativePath(f)} import 了 {m.Groups["path"].Value}"))
            .ToList();

        Assert.True(
            offenders.Count == 0,
            "这些调用每个都要按组件实例付一趟电路往返。请改用 " +
            "JsModules.GetAsync(JSRuntime, path)——primitive bundle 请用 " +
            "PrimitiveModules.GetAsync(JSRuntime)：\n  " + string.Join("\n  ", offenders));
    }

    [FactWhen(FileOrDirectory = "src/AtomBlazor.Primitives/wwwroot/js/primitives")]
    public void OnlyTheEscapeStackWatchesEscapeAtTheDocument()
    {
 // 优雅跳过：primitives JS 目录（起）尚未交付时没有模块可扫描。
        // Escape 必须只关闭最顶层的浮层，而不是全部打开的浮层。escape-keydown.js 在单个 document
        // 监听器后面维护着一个栈，是唯一能对其排序的东西；第二个模块若再加自己的 document 监听器，
        // 就游离于该排序之外，dialog 里的 popover 一按 Escape 会把两层同时关掉。
        // 内容持有焦点的浮层（Select、菜单）在自己的容器上处理 Escape 并阻止其冒泡，
        // 这就是为什么容器级监听器没问题、document 级监听器不行。
        //
        // document 级 keydown 监听器只在它对 Escape 作出反应时才是问题。
        // element-utils.js 监听 Tab 是为了区分键盘抵达与鼠标抵达，keyboard-shortcuts.js 服务于
        // 应用级快捷键；两者都不看 Escape。
        var offenders = Directory
            .EnumerateFiles(primitiveJsRoot, "*.js")
            .Where(file => Path.GetFileName(file) != "escape-keydown.js")
            .Select(file => (Name: Path.GetFileName(file), Text: File.ReadAllText(file)))
            .Where(m => documentKeydownListener.IsMatch(m.Text) && m.Text.Contains("Escape", StringComparison.Ordinal))
            .Select(m => m.Name)
            .ToList();

        Assert.True(
            offenders.Count == 0,
            "这些模块加了自己的 document 级 keydown 监听器，于是 Escape 会绕过 escape-keydown.js " +
            $"中的排序：{string.Join(", ", offenders)}。请改用 escapeKeydown.initialize(ref, id, method) " +
            "注册，或在自己的容器上处理按键并调用 stopPropagation()。");
    }

    /// <summary>
    /// 库内所有可能调用 primitive 模块的源文件。范围比 <see cref="SourceTree.ComponentSources"/>
    /// 更宽——后者止步于组件目录，而负责定位、焦点与快捷键的服务位于其外。
    /// </summary>
    private static IEnumerable<FileInfo> InteropSources()
    {
        string[] roots =
        [
            Path.Combine("src", "AtomBlazor"),
            Path.Combine("src", "AtomBlazor.Primitives"),
        ];

        return roots
            .Select(relative => new DirectoryInfo(Path.Combine(SourceTree.RepoRoot.FullName, relative)))
            .Where(dir => dir.Exists)
            .SelectMany(dir => dir.EnumerateFiles("*", SearchOption.AllDirectories))
            .Where(f => f.Extension is ".razor" or ".cs")
            .Where(f => !f.FullName.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(f => !f.FullName.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal));
    }

    private static string ToCamelCase(string kebab)
    {
        var parts = kebab.Split('-');
        return parts[0] + string.Concat(parts.Skip(1).Select(p => char.ToUpperInvariant(p[0]) + p[1..]));
    }
}
