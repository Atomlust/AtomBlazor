using Microsoft.AspNetCore.Components;

namespace AtomBlazor.Primitives.Services;

/// <summary>
/// 一个已注册门户：内容、类别与插入顺序。
/// 插入顺序保证同一类别内父门户先于子门户渲染。
/// </summary>
internal sealed class PortalEntry
{
    /// <summary>
    /// 门户的 RenderFragment 内容。
    /// </summary>
    public required RenderFragment Content { get; set; }

    /// <summary>
    /// 该门户所属的类别。
    /// </summary>
    public required PortalCategory Category { get; init; }

    /// <summary>
    /// 注册时分配的单调递增顺序值。
    /// 遍历同一类别的门户时用它维持插入顺序。
    /// </summary>
    public required long Order { get; init; }
}
