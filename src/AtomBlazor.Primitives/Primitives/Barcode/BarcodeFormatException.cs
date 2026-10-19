namespace AtomBlazor.Primitives.Barcode;

/// <summary>
/// 码制无法承载的值。
/// </summary>
/// <remarks>
/// <para>
/// 它是 <see cref="ArgumentException"/>，现有 catch 逻辑照常工作——但它的
/// <see cref="Message"/> 恰好就是传入的那句话，不追加任何内容。
/// </para>
/// <para>
/// 这正是它存在的全部理由。<see cref="ArgumentException.Message"/> 会把消息
/// 与参数名拼接，而该后缀的措辞来自框架的
/// 资源字符串。在 WebAssembly 上这些资源常被常规裁剪掉，取而代之
/// 出现的是资源键——于是一个坏值的条形码曾在页面上印出
/// <c>Arg_ParamName_Name</c>。这些消息是写给输入该值的人看的，
/// 因此消息必须完全由我们掌控，并在每个宿主上说出同样的话。
/// </para>
/// <para>
/// 参数名仍保留在 <see cref="ArgumentException.ParamName"/> 上供需要的代码使用，
/// 只是不再出现在文本里。
/// </para>
/// </remarks>
public sealed class BarcodeFormatException : ArgumentException
{
    private readonly string text;

    /// <summary>创建异常。</summary>
    /// <param name="message">以展示给他人的句子说明错在何处。</param>
    /// <param name="paramName">出错的参数。</param>
    public BarcodeFormatException(string message, string? paramName)
        : base(message, paramName)
    {
        text = message;
    }

    /// <summary>创建异常。</summary>
    /// <param name="message">以展示给他人的句子说明错在何处。</param>
    public BarcodeFormatException(string message)
        : base(message)
    {
        text = message;
    }

    /// <summary>创建异常。</summary>
    public BarcodeFormatException()
    {
        text = base.Message;
    }

    /// <summary>创建异常。</summary>
    /// <param name="message">以展示给他人的句子说明错在何处。</param>
    /// <param name="innerException">引发本异常的内部异常。</param>
    public BarcodeFormatException(string message, Exception innerException)
        : base(message, innerException)
    {
        text = message;
    }

    /// <summary>获取错误说明，只有一句话、别无其他。</summary>
    public override string Message => text;
}
