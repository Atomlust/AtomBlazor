using AtomBlazor.Primitives;
using AtomBlazor;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AtomBlazor.Tests.Components;

/// <summary>
/// ToastService 单元面测试：四类快捷方法的变体与缺省落值、Show 入队顺序与
/// OnChange 播报次数、Dismiss 按 id（未知 id 静默不播报）、DismissAll（空集合仍播报）、
/// SetPosition/ResetPosition 的播报与 null 回落，以及 Toasts 快照数组的复制语义。
/// 全部为纯服务面，不涉及渲染与计时器；计时器沿由 ToastProviderTests 承载。
/// </summary>
public class ToastServiceTests
{
    private static ToastService Create() => new();

    /// <summary>订阅 OnChange，返回可读取并可清零的播报计数器（数组槽位供闭包共享）。</summary>
    private static int[] Track(ToastService service)
    {
        var counter = new int[1];
        service.OnChange += () => counter[0]++;
        return counter;
    }

    // ------------------------------------------------------------------
    // 快捷方法与缺省落值
    // ------------------------------------------------------------------

    [Fact]
    public void 四类快捷方法应各自落到对应变体并沿用缺省时长()
    {
        var service = Create();

        service.Success("已保存", "完成");
        service.Info("有新版本");
        service.Warning("空间不足");
        service.Error("上传失败", "出错了");

        var toasts = service.Toasts;
        Assert.Equal(4, toasts.Count);
        Assert.Equal(
            [ToastVariant.Success, ToastVariant.Info, ToastVariant.Warning, ToastVariant.Destructive],
            toasts.Select(t => t.Variant));

        // Error 落 Destructive（无独立 Error 变体）；仅传描述者 Title 为 null
        Assert.Equal("完成", toasts[0].Title);
        Assert.Null(toasts[1].Title);
        Assert.Equal("出错了", toasts[3].Title);
        Assert.All(toasts, toast => Assert.Equal(5000, toast.Duration));

        // 快捷方法经 Show(description,...) 通道入队，ToastData 其余缺省位照类默认值
        Assert.All(toasts, toast => Assert.True(toast.ShowIcon));
        Assert.All(toasts, toast => Assert.True(toast.ShowClose));
        Assert.All(toasts, toast => Assert.Null(toast.ShowCountdown));
        Assert.All(toasts, toast => Assert.Equal(ToastSize.Default, toast.Size));
        Assert.All(toasts, toast => Assert.Null(toast.Position));
        Assert.All(toasts, toast => Assert.Null(toast.Class));
        Assert.All(toasts, toast => Assert.Null(toast.ActionText));
        Assert.All(toasts, toast => Assert.Null(toast.OnAction));
    }

    [Fact]
    public void 简单Show应保留显式变体与时长并可覆盖缺省()
    {
        var service = Create();

        service.Show("只有描述");
        service.Show("带标题与时长", "标题", ToastVariant.Warning, 1200);
        service.Show("不自动消失", duration: 0);

        Assert.Equal(3, service.Toasts.Count);
        Assert.Equal(ToastVariant.Default, service.Toasts[0].Variant);
        Assert.Equal(5000, service.Toasts[0].Duration);
        Assert.Null(service.Toasts[0].Title);
        Assert.Equal(("标题", ToastVariant.Warning, 1200),
            (service.Toasts[1].Title, service.Toasts[1].Variant, service.Toasts[1].Duration));
        Assert.Equal(0, service.Toasts[2].Duration);
    }

    [Fact]
    public void Show入队应按调用顺序追加且每次恰播报一次()
    {
        var service = Create();
        var reports = 0;
        service.OnChange += () => reports++;

        var first = new ToastData { Description = "第一条" };
        var second = new ToastData { Description = "第二条" };
        service.Show(first);
        service.Show(second);

        Assert.Equal(2, reports);
        Assert.Equal([first, second], service.Toasts);
        Assert.Equal(["第一条", "第二条"], service.Toasts.Select(t => t.Description));
    }

    [Fact]
    public void Toasts应返回快照副本而非内部列表()
    {
        var service = Create();
        service.Info("快照");

        var first = service.Toasts;
        var second = service.Toasts;
        Assert.NotSame(first, second); // 每次 ToArray，非同一实例
        Assert.Equal(first.Select(t => t.Id), second.Select(t => t.Id));

        // 返回值写不动内部集合（IReadOnlyList 面 + 复制语义）
        service.Info("第二条");
        Assert.Single(first);
        Assert.Equal(2, service.Toasts.Count);
    }

    // ------------------------------------------------------------------
    // Dismiss 与 DismissAll 的播报次数
    // ------------------------------------------------------------------

    [Fact]
    public void Dismiss应按id命中并从队列移除()
    {
        var service = Create();
        var reports = 0;
        service.OnChange += () => reports++;

        service.Show(new ToastData { Id = "keep", Description = "留着" });
        service.Show(new ToastData { Id = "drop", Description = "去掉" });
        reports = 0;

        service.Dismiss("drop");

        Assert.Equal(1, reports);
        Assert.Equal(["keep"], service.Toasts.Select(t => t.Id));
    }

    [Fact]
    public void Dismiss未知id应静默且不播报变更()
    {
        var service = Create();
        service.Info("在场");
        var reports = Track(service);
        reports[0] = 0;

        service.Dismiss("不在场");

        Assert.Equal(0, reports[0]); // removed 为 false 时不播报（ quirk）
        Assert.Single(service.Toasts);
    }

