using AtomBlazor.Primitives;
namespace AtomBlazor.Tests.ApiSurface;

/// <summary>
/// Components 库 API 面快照测试：检测组件参数与公开类型的非预期破坏性变更。
/// 首次运行无 .verified.txt 基线会失败，审阅 .received.txt 后按 Verify 流程接受为基线。
/// </summary>
public class ComponentsApiSurfaceTests
{
    [Fact]
    public Task ComponentsApiSurfaceMatchesBaseline()
    {
 // 借用真实组件类型取得程序集（首批展示组件已交付）。
        var assembly = typeof(AtomBlazor.Alert).Assembly;
        var apiSurface = ApiSurfaceGenerator.Generate(assembly);
        return Verify(apiSurface);
    }
}
