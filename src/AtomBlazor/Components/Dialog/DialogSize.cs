namespace AtomBlazor;

/// <summary>
/// 通过 <see cref="DialogService.OpenAsync{TComponent}(Dictionary{string, object?}, AtomBlazor.DialogOpenOptions?)"/> 打开的对话框的预设宽度选项。
/// </summary>
public enum DialogSize
{
    /// <summary>
    /// 小宽度对话框。
    /// </summary>
    Small,

    /// <summary>
    /// 默认的中等宽度对话框。
    /// </summary>
    Default,

    /// <summary>
    /// 大宽度对话框。
    /// </summary>
    Large,

    /// <summary>
    /// 超大宽度对话框。
    /// </summary>
    ExtraLarge,

    /// <summary>
    /// 全宽度对话框。
    /// </summary>
    Full
}
