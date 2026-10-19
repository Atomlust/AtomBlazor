using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// <see cref="FileUpload"/> 的表单字段封装，提供自动的标签、
/// 帮助文本与错误消息展示。
/// </summary>
public partial class FormFieldFileUpload : FormFieldBase
{
    /// <summary>标识用于 EditForm 校验的绑定字段。</summary>
    [Parameter] public Expression<Func<IReadOnlyList<FileUploadItem>?>>? FilesExpression { get; set; }

    /// <summary>
    /// 获取或设置已选择的文件。
    /// </summary>
    [Parameter]
    public IReadOnlyList<FileUploadItem>? Files { get; set; }

    /// <summary>
    /// 获取或设置文件变化时调用的回调。
    /// </summary>
    [Parameter]
    public EventCallback<IReadOnlyList<FileUploadItem>> FilesChanged { get; set; }

    /// <summary>
    /// 获取或设置是否可以选择多个文件。
    /// </summary>
    [Parameter]
    public bool Multiple { get; set; }

    /// <summary>
    /// 获取或设置接受的文件类型（MIME 类型或扩展名）。
    /// </summary>
    [Parameter]
    public string? Accept { get; set; }

    /// <summary>
    /// 获取或设置最大文件大小（字节）。
    /// </summary>
    [Parameter]
    public long MaxFileSize { get; set; } = 10485760;

    /// <summary>
    /// 获取或设置最大文件数量。
    /// </summary>
    [Parameter]
    public int MaxFileCount { get; set; } = 10;

    /// <summary>
    /// 获取或设置是否显示图片预览。
    /// </summary>
    [Parameter]
    public bool ShowPreview { get; set; } = true;

    /// <summary>
    /// 获取或设置上传是否禁用。
    /// </summary>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>
    /// 获取或设置文件上传是否必填。
    /// </summary>
    [Parameter]
    public bool Required { get; set; }

    /// <summary>
    /// 获取或设置校验出错时调用的回调。
    /// </summary>
    [Parameter]
    public EventCallback<FileValidationError> OnValidationError { get; set; }

    /// <summary>
    /// 获取或设置自定义拖放区内容。
    /// </summary>
    [Parameter]
    public RenderFragment? DropzoneContent { get; set; }

    /// <summary>
    /// 获取或设置应用到内部 FileUpload 的额外 CSS 类。
    /// </summary>
    [Parameter]
    public string? InputClass { get; set; }

    /// <inheritdoc />
    protected override LambdaExpression? GetFieldExpression() => FilesExpression;

    private FileUpload? fileUploadRef;

    /// <summary>
    /// 清除全部文件与校验错误，将组件重置到初始状态。
    /// </summary>
    public async Task ClearFiles()
    {
        if (fileUploadRef != null)
        {
            await fileUploadRef.ClearFiles();
        }
    }

    private async Task HandleFilesChanged(IReadOnlyList<FileUploadItem> files)
    {
        Files = files;
        await FilesChanged.InvokeAsync(files);
        NotifyFieldChanged();
    }
}
