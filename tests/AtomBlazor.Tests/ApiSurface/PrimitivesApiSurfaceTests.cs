using AtomBlazor.Primitives;
namespace AtomBlazor.Tests.ApiSurface;

/// <summary>
/// Primitives 库 API 面快照测试：检测基元组件参数、枚举与服务接口的
/// 非预期破坏性变更。首次运行无 .verified.txt 基线会失败，审阅
/// .received.txt 后按 Verify 流程接受为基线。
/// </summary>
public class PrimitivesApiSurfaceTests
{
    [Fact]
    public Task PrimitivesApiSurfaceMatchesBaseline()
    {
 // 借用真实基元类型取得程序集（起 Primitives 已交付基础设施类型）。
        var assembly = typeof(AtomBlazor.Primitives.FloatingPortal).Assembly;
        var apiSurface = ApiSurfaceGenerator.Generate(assembly);
        return Verify(apiSurface);
    }
}
