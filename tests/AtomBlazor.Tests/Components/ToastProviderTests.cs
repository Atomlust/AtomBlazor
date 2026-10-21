using AtomBlazor.Primitives;
using System.Text.RegularExpressions;
using AtomBlazor;
using AtomBlazor.Tests.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Xunit;

namespace AtomBlazor.Tests.Components;

/// <summary>
/// ToastProvider 渲染面测试：容器常驻与位置类、多枚并列顺序、逐条位置覆盖另起容器、
/// 服务级 SetPosition/ResetPosition 对提供器 Position 参数的优先覆盖、OnChange 订阅即刷新、
/// 关闭钮点击回服务并把该枚移出 DOM、MaxToasts 的 TakeLast 沿（超限者留队列不丢弃、零即不限）、
/// ShowCountdown 的级联与逐条覆盖、ToastData.Class 透传落点、DisposeAsync 摘除订阅。
/// 服务注入照 DialogTests 先例以**固定实例**注册（生产 DI 为 Scoped，见 ToastServiceTests 的
/// AddAtomBlazorComponents 用例），以便测试侧直接驱动 Show/Dismiss。
/// 计时器沿（自动关闭、悬停暂停/恢复、PauseOnHover 门控）以真实短钟 + 轮询覆盖，容差见各用例注释
/// 测试面局限：续期分段（RemainingMs 扣减精度）、ResumeSignal 竞态与 DisposeAsync 对在飞
/// Task.Delay 的取消时序依赖真实线程调度，headless 下只以「不消失/消失」两端不变量断言
/// 不断精确毫秒。
/// </summary>
public class ToastProviderTests
{
    /// <summary>归一化 GUID（Id 若入 DOM）与时间戳，使快照跨运行确定。</summary>
    private static string Scrub(string markup) =>
        Regex.Replace(
            Regex.Replace(markup, @"[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}", "toast-<id>"),
            @"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?", "<timestamp>");

