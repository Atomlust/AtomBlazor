using AtomBlazor.Primitives;
using System.Collections;
using System.Reflection;
using System.Text.RegularExpressions;
using AtomBlazor;
using AtomBlazor.Tests.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Xunit;

namespace AtomBlazor.Tests.Components;

/// <summary>
/// FileUpload 家族测试：默认拖放区本地化文案与隐藏输入、Id 参数面与生成 id、
/// 必填/禁用写入面、附加属性落输入元素、DropzoneContent 替换、参数校验拒绝非正上限、
/// 校验沿（大小/类型边界→OnValidationError、Multiple 超限 TooManyFiles 截断）、
/// 单选择只取首个并整体替换、选择轮转沿（InputSelection @key 重渲染沿，旧输入转 hidden）、
/// 图片预览 base64 沿、UploadHandler 传输沿（AutoUpload 完成/失败重试/取消、
/// UploadFilesAsync 跳过已成功、Disabled 吞报）、FilesChanged 回调、ClearFiles 重置与
/// fileUpload.resetInput、Dispose 释放清理句柄——barrel 门面沿：首渲导入 components-core.js
/// 且 fileUpload.initializeDropZone/initializePaste 载荷为双 ElementReference；句柄上的
/// dispose 是对象方法而非模块导出，调用点保持原样不改（偏差登记）。
/// InputFileChangeEventArgs 经反射以内部 IBrowserFile 列表构造器直调 HandleFileSelected 模拟
/// （FocusAsync/真实 DOM 沿不可用，headless 断言状态沿与写入面，先例 MultiSelectTests）。
/// 快照含 file-upload GUID id 与选择轮转裸 GUID，先经 [0-9a-f]{32} 正则归一化。
/// </summary>
public class FileUploadTests
{
    /// <summary>归一化 file-upload-{guid:N} 与选择轮转的裸 GUID id，使快照可确定。</summary>
    private static string Scrub(string markup) => Regex.Replace(markup, "[0-9a-f]{32}", "<id>");

