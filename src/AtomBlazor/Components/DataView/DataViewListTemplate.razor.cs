using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 定义 DataView 列表布局模式的项目渲染模板。
/// 将本组件放置在 DataView 内，指定列表模式下每个项目的渲染方式。
/// 与 DataViewGridTemplate 配合使用可启用工具栏布局切换。
/// </summary>
/// <typeparam name="TItem">视图中的数据项类型。</typeparam>
/// <remarks>
/// <para>
/// DataViewListTemplate 捕获一个强类型渲染片段，DataView 用它渲染列表布局中的
/// 每个项目。当仅放置 DataViewListTemplate（没有 DataViewGridTemplate）时，
/// 视图锁定为列表模式并隐藏布局切换按钮。
/// 同时提供 DataViewListTemplate 与 DataViewGridTemplate 才能启用切换。
/// </para>
/// </remarks>
/// <example>
/// <code>
/// &lt;DataView TItem="Person" Data="@people"&gt;
///     &lt;DataViewListTemplate TItem="Person" Context="person"&gt;
///         &lt;div class="flex items-center gap-3 p-4"&gt;@person.Name&lt;/div&gt;
///     &lt;/DataViewListTemplate&gt;
/// &lt;/DataView&gt;
/// </code>
/// </example>
public partial class DataViewListTemplate<TItem> : ComponentBase where TItem : class
{
    /// <summary>
    /// 获取或设置用于在列表布局中显示每个项目的渲染片段。
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
                $"{nameof(DataViewListTemplate<TItem>)} must be placed inside a {nameof(DataView<TItem>)} component.");
        }

        ParentView.SetListTemplate(ChildContent);
    }
}
