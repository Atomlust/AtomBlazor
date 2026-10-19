using System.Globalization;

namespace AtomBlazor.Primitives;

/// <summary>
/// <see cref="DirectionProvider"/> 向后代级联的书写方向。
/// </summary>
/// <remarks>
/// <para>
/// 布局经 CSS 跟随方向：本库样式使用逻辑属性（<c>margin-inline-start</c> 而非
/// <c>margin-left</c>），所以 provider 写出的 <c>dir</c> 特性足以完成镜像。
/// 本对象服务于 CSS 无法决定的部分——例如方向键应向哪边移动——也服务于需要与
/// 渲染出该标记的 C# 得到同一答案的 JavaScript。
/// </para>
/// <para>
/// 组件通过级联参数读取它，并用 <see cref="Resolve(DirectionContext?)"/> 求解，
/// 于是没有 provider 时组件依旧按老行为运作：跟随区域文化。
/// </para>
/// </remarks>
public sealed class DirectionContext
{
    /// <summary>配置的方向原值，<see cref="TextDirection.Auto"/> 尚未求解。</summary>
    public required TextDirection Direction { get; init; }

    /// <summary>
    /// 求解后的方向是否为右到左。<see cref="TextDirection.Auto"/> 依据
    /// <see cref="CultureInfo.CurrentCulture"/> 求解。
    /// </summary>
    public bool IsRightToLeft => Direction switch
    {
        TextDirection.RightToLeft => true,
        TextDirection.LeftToRight => false,
        _ => CultureInfo.CurrentCulture.TextInfo.IsRightToLeft
    };

    /// <summary>HTML <c>dir</c> 特性的取值：<c>"rtl"</c> 或 <c>"ltr"</c>。</summary>
    public string Attribute => IsRightToLeft ? "rtl" : "ltr";

    /// <summary>
    /// 解析级联来的上下文；无 provider 时回退到当前区域文化。
    /// 组件应当调用这个方法，无需自己判空。
    /// </summary>
    /// <param name="context">级联来的上下文；无 provider 时为 <c>null</c>。</param>
    public static bool Resolve(DirectionContext? context) =>
        context?.IsRightToLeft ?? CultureInfo.CurrentCulture.TextInfo.IsRightToLeft;
}
