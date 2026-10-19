using AtomBlazor.Primitives.Utilities;
using Microsoft.AspNetCore.Components;

namespace AtomBlazor.Primitives;

/// <summary>
/// 计算内联样式、并需要把计算结果与消费者通过 <see cref="AdditionalAttributes"/>
/// 传入的样式合并的基元组件基类。
/// <para>
/// Blazor 的 <c>@attributes</c> 展开（splat）会在消费者同时通过 <c>AdditionalAttributes</c>
/// 传入 <c>style</c> 时覆盖显式写出的 <c>style</c> 特性。
/// 本基类提供 <see cref="MergeStyles"/> 与 <see cref="FilteredAttributes"/>，
/// 用于在没有冲突的前提下安全地合并组件计算样式与用户样式。
/// </para>
/// </summary>
public abstract class PrimitiveBase : ComponentBase
{
    /// <summary>
    /// 要应用到根元素的附加 HTML 特性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>
    /// 把组件自身计算出的样式与 <see cref="AdditionalAttributes"/> 中消费者提供的
    /// <c>style</c> 合并。
    /// </summary>
    /// <param name="computedStyle">组件的功能性样式（定位、尺寸等）。</param>
    /// <returns>
    /// 合并后的样式字符串；若计算样式与用户样式均为空则返回 <c>null</c>。
    /// </returns>
    protected string? MergeStyles(string? computedStyle) =>
        InlineStyleMerge.Merge(computedStyle, AdditionalAttributes);

    /// <summary>
    /// 返回移除了 <c>style</c> 键的 <see cref="AdditionalAttributes"/>，
    /// 防止用户的样式经 <c>@attributes</c> 展开后覆盖已合并的样式。
    /// </summary>
    protected Dictionary<string, object>? FilteredAttributes =>
        InlineStyleMerge.WithoutStyle(AdditionalAttributes);
}
