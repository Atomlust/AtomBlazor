namespace AtomBlazor;

/// <summary>
/// 提供掩码模式定义。
/// </summary>
public static class MaskDefinitions
{
    /// <summary>
    /// 获取某个预设对应的掩码模式。
    /// </summary>
    public static string GetPattern(MaskPreset preset) => preset switch
    {
        MaskPreset.Phone => "(999) 999-9999",
        MaskPreset.SSN => "999-99-9999",
        MaskPreset.CreditCard => "9999 9999 9999 9999",
        MaskPreset.Date => "99/99/9999",
        MaskPreset.Time => "99:99",
        MaskPreset.Time24 => "99:99:99",
        MaskPreset.ZipCode => "99999-9999",
        MaskPreset.EIN => "99-9999999",
        _ => string.Empty
    };

    /// <summary>
    /// 获取某个预设对应的占位文本。
    /// </summary>
    public static string GetPlaceholder(MaskPreset preset) => preset switch
    {
        MaskPreset.Phone => "(___) ___-____",
        MaskPreset.SSN => "___-__-____",
        MaskPreset.CreditCard => "____ ____ ____ ____",
        MaskPreset.Date => "__/__/____",
        MaskPreset.Time => "__:__",
        MaskPreset.Time24 => "__:__:__",
        MaskPreset.ZipCode => "_____-____",
        MaskPreset.EIN => "__-_______",
        _ => string.Empty
    };

    /// <summary>
    /// 获取某个预设对应的输入方式提示。
    /// </summary>
    public static string GetInputMode(MaskPreset preset) => preset switch
    {
        MaskPreset.Phone => "tel",
        MaskPreset.SSN => "numeric",
        MaskPreset.CreditCard => "numeric",
        MaskPreset.Date => "numeric",
        MaskPreset.Time => "numeric",
        MaskPreset.Time24 => "numeric",
        MaskPreset.ZipCode => "numeric",
        MaskPreset.EIN => "numeric",
        _ => "text"
    };
}
