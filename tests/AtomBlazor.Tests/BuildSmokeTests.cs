using AtomBlazor.Primitives;
using AtomBlazor;
using VerifyXunit;
using Xunit;

namespace AtomBlazor.Tests;

/// <summary>
/// 骨架构建冒烟测试：验证双库引用、xUnit 与 Verify 快照管线在 net11.0 下可用。
/// 真正的渲染快照体系由 ComponentTestRenderer 建立。
/// </summary>
public class BuildSmokeTests
{
    [Fact]
    public void 两个核心程序集应可被测试工程引用()
    {
 // 起占位类型已删除：改用真实类型取程序集。
        var componentsAssembly = typeof(Alert).Assembly;
        var primitivesAssembly = typeof(AtomBlazor.Primitives.FloatingPortal).Assembly;

        Assert.Equal("AtomBlazor", componentsAssembly.GetName().Name);
        Assert.Equal("AtomBlazor.Primitives", primitivesAssembly.GetName().Name);
    }

    [Fact]
    public Task Verify快照管线应可用() => Verifier.Verify("AtomBlazor 骨架就绪");
}
