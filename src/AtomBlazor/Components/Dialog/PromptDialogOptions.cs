namespace AtomBlazor;

/// <summary>
/// 为通过 <see cref="DialogService.PromptAsync(string, string?, PromptDialogOptions?)"/> 显示的提示输入对话框提供配置选项。
/// </summary>
public class PromptDialogOptions : DialogOptions
{
    /// <summary>
    /// 初始化 <see cref="PromptDialogOptions"/> 类的新实例。
    /// </summary>
    /// <remarks>
    /// 将默认的确认按钮文本设置为 <c>"OK"</c>。
    /// </remarks>
    public PromptDialogOptions()
    {
        ConfirmText = "OK";
    }

    /// <summary>
    /// 获取或设置输入字段的初始值。
    /// </summary>
    public string? DefaultValue { get; set; }

    /// <summary>
    /// 获取或设置显示在输入字段中的占位符文本。
    /// </summary>
    public string? Placeholder { get; set; }

    /// <summary>
    /// 获取或设置一个值，该值指示输入字段是否为必填。
    /// </summary>
    /// <remarks>
    /// 当为 <c>true</c> 时，在输入非空值之前，
    /// 确认按钮应处于禁用状态。
    /// </remarks>
    public bool Required { get; set; }

    /// <summary>
    /// 获取或设置输入值允许的最大长度。
    /// </summary>
    /// <remarks>
    /// 当指定该值时，如果输入超出此长度，
    /// 会显示验证错误并禁用确认按钮。
    /// </remarks>
    public int? MaxLength { get; set; }
}
