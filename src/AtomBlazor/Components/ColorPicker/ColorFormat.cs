namespace AtomBlazor;

/// <summary>
/// 定义 ColorPicker 使用的颜色格式。
/// </summary>
public enum ColorFormat
{
    /// <summary>
    /// 十六进制格式（#RRGGBB 或 #RRGGBBAA）。
    /// </summary>
    Hex,

    /// <summary>
    /// RGB 格式（rgb(r, g, b) 或 rgba(r, g, b, a)）。
    /// </summary>
    RGB,

    /// <summary>
    /// HSL 格式（hsl(h, s%, l%) 或 hsla(h, s%, l%, a)）。
    /// </summary>
    HSL
}
