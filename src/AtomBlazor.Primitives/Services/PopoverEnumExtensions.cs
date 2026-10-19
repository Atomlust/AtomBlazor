namespace AtomBlazor.Primitives.Services;

/// <summary>
/// 把浮层相关枚举转换为字符串表示的扩展方法。
/// </summary>
public static class PopoverEnumExtensions
{
    /// <summary>
    /// 把 <see cref="PopoverSide"/> 转为小写字符串。
    /// </summary>
    public static string ToValue(this PopoverSide side) => side switch
    {
        PopoverSide.Top => "top",
        PopoverSide.Right => "right",
        PopoverSide.Bottom => "bottom",
        PopoverSide.Left => "left",
        _ => "bottom"
    };

    /// <summary>
    /// 把 <see cref="PopoverAlign"/> 转为小写字符串。
    /// </summary>
    public static string ToValue(this PopoverAlign align) => align switch
    {
        PopoverAlign.Start => "start",
        PopoverAlign.Center => "center",
        PopoverAlign.End => "end",
        _ => "center"
    };

    /// <summary>
    /// 把 <see cref="PositioningStrategy"/> 转为小写字符串。
    /// </summary>
    public static string ToValue(this PositioningStrategy strategy) => strategy switch
    {
        PositioningStrategy.Absolute => "absolute",
        PositioningStrategy.Fixed => "fixed",
        _ => "absolute"
    };

    /// <summary>
    /// 把 <see cref="PopoverPlacement"/> 转为 kebab-case 字符串。
    /// </summary>
    public static string ToValue(this PopoverPlacement placement) => placement switch
    {
        PopoverPlacement.Top => "top",
        PopoverPlacement.TopStart => "top-start",
        PopoverPlacement.TopEnd => "top-end",
        PopoverPlacement.Right => "right",
        PopoverPlacement.RightStart => "right-start",
        PopoverPlacement.RightEnd => "right-end",
        PopoverPlacement.Bottom => "bottom",
        PopoverPlacement.BottomStart => "bottom-start",
        PopoverPlacement.BottomEnd => "bottom-end",
        PopoverPlacement.Left => "left",
        PopoverPlacement.LeftStart => "left-start",
        PopoverPlacement.LeftEnd => "left-end",
        _ => "bottom"
    };
}
