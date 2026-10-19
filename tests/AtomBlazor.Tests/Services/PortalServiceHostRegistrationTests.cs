using AtomBlazor.Primitives;
using AtomBlazor.Primitives.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AtomBlazor.Tests.Services;

/// <summary>
/// 宿主注册按计数而非标志位（#545）。
/// <para>两个宿主同时存活的情形，
/// 远比旧「只有第一个注册」规则所假设的多。
/// <c>PortalHost</c> 自身就是两个类别的宿主，而 Blazor
/// 先初始化进来的页面、后处置出去的布局，布局切换因此出现重叠。
/// 用布尔标志时，先处置的宿主会替仍在渲染的宿主把它清掉，
/// 此后每个再注册的门户都会记一条
/// "no &lt;PortalHost /&gt; is registered" 的日志。</para>
/// </summary>
public class PortalServiceHostRegistrationTests
{
    private static PortalService CreateService() =>
        new(NullLogger<PortalService>.Instance);

    [Fact]
    public void NoHostByDefault()
    {
        var service = CreateService();

        Assert.False(service.HasHost);
    }

    [Fact]
    public void OneRegistrationIsEnough()
    {
        var service = CreateService();

        service.RegisterHost();

        Assert.True(service.HasHost);
    }

    [Fact]
    public void SecondHostDisposingDoesNotClearTheFirst()
    {
        // PortalHost 由 Container 宿主与 Overlay 宿主组合而成。两者都注册；
        // 其一离去不得把另一个的注册一并带走。
        var service = CreateService();
        service.RegisterHost();
        service.RegisterHost();

        service.UnregisterHost();

        Assert.True(service.HasHost);
    }

    [Fact]
    public void OutgoingHostDisposingAfterTheIncomingOneRegistersLeavesAHost()
    {
        // #545 里的布局切换次序：新页面的宿主先初始化，旧布局的宿主后处置。
        // 幸存的宿主必须仍处于注册状态。
        var service = CreateService();
        service.RegisterHost();   // 移出的布局的宿主
        service.RegisterHost();   // 移入的页面的宿主
        service.UnregisterHost(); // 移出的布局被处置

        Assert.True(service.HasHost);
    }

    [Fact]
    public void LastHostLeavingClearsRegistration()
    {
        var service = CreateService();
        service.RegisterHost();
        service.RegisterHost();

        service.UnregisterHost();
        service.UnregisterHost();

        Assert.False(service.HasHost);
    }

    [Fact]
    public void UnbalancedUnregisterDoesNotPoisonALaterRegistration()
    {
        // 没有配对注册的处置不得把计数打成负数，否则下一个真实宿主
        // 注册时落在亏空上，仍会自报缺宿主。
        var service = CreateService();

        service.UnregisterHost();
        service.UnregisterHost();
        service.RegisterHost();

        Assert.True(service.HasHost);
    }
}
