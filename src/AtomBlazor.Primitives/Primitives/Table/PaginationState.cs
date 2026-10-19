namespace AtomBlazor.Primitives;

/// <summary>
/// 管理表格的分页配置与状态。
/// 负责页码导航、页大小与边界计算。
/// </summary>
public class PaginationState
{
    private int currentPage = 1;
    private int pageSize = 10;
    private int totalItems;

    /// <summary>
    /// 获取或设置当前页码（从 1 开始）。
    /// 至少钳制为 1。如需同时钳制到 TotalPages，请使用 GoToPage。
    /// </summary>
    public int CurrentPage
    {
        get => currentPage;
        set => currentPage = Math.Max(1, value);
    }

    /// <summary>
    /// 获取或设置每页条目数。
    /// 至少为 1。
    /// </summary>
    public int PageSize
    {
        get => pageSize;
        set
        {
            pageSize = Math.Max(1, value);
            // 页大小变化时回到第 1 页
            currentPage = 1;
        }
    }

    /// <summary>
    /// 获取或设置数据集中的条目总数。
    /// 在数据处理期间自动更新。
    /// </summary>
    public int TotalItems
    {
        get => totalItems;
        set
        {
            totalItems = Math.Max(0, value);
            // 将当前页钳制到有效范围
            if (currentPage > TotalPages && TotalPages > 0)
            {
                currentPage = TotalPages;
            }
        }
    }

    /// <summary>
    /// 获取按条目总数与页大小计算出的总页数。
    /// </summary>
    public int TotalPages => TotalItems == 0 ? 0 : (int)Math.Ceiling(TotalItems / (double)PageSize);

    /// <summary>
    /// 获取当前页的零基起始索引。
    /// </summary>
    public int StartIndex => (CurrentPage - 1) * PageSize;

    /// <summary>
    /// 获取当前页的零基结束索引（不含）。
    /// </summary>
    public int EndIndex => Math.Min(StartIndex + PageSize, TotalItems);

    /// <summary>
    /// 获取是否可以导航到下一页。
    /// </summary>
    public bool CanGoNext => CurrentPage < TotalPages;

    /// <summary>
    /// 获取是否可以导航到上一页。
    /// </summary>
    public bool CanGoPrevious => CurrentPage > 1;

    /// <summary>
    /// 获取当前页第一个条目的一基索引。
    /// </summary>
    public int FirstItemIndex => TotalItems == 0 ? 0 : StartIndex + 1;

    /// <summary>
    /// 获取当前页最后一个条目的一基索引。
    /// </summary>
    public int LastItemIndex => EndIndex;

    /// <summary>
    /// 若可以，导航到下一页。
    /// </summary>
    public void NextPage()
    {
        if (CanGoNext)
        {
            CurrentPage++;
        }
    }

    /// <summary>
    /// 若可以，导航到上一页。
    /// </summary>
    public void PreviousPage()
    {
        if (CanGoPrevious)
        {
            CurrentPage--;
        }
    }

    /// <summary>
    /// 导航到第一页。
    /// </summary>
    public void FirstPage() => CurrentPage = 1;

    /// <summary>
    /// 导航到最后一页。
    /// </summary>
    public void LastPage()
    {
        if (TotalPages > 0)
        {
            CurrentPage = TotalPages;
        }
    }

    /// <summary>
    /// 导航到指定页。
    /// 页码会自动钳制到有效范围。
    /// </summary>
    /// <param name="page">要导航到的页码（从 1 开始）。</param>
    public void GoToPage(int page) => CurrentPage = Math.Max(1, Math.Min(page, TotalPages > 0 ? TotalPages : 1));

    /// <summary>
    /// 将分页重置到第一页。
    /// 适用于数据或筛选条件变化时。
    /// </summary>
    public void Reset() => CurrentPage = 1;
}
