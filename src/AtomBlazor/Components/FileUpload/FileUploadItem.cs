using Microsoft.AspNetCore.Components.Forms;

namespace AtomBlazor;

/// <summary>
/// 表示上传队列中的一个文件。
/// </summary>
public class FileUploadItem
{
    /// <summary>当前传输状态。仅提供 UploadHandler 时选择才会启动传输。</summary>
    public FileUploadStatus Status { get; internal set; }
    public long BytesTransferred { get; internal set; }
    public double Progress => Size == 0 ? Status == FileUploadStatus.Succeeded ? 100 : 0 : 100.0 * BytesTransferred / Size;
    /// <summary>最近一次传输异常，供应用诊断使用。默认 UI 展示通用失败文案。</summary>
    public Exception? UploadError { get; internal set; }

    /// <summary>
    /// 获取或设置唯一标识符。
    /// </summary>
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// 获取或设置浏览器文件引用。
    /// </summary>
    public required IBrowserFile File { get; init; }

    /// <summary>
    /// 获取或设置预览数据 URL（用于图片）。
    /// </summary>
    public string? PreviewUrl { get; set; }

    /// <summary>
    /// 获取或设置该文件是否为图片。
    /// </summary>
    public bool IsImage => File.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 获取文件名。
    /// </summary>
    public string Name => File.Name;

    /// <summary>
    /// 获取文件字节大小。
    /// </summary>
    public long Size => File.Size;

    /// <summary>
    /// 获取内容类型。
    /// </summary>
    public string ContentType => File.ContentType;

    /// <summary>
    /// 获取格式化后的文件大小字符串。
    /// </summary>
    public string FormattedSize
    {
        get
        {
            if (Size < 1024)
            {
                return $"{Size} B";
            }

            if (Size < 1024 * 1024)
            {
                return $"{Size / 1024.0:F1} KB";
            }

            if (Size < 1024 * 1024 * 1024)
            {
                return $"{Size / (1024.0 * 1024):F1} MB";
            }
            return $"{Size / (1024.0 * 1024 * 1024):F1} GB";
        }
    }

    /// <summary>
    /// 获取文件扩展名。
    /// </summary>
    public string Extension => Path.GetExtension(File.Name).TrimStart('.').ToUpperInvariant();
}
