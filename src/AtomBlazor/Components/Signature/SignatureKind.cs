namespace AtomBlazor;

/// <summary>
/// 签名的给出方式。
/// </summary>
public enum SignatureKind
{
    /// <summary>
    /// 尚未签名。
    /// </summary>
    None,

    /// <summary>
    /// 用指针、手指或手写笔手绘而成。
    /// </summary>
    Drawn,

    /// <summary>
    /// 以键盘输入的姓名。给无法手绘的人留的路。
    /// </summary>
    Typed
}
