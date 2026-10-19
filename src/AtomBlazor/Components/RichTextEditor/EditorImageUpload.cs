namespace AtomBlazor;

/// <summary>
/// 用户添加到 <see cref="RichTextEditor"/> 的图片——无论经工具栏选取、
/// 拖放进编辑器还是粘贴而来——都会交给 <see cref="RichTextEditor.ImageUploader"/>。
/// </summary>
/// <param name="FileName">浏览器报告的文件名，例如 <c>photo.png</c>。不可信：至多用作显示名或取扩展名。</param>
/// <param name="ContentType">浏览器报告的 MIME 类型，例如 <c>image/png</c>。</param>
/// <param name="Size">文件字节大小。</param>
/// <param name="Content">图片字节流。请在处理程序内读完；处理程序返回时即被释放。</param>
public sealed record EditorImageUpload(string FileName, string ContentType, long Size, Stream Content);