    [Fact]
    public void DismissAll应清空队列并恒播报一次即使本来为空()
    {
        var service = Create();
        var reports = Track(service);

        service.DismissAll(); // 空集合：无条件播报
        Assert.Equal(1, reports[0]);

        service.Info("一条");
        service.Warning("两条");
        reports[0] = 0;

        service.DismissAll();
        Assert.Equal(1, reports[0]); // 一次清空只播报一次，与条数无关
        Assert.Empty(service.Toasts);
    }

    // ------------------------------------------------------------------
    // 位置面：SetPosition / ResetPosition 与优先级语义
    // ------------------------------------------------------------------

    [Fact]
    public void SetPosition应覆盖式写入并播报()
    {
        var service = Create();
        var reports = Track(service);

        Assert.Null(service.Position);

        service.SetPosition(ToastPosition.TopCenter);
        Assert.Equal(ToastPosition.TopCenter, service.Position);
        Assert.Equal(1, reports[0]);

        service.SetPosition(ToastPosition.TopCenter); // 同值重设仍播报
        Assert.Equal(2, reports[0]);
    }

    [Fact]
    public void ResetPosition应回落null且重复回落仍播报()
    {
        var service = Create();
        service.SetPosition(ToastPosition.TopLeft);

        var reports = Track(service);
        service.ResetPosition();
        Assert.Null(service.Position);
        Assert.Equal(1, reports[0]);

        service.ResetPosition(); // 已经是 null 也照播（ OnChange?.Invoke() 无守卫）
        Assert.Equal(2, reports[0]);
    }

    [Fact]
    public void 服务级位置与逐条位置应彼此独立互不覆盖()
    {
        var service = Create();

        // ToastData.Position 是数据位（可空、由消费方设置），服务级 Position 是集合位
        // 两者的优先级在 ToastProvider 的分组里兑现（见 ToastProviderTests）。
        service.Show(new ToastData { Id = "pinned", Description = "钉在左上", Position = ToastPosition.TopLeft });
        service.SetPosition(ToastPosition.BottomLeft);

        Assert.Equal(ToastPosition.BottomLeft, service.Position);
        Assert.Equal(ToastPosition.TopLeft, Assert.Single(service.Toasts).Position);
    }

    [Fact]
    public void Show的ToastData应原样入库不复制不改写标识()
    {
        var service = Create();
        var toast = new ToastData
        {
            Description = "自定义",
            Duration = 0,
            ShowIcon = false,
            ShowClose = false,
            ShowCountdown = true,
            Size = ToastSize.Compact,
            Class = "my-toast",
            ActionText = "撤销",
            OnAction = () => { },
        };

        service.Show(toast);

        var stored = Assert.Single(service.Toasts);
        Assert.Same(toast, stored); // 直接入队同一引用
        Assert.Equal(toast.Id, stored.Id);
        Assert.Equal(toast.CreatedAt, stored.CreatedAt);
        Assert.False(stored.ShowIcon);
        Assert.False(stored.ShowClose);
        Assert.True(stored.ShowCountdown!.Value);
        Assert.Equal(ToastSize.Compact, stored.Size);
        Assert.Equal("my-toast", stored.Class);
        Assert.Equal("撤销", stored.ActionText);
        Assert.NotNull(stored.OnAction);
    }

    [Fact]
    public void ToastData缺省面应按声明的初始值成形()
    {
        var toast = new ToastData();

        Assert.False(string.IsNullOrEmpty(toast.Id));
        Assert.NotEqual(Guid.Empty, Guid.Parse(toast.Id)); // Id 为 GUID 字符串
        Assert.Null(toast.Title);
        Assert.Null(toast.Description);
        Assert.Equal(ToastVariant.Default, toast.Variant);
        Assert.Equal(ToastSize.Default, toast.Size);
        Assert.Null(toast.Position);
        Assert.True(toast.ShowIcon);
        Assert.Equal(5000, toast.Duration);
        Assert.True(toast.ShowClose);
        Assert.Null(toast.ActionText);
        Assert.Null(toast.OnAction);
        Assert.Null(toast.ShowCountdown);
        Assert.Null(toast.Class);
        Assert.NotEqual(default, toast.CreatedAt);
    }

    // ------------------------------------------------------------------
 // DI 收口（兑现）
    // ------------------------------------------------------------------

    [Fact]
    public void AddAtomBlazorComponents应把ToastService注册为Scoped且排在DialogService之前()
    {
        var services = new ServiceCollection();
        services.AddAtomBlazorComponents();

        var toast = Assert.Single(services, d => d.ServiceType == typeof(ToastService));
        Assert.Equal(ServiceLifetime.Scoped, toast.Lifetime);
        Assert.Equal(typeof(ToastService), toast.ImplementationType);

        var dialog = Assert.Single(services, d => d.ServiceType == typeof(DialogService));
        Assert.Equal(ServiceLifetime.Scoped, dialog.Lifetime);

        // 同位同形：ToastService 的注册语句紧接基元注册之后、DialogService 之前
        Assert.True(
            services.IndexOf(toast) < services.IndexOf(dialog),
            "ToastService 应先于 DialogService 注册（照 ServiceCollectionExtensions 顺序）");
    }
}
