namespace AtomBlazor;

/// <summary>选中文件的生命周期状态。重试会启动全新流并重置进度。</summary>
public enum FileUploadStatus { Selected, Queued, Uploading, Succeeded, Canceled, Failed }

/// <summary>一次传输调用。请上报实际发送的字节数；成功返回即完成上传。</summary>
public sealed class FileUploadContext
{
    private readonly Func<long, Task> report;
    private readonly long maximumSize;
    private readonly CancellationToken cancellationToken;

    internal FileUploadContext(FileUploadItem item, long maximumSize, Func<long, Task> report, CancellationToken cancellationToken)
    {
        Item = item;
        this.maximumSize = maximumSize;
        this.cancellationToken = cancellationToken;
        this.report = report;
    }

    public FileUploadItem Item { get; }
    /// <summary>打开一个以组件 MaxFileSize 与取消令牌为界的全新浏览器流。</summary>
    public Stream OpenReadStream() => Item.File.OpenReadStream(maximumSize, cancellationToken);
    /// <summary>上报本次尝试中累计发送的字节数。倒退与迟到上报将被忽略。</summary>
    public Task ReportProgressAsync(long bytesTransferred) => report(bytesTransferred);
}
