using AtomBlazor.Primitives;
using AtomBlazor.Primitives.Services;
using AtomBlazor.Primitives.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AtomBlazor.Tests.Dropdown;

/// <summary>
/// DropdownManagerService 的纯逻辑测试：同一时刻只保一个打开登记、
/// 后登记者挤掉前登记者（并触发其关闭回调）、按 ID 的注销/查询语义，
/// 以及 ID 校验与 DI 生命周期。本服务不经 JS——是登记表的内存实现。
/// </summary>
public class DropdownManagerTests
{
    private static DropdownManagerService CreateManager() => new();

    // ------------------------------------------------------------------
 // 登记与互斥
    // ------------------------------------------------------------------

    [Fact]
    public void 登记为打开后应可查询且重复登记自身不触发关闭()
    {
        var manager = CreateManager();
        var closeCount = 0;

        manager.RegisterOpen("a", () => closeCount++);

        Assert.True(manager.IsOpen("a"));
        Assert.False(manager.IsOpen("b"));

 // 同一 ID 再次登记：自己不算"别的答案"，不应被挤掉。
        manager.RegisterOpen("a", () => closeCount++);
        Assert.Equal(0, closeCount);
    }

    [Fact]
    public void 新登记者应挤掉当前打开者并调用其关闭回调()
    {
        var manager = CreateManager();
        var closed = new List<string>();

        manager.RegisterOpen("a", () => closed.Add("a"));
        manager.RegisterOpen("b", () => closed.Add("b"));

        Assert.Equal(["a"], closed);
        Assert.False(manager.IsOpen("a"));
        Assert.True(manager.IsOpen("b"));
    }

    [Fact]
    public void 注销当前者后应无人打开且注销他人不生效()
    {
        var manager = CreateManager();
        manager.RegisterOpen("a", () => { });
        manager.RegisterOpen("b", () => { });

        // 注销的不是当前打开的那个——什么都不动。
        manager.Unregister("a");
        Assert.True(manager.IsOpen("b"));

        manager.Unregister("b");
        Assert.False(manager.IsOpen("b"));
        Assert.False(manager.IsOpen("a"));

        // 无人打开时再注销应安全无操作。
        manager.Unregister("b");
    }

    [Fact]
    public void 空回调关闭不应引发异常()
    {
        var manager = CreateManager();

 // 从未登记过任何下拉时，IsOpen 全部为 false、注销无害。
        Assert.False(manager.IsOpen("anything"));
        manager.Unregister("anything");
    }

    // ------------------------------------------------------------------
    // ID 校验
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void 空或空白ID应抛ArgumentException(string? id)
    {
        var manager = CreateManager();

        Assert.Throws<ArgumentException>(() => manager.RegisterOpen(id!, () => { }));
    }

    [Theory]
    [InlineData("bad id")]      // 空格
    [InlineData("bad/id")]      // 斜杠
    [InlineData("bad.id")]      // 点号
    [InlineData("emoji🎉")]     // 非字母数字符号
    public void 含非法字符的ID应抛ArgumentException(string id)
    {
        var manager = CreateManager();

        Assert.Throws<ArgumentException>(() => manager.RegisterOpen(id, () => { }));
    }

    [Theory]
    [InlineData("simple")]
    [InlineData("with-dash")]
    [InlineData("with_underscore")]
    [InlineData("with:colon")]
    [InlineData("bb-01_x:Z")]
    public void 合法字符组合应被接受(string id)
    {
        var manager = CreateManager();

        manager.RegisterOpen(id, () => { });

        Assert.True(manager.IsOpen(id));
    }

    [Fact]
    public void 空关闭回调应抛ArgumentNullException()
    {
        var manager = CreateManager();

        Assert.Throws<ArgumentNullException>(() => manager.RegisterOpen("a", null!));
    }

    // ------------------------------------------------------------------
 // 并发登记（Blazor Server 上多个异步操作可能并发访问）
    // ------------------------------------------------------------------

    [Fact]
    public void 并发登记应始终只留最后一个打开且每个被挤掉者恰好关一次()
    {
        var manager = CreateManager();
        var closed = new System.Collections.Concurrent.ConcurrentBag<string>();
        var ids = Enumerable.Range(0, 32).Select(i => $"dropdown-{i}").ToArray();

        Parallel.ForEach(ids, id => manager.RegisterOpen(id, () => closed.Add(id)));

 // 每个登记过的 ID 要么仍打开、要么被挤掉过；总数上"打开过但未注销且未再登记"
        // 至多一个。
        var stillOpen = ids.Where(manager.IsOpen).ToList();
        Assert.True(stillOpen.Count <= 1);

        // 被挤掉的每个 ID 各调用了一次关闭。
        var distinctClosed = closed.Distinct().ToList();
        Assert.True(distinctClosed.All(c => !manager.IsOpen(c)));
        Assert.Equal(ids.Length - stillOpen.Count, distinctClosed.Count);
    }

    // ------------------------------------------------------------------
    // DI
    // ------------------------------------------------------------------

    [Fact]
    public void AddAtomBlazorPrimitives应把下拉管理器注册为Scoped具体类型()
    {
        var services = new ServiceCollection();
        services.AddAtomBlazorPrimitives();

        // 与源一致：注册具体类型 DropdownManagerService（无接口转发）。
        var descriptor = Assert.Single(services, d => d.ServiceType == typeof(DropdownManagerService));
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
        Assert.Equal(typeof(DropdownManagerService), descriptor.ImplementationType);
    }
}