    private static ComponentTestRenderer CreateRendererWith(ToastService service)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(new NoopJavaScript());
        services.AddSingleton<ILocalizer, DefaultLocalizer>();
        services.AddSingleton(service);
        return new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance);
    }

    /// <summary>挂载提供器并返回渲染器/服务/组件三件套。</summary>
    private static async Task<(ComponentTestRenderer Renderer, ToastService Service, ToastProvider Provider)> MountAsync(
        Dictionary<string, object?>? parameters = null, ToastService? service = null)
    {
        service ??= new ToastService();
        var renderer = CreateRendererWith(service);
        var provider = await renderer.MountAsync<ToastProvider>(parameters ?? []);
        await FlushAsync(renderer);
        return (renderer, service, provider);
    }

    private static Task FlushAsync(ComponentTestRenderer renderer) =>
        renderer.Dispatcher.InvokeAsync(() => { });

    /// <summary>在分发器上驱动服务变更（OnChange 同步回调到提供器），并冲刷两批渲染。</summary>
    private static async Task PumpAsync(ComponentTestRenderer renderer, Action action)
    {
        await renderer.Dispatcher.InvokeAsync(action);
        await FlushAsync(renderer);
        await FlushAsync(renderer);
    }

    private static Task DispatchAsync(ComponentTestRenderer renderer, string attributeName, int occurrence = 0) =>
        renderer.Dispatcher.InvokeAsync(() => renderer.DispatchAsync(attributeName, new MouseEventArgs(), occurrence));

    private static Task<string> MarkupAsync(ComponentTestRenderer renderer) =>
        renderer.Dispatcher.InvokeAsync(renderer.Markup);

    /// <summary>轮询等待某个标记谓词成立（真实钟沿用），超时即返回 false。</summary>
    private static async Task<bool> WaitUntilAsync(
        ComponentTestRenderer renderer, Func<string, bool> predicate, int timeoutMs = 3000)
    {
        var deadline = Environment.TickCount64 + timeoutMs;
        while (Environment.TickCount64 < deadline)
        {
            if (predicate(await MarkupAsync(renderer)))
            {
                return true;
            }

            await Task.Delay(25);
        }

        return false;
    }

    private static Task DisposeProviderAsync(ComponentTestRenderer renderer, ToastProvider provider) =>
        renderer.Dispatcher.InvokeAsync(async () => await provider.DisposeAsync());

    /// <summary>toast 枚数：以根元素的 role="alert" 计数（本家族图标全部在册，无回退告警元素）。</summary>
    private static int AlertCount(string markup) => Regex.Count(markup, "role=\"alert\"", RegexOptions.None);

    /// <summary>容器元素开标签（组合类指纹：pointer-events-none + fixed，避免误中 toast 根）。</summary>
    private static List<string> ContainerTags(string markup) =>
        Regex.Matches(markup, "<div[^>]*atom:pointer-events-none[^>]*atom:fixed[^>]*>", RegexOptions.Singleline)
            .Select(m => m.Value)
            .ToList();

    /// <summary>
    /// 容器整段：自该容器开标签起、至下一容器开标签前（无下一段则到末尾）。
    /// 容器皆为根级并列兄弟，故按索引切片即可把每枚 toast 归属到其所在容器。
    /// </summary>
    private static List<string> ContainerParts(string markup)
    {
        var starts = Regex.Matches(markup, "<div[^>]*atom:pointer-events-none[^>]*atom:fixed[^>]*>", RegexOptions.Singleline)
            .Select(m => m.Index)
            .ToList();
        return starts
            .Select((start, i) => markup[start..(i + 1 < starts.Count ? starts[i + 1] : markup.Length)])
            .ToList();
    }

    // ------------------------------------------------------------------
    // 容器与位置
    // ------------------------------------------------------------------

    [Fact]
    public async Task 零toast时容器仍常驻并落右下缺省位置类()
    {
        var (renderer, _, _) = await MountAsync();

        var markup = await MarkupAsync(renderer);
        var container = Assert.Single(ContainerTags(markup));
        Assert.Contains("atom:pointer-events-none", container);
        Assert.Contains("atom:bottom-0 atom:right-0", container);
        Assert.Equal(0, AlertCount(markup));
    }

    [Fact]
    public async Task Position参数应决定主容器位置类而服务级覆盖优先()
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(ToastProvider.Position)] = ToastPosition.TopLeft,
        };
        var (renderer, service, _) = await MountAsync(parameters);

        Assert.Contains("atom:top-0 atom:left-0", Assert.Single(ContainerTags(await MarkupAsync(renderer))));

        // 服务级 SetPosition 覆盖参数位（EffectivePosition = ToastService.Position ?? Position）
        await PumpAsync(renderer, () => service.SetPosition(ToastPosition.BottomCenter));
        Assert.Contains(
            "atom:bottom-0 atom:left-1/2 atom:-translate-x-1/2",
            Assert.Single(ContainerTags(await MarkupAsync(renderer))));

        // ResetPosition 回落参数位 TopLeft（null 即交还给提供器）
        await PumpAsync(renderer, () => service.ResetPosition());
        Assert.Contains("atom:top-0 atom:left-0", Assert.Single(ContainerTags(await MarkupAsync(renderer))));
    }

    [Fact]
    public async Task 多枚toast应按入队顺序同容器并列()
    {
        var (renderer, service, _) = await MountAsync();

        await PumpAsync(renderer, () =>
        {
            service.Show(new ToastData { Id = "t1", Description = "第一条", Duration = 0 });
            service.Show(new ToastData { Id = "t2", Description = "第二条", Duration = 0 });
            service.Show(new ToastData { Id = "t3", Description = "第三条", Duration = 0 });
        });

        var markup = await MarkupAsync(renderer);
        Assert.Equal(3, AlertCount(markup));
        Assert.Single(ContainerTags(markup));

        // 入队顺序即 DOM 顺序（服务追加、提供器按序渲染）
        Assert.True(
            markup.IndexOf("第一条", StringComparison.Ordinal) <
            markup.IndexOf("第二条", StringComparison.Ordinal) &&
            markup.IndexOf("第二条", StringComparison.Ordinal) <
            markup.IndexOf("第三条", StringComparison.Ordinal));
    }

    [Fact]
    public async Task 逐条位置覆盖应另起独立容器且主栈不再含该枚()
    {
        var (renderer, service, _) = await MountAsync();

        await PumpAsync(renderer, () =>
        {
            service.Show(new ToastData { Id = "main", Description = "主栈", Duration = 0 });
            service.Show(new ToastData { Id = "pinned", Description = "钉左上", Position = ToastPosition.TopLeft, Duration = 0 });
            service.Show(new ToastData { Id = "pinned2", Description = "也钉左上", Position = ToastPosition.TopLeft, Duration = 0 });
        });

        var markup = await MarkupAsync(renderer);
        Assert.Equal(2, ContainerTags(markup).Count); // 主栈 + 一个 TopLeft 组（Distinct 位置）

        // 主容器只含无覆盖的那枚；TopLeft 容器聚合同位置的逐条覆盖
        var parts = ContainerParts(markup);
        Assert.Contains("atom:bottom-0 atom:right-0", parts[0]);
        Assert.Contains("atom:top-0 atom:left-0", parts[1]);
        Assert.Contains("主栈", parts[0]);
        Assert.DoesNotContain("钉左上", parts[0]);
        Assert.DoesNotContain("主栈", parts[1]);
        Assert.Contains("钉左上", parts[1]);
        Assert.Contains("也钉左上", parts[1]);
        Assert.Equal(1, AlertCount(parts[0]));
        Assert.Equal(2, AlertCount(parts[1]));
        Assert.Equal(3, AlertCount(markup));
    }

    // ------------------------------------------------------------------
    // OnChange 刷新与关闭沿
    // ------------------------------------------------------------------

    [Fact]
    public async Task 服务变更后无需手动重渲染即入树并在关闭后移出()
    {
        var (renderer, service, _) = await MountAsync();

        await PumpAsync(renderer, () =>
        {
            service.Show(new ToastData { Id = "stay", Description = "留下", Duration = 0 });
            service.Show(new ToastData { Id = "bye", Description = "会走", Duration = 0 });
        });
        Assert.Equal(2, AlertCount(await MarkupAsync(renderer)));

        // 关闭钮按渲染序：occurrence 0 = 第一枚（"留下"）；点第二枚即移除"会走"
        await DispatchAsync(renderer, "onclick", occurrence: 1);
        await FlushAsync(renderer);

        var markup = await MarkupAsync(renderer);
        Assert.Equal(1, AlertCount(markup));
        Assert.Contains("留下", markup);
        Assert.DoesNotContain("会走", markup);
        Assert.Equal(["stay"], service.Toasts.Select(t => t.Id)); // 服务侧同步摘除，仅剩未点的那枚
    }

    [Fact]
    public async Task DismissAll应清空枚集而容器本身仍在()
    {
        var (renderer, service, _) = await MountAsync();

        await PumpAsync(renderer, () =>
        {
            service.Show(new ToastData { Id = "a", Description = "甲", Duration = 0 });
            service.Show(new ToastData { Id = "b", Description = "乙", Duration = 0 });
        });
        Assert.Equal(2, AlertCount(await MarkupAsync(renderer)));

        await PumpAsync(renderer, service.DismissAll);

        var markup = await MarkupAsync(renderer);
        Assert.Equal(0, AlertCount(markup));
        Assert.Single(ContainerTags(markup)); // 常驻容器不随集合清空而消失
    }

    [Fact]
    public async Task 悬停沿应由提供器接线到该枚计时器并在离开后恢复()
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(ToastProvider.ShowCountdown)] = true,
        };
        var (renderer, service, provider) = await MountAsync(parameters);

        // 150ms 的短钟：悬停 600ms 仍未消失即证暂停生效（不暂停时早已关闭）
        await PumpAsync(renderer, () => service.Show(new ToastData { Description = "悬停不吃", Duration = 150 }));
        await DispatchAsync(renderer, "onmouseenter", occurrence: 0);

        await Task.Delay(600);
        Assert.Contains("悬停不吃", await MarkupAsync(renderer));
        Assert.Contains("animation-play-state: paused", await MarkupAsync(renderer));

        // 离开后恢复剩余时长：终态必关（真实钟，容差 3 秒）
        await DispatchAsync(renderer, "onmouseleave", occurrence: 0);
        Assert.True(
            await WaitUntilAsync(renderer, m => !m.Contains("悬停不吃", StringComparison.Ordinal)),
            "恢复后计时器未在容差内关闭该枚");

        await DisposeProviderAsync(renderer, provider);
        Assert.NotNull(provider);
        Assert.Empty(service.Toasts);
    }

    // ------------------------------------------------------------------
    // MaxToasts
    // ------------------------------------------------------------------

    [Fact]
    public async Task 缺省MaxToasts为五时只显最新五枚超限者仍在队列()
    {
        var (renderer, service, _) = await MountAsync();

        await PumpAsync(renderer, () =>
        {
            for (var i = 1; i <= 7; i++)
            {
                service.Show(new ToastData { Description = $"第{i}条", Duration = 0 });
            }
        });

        var markup = await MarkupAsync(renderer);
        Assert.Equal(5, AlertCount(markup)); // TakeLast(5)
        Assert.DoesNotContain("第1条", markup);
        Assert.DoesNotContain("第2条", markup);
        Assert.Contains("第7条", markup);
        Assert.Equal(7, service.Toasts.Count); // 超限者不丢弃，仍排队
    }

    [Fact]
    public async Task MaxToasts为零即不限制且与逐条位置分组各算各的()
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(ToastProvider.MaxToasts)] = 0,
        };
        var (renderer, service, _) = await MountAsync(parameters);

        await PumpAsync(renderer, () =>
        {
            for (var i = 1; i <= 7; i++)
            {
                service.Show(new ToastData { Description = $"主{i}", Duration = 0 });
            }

            service.Show(new ToastData { Description = "角顶", Position = ToastPosition.TopRight, Duration = 0 });
        });

        var markup = await MarkupAsync(renderer);
        Assert.Equal(8, AlertCount(markup));
        Assert.Contains("主1", markup);
        Assert.Equal(2, ContainerTags(markup).Count);

        // 主栈（右下）与 TopRight 组各算各的：逐条分组不受主栈条数影响
        var parts = ContainerParts(markup);
        Assert.Contains("atom:bottom-0 atom:right-0", parts[0]);
        Assert.Contains("atom:top-0 atom:right-0", parts[1]);
        Assert.Equal(7, AlertCount(parts[0]));
        Assert.Equal(1, AlertCount(parts[1]));
        Assert.Contains("角顶", parts[1]);
    }

    // ------------------------------------------------------------------
    // ShowCountdown 级联与 Class 透传
    // ------------------------------------------------------------------

    [Fact]
    public async Task ShowCountdown应级联到逐条并可被逐条覆盖双向()
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(ToastProvider.ShowCountdown)] = true,
        };
        var (renderer, service, _) = await MountAsync(parameters);

        await PumpAsync(renderer, () =>
        {
            service.Show(new ToastData { Id = "inherit", Description = "跟随级联", Duration = 4000 });
            service.Show(new ToastData { Id = "off", Description = "逐条关掉", Duration = 4000, ShowCountdown = false });
            service.Show(new ToastData { Id = "zero", Description = "零时长不显", Duration = 0, ShowCountdown = true });
        });

        var markup = await MarkupAsync(renderer);

        // 缺省 null → 跟随提供器；显式 false → 覆盖；Duration 为 0 → 联动不产出。
        // 每产出一次倒计时，内联样式里就恰好出现一次动画名，故计数等于"带倒计时条的枚数"。
        Assert.Equal(1, Regex.Count(markup, "atom-alert-countdown", RegexOptions.None));
        Assert.Contains("跟随级联", markup);
        Assert.Contains("逐条关掉", markup);
        Assert.Contains("零时长不显", markup);
    }

    [Fact]
    public async Task 提供器关掉时逐条显式开仍产出倒计时条()
    {
        var (renderer, service, _) = await MountAsync(); // ShowCountdown 缺省 false

        await PumpAsync(renderer, () =>
        {
            service.Show(new ToastData { Id = "on", Description = "逐条开启", Duration = 3000, ShowCountdown = true });
            service.Show(new ToastData { Id = "follow", Description = "跟随关掉", Duration = 3000 });
        });

        var markup = await MarkupAsync(renderer);
        Assert.Equal(1, Regex.Count(markup, "atom-alert-countdown", RegexOptions.None));
        Assert.Contains("逐条开启", markup);
    }

    [Fact]
    public async Task ToastData点名的Class应透传到该枚根元素而不影响他枚()
    {
        var (renderer, service, _) = await MountAsync();

        await PumpAsync(renderer, () =>
        {
            service.Show(new ToastData { Id = "styled", Description = "带类", Class = "my-toast-class", Duration = 0 });
            service.Show(new ToastData { Id = "plain", Description = "裸的", Duration = 0 });
        });

        var markup = await MarkupAsync(renderer);
        var tags = Regex.Matches(markup, "<div[^>]*role=\"alert\"[^>]*>", RegexOptions.Singleline)
            .Select(m => m.Value)
            .ToList();
        Assert.Equal(2, tags.Count);
        Assert.Contains("my-toast-class", tags[0]);
        Assert.DoesNotContain("my-toast-class", tags[1]);
    }

    // ------------------------------------------------------------------
    // splat 与 DisposeAsync
    // ------------------------------------------------------------------

    [Fact]
    public async Task 提供器的Class与透传应落主容器而透传按注释宣告无效于其余容器()
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(ToastProvider.Class)] = "my-stack",
            ["data-provider-probe"] = "on-container",
        };
        var (renderer, service, _) = await MountAsync(parameters);

        var markup = await MarkupAsync(renderer);
        var container = Assert.Single(ContainerTags(markup));
        Assert.Contains("my-stack", container);
        Assert.Contains("data-provider-probe=\"on-container\"", container);

        // 逐条位置容器不带 Class/splat（只在主容器展开 @attributes）
        await PumpAsync(renderer, () => service.Show(new ToastData { Description = "钉顶", Position = ToastPosition.TopCenter, Duration = 0 }));
        var containers = ContainerTags(await MarkupAsync(renderer));
        Assert.Equal(2, containers.Count);
        Assert.DoesNotContain("data-provider-probe", containers[1]);
    }

    [Fact]
    public async Task DisposeAsync应摘除OnChange订阅()
    {
        var (renderer, service, provider) = await MountAsync();

        await DisposeProviderAsync(renderer, provider);

        // 摘除后服务变更不再驱动渲染（且零异常：计时器与订阅均已收口）
        await PumpAsync(renderer, () => service.Show(new ToastData { Description = "迟到的", Duration = 0 }));
        Assert.DoesNotContain("迟到的", await MarkupAsync(renderer));
    }

    [Fact]
    public async Task 后台线程入队的toast应经分发器渲染且计时器仍生效()
    {
        var (renderer, service, provider) = await MountAsync();

        await Task.Run(() => service.Show(new ToastData { Description = "后台来的", Duration = 150 }));

        Assert.True(
            await WaitUntilAsync(renderer, m => m.Contains("后台来的", StringComparison.Ordinal)),
            "后台线程触发的 OnChange 未在渲染器线程上兑现");
        Assert.True(
            await WaitUntilAsync(renderer, m => !m.Contains("后台来的", StringComparison.Ordinal)),
            "后台入队的 toast 自动关闭计时器未生效");
        Assert.Empty(service.Toasts);

        await DisposeProviderAsync(renderer, provider);
    }

    [Fact]
    public async Task 正时长toast应在容差内自动关闭()
    {
        var (renderer, service, provider) = await MountAsync();

        await PumpAsync(renderer, () => service.Show(new ToastData { Description = "会自己走", Duration = 150 }));
        Assert.Contains("会自己走", await MarkupAsync(renderer));

        Assert.True(
            await WaitUntilAsync(renderer, m => !m.Contains("会自己走", StringComparison.Ordinal)),
            "计时器未在容差内驱动自动关闭");
        Assert.Empty(service.Toasts);

        await DisposeProviderAsync(renderer, provider);
    }

    [Fact]
    public async Task PauseOnHover为假时悬停不拦截自动关闭()
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(ToastProvider.PauseOnHover)] = false,
        };
        var (renderer, service, provider) = await MountAsync(parameters);

        await PumpAsync(renderer, () => service.Show(new ToastData { Description = "悬停也照走", Duration = 150 }));
        await DispatchAsync(renderer, "onmouseenter", occurrence: 0);

        Assert.True(
            await WaitUntilAsync(renderer, m => !m.Contains("悬停也照走", StringComparison.Ordinal)),
            "PauseOnHover=false 时悬停不应拦截计时器");
        Assert.Empty(service.Toasts);

        await DisposeProviderAsync(renderer, provider);
    }

    // ------------------------------------------------------------------
    // 快照
    // ------------------------------------------------------------------

    [Fact]
    public async Task 默认结构快照应与基线一致()
    {
        var (renderer, service, _) = await MountAsync();

        await PumpAsync(renderer, () =>
        {
            service.Show(new ToastData
            {
                Title = "已保存",
                Description = "草稿已更新",
                Variant = ToastVariant.Success,
                Duration = 0,
                ActionText = "查看",
                OnAction = () => { },
            });
            service.Show(new ToastData
            {
                Description = "钉在顶部居中的提示",
                Variant = ToastVariant.Warning,
                Position = ToastPosition.TopCenter,
                Duration = 0,
                Class = "snapshot-pinned",
            });
        });

        var markup = await MarkupAsync(renderer);
        Assert.Equal(markup, Scrub(markup)); // 实探：Id/CreatedAt 不入 DOM，归一为无害保险
        await Verify(Scrub(markup));
    }
}
