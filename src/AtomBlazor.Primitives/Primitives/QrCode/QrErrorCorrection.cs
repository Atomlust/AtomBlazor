namespace AtomBlazor.Primitives.QrCode;

/// <summary>
/// 二维码可以丢失或被遮盖多少仍然能被读出。
/// </summary>
/// <remarks>
/// 等级越高能承受的损坏越多，但同样的数据需要更多模块，因此码会变大，
/// 或者数据必须缩减。<see cref="Medium"/> 是常用选择。当二维码
/// 要印得很小、印在易折皱的东西上，或被中心图案遮盖时，请调高等级。
/// </remarks>
public enum QrErrorCorrection
{
    /// <summary>可恢复约 7% 的码字。同一字符串下码型最小的等级。</summary>
    Low = 0,

    /// <summary>可恢复约 15% 的码字。默认等级。</summary>
    Medium = 1,

    /// <summary>可恢复约 25% 的码字。</summary>
    Quartile = 2,

    /// <summary>可恢复约 30% 的码字。耐损坏能力最强。</summary>
    High = 3,
}
