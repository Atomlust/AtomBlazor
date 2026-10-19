using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 可以互相叠放的系列类型的基类：柱状、折线、面积、散点
/// 与径向柱。
/// </summary>
/// <remarks>
/// 堆叠过去挂在每个系列都继承的 <see cref="SeriesBase"/> 上——于是仪表盘、
/// 雷达或热力图也挂着 <c>Stacked</c> 与 <c>StackGroup</c> 示人，尽管堆叠对它们
/// 毫无意义、也没有任何东西读取这些值。改在本类声明后，这些参数
/// 只出现在真正起作用的地方。
/// </remarks>
public abstract class StackableSeriesBase : SeriesBase
{
    /// <summary>
    /// 获取或设置本系列是否与其他系列叠放。
    /// </summary>
    /// <remarks>
    /// 为 true 时，各系列的值相互叠放。
    /// 用 <see cref="StackGroup"/> 控制哪些系列叠作一堆。
    /// </remarks>
    [Parameter]
    public bool Stacked { get; set; }

    /// <summary>
    /// 获取或设置堆叠组的标识。
    /// </summary>
    /// <remarks>
    /// StackGroup 值相同的系列叠作一堆。
    /// 默认为 "stack"。仅当 <see cref="Stacked"/> 为 true 时生效。
    /// </remarks>
    [Parameter]
    public string StackGroup { get; set; } = "stack";
}
