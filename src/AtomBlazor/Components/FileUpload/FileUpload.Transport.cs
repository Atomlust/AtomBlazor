using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace AtomBlazor;

public partial class FileUpload
{
    private readonly Dictionary<string, FileUploadOperation> operations = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim uploadSlots = new(3);
    private readonly List<InputSelection> inputSelections = [new()];
    private bool disposed;

    /// <summary>使用应用自身的传输方式上传文件。为 null 时保持仅选择行为。</summary>
    [Parameter] public Func<FileUploadContext, CancellationToken, Task>? UploadHandler { get; set; }
    /// <summary>提供 UploadHandler 时，选择后自动开始上传。最多三个并发执行。</summary>
    [Parameter] public bool AutoUpload { get; set; } = true;
    /// <summary>一次尝试抵达 Succeeded、Failed 或 Canceled 后触发。</summary>
    [Parameter] public EventCallback<FileUploadItem> OnUploadFinished { get; set; }

    /// <summary>启动处于 selected、canceled 或 failed 状态的文件。成功的文件不会被重复上传。</summary>
    public async Task UploadFilesAsync()
    {
        var files = _files.Where(f => f.Status is FileUploadStatus.Selected or FileUploadStatus.Canceled or FileUploadStatus.Failed).ToArray();
        await Task.WhenAll(files.Select(UploadFileAsync));
    }

    /// <summary>启动或重试单个文件，并重置其进度。处理器必须支持全新尝试。</summary>
    public async Task UploadFileAsync(FileUploadItem file)
    {
        ArgumentNullException.ThrowIfNull(file);
        if (Disabled || disposed || UploadHandler == null || !_files.Contains(file)
            || file.Status is FileUploadStatus.Succeeded or FileUploadStatus.Queued or FileUploadStatus.Uploading)
        {
            return;
        }
        if (!operations.TryGetValue(file.Id, out var operation))
        {
            operation = new FileUploadOperation(file);
            operations.Add(file.Id, operation);
        }
        await operation.RunAsync(UploadHandler, MaxFileSize, uploadSlots, NotifyUploadChangedAsync);
        if (!disposed && _files.Contains(file))
        {
            await OnUploadFinished.InvokeAsync(file);
        }
    }

    /// <summary>请求取消排队中或进行中的尝试。取消在处理器返回时完成。</summary>
    public void CancelUpload(FileUploadItem file)
    {
        ArgumentNullException.ThrowIfNull(file);
        if (operations.TryGetValue(file.Id, out var operation))
        {
            operation.Cancel();
        }
    }

    private Task NotifyUploadChangedAsync() => disposed ? Task.CompletedTask : InvokeAsync(StateHasChanged);

    private void ReleaseFile(FileUploadItem file)
    {
        if (operations.Remove(file.Id, out var operation))
        {
            operation.Dispose();
        }
        foreach (var selection in inputSelections)
        {
            selection.FileIds.Remove(file.Id);
        }
        inputSelections.RemoveAll(s => s != inputSelections[^1] && s.FileIds.Count == 0);
    }

    // 每次选择之后都会渲染一个全新的 InputFile。旧输入保持隐藏直到其文件被移除：
    // Blazor 会在该输入变更时使其 IBrowserFile 引用失效。
    private sealed class InputSelection
    {
        internal string Id { get; } = Guid.NewGuid().ToString("N");
        internal InputFile? Input { get; set; }
        internal HashSet<string> FileIds { get; } = new(StringComparer.Ordinal);
    }
}
