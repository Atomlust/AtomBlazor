using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 为 Pie（环形图）与 RadialBar 系列配置中心标签。
/// </summary>
/// <remarks>
/// <para>
/// 必须作为内半径非零的 <see cref="Pie"/> 或 <see cref="RadialBar"/> 组件的子组件
/// 置于其内部。本组件向父系列注册自身，以在 ECharts option 中
/// 配置居于中心的标签。
/// </para>
/// </remarks>
/// <example>
/// <code>
/// &lt;Pie DataKey="visitors" NameKey="browser" InnerRadius="60"&gt;
///     &lt;CenterLabel Title="Browsers" Value="5" /&gt;
/// &lt;/Pie&gt;
/// </code>
/// </example>
public partial class CenterLabel : ComponentBase
{
    [CascadingParameter]
    private SeriesBase? ParentSeries { get; set; }

    /// <summary>
    /// 获取或设置在中心显示的标题文字，位于数值下方。
    /// </summary>
    [Parameter]
    public string? Title { get; set; }

    /// <summary>
    /// 获取或设置在中心显示的值文字，位于标题上方。
    /// </summary>
    [Parameter]
    public string? Value { get; set; }

    /// <summary>
    /// 获取或设置显示在中心的标签文字。
    /// 设置后覆盖 <see cref="Title"/> 与 <see cref="Value"/>。
    /// </summary>
    [Parameter]
    public string? Text { get; set; }

    /// <summary>
    /// 获取或设置中心标签值的字号（像素）。
    /// </summary>
    [Parameter]
    public int FontSize { get; set; } = 24;

    /// <summary>
    /// 获取或设置中心标签值的字重。
    /// </summary>
    [Parameter]
    public string FontWeight { get; set; } = "bold";

    /// <summary>
    /// 获取中心标签计算出的展示文字。
    /// </summary>
    internal string DisplayText
    {
        get
        {
            if (!string.IsNullOrEmpty(Text))
            {
                return Text;
            }

            if (!string.IsNullOrEmpty(Title) && !string.IsNullOrEmpty(Value))
            {
                return $"{Title}\n{Value}";
            }

            return Title ?? Value ?? "";
        }
    }

    protected override void OnInitialized()
    {
        if (ParentSeries is Pie pie)
        {
            pie.SetCenterLabel(this);
        }
        else if (ParentSeries is RadialBar radialBar)
        {
            radialBar.SetCenterLabel(this);
        }
    }
}
