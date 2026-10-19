using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 把填充定义桥接给其父系列的容器组件。
/// </summary>
/// <remarks>
/// <para>
/// Fill 充当系列组件（Line、Bar、Area 等）与填充定义
/// （如 <see cref="LinearGradient"/>）之间的中介。填充组件
/// 向 Fill 注册之时，便把填充颜色/渐变施加给父系列。
/// </para>
/// </remarks>
/// <example>
/// <code>
/// &lt;Area DataKey="desktop"&gt;
///     &lt;Fill&gt;
///         &lt;LinearGradient&gt;
///             &lt;GradientStop Offset="0" Color="var(--chart-1)" Opacity="0.8" /&gt;
///             &lt;GradientStop Offset="1" Color="var(--chart-1)" Opacity="0.1" /&gt;
///         &lt;/LinearGradient&gt;
///     &lt;/Fill&gt;
/// &lt;/Area&gt;
/// </code>
/// </example>
public partial class Fill : ComponentBase
{
    [CascadingParameter]
    private SeriesBase? ParentSeries { get; set; }

    /// <summary>
    /// 获取或设置包含填充定义组件的子内容。
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    private IFillComponent? fillComponent;

    /// <summary>
    /// 由子填充定义组件调用，以完成自身注册。
    /// </summary>
    /// <param name="component">提供该填充定义的填充组件。</param>
    internal void RegisterFill(IFillComponent component)
    {
        fillComponent = component;

        // 设置惰性求值的提供者——BuildFill 在 BuildSeriesCore 期间
        //（OnAfterRenderAsync）被调用，届时全部子 GradientStop 都已存在。
        ParentSeries?.FillProvider = component;
    }
}
