namespace AtomBlazor;

/// <summary>
/// 定义 Avatar 组件的尺寸变体。
/// </summary>
/// <remarks>
/// Avatar 尺寸控制头像容器及其内容（图片或回退文本）的宽度、高度与字号。
/// </remarks>
public enum AvatarSize
{
    /// <summary>
    /// 小号头像（32px）。
    /// </summary>
    /// <remarks>
    /// 适合紧凑布局、用户列表或行内提及。
    /// </remarks>
    Small,

    /// <summary>
    /// 默认头像尺寸（40px）。
    /// </summary>
    /// <remarks>
    /// 标准尺寸，适用于导航栏、评论区等大多数场景。
    /// </remarks>
    Default,

    /// <summary>
    /// 大号头像（48px）。
    /// </summary>
    /// <remarks>
    /// 用于需要突出用户形象的展示位或个人资料头部。
    /// </remarks>
    Large,

    /// <summary>
    /// 超大头像（64px）。
    /// </summary>
    /// <remarks>
    /// 留给用户身份为核心的个人主页或设置页。
    /// </remarks>
    ExtraLarge
}
