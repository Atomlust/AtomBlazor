using Microsoft.AspNetCore.Components;

namespace AtomBlazor.Primitives.Services;

/// <summary>
/// 在文档 body 层级渲染内容的服务，绕开正常 DOM 层级结构。
/// 为 Dialog、Popover、Dropdown 等浮层提供正确的 z-index 堆叠。
/// 门户按类别（Container 与 Overlay）划分，使每个类别的宿主只在本类别的门户
/// 发生变化时才重新渲染。
/// </summary>
public interface IPortalService
{
    /// <summary>
    /// 是否已有门户宿主注册到本服务。
    /// </summary>
    public bool HasHost { get; }

    /// <summary>
    /// 向服务注册一个门户宿主实例。
    /// 由门户宿主组件在初始化阶段调用。
    /// </summary>
    public void RegisterHost();

    /// <summary>
    /// 从服务注销一个门户宿主实例。
    /// 由门户宿主组件在释放阶段调用。
    /// </summary>
    public void UnregisterHost();

    /// <summary>
    /// 特定类别的门户发生变更（新增、更新或移除）时触发的事件。
    /// 按类别划分的宿主订阅此事件，避免无关类别变化引发重渲染。
    /// </summary>
    public event Action<PortalCategory>? OnPortalsCategoryChanged;

    /// <summary>
    /// 某个具体门户已在 DOM 中渲染完成后触发的事件。
    /// 用于内容组件与门户宿主之间的同步。
    /// </summary>
    public event Action<string>? OnPortalRendered;

    /// <summary>
    /// 通知某个门户已在 DOM 中渲染。
    /// 由门户宿主在渲染门户内容之后调用。
    /// </summary>
    /// <param name="portalId">已渲染门户的 ID。</param>
    public void NotifyPortalRendered(string portalId);

    /// <summary>
    /// 以指定 ID、内容与类别注册一个新门户。
    /// </summary>
    /// <param name="id">门户的唯一标识。</param>
    /// <param name="content">门户中渲染的内容。</param>
    /// <param name="category">门户类别（Container 或 Overlay）。</param>
    public void RegisterPortal(string id, RenderFragment content, PortalCategory category);

    /// <summary>
    /// 按 ID 注销门户，使其不再渲染。
    /// </summary>
    /// <param name="id">要移除的门户 ID。</param>
    public void UnregisterPortal(string id);

    /// <summary>
    /// 更新既有门户的内容。
    /// </summary>
    /// <param name="id">要更新的门户 ID。</param>
    /// <param name="content">新的渲染内容。</param>
    public void UpdatePortalContent(string id, RenderFragment content);

    /// <summary>
    /// 在不替换 RenderFragment 的前提下刷新门户。
    /// 触发一次重渲染，让既有片段拾取更新后的捕获值，且不产生新的 DOM 元素。
    /// </summary>
    /// <param name="id">要刷新的门户 ID。</param>
    public void RefreshPortal(string id);

    /// <summary>
    /// 获取指定类别的全部已注册门户，按插入顺序排列。
    /// </summary>
    /// <param name="category">用于筛选的类别。</param>
    /// <returns>有序的「门户 ID/内容」键值对列表。</returns>
    public IReadOnlyList<KeyValuePair<string, RenderFragment>> GetPortals(PortalCategory category);
}
