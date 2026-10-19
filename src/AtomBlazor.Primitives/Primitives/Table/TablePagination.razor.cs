using Microsoft.AspNetCore.Components;

namespace AtomBlazor.Primitives;

/// <summary>
/// 无头（headless）分页组件，为表格提供分页控件。
/// 标记与样式完全由开发者掌控。
/// </summary>
/// <typeparam name="TData">表格中展示的数据类型。</typeparam>
public partial class TablePagination<TData> : ComponentBase, IDisposable
{
    private bool _disposed;
    private PaginationContext _context = default!;

    /// <summary>
    /// 获取或设置要控制与展示的分页状态。
    /// </summary>
    [Parameter]
    public PaginationState State { get; set; } = default!;

    /// <summary>
    /// 获取或设置页变化时调用的回调。
    /// </summary>
    [Parameter]
    public EventCallback<int> OnPageChange { get; set; }

    /// <summary>
    /// 获取或设置页大小变化时调用的回调。
    /// </summary>
    [Parameter]
    public EventCallback<int> OnPageSizeChange { get; set; }

    /// <summary>
    /// 获取或设置选择器中可选的页大小。
    /// 默认值：[10, 25, 50, 100]
    /// </summary>
    [Parameter]
    public int[] PageSizeOptions { get; set; } = [10, 25, 50, 100];

    /// <summary>
    /// 获取或设置是否显示页大小选择器。
    /// 默认值：true
    /// </summary>
    [Parameter]
    public bool ShowPageSizeSelector { get; set; } = true;

    /// <summary>
    /// 获取或设置是否显示页面信息（例如 "1-10 of 100 items"）。
    /// 默认值：true
    /// </summary>
    [Parameter]
    public bool ShowPageInfo { get; set; } = true;

    /// <summary>
    /// 获取或设置自定义分页模板。
    /// 该模板可完全掌控分页的标记。
    /// </summary>
    [Parameter]
    public RenderFragment<PaginationContext>? PaginationTemplate { get; set; }

    /// <summary>
    /// 获取或设置子内容（未提供 PaginationTemplate 时使用）。
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// 获取或设置应用到容器的额外 CSS 类。
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置导航地标（landmark）的标签。
    /// </summary>
    [Parameter]
    public string PaginationAriaLabel { get; set; } = "Pagination Navigation";

    /// <summary>
    /// 获取或设置第一页按钮的标签。
    /// </summary>
    [Parameter]
    public string FirstPageLabel { get; set; } = "First";

    /// <summary>
    /// 获取或设置第一页按钮的 aria-label。
    /// </summary>
    [Parameter]
    public string FirstPageAriaLabel { get; set; } = "Go to first page";

    /// <summary>
    /// 获取或设置上一页按钮的标签。
    /// </summary>
    [Parameter]
    public string PreviousPageLabel { get; set; } = "Previous";

    /// <summary>
    /// 获取或设置上一页按钮的 aria-label。
    /// </summary>
    [Parameter]
    public string PreviousPageAriaLabel { get; set; } = "Go to previous page";

    /// <summary>
    /// 获取或设置下一页按钮的标签。
    /// </summary>
    [Parameter]
    public string NextPageLabel { get; set; } = "Next";

    /// <summary>
    /// 获取或设置下一页按钮的 aria-label。
    /// </summary>
    [Parameter]
    public string NextPageAriaLabel { get; set; } = "Go to next page";

    /// <summary>
    /// 获取或设置最后一页按钮的标签。
    /// </summary>
    [Parameter]
    public string LastPageLabel { get; set; } = "Last";

    /// <summary>
    /// 获取或设置最后一页按钮的 aria-label。
    /// </summary>
    [Parameter]
    public string LastPageAriaLabel { get; set; } = "Go to last page";

    /// <summary>
    /// 获取或设置"每页条数"的标签。
    /// </summary>
    [Parameter]
    public string ItemsPerPageLabel { get; set; } = "Items per page:";

    /// <summary>
    /// 获取或设置页大小选择器的 aria-label。
    /// </summary>
    [Parameter]
    public string PageSizeSelectorAriaLabel { get; set; } = "Select page size";

    /// <summary>
    /// 获取或设置生成页面信息文本的函数。
    /// 接收 (currentPage, totalPages)，返回展示文本。
    /// </summary>
    [Parameter]
    public Func<int, int, string>? PageInfoFormat { get; set; }

    private string GetPageInfoLabel() => PageInfoFormat?.Invoke(State.CurrentPage, State.TotalPages)
        ?? $"Page {State.CurrentPage} of {State.TotalPages}";

    /// <summary>
    /// 获取提供给模板的分页上下文。
    /// </summary>
    private PaginationContext Context => _context;

    /// <summary>
    /// 初始化组件并创建分页上下文。
    /// </summary>
    protected override void OnParametersSet()
    {
        _context = new PaginationContext
        {
            State = State,
            PageSizeOptions = PageSizeOptions,
            ShowPageSizeSelector = ShowPageSizeSelector,
            ShowPageInfo = ShowPageInfo,
            GoToFirstPage = HandleFirstPage,
            GoToPreviousPage = HandlePreviousPage,
            GoToNextPage = HandleNextPage,
            GoToLastPage = HandleLastPage,
            GoToPage = HandleGoToPage,
            ChangePageSize = HandlePageSizeChange
        };
    }

