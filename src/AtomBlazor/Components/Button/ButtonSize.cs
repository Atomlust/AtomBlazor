namespace AtomBlazor;

/// <summary>
/// 定义 Button 组件的尺寸变体。
/// </summary>
/// <remarks>
/// 按钮尺寸控制内边距、字号与整体尺寸。
/// 所有尺寸都保持合适的触控目标（最小 44x44 像素）以满足无障碍要求。
/// 遵循 shadcn/ui 尺寸刻度约定。
/// </remarks>
public enum ButtonSize
{
    /// <summary>
    /// 小号按钮（sm）。
    /// 高度约 36px，水平内边距 0.5rem。
    /// 适合紧凑界面、工具栏或行内操作。
    /// </summary>
    Small,

    /// <summary>
    /// 默认/中号按钮（md）。
    /// 高度约 40px，水平内边距 1rem。
    /// 推荐用于大多数场景的默认尺寸。
    /// </summary>
    Default,

    /// <summary>
    /// 大号按钮（lg）。
    /// 高度约 44px，水平内边距 1.5rem。
    /// 最适合主要 CTA 或醒目的操作。
    /// </summary>
    Large,

    /// <summary>
    /// 纯图标按钮尺寸（icon）。
    /// 正方形尺寸：40x40px（h-10 w-10）。
    /// 为只含图标、不含文本的按钮设计。
    /// 必须配合 aria-label 以保持无障碍。
    /// </summary>
    Icon,

    /// <summary>
    /// 小尺寸纯图标按钮（icon-sm）。
    /// 正方形尺寸：36x36px（h-9 w-9）。
    /// 适合工具栏或紧凑布局的图标按钮。
    /// 必须配合 aria-label 以保持无障碍。
    /// </summary>
    IconSmall,

    /// <summary>
    /// 大尺寸纯图标按钮（icon-lg）。
    /// 正方形尺寸：44x44px（h-11 w-11）。
    /// 适合主要图标操作的醒目按钮。
    /// 必须配合 aria-label 以保持无障碍。
    /// </summary>
    IconLarge
}
