using System.Collections.Concurrent;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace AtomBlazor.Primitives.Services;

/// <summary>
/// Blazor 门户渲染服务的实现。
/// 维护一个按类别划分的门户注册表，门户内容可渲染在文档 body 层级。
/// 门户分为 Container（Dialog、Sheet）与 Overlay（Popover、Select 等）两类，
/// 各自记录插入顺序，保证渲染结果可预期。
/// </summary>
public class PortalService(ILogger<PortalService> logger) : IPortalService
{
    private static readonly Action<ILogger, Exception?> logMissingPortalHost =
        LoggerMessage.Define(LogLevel.Warning, new EventId(1, "MissingPortalHost"),
            "Atom：当前渲染上下文中没有注册任何 <PortalHost />，因此 Dialog、Select、Popover 等" +
            "基于门户的组件无法渲染。请按以下顺序排查。" +
            "（1）你书写 <PortalHost /> 的位置是否在作用域内有 `@using AtomBlazor.Primitives`？" +
            "缺少它时 Razor 不会把该标签识别为组件：它会输出一个字面上的 <portalhost> 元素，" +
            "既无编译错误、也无宿主——此时给它加 @rendermode 会报 RZ10023" +
            "（'only valid when used on a component'），这正是破绽。" +
            "（2）到底有没有 <PortalHost />？请在 MainLayout.razor 中加一个。" +
            "（3）如果它存在且已正确引入，那它正渲染在代码无法触达的地方。在 Blazor Web App 中，" +
            "这通常是因为布局是静态的而页面是交互的：只有当交互性全局应用时，路由页面的布局" +
            "才会继承页面的渲染模式。要么在 App.razor 的 <Routes /> 上设置 @rendermode，" +
            "要么——仅在逐页交互模式下（周围布局是静态的）——把 <PortalHost />、<ToastProvider />、" +
            "<DialogProvider /> 作为带各自 @rendermode 的交互孤岛来渲染。" +
            "在 InteractiveWebAssembly 下，宿主还必须位于客户端项目加载的程序集中——" +
            "服务器项目里的布局无论怎么标记都无法在浏览器中运行。");

    private readonly ConcurrentDictionary<string, PortalEntry> portals = new();
    private long nextOrder;
    private int hostCount;
    private int hasWarnedMissingHost;

    /// <inheritdoc />
    public bool HasHost => Volatile.Read(ref hostCount) > 0;

    /// <inheritdoc />
    public event Action<PortalCategory>? OnPortalsCategoryChanged;

    /// <inheritdoc />
    public event Action<string>? OnPortalRendered;

    /// <inheritdoc />
    /// <remarks>
    /// 用计数而不是布尔标志。同时存活两个宿主的情形比看上去常见得多——
    /// 组合式 <c>PortalHost</c> 本身就内含两个类别宿主，而且布局切换时，
    /// 新页面的宿主会在旧布局的宿主释放之前完成初始化。若用布尔标志，
    /// 先释放的那个宿主会把仍在运行的宿主的标志清掉，于是活着的宿主自称缺失，
    /// 此后每个门户都会记一条警告（#545）。
    /// </remarks>
    public void RegisterHost()
    {
        Interlocked.Increment(ref hostCount);

        // 新宿主到来时清掉"只警告一次"的闩锁，这样本电路后续真实发生的失败
        // 仍会被报告，而不会被先前一条无关的警告吞掉。
        Interlocked.Exchange(ref hasWarnedMissingHost, 0);
    }

    /// <inheritdoc />
    public void UnregisterHost()
    {
        // 钳位：没有对应注册的释放不得把计数压成负数，
        // 否则新宿主注册之后 HasHost 仍会报 false。
        // 用 CAS 而不是 Exchange(ref, 0)：后者会把并发注册刚加回来的计数一并抹掉。
        var observed = Interlocked.Decrement(ref hostCount);
        while (observed < 0)
        {
            var previous = Interlocked.CompareExchange(ref hostCount, 0, observed);
            if (previous == observed || previous >= 0)
            {
                return;
            }

            observed = previous;
        }
    }

    /// <inheritdoc />
    public void NotifyPortalRendered(string portalId) =>
        OnPortalRendered?.Invoke(portalId);

    /// <inheritdoc />
    public void RegisterPortal(string id, RenderFragment content, PortalCategory category)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Portal ID cannot be null or whitespace.", nameof(id));
        }

        ArgumentNullException.ThrowIfNull(content);

        if (!HasHost && Interlocked.Exchange(ref hasWarnedMissingHost, 1) == 0)
        {
            logMissingPortalHost(logger, null);
        }

        var entry = new PortalEntry
        {
            Content = content,
            Category = category,
            Order = Interlocked.Increment(ref nextOrder)
        };

        portals[id] = entry;
        OnPortalsCategoryChanged?.Invoke(category);
    }

    /// <inheritdoc />
    public void UnregisterPortal(string id)
    {
        if (portals.TryRemove(id, out var entry))
        {
            OnPortalsCategoryChanged?.Invoke(entry.Category);
        }
    }

    /// <inheritdoc />
    public void UpdatePortalContent(string id, RenderFragment content)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (!portals.TryGetValue(id, out var entry))
        {
            throw new InvalidOperationException($"Portal with ID '{id}' is not registered.");
        }

        // 更新内容，同时保留类别与插入顺序
        entry.Content = content;
        OnPortalsCategoryChanged?.Invoke(entry.Category);
    }

    /// <inheritdoc />
    public void RefreshPortal(string id)
    {
        if (portals.TryGetValue(id, out var entry))
        {
            // 通知类别宿主重渲染，但不替换 RenderFragment。
            // 这让既有片段能拾取新的捕获值，而不会创建新的 DOM 元素
            // （那会破坏 ElementReference）。
            OnPortalsCategoryChanged?.Invoke(entry.Category);
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<KeyValuePair<string, RenderFragment>> GetPortals(PortalCategory category)
    {
        return portals
            .Where(kvp => kvp.Value.Category == category)
            .OrderBy(kvp => kvp.Value.Order)
            .Select(kvp => new KeyValuePair<string, RenderFragment>(kvp.Key, kvp.Value.Content))
            .ToList();
    }
}