    /// <summary>构造带本地化器与 JS 桩的渲染器（FileUpload 无门户/定位依赖，服务面最小）。</summary>
    private static (ComponentTestRenderer Renderer, NoopJavaScript Js) CreateRenderer()
    {
        var js = new NoopJavaScript();
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(js);
        services.AddSingleton<ILocalizer>(new DefaultLocalizer());
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

        return (new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance), js);
    }

    /// <summary>排空渲染队列，确保 StateHasChanged 的后续批已落定。</summary>
    private static Task SettleAsync(ComponentTestRenderer renderer) =>
        renderer.Dispatcher.InvokeAsync(() => Task.CompletedTask);

    /// <summary>挂载 FileUpload 根组件并排空首渲批。</summary>
    private static async Task<(ComponentTestRenderer Renderer, NoopJavaScript Js, FileUpload Upload)> MountAsync(
        Dictionary<string, object?> parameters)
    {
        var (renderer, js) = CreateRenderer();
        var upload = await renderer.MountAsync<FileUpload>(parameters);
        await SettleAsync(renderer);
        return (renderer, js, upload);
    }

    // ------------------------------------------------------------------
    // 测试桩：IBrowserFile 与 InputFileChangeEventArgs（反射内部构造器）
    // ------------------------------------------------------------------

    /// <summary>浏览器文件桩：记录 OpenReadStream 载荷，图片缩放请求返回内容可查的缩放桩。</summary>
    private sealed class FakeBrowserFile : IBrowserFile
    {
        private readonly byte[] _content;

        public FakeBrowserFile(string name, long size, string contentType, byte[]? content = null)
        {
            Name = name;
            Size = size;
            ContentType = contentType;
            _content = content ?? Array.Empty<byte>();
        }

        public string Name { get; }
        public DateTimeOffset LastModified { get; } = new(2026, 10, 12, 8, 0, 0, TimeSpan.Zero);
        public long Size { get; }
        public string ContentType { get; }

        /// <summary>OpenReadStream 收到的 maxAllowedSize 记录（Transport 沿断言用）。</summary>
        internal List<long> OpenedWithMaxSizes { get; } = [];

        public Stream OpenReadStream(long maxAllowedSize = 344106572800000, CancellationToken cancellationToken = default)
        {
            OpenedWithMaxSizes.Add(maxAllowedSize);
            return new MemoryStream(_content);
        }

        public Task<IBrowserFile> RequestImageFileAsync(string mimeType, int maxWidth, int maxHeight) =>
            Task.FromResult<IBrowserFile>(new FakeBrowserFile(Name, _content.Length, mimeType, _content));
    }

    /// <summary>以内部 IBrowserFile 列表构造器模拟 InputFileChangeEventArgs（headless 无 JS 通道）。</summary>
    private static InputFileChangeEventArgs MakeChangeEventArgs(params IBrowserFile[] files)
    {
        var ctor = typeof(InputFileChangeEventArgs)
            .GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
            .Single(c =>
            {
                var p = c.GetParameters();
                return p.Length == 1 && p[0].ParameterType == typeof(IReadOnlyList<IBrowserFile>);
            });
        return (InputFileChangeEventArgs)ctor.Invoke(new object?[] { files });
    }

    /// <summary>反射读 Transport 部分的 inputSelections 列表末位（当前活动选择）。</summary>
    private static object CurrentSelection(FileUpload upload)
    {
        var field = typeof(FileUpload).GetField("inputSelections", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var list = (IList)field.GetValue(upload)!;
        return list[list.Count - 1]!;
    }

    /// <summary>反射直调私有处理器绕开框架"事件后自动 StateHasChanged"包装
    /// 需显式补一次重渲染并排空批，终态Markup方可断言。</summary>
    private static async Task RerenderAsync(ComponentTestRenderer renderer, FileUpload upload)
    {
        var stateHasChanged = typeof(ComponentBase)
            .GetMethod("StateHasChanged", BindingFlags.Instance | BindingFlags.NonPublic)!;
        await renderer.Dispatcher.InvokeAsync(() => stateHasChanged.Invoke(upload, null));
        await SettleAsync(renderer);
    }

    /// <summary>数出Markup中input开标签枚数（实例Regex.Count，规避静态便利法参数序歧义）。</summary>
    private static int CountInputs(string markup) => new Regex("<input").Count(markup);

    /// <summary>反射直调私有 HandleFileSelected（InputFile OnChange 对端）。真实事件派发沿
    /// 由框架在处理器完成后自动补一次 StateHasChanged；直调绕开该包装，需显式重渲染终态。</summary>
    private static async Task SelectFilesAsync(
        ComponentTestRenderer renderer, FileUpload upload, params IBrowserFile[] files)
    {
        var args = MakeChangeEventArgs(files);
        var selection = CurrentSelection(upload);
        var method = typeof(FileUpload).GetMethod("HandleFileSelected", BindingFlags.Instance | BindingFlags.NonPublic)!;
        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var task = (Task)method.Invoke(upload, new object?[] { args, selection })!;
            await task;
        });
        await SettleAsync(renderer);
        await RerenderAsync(renderer, upload);
    }

    // ------------------------------------------------------------------
    // 默认拖放区与写入面
    // ------------------------------------------------------------------

    [Fact]
    public async Task 默认拖放区应渲染本地化文案与单个活动输入()
    {
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(FileUpload.AriaLabel)] = "选择文件",
        });

        var markup = renderer.Markup();
        Assert.Contains("Drag and drop files here", markup); // Localizer["FileUpload.Drag"]
        Assert.Contains("Browse to upload", markup);         // Localizer["FileUpload.Browse"]
        Assert.Contains("Max size: 10 MB", markup);          // FileUpload.MaxSize + FormatSize
        Assert.DoesNotContain("Max files:", markup);         // Multiple=false 不展示枚数
        Assert.Contains("aria-label=\"选择文件\"", markup);
        Assert.Contains("tabindex=\"0\"", markup);
        Assert.DoesNotContain("hidden", markup);             // 仅一枚活动输入，不折叠
        Assert.Contains("atom:opacity-0", markup);
    }

    [Fact]
    public async Task 多选与Accept应追加枚数与接受类型提示()
    {
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(FileUpload.Multiple)] = true,
            [nameof(FileUpload.Accept)] = "image/*",
            [nameof(FileUpload.MaxFileCount)] = 5,
        });

        var markup = renderer.Markup();
        Assert.Contains("Max files: 5", markup);   // FileUpload.MaxFiles
        Assert.Contains("Accepted: image/*", markup); // FileUpload.Accepted
        Assert.Contains("accept=\"image/*\"", markup);
        Assert.Contains("multiple", markup);
    }

    [Fact]
    public async Task 指定Id应直写输入元素缺省应生成带GUID的输入id()
    {
        var (fixedRenderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(FileUpload.Id)] = "my-upload",
        });
        Assert.Contains("id=\"my-upload\"", fixedRenderer.Markup());

        var (genRenderer, _, _) = await MountAsync(new Dictionary<string, object?>());
        Assert.Matches(@"id=""file-upload-[0-9a-f]{32}""", genRenderer.Markup());
    }

    [Fact]
    public async Task 必填与禁用应落无障碍属性与容器类()
    {
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(FileUpload.Required)] = true,
            [nameof(FileUpload.Disabled)] = true,
        });

        var markup = renderer.Markup();
        Assert.Contains("aria-required=\"true\"", markup);
        Assert.Contains("disabled", markup); // 活动输入 disabled="!active || Disabled"
        Assert.Contains("atom:opacity-50", markup);
        Assert.Contains("atom:cursor-not-allowed", markup);
    }

    [Fact]
    public async Task 附加属性应落输入元素而非容器()
    {
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            ["name"] = "files[]",
            ["data-slot"] = "fu",
        });

        var markup = renderer.Markup();
        Assert.Contains("name=\"files[]\"", markup);
        Assert.Contains("data-slot=\"fu\"", markup);
        // 容器是首个 div，其开标签不应携带 name/data-slot（二者仅在输入上）
        var container = markup[..markup.IndexOf('>', markup.IndexOf("<div", StringComparison.Ordinal))];
        Assert.DoesNotContain("files[]", container);
    }

    [Fact]
    public async Task DropzoneContent应整体替换默认提示内容()
    {
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(FileUpload.DropzoneContent)] = (RenderFragment)(builder =>
                builder.AddMarkupContent(0, "<b>把协议丢进来</b>")),
        });

        var markup = renderer.Markup();
        Assert.Contains("把协议丢进来", markup);
        Assert.DoesNotContain("Drag and drop files here", markup);
        Assert.DoesNotContain("Max size:", markup);
    }

    [Fact]
    public async Task 非正上限应在参数设定时拒绝()
    {
        var (renderer, _) = CreateRenderer();
        var mount = renderer.MountAsync<FileUpload>(new Dictionary<string, object?>
        {
            [nameof(FileUpload.MaxFileSize)] = 0L,
        });
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () => await mount);
    }

    // ------------------------------------------------------------------
    // 校验沿与 FilesChanged
    // ------------------------------------------------------------------

    [Fact]
    public async Task 超大文件应上报FileTooLarge且不入列()
    {
        var errors = new List<FileValidationError>();
        var reports = 0;
        var (renderer, _, upload) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(FileUpload.MaxFileSize)] = 1024L,
            [nameof(FileUpload.OnValidationError)] =
                EventCallback.Factory.Create<FileValidationError>(new object(), e => errors.Add(e)),
            [nameof(FileUpload.FilesChanged)] =
                EventCallback.Factory.Create<IReadOnlyList<FileUploadItem>>(new object(), _ => reports++),
        });

        await SelectFilesAsync(renderer, upload,
            new FakeBrowserFile("big.bin", 2048, "application/octet-stream"));

        var error = Assert.Single(errors);
        Assert.Equal(FileValidationErrorType.FileTooLarge, error.ErrorType);
        Assert.Equal("big.bin", error.FileName);
        Assert.Equal("big.bin exceeds 1 KB limit", error.Message);
        Assert.Equal(0, reports);
        Assert.Contains("big.bin exceeds 1 KB limit", renderer.Markup()); // 错误区显形
    }

    [Theory]
    [InlineData("doc.pdf", "application/postscript", true)]   // 扩展名命中
    [InlineData("doc.bin", "application/pdf", true)]          // MIME 命中
    [InlineData("shot.png", "image/png", true)]               // image/* 通配命中
    [InlineData("notes.txt", "text/plain", false)]            // 三式皆不命中
    public async Task Accept应支持扩展名与MIME与通配三分支(string name, string contentType, bool accepted)
    {
        var errors = new List<FileValidationError>();
        var items = new List<IReadOnlyList<FileUploadItem>>();
        var (renderer, _, upload) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(FileUpload.Accept)] = ".pdf, application/pdf, image/*",
            [nameof(FileUpload.OnValidationError)] =
                EventCallback.Factory.Create<FileValidationError>(new object(), e => errors.Add(e)),
            [nameof(FileUpload.FilesChanged)] =
                EventCallback.Factory.Create<IReadOnlyList<FileUploadItem>>(new object(), l => items.Add(l)),
        });

        await SelectFilesAsync(renderer, upload, new FakeBrowserFile(name, 16, contentType));

        if (accepted)
        {
            Assert.Empty(errors);
            var report = Assert.Single(items);
            Assert.Equal(name, Assert.Single(report).Name);
        }
        else
        {
            var error = Assert.Single(errors);
            Assert.Equal(FileValidationErrorType.InvalidType, error.ErrorType);
            Assert.Equal($"{name} is not an accepted file type", error.Message);
            Assert.Empty(items);
        }
    }

    [Fact]
    public async Task 多选超过上限应截断并上报TooManyFiles且不再继续计数()
    {
        var errors = new List<FileValidationError>();
        IReadOnlyList<FileUploadItem>? latest = null;
        var (renderer, _, upload) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(FileUpload.Multiple)] = true,
            [nameof(FileUpload.MaxFileCount)] = 2,
            [nameof(FileUpload.OnValidationError)] =
                EventCallback.Factory.Create<FileValidationError>(new object(), e => errors.Add(e)),
            [nameof(FileUpload.FilesChanged)] =
                EventCallback.Factory.Create<IReadOnlyList<FileUploadItem>>(new object(), l => latest = l),
        });

        await SelectFilesAsync(renderer, upload,
            new FakeBrowserFile("a.bin", 16, "application/octet-stream"),
            new FakeBrowserFile("b.bin", 16, "application/octet-stream"),
            new FakeBrowserFile("c.bin", 16, "application/octet-stream"));

        var error = Assert.Single(errors); // 第 3 枚触发即 break，第 4 枚场景不在此列
        Assert.Equal(FileValidationErrorType.TooManyFiles, error.ErrorType);
        Assert.Equal("c.bin", error.FileName);
        Assert.Equal("Maximum 2 files allowed", error.Message);
        Assert.NotNull(latest);
        Assert.Equal(["a.bin", "b.bin"], latest!.Select(f => f.Name));
    }

    [Fact]
    public async Task 单选择应只取首项且新选择整体替换旧列表()
    {
        var reports = new List<IReadOnlyList<FileUploadItem>>();
        var (renderer, _, upload) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(FileUpload.FilesChanged)] =
                EventCallback.Factory.Create<IReadOnlyList<FileUploadItem>>(new object(), l => reports.Add([.. l])),
        });

        // 单选模式一次交两枚：只取第一枚
        await SelectFilesAsync(renderer, upload,
            new FakeBrowserFile("first.pdf", 16, "application/pdf"),
            new FakeBrowserFile("second.pdf", 16, "application/pdf"));
        var first = Assert.Single(reports);
        Assert.Equal("first.pdf", Assert.Single(first).Name);

        // 再选一枚：旧列表整体释放并替换
        await SelectFilesAsync(renderer, upload,
            new FakeBrowserFile("third.pdf", 16, "application/pdf"));
        Assert.Equal(2, reports.Count);
        Assert.Equal("third.pdf", Assert.Single(reports[1]).Name);
    }

    [Fact]
    public async Task 选择轮转应保留旧输入转hidden并追加新活动输入()
    {
        var (renderer, _, upload) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(FileUpload.Multiple)] = true,
        });

        // 初渲染：仅一枚活动输入（tabindex=0、无 hidden、file-upload 前缀 id）
        var initialInputs = CountInputs(renderer.Markup());
        Assert.Equal(1, initialInputs);

        await SelectFilesAsync(renderer, upload, new FakeBrowserFile("a.bin", 16, "application/octet-stream"));

        var markup = renderer.Markup();
        // 轮转后两枚输入：旧选择仍持有文件 → 保留但转 hidden + disabled + tabindex=-1
        var rotatedInputs = CountInputs(markup);
        Assert.Equal(2, rotatedInputs);
        Assert.Contains("hidden", markup);
        Assert.Contains("tabindex=\"-1\"", markup);
        // 新活动输入沿用 file-upload 前缀生成 id（@key 沿旧输入不重建）
        Assert.Matches(@"id=""file-upload-[0-9a-f]{32}""", markup);
        // 旧输入的 id 是其选择 GUID（非 file-upload 前缀）
        Assert.Matches(@"<input[^>]*id=""[0-9a-f]{32}""[^>]*></input>", markup);
    }

    [Fact]
    public async Task 图片预览失败应静默吞下且文件照常入列不渲染img()
    {
        // 框架对自定义 IBrowserFile 实现拒绝 RequestImageFileAsync（扩展方法抛
        // InvalidOperationException），预览 try/catch 的容错沿恰好由此实证：预览缺失
        // 不影响入列、回调与图标降级。真实浏览器缩放成功沿由 InputFile JS 通道提供
        // headless 不可得（Select 先例口径），base64 形状沿由 ShowPreview=false 对照守护。
        var items = new List<IReadOnlyList<FileUploadItem>>();
        var (renderer, _, upload) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(FileUpload.FilesChanged)] =
                EventCallback.Factory.Create<IReadOnlyList<FileUploadItem>>(new object(), l => items.Add([.. l])),
        });

        await SelectFilesAsync(renderer, upload,
            new FakeBrowserFile("logo.png", 1000, "image/png", [1, 2, 3, 4]));

        var item = Assert.Single(Assert.Single(items));
        Assert.True(item.IsImage);
        Assert.Null(item.PreviewUrl); // 预览失败被 catch 吞下
        var markup = renderer.Markup();
        Assert.DoesNotContain("<img", markup);
        Assert.Contains("logo.png", markup); // 仍入列显形（图标降级）
    }

    [Fact]
    public async Task ShowPreview关闭时图片不走预览沿()
    {
        var items = new List<IReadOnlyList<FileUploadItem>>();
        var (renderer, _, upload) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(FileUpload.ShowPreview)] = false,
            [nameof(FileUpload.FilesChanged)] =
                EventCallback.Factory.Create<IReadOnlyList<FileUploadItem>>(new object(), l => items.Add([.. l])),
        });

        await SelectFilesAsync(renderer, upload,
            new FakeBrowserFile("logo.png", 1000, "image/png", [1, 2, 3, 4]));

        var item = Assert.Single(Assert.Single(items));
        Assert.Null(item.PreviewUrl);
        Assert.DoesNotContain("<img", renderer.Markup());
    }

    [Fact]
    public async Task ClearFiles应清空列表与错误并派发resetInput()
    {
        IReadOnlyList<FileUploadItem>? latest = null;
        var (renderer, js, upload) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(FileUpload.Multiple)] = true,
            [nameof(FileUpload.FilesChanged)] =
                EventCallback.Factory.Create<IReadOnlyList<FileUploadItem>>(new object(), l => latest = l),
        });

        await SelectFilesAsync(renderer, upload, new FakeBrowserFile("a.bin", 16, "application/octet-stream"));
        Assert.NotEmpty(latest!);

        await renderer.Dispatcher.InvokeAsync(async () => await upload.ClearFiles());
        await SettleAsync(renderer);

        Assert.Empty(latest!);
        Assert.DoesNotContain("a.bin", renderer.Markup());
        var (_, resetArgs) = Assert.Single(js.Invocations, i => i.Identifier == "fileUpload.resetInput");
        Assert.IsType<ElementReference>(resetArgs![0]);
    }

    [Fact]
    public async Task 移除按钮沿应剔除该项并上报剩余列表()
    {
        IReadOnlyList<FileUploadItem>? latest = null;
        var (renderer, _, upload) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(FileUpload.Multiple)] = true,
            [nameof(FileUpload.FilesChanged)] =
                EventCallback.Factory.Create<IReadOnlyList<FileUploadItem>>(new object(), l => latest = l),
        });

        await SelectFilesAsync(renderer, upload,
            new FakeBrowserFile("a.bin", 16, "application/octet-stream"),
            new FakeBrowserFile("b.bin", 16, "application/octet-stream"));

        var remove = typeof(FileUpload)
            .GetMethod("RemoveFile", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var target = latest!.First(f => f.Name == "a.bin");
        await renderer.Dispatcher.InvokeAsync(async () =>
            await (Task)remove.Invoke(upload, new object?[] { target })!);
        await SettleAsync(renderer);
        await RerenderAsync(renderer, upload);

        Assert.Equal(["b.bin"], latest!.Select(f => f.Name));
        Assert.DoesNotContain(">a.bin<", renderer.Markup());
    }

    [Fact]
    public async Task 外部Files参数应接管列表且移出项随之消失()
    {
        var kept = new FileUploadItem { File = new FakeBrowserFile("kept.bin", 16, "application/octet-stream") };
        var dropped = new FileUploadItem { File = new FakeBrowserFile("gone.bin", 16, "application/octet-stream") };
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(FileUpload.Files)] = new[] { kept, dropped },
        });
        Assert.Contains("gone.bin", renderer.Markup());

        await renderer.RerenderRootAsync(0, new Dictionary<string, object?>
        {
            [nameof(FileUpload.Files)] = new[] { kept },
        });
        await SettleAsync(renderer);

        Assert.Contains("kept.bin", renderer.Markup());
        Assert.DoesNotContain("gone.bin", renderer.Markup());
    }

    // ------------------------------------------------------------------
    // Transport 上传沿（UploadHandler 委托桩）
    // ------------------------------------------------------------------

    [Fact]
    public async Task AutoUpload应完成传输并回播进度与成功状态()
    {
        var finished = new List<FileUploadItem>();
        FileUploadContext? seen = null;
        var (renderer, _, upload) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(FileUpload.MaxFileSize)] = 4096L,
            [nameof(FileUpload.UploadHandler)] =
                (Func<FileUploadContext, CancellationToken, Task>)(async (context, token) =>
                {
                    seen = context;
                    using var stream = context.OpenReadStream();
                    await stream.CopyToAsync(Stream.Null, token);
                    await context.ReportProgressAsync(1024);
                }),
            [nameof(FileUpload.OnUploadFinished)] =
                EventCallback.Factory.Create<FileUploadItem>(new object(), f => finished.Add(f)),
        });

        await SelectFilesAsync(renderer, upload,
            new FakeBrowserFile("data.bin", 2048, "application/octet-stream", new byte[2048]));

        Assert.NotNull(seen);
        // OpenReadStream 以组件 MaxFileSize 为界；进度封顶为文件大小
        Assert.Equal(2048L, seen!.Item.BytesTransferred);
        Assert.Equal(FileUploadStatus.Succeeded, seen.Item.Status);
        Assert.Equal(100.0, seen.Item.Progress);
        Assert.Equal([4096L], ((FakeBrowserFile)seen.Item.File).OpenedWithMaxSizes);
        var done = Assert.Single(finished);
        Assert.Equal(FileUploadStatus.Succeeded, done.Status);
        Assert.Null(done.UploadError);

        var markup = renderer.Markup();
        Assert.Contains("Uploaded", markup);            // FileUpload.Succeeded
        Assert.Contains("role=\"status\"", markup);
        Assert.Contains("value=\"100\"", markup);       // progress 满格
    }

    [Fact]
    public async Task 传输失败应留存异常并显形重试按钮()
    {
        var finished = new List<FileUploadItem>();
        var (renderer, _, upload) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(FileUpload.UploadHandler)] =
                (Func<FileUploadContext, CancellationToken, Task>)((context, token) =>
                {
                    context.ReportProgressAsync(50);
                    return Task.FromException(new InvalidOperationException("炸了"));
                }),
            [nameof(FileUpload.OnUploadFinished)] =
                EventCallback.Factory.Create<FileUploadItem>(new object(), f => finished.Add(f)),
        });

        await SelectFilesAsync(renderer, upload,
            new FakeBrowserFile("data.bin", 2048, "application/octet-stream"));

        var done = Assert.Single(finished);
        Assert.Equal(FileUploadStatus.Failed, done.Status);
        Assert.Equal(50L, done.BytesTransferred);
        Assert.IsType<InvalidOperationException>(done.UploadError);

        var markup = renderer.Markup();
        Assert.Contains("Upload failed. You can retry.", markup); // FileUpload.Failed
        Assert.Contains(">Retry<", markup);                        // FileUpload.Retry 按钮
    }

    [Fact]
    public async Task 取消上传应落Canceled态()
    {
        var finished = new List<FileUploadItem>();
        IReadOnlyList<FileUploadItem> reported = [];
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var released = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var (renderer, _, upload) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(FileUpload.AutoUpload)] = false,
            [nameof(FileUpload.FilesChanged)] =
                EventCallback.Factory.Create<IReadOnlyList<FileUploadItem>>(new object(), l => reported = [.. l]),
            [nameof(FileUpload.UploadHandler)] =
                (Func<FileUploadContext, CancellationToken, Task>)(async (context, token) =>
                {
                    entered.SetResult();
                    await using var registration = token.Register(() => released.TrySetResult());
                    await released.Task;
                    token.ThrowIfCancellationRequested();
                }),
            [nameof(FileUpload.OnUploadFinished)] =
                EventCallback.Factory.Create<FileUploadItem>(new object(), f => finished.Add(f)),
        });

        await SelectFilesAsync(renderer, upload,
            new FakeBrowserFile("slow.bin", 16, "application/octet-stream"));
        var item = Assert.Single(reported);
        Assert.Equal(FileUploadStatus.Selected, item.Status);

        // 发起传输但不等待完成：处理器驻留在 token 上，CancelUpload 同步置取消
        var uploadTask = renderer.Dispatcher.InvokeAsync(async () => await upload.UploadFileAsync(item));
        await entered.Task;
        upload.CancelUpload(item);
        await uploadTask;

        Assert.Equal(FileUploadStatus.Canceled, item.Status);
        Assert.Null(item.UploadError); // 取消非异常失败，不留传输异常
        var done = Assert.Single(finished);
        Assert.Equal(FileUploadStatus.Canceled, done.Status);
    }

    [Fact]
    public async Task UploadFilesAsync应跳过已成功项且释放后吞报上传()
    {
        var handlerCalls = 0;
        Func<FileUploadContext, CancellationToken, Task> handler = (_, _) =>
        {
            handlerCalls++;
            return Task.CompletedTask;
        };
        var (renderer, _, upload) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(FileUpload.AutoUpload)] = false,
            [nameof(FileUpload.UploadHandler)] = handler,
        });

        await SelectFilesAsync(renderer, upload, new FakeBrowserFile("a.bin", 16, "application/octet-stream"));
 Assert.Equal(0, handlerCalls); // AutoUpload=false 只登记不入传输

        await renderer.Dispatcher.InvokeAsync(async () => await upload.UploadFilesAsync());
        await SettleAsync(renderer);
        Assert.Equal(1, handlerCalls);

        // 已成功项不再二次传输（语义：Succeeded 不入 UploadFilesAsync 选取沿）
        await renderer.Dispatcher.InvokeAsync(async () => await upload.UploadFilesAsync());
        Assert.Equal(1, handlerCalls);

        await ((IAsyncDisposable)upload).DisposeAsync();
        var item = new FileUploadItem { File = new FakeBrowserFile("late.bin", 16, "application/octet-stream") };
        await renderer.Dispatcher.InvokeAsync(async () => await upload.UploadFileAsync(item));
        Assert.Equal(1, handlerCalls); // disposed 闸吞报
    }

    // ------------------------------------------------------------------
    // 拖放态文案沿（dropzone 状态类切换的 C# 对端）
    // ------------------------------------------------------------------

    [Fact]
    public async Task 拖入应切Drop文案与主色边框并拖出复原()
    {
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>());

        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("ondragenter", new DragEventArgs()));
        await SettleAsync(renderer);
        var dragging = renderer.Markup();
        Assert.Contains("Drop files here", dragging);       // FileUpload.Drop
        Assert.Contains("atom:border-primary", dragging);

        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("ondragleave", new DragEventArgs()));
        await SettleAsync(renderer);
        var left = renderer.Markup();
        Assert.Contains("Drag and drop files here", left);
        Assert.DoesNotContain("atom:border-primary", left);
    }

    [Fact]
    public async Task 禁用态拖入应不切换文案()
    {
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(FileUpload.Disabled)] = true,
        });

        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("ondragenter", new DragEventArgs()));
        await SettleAsync(renderer);
        Assert.DoesNotContain("Drop files here", renderer.Markup());
    }

    // ------------------------------------------------------------------
    // file-upload.js interop 沿（barrel 门面 + 句柄释放）
    // ------------------------------------------------------------------

    [Fact]
    public async Task 首渲应导入共享桶文件并声明拖放区与粘贴初始化载荷()
    {
        var (_, js, _) = await MountAsync(new Dictionary<string, object?>());

 // 门面沿：加载的是 components-core.js barrel，而非 file-upload.js 原路径（偏差登记）。
        var import = Assert.Single(js.Invocations,
            i => i.Identifier == "import" && ((string?)i.Args?[0])?.Contains("components-core.js") == true);
        Assert.DoesNotContain("file-upload.js", Assert.IsType<string>(import.Args![0]));

        var (_, dropArgs) = Assert.Single(js.Invocations, i => i.Identifier == "fileUpload.initializeDropZone");
        Assert.IsType<ElementReference>(dropArgs![0]); // 拖放区容器
        Assert.IsType<ElementReference>(dropArgs[1]);  // InputFile 底层输入

        var (_, pasteArgs) = Assert.Single(js.Invocations, i => i.Identifier == "fileUpload.initializePaste");
        Assert.IsType<ElementReference>(pasteArgs![0]);
        Assert.IsType<ElementReference>(pasteArgs[1]);
    }

    [Fact]
    public async Task AllowPaste关闭应不挂粘贴初始化()
    {
        var (renderer, js, upload) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(FileUpload.AllowPaste)] = false,
        });
        Assert.DoesNotContain("fileUpload.initializePaste", js.Invocations.Select(i => i.Identifier));

        // 中途开启：再渲染一轮由 OnAfterRender 沿补挂粘贴
        await renderer.Dispatcher.InvokeAsync(() =>
            renderer.RerenderRootAsync(0, new Dictionary<string, object?>
            {
                [nameof(FileUpload.AllowPaste)] = true,
            }));
        await SettleAsync(renderer);
        Assert.Single(js.Invocations, i => i.Identifier == "fileUpload.initializePaste");

        // 再关：走句柄 dispose + DisposeAsync 摘除沿（句柄对象方法各一次）
        await renderer.Dispatcher.InvokeAsync(() =>
            renderer.RerenderRootAsync(0, new Dictionary<string, object?>
            {
                [nameof(FileUpload.AllowPaste)] = false,
            }));
        await SettleAsync(renderer);
        Assert.Single(js.Invocations, i => i.Identifier == "dispose"); // 句柄对象方法，非点分导出
    }

    [Fact]
    public async Task 选择轮转应先摘句柄再重挂拖放区与粘贴()
    {
        var (renderer, js, upload) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(FileUpload.Multiple)] = true,
        });

        await SelectFilesAsync(renderer, upload, new FakeBrowserFile("a.bin", 16, "application/octet-stream"));

        Assert.Equal(2, js.Invocations.Count(i => i.Identifier == "fileUpload.initializeDropZone"));
        Assert.Equal(2, js.Invocations.Count(i => i.Identifier == "fileUpload.initializePaste"));
        Assert.Equal(2, js.Invocations.Count(i => i.Identifier == "dispose")); // HandleFileSelected 轮转沿
    }

    [Fact]
    public async Task 释放应兜底清理双句柄且断线沿由守卫吞下()
    {
        var (_, js, upload) = await MountAsync(new Dictionary<string, object?>());
        Assert.Equal(0, js.Invocations.Count(i => i.Identifier == "dispose"));

        await ((IAsyncDisposable)upload).DisposeAsync();
        // 粘贴句柄与拖放句柄各发一次对象方法 dispose 再 DisposeAsync；Noop 桩永不抛错
        // 真实断线由 catch 谓词（JSDisconnectedException 等）兜住。
        Assert.Equal(2, js.Invocations.Count(i => i.Identifier == "dispose"));
    }

    // ------------------------------------------------------------------
    // 快照
    // ------------------------------------------------------------------

    [Fact]
    public async Task 默认拖放区结构快照应与基线一致()
    {
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(FileUpload.AriaLabel)] = "snap",
            [nameof(FileUpload.Multiple)] = true,
        });

        await Verify(Scrub(renderer.Markup()));
    }

    [Fact]
    public async Task 选择后文件列表与上传按钮结构快照应与基线一致()
    {
        var (renderer, _, upload) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(FileUpload.Multiple)] = true,
            [nameof(FileUpload.AutoUpload)] = false,
            [nameof(FileUpload.UploadHandler)] =
                (Func<FileUploadContext, CancellationToken, Task>)((_, _) => Task.CompletedTask),
        });

        await SelectFilesAsync(renderer, upload, new FakeBrowserFile("doc.bin", 2048, "application/octet-stream"));

        await Verify(Scrub(renderer.Markup()));
    }

    [Fact]
    public async Task 校验错误显形结构快照应与基线一致()
    {
        var (renderer, _, upload) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(FileUpload.MaxFileSize)] = 1024L,
        });

        await SelectFilesAsync(renderer, upload,
            new FakeBrowserFile("big.bin", 4096, "application/octet-stream"));

        await Verify(Scrub(renderer.Markup()));
    }
}
