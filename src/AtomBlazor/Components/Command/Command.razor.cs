using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

public partial class Command
{
    private CommandContext _context = new();
    private string _searchQuery = string.Empty;

    /// <summary>
    /// 获取或设置应用到命令容器的额外 CSS 类。
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置在命令内部渲染的内容。
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// 获取或设置当前搜索查询。
    /// </summary>
#pragma warning disable BL0007 // 需要自定义 setter 与上下文同步
    [Parameter]
    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (_searchQuery != value)
            {
                _searchQuery = value;
                _context.SetSearchQuery(value);
            }
        }
    }
#pragma warning restore BL0007

    /// <summary>
    /// 搜索查询变化时调用的事件回调。
    /// </summary>
    [Parameter]
    public EventCallback<string> SearchQueryChanged { get; set; }

    /// <summary>
    /// 项被选中时调用的事件回调。
    /// </summary>
    [Parameter]
    public EventCallback<string> OnValueChange { get; set; }

    /// <summary>
    /// 项的自定义过滤函数。
    /// </summary>
    [Parameter]
    public Func<CommandItemMetadata, string, bool>? FilterFunction { get; set; }

    /// <summary>
    /// 命令是否禁用。禁用的命令忽略键盘、忽略对其项的点击，并禁用其输入框。
    /// </summary>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>
    /// 获取或设置应用到组件的额外属性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>
    /// 获取命令容器计算后的 CSS 类。
    /// </summary>
    private string CssClass => ClassNames.cn(
        "atom:flex atom:h-full atom:w-full atom:flex-col atom:overflow-hidden atom:rounded-md atom:bg-popover atom:text-popover-foreground",
        Disabled ? "atom:pointer-events-none atom:opacity-50" : null,
        Class
    );

    protected override void OnInitialized()
    {
        _context.OnValueChange = OnValueChange;
        _context.FilterFunction = FilterFunction;
        _context.Disabled = Disabled;
        _context.OnStateChanged += HandleContextStateChanged;
    }

    protected override void OnParametersSet()
    {
        _context.OnValueChange = OnValueChange;
        _context.FilterFunction = FilterFunction;
        _context.Disabled = Disabled;

        // 从参数同步搜索查询
        if (_context.SearchQuery != _searchQuery)
        {
            _context.SetSearchQuery(_searchQuery);
        }
    }

    private async void HandleContextStateChanged()
    {
        try
        {
            // 把搜索查询同步回参数
            if (_searchQuery != _context.SearchQuery)
            {
                _searchQuery = _context.SearchQuery;
                if (SearchQueryChanged.HasDelegate)
                {
                    await InvokeAsync(async () => await SearchQueryChanged.InvokeAsync(_searchQuery));
                }
            }
            await InvokeAsync(StateHasChanged);
        }
        catch (Exception)
        {
            // async void：无人 await 这个方法，逃逸的异常没有调用方可达，
            // Blazor Server 会将其视为致命错误——电路关闭，用户看到重连遮罩。
            // 这个方法做的一切都是尽力而为，没有哪一件值得付出那种代价。
        }
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _context.OnStateChanged -= HandleContextStateChanged;
    }
}
