namespace AtomBlazor;

/// <summary>
/// 二维码每个深色模块所绘制的形状。
/// </summary>
/// <remarks>
/// 无论选择哪种形状，三个角上的定位图案始终保持实心方块，因为扫码器
/// 是先靠这些方块找到二维码，然后才读取其余内容。
/// </remarks>
public enum QrCodeModuleShape
{
    /// <summary>实心方块，相邻模块相接。扫码最可靠。</summary>
    Square,

    /// <summary>圆角方块。更柔和，同色连续段内仍保持相连。</summary>
    Rounded,

    /// <summary>独立圆点。装饰性最强而扫码可靠性最低。</summary>
    Dot,
}
