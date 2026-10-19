namespace AtomBlazor;

/// <summary>
/// 指定 InputOTP 组件接受的字符类型。
/// </summary>
public enum InputOTPInputMode
{
    /// <summary>
    /// 仅接受数字（0-9）。
    /// </summary>
    Numbers,

    /// <summary>
    /// 仅接受字母（a-z、A-Z）。
    /// </summary>
    Letters,

    /// <summary>
    /// 字母与数字均可接受。
    /// </summary>
    LettersAndNumbers
}
