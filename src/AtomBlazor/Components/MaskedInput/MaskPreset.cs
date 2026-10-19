namespace AtomBlazor;

/// <summary>
/// 预定义的掩码模式。
/// </summary>
public enum MaskPreset
{
    /// <summary>
    /// 自定义掩码模式（使用 Mask 参数）。
    /// </summary>
    Custom,

    /// <summary>
    /// 美国电话号码：(999) 999-9999
    /// </summary>
    Phone,

    /// <summary>
    /// 社会安全号码：999-99-9999
    /// </summary>
    SSN,

    /// <summary>
    /// 信用卡号：9999 9999 9999 9999
    /// </summary>
    CreditCard,

    /// <summary>
    /// 日期：99/99/9999
    /// </summary>
    Date,

    /// <summary>
    /// 时间：99:99
    /// </summary>
    Time,

    /// <summary>
    /// 24 小时制时间（含秒）：99:99:99
    /// </summary>
    Time24,

    /// <summary>
    /// 美国邮政编码：99999 或 99999-9999
    /// </summary>
    ZipCode,

    /// <summary>
    /// 雇主识别号：99-9999999
    /// </summary>
    EIN
}
