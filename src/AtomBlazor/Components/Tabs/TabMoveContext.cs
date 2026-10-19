namespace AtomBlazor;

/// <summary>
/// 阅读者刚刚拖拽、或用 Ctrl 加方向键移到了新位置的一个标签。
/// </summary>
/// <remarks>
/// 列表不会自行重排，因为只有调用方知道自己保存的那个顺序是一份列表、一个排序字段
/// 还是数据库里的一列。把这次移动应用到标签页所依据的那个集合上，或者什么都不做——
/// 两种情形下标签页都从那个集合绘制，所以拒绝应用
/// <em>就是</em>拒绝本身。这里刻意没有 Cancel 标志，因为并不存在可供撤销的
/// 乐观状态。
/// </remarks>
public sealed class TabMoveContext
{
    /// <summary>获取标识该移动了的标签的值。</summary>
    public required string Value { get; init; }

    /// <summary>获取移动前该标签所占的位置，从零开始计数。</summary>
    public required int OldIndex { get; init; }

    /// <summary>获取该标签被要求占据的位置，从零开始计数。</summary>
    /// <remarks>
    /// 已求解为最终下标而非插入点，所以应用这次移动就是一次移除再在该数字处插入，
    /// 绝不会出现 off-by-one。
    /// </remarks>
    public required int NewIndex { get; init; }
}
