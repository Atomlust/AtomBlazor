namespace AtomBlazor;

/// <summary>
/// 定义 Skeleton 组件的形状变体。
/// </summary>
/// <remarks>
/// Skeleton 形状决定占位元素的 border-radius。
/// 请根据正在加载的内容选择形状：
/// <list type="bullet">
/// <item><see cref="Rectangular"/>：文本、图片、卡片与通用内容块</item>
/// <item><see cref="Circular"/>：头像、个人资料图片与圆形图标</item>
/// </list>
/// </remarks>
public enum SkeletonShape
{
    /// <summary>
    /// 带圆角的矩形骨架（rounded-md）。
    /// </summary>
    /// <remarks>
    /// 默认形状，适用于大多数加载占位场景，包括：
    /// <list type="bullet">
    /// <item>文本行与段落</item>
    /// <item>图片与缩略图</item>
    /// <item>卡片与面板</item>
    /// <item>按钮与表单字段</item>
    /// </list>
    /// 使用 Tailwind 的 <c>rounded-md</c> 类实现轻微圆角。
    /// </remarks>
    Rectangular,

    /// <summary>
    /// 完全圆角的圆形骨架（rounded-full）。
    /// </summary>
    /// <remarks>
    /// 适合头像与图标占位，包括：
    /// <list type="bullet">
    /// <item>用户头像</item>
    /// <item>圆形徽章或指示器</item>
    /// <item>图标占位</item>
    /// </list>
    /// 使用 Tailwind 的 <c>rounded-full</c> 类实现正圆形状。
    /// 需要等宽等高（如 <c>h-12 w-12</c>）才能得到标准圆形。
    /// </remarks>
    Circular
}
