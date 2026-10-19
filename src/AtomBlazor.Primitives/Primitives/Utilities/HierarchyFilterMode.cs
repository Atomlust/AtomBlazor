namespace AtomBlazor.Primitives.Utilities;

/// <summary>
/// 控制层级筛选如何处理匹配节点的后代。
/// </summary>
public enum HierarchyFilterMode
{
    /// <summary>
    /// 当某个父节点匹配筛选时，显示其子树结构并让后代作为上下文可见。
    /// 筛选仍然生效——未通过筛选的后代会被排除，但作为中间层的非匹配节点
    /// 会以较低的不透明度显示，以保留树结构。这是默认行为，适用于
    /// 较高层的匹配意味着整个子树相关、但单个条目仍应被过滤的场合
    /// （例如：在匹配的 VP 之下按 “Engineer” 筛选时，会展示组织层级结构，
    /// 同时仍然排除 Intern）。
    /// </summary>
    ShowMatchedSubtree,

    /// <summary>
    /// 只展示直接匹配筛选的条目及其祖先作为上下文。
    /// 匹配条目的后代不会被自动纳入，除非它们本身也匹配。
    /// 适用于严格逐项筛选的场合（例如按职位头衔筛选时，
    /// 应只显示拥有该特定头衔的人）。
    /// </summary>
    ShowMatchedOnly
}
