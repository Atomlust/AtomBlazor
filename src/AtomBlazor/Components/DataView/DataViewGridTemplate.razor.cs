using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 定义 DataView 网格布局模式的项目渲染模板。
/// 将本组件放置在 DataView 内，指定网格模式下每个项目的渲染方式。
/// 与 DataViewListTemplate 配合使用可启用工具栏布局切换。
/// </summary>
/// <typeparam name="TItem">视图中的数据项类型。</typeparam>
/// <remarks>
/// <para>
/// DataViewGridTemplate 捕获一个强类型渲染片段，DataView 用它渲染网格布局中的
/// 每个项目。当仅放置 DataViewGridTemplate（没有 DataViewListTemplate）时，
/// 视图锁定为网格模式并隐藏布局切换按钮。
/// 同时提供 DataViewListTemplate 与 DataViewGridTemplate 才能启用切换。
/// </para>
/// </remarks>
/// <example>
/// <code>
/// &lt;DataView TItem="Product" Data="@products"&gt;
///     &lt;DataViewGridTemplate TItem="Product" Context="product"&gt;
///         &lt;div class="p-4 border rounded-lg"&gt;@product.Name&lt;/div&gt;
///     &lt;/DataViewGridTemplate&gt;
/// &lt;/DataView&gt;
/// </code>
/// </example>
public partial class DataViewGridTemplate<TItem> : ComponentBase where TItem : class
{
    /// <summary>
    /// 获取或设置用于在网格布局中显示每个项目的渲染片段。
    /// 上下文参数提供类型为 TItem 的数据项。
    /// </summary>
    [Parameter]
    public RenderFragment<TItem>? ChildContent { get; set; }

    /// <summary>
    /// 获取或设置父级 DataView 组件。
    /// 通过级联参数自动设置。
    /// </summary>
    [CascadingParameter]
    internal DataView<TItem>? ParentView { get; set; }

    protected override void OnInitialized()
    {
        if (ParentView == null)
        {
            throw new InvalidOperationException(
                $"{nameof(DataViewGridTemplate<TItem>)} must be placed inside a {nameof(DataView<TItem>)} component.");
        }

        ParentView.SetGridTemplate(ChildContent);
    }
}