    /// <summary>
    /// 处理导航到第一页。
    /// </summary>
    private async Task HandleFirstPage()
    {
        if (State.CurrentPage == 1)
        {
            return;
        }

        State.FirstPage();
        await NotifyPageChange();
    }

    /// <summary>
    /// 处理导航到上一页。
    /// </summary>
    private async Task HandlePreviousPage()
    {
        if (!State.CanGoPrevious)
        {
            return;
        }

        State.PreviousPage();
        await NotifyPageChange();
    }

    /// <summary>
    /// 处理导航到下一页。
    /// </summary>
    private async Task HandleNextPage()
    {
        if (!State.CanGoNext)
        {
            return;
        }

        State.NextPage();
        await NotifyPageChange();
    }

    /// <summary>
    /// 处理导航到最后一页。
    /// </summary>
    private async Task HandleLastPage()
    {
        if (State.CurrentPage == State.TotalPages)
        {
            return;
        }

        State.LastPage();
        await NotifyPageChange();
    }

    /// <summary>
    /// 处理导航到指定页。
    /// </summary>
    /// <param name="page">要导航到的页码（从 1 开始）。</param>
    private async Task HandleGoToPage(int page)
    {
        if (State.CurrentPage == page)
        {
            return;
        }

        State.GoToPage(page);
        await NotifyPageChange();
    }

    /// <summary>
    /// 处理页大小变化。
    /// </summary>
    /// <param name="newPageSize">新的页大小。</param>
    private async Task HandlePageSizeChange(int newPageSize)
    {
        if (State.PageSize == newPageSize)
        {
            return;
        }

        State.PageSize = newPageSize;

        if (OnPageSizeChange.HasDelegate)
        {
            await OnPageSizeChange.InvokeAsync(newPageSize);
        }

        await NotifyPageChange();
    }

    /// <summary>
    /// 通知监听者页面已变化。
    /// </summary>
    private async Task NotifyPageChange()
    {
        if (OnPageChange.HasDelegate)
        {
            await OnPageChange.InvokeAsync(State.CurrentPage);
        }

        if (!_disposed)
        {
            StateHasChanged();
        }
    }

    /// <summary>
    /// 释放组件使用的资源。
    /// </summary>
    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _disposed = true;
    }
}

/// <summary>
/// 提供给分页模板的上下文。
/// </summary>
public class PaginationContext
{
    /// <summary>
    /// 获取或设置分页状态。
    /// </summary>
    public PaginationState State { get; set; } = default!;

    /// <summary>
    /// 获取或设置可选的页大小列表。
    /// </summary>
    public int[] PageSizeOptions { get; set; } = [];

    /// <summary>
    /// 获取或设置是否显示页大小选择器。
    /// </summary>
    public bool ShowPageSizeSelector { get; set; }

    /// <summary>
    /// 获取或设置是否显示页面信息。
    /// </summary>
    public bool ShowPageInfo { get; set; }

    /// <summary>
    /// 获取导航到第一页的操作。
    /// </summary>
    public Func<Task> GoToFirstPage { get; set; } = default!;

    /// <summary>
    /// 获取导航到上一页的操作。
    /// </summary>
    public Func<Task> GoToPreviousPage { get; set; } = default!;

    /// <summary>
    /// 获取导航到下一页的操作。
    /// </summary>
    public Func<Task> GoToNextPage { get; set; } = default!;

    /// <summary>
    /// 获取导航到最后一页的操作。
    /// </summary>
    public Func<Task> GoToLastPage { get; set; } = default!;

    /// <summary>
    /// 获取导航到指定页的操作。
    /// </summary>
    public Func<int, Task> GoToPage { get; set; } = default!;

    /// <summary>
    /// 获取更改页大小的操作。
    /// </summary>
    public Func<int, Task> ChangePageSize { get; set; } = default!;

    /// <summary>
    /// 获取页面信息文本（例如 "1-10 of 100 items"）。
    /// </summary>
    public string GetPageInfoText()
    {
        if (State.TotalItems == 0)
        {
            return "No items";
        }


        return $"{State.FirstItemIndex}-{State.LastItemIndex} of {State.TotalItems} items";
    }

    /// <summary>
    /// 获取用于展示页码的范围（例如 "1 2 3 ... 10" 式分页）。
    /// </summary>
    /// <param name="maxPages">要显示的页码按钮最大数量。</param>
    /// <returns>要展示的页码数组。</returns>
    public int[] GetPageRange(int maxPages = 5)
    {
        if (State.TotalPages <= maxPages)
        {
            return Enumerable.Range(1, State.TotalPages).ToArray();
        }

        var halfMax = maxPages / 2;
        var startPage = Math.Max(1, State.CurrentPage - halfMax);
        var endPage = Math.Min(State.TotalPages, startPage + maxPages - 1);

        if (endPage - startPage < maxPages - 1)
        {
            startPage = Math.Max(1, endPage - maxPages + 1);
        }

        return Enumerable.Range(startPage, endPage - startPage + 1).ToArray();
    }
}
