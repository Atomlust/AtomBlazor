namespace AtomBlazor;

/// <summary>
/// 定义 <see cref="Link"/> 的着色方案。
/// </summary>
public enum LinkVariant
{
    /// <summary>主色。默认值，也是正文中的链接应有的样子。</summary>
    Default,

    /// <summary>继承周围文字颜色，用于段落里不应抢眼的链接。</summary>
    Inherit,

    /// <summary>次要色，用于页脚等位置的次要链接。</summary>
    Muted,

    /// <summary>危险色，用于指向移除或取消操作的链接。</summary>
    Destructive
}
