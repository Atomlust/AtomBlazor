namespace AtomBlazor;

/// <summary>
/// 定义 <see cref="Image"/> 如何填充被分配到的空间。
/// </summary>
public enum ImageFit
{
    /// <summary>不施加 object-fit；图片保持自身尺寸。默认值。</summary>
    None,

    /// <summary>盖满盒子并裁掉溢出。固定尺寸缩略图的常用选择。</summary>
    Cover,

    /// <summary>完整收入盒子，宁可留白也不裁剪。</summary>
    Contain,

    /// <summary>拉伸到盒子尺寸，会扭曲图片。</summary>
    Fill,

    /// <summary>类似 <see cref="Contain"/>，但绝不把图片放大到超过其原始尺寸。</summary>
    ScaleDown
}
