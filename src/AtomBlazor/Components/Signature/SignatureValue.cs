namespace AtomBlazor;

/// <summary>
/// 一份已捕获的签名，无论它以何种方式给出。
/// </summary>
public sealed record SignatureValue
{
    /// <summary>
    /// 获取签名的给出方式。
    /// </summary>
    public SignatureKind Kind { get; init; } = SignatureKind.None;

    /// <summary>
    /// 获取签名的 SVG 标记，没有签名时为 <c>null</c>。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 墨迹为 <c>currentColor</c>，所以同一份标记在纸面上呈深色、在深色页面上呈浅色。
    /// 应存储的是这一形式。
    /// </para>
    /// <para>
    /// 输入式签名是单个带字体栈的 <c>&lt;text&gt;</c> 元素而非轮廓，因此它会以浏览机器上
    /// 装配的手写字体渲染。在意这一点的场合，请另存一份渲染后的图片。
    /// </para>
    /// </remarks>
    public string? Svg { get; init; }

    /// <summary>
    /// 获取输入的姓名，当 <see cref="Kind"/> 为 <see cref="SignatureKind.Typed"/> 时有值。
    /// </summary>
    public string? Text { get; init; }

    /// <summary>
    /// 获取是否尚未签名。
    /// </summary>
    public bool IsEmpty => Kind == SignatureKind.None;

    /// <summary>
    /// 一个未签署的值。
    /// </summary>
    public static SignatureValue Empty { get; } = new();
}
