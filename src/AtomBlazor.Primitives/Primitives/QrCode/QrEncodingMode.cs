namespace AtomBlazor.Primitives.QrCode;

/// <summary>
/// 文本被打包进二维码比特流的方式。
/// </summary>
/// <remarks>
/// 编码器会选择能覆盖整个字符串的最紧凑模式，因此一串数字
/// 占用的空间大约只有按字节编码时的三分之一。
/// </remarks>
public enum QrEncodingMode
{
    /// <summary>仅数字。每字符约 3.33 比特。</summary>
    Numeric,

    /// <summary>数字、大写字母以及 <c>$%*+-./:</c> 与空格。每字符约 5.5 比特。</summary>
    Alphanumeric,

    /// <summary>任意文本，按 UTF-8 字节编码。每字节 8 比特。</summary>
    Byte,
}
