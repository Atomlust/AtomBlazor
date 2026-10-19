namespace AtomBlazor;

/// <summary>
/// 表示一次文件校验错误。
/// </summary>
public class FileValidationError
{
    /// <summary>
    /// 获取或设置文件名。
    /// </summary>
    public required string FileName { get; init; }

    /// <summary>
    /// 获取或设置错误类型。
    /// </summary>
    public required FileValidationErrorType ErrorType { get; init; }

    /// <summary>
    /// 获取或设置错误消息。
    /// </summary>
    public required string Message { get; init; }
}

/// <summary>
/// 文件校验错误的类型。
/// </summary>
public enum FileValidationErrorType
{
    /// <summary>
    /// 文件超出大小上限。
    /// </summary>
    FileTooLarge,

    /// <summary>
    /// 文件类型不被接受。
    /// </summary>
    InvalidType,

    /// <summary>
    /// 选择的文件过多。
    /// </summary>
    TooManyFiles
}
