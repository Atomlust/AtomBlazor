using AtomBlazor.Demo.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;

namespace AtomBlazor.Demo.Shared;

/// <summary>
/// 页头控件，用于步进到上一个/下一个组件演示页。
/// </summary>
/// <remarks>
/// 顺序取自 <see cref="ComponentCatalog.DemoPages"/>——与侧栏、组件主页和命令搜索
/// 已经在走的扁平列表一致，因此“下一个”始终是读者在侧栏里能看到的下一行。
/// 该列表之外的页面不渲染分页器——主页、指南页或基元页没有有意义的邻居可提供。
/// </remarks>
public partial class DemoPager : IDisposable
{
    [Inject]
    private NavigationManager NavigationManager { get; set; } = null!;

    /// <summary>显示语言快照（MainLayout 级联下发；缺省回落中文）。</summary>
    [CascadingParameter] public DemoCultureState L { get; set; } = DemoCultureState.Default;


    private ComponentCatalog.Entry? _previous;
    private ComponentCatalog.Entry? _next;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        NavigationManager.LocationChanged += OnLocationChanged;
        UpdateNeighbours();
    }

    private void OnLocationChanged(object? sender, LocationChangedEventArgs e)
    {
        UpdateNeighbours();
        StateHasChanged();
    }

    private void UpdateNeighbours()
    {
        var pages = ComponentCatalog.DemoPages;
        var current = "/" + NavigationManager.ToBaseRelativePath(NavigationManager.Uri)
            .Split('?')[0].Split('#')[0].TrimEnd('/');

        var index = -1;
        for (var i = 0; i < pages.Count; i++)
        {
            if (string.Equals(pages[i].Url, current, StringComparison.OrdinalIgnoreCase))
            {
                index = i;
                break;
            }
        }

        _previous = index > 0 ? pages[index - 1] : null;
        _next = index >= 0 && index < pages.Count - 1 ? pages[index + 1] : null;
    }

    private void Go(ComponentCatalog.Entry? entry)
    {
        if (entry is not null)
        {
            NavigationManager.NavigateTo(entry.Url);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        NavigationManager.LocationChanged -= OnLocationChanged;
        GC.SuppressFinalize(this);
    }
}
