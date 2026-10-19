using AtomBlazor.Primitives;
using System.Linq.Expressions;
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
/// FormFieldFileUpload 组合字段测试：在可选 EditContext 宿主下挂载
/// FormFieldFileUpload（根 0，底座无弹层，无需 PortalHost），验证默认拖放区结构、
/// 参数透传（Multiple/Accept/MaxFileSize/MaxFileCount/ShowPreview/Disabled/Required/
/// DropzoneContent）、InputClass+splat 落点、ErrorText 手动覆写、文件选择经封装层
/// 两步沿回填（Files+FilesChanged+NotifyFieldChanged）并消解 EditContext 校验错误、
/// ClearFiles 经底座清空。未接线沿（ 保留）：封装层不传 For 与 Id
/// label 无 for 属性、控件无 aria-describedby——与 FormFieldSwitch 家族实通形态不同。
/// 文件选择经反射直调底座私有 HandleFileSelected 模拟（InputFileChangeEventArgs
/// 以内部 IBrowserFile 列表构造器构造，先例 FileUploadTests）。
/// 快照含 formfield GUID 与 file-upload GUID，先经正则归一化。
/// </summary>
public class FormFieldFileUploadTests
{
    /// <summary>归一化 ControlId 与 FileUpload 生成 id 的随机 GUID，使快照可确定。</summary>
    private static string Scrub(string markup) =>
        Regex.Replace(
            Regex.Replace(markup, "formfield-[0-9a-f]{32}", "formfield-<id>"),
            "file-upload-[0-9a-f]{32}", "file-upload-<id>");

    /// <summary>构造带基元服务的渲染器（底座 FileUpload 走 barrel interop，JS 用桩）。</summary>
    private static ComponentTestRenderer CreateRenderer()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(new NoopJavaScript());
        services.AddSingleton<ILocalizer>(new DefaultLocalizer());
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

        return new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance);
    }

    /// <summary>排空渲染队列，确保 StateHasChanged 的后续批已落定。</summary>
    private static async Task SettleAsync(ComponentTestRenderer renderer, int rounds = 2)
    {
        for (var i = 0; i < rounds; i++)
        {
            await renderer.Dispatcher.InvokeAsync(() => { });
        }
    }

    /// <summary>校验沿测试模型：Files 为空即无效（由假校验器执行）。</summary>
    internal sealed class Doc
    {
        public IReadOnlyList<FileUploadItem>? Files { get; set; }
    }

    /// <summary>
    /// 构造挂接假校验器的 EditContext：Files 为空时向该字段写入消息。
    /// </summary>
    private static EditContext CreateValidatedContext(Doc doc)
    {
        var context = new EditContext(doc);
        var messages = new ValidationMessageStore(context);
        var field = new FieldIdentifier(doc, nameof(Doc.Files));

        void Validate()
        {
            messages.Clear();
            if (doc.Files is null || doc.Files.Count == 0)
            {
                messages.Add(field, "请至少选择一个文件");
            }
            context.NotifyValidationStateChanged();
        }

        context.OnFieldChanged += (_, _) => Validate();
        context.OnValidationRequested += (_, _) => Validate();
        return context;
    }

    private static Dictionary<string, object?> FieldParams(
        IReadOnlyList<FileUploadItem>? files = null,
        EventCallback<IReadOnlyList<FileUploadItem>> filesChanged = default,
        Expression<Func<IReadOnlyList<FileUploadItem>?>>? filesExpression = null,
        bool? multiple = null,
        string? accept = null,
        long? maxFileSize = null,
        int? maxFileCount = null,
        bool? showPreview = null,
        bool? disabled = null,
        bool? required = null,
        EventCallback<FileValidationError>? onValidationError = null,
        RenderFragment? dropzoneContent = null,
        string? inputClass = null,
        string? label = null,
        string? helperText = null,
        string? errorText = null,
        string? ariaLabel = null,
        IReadOnlyDictionary<string, object?>? splat = null)
    {
        var parameters = new Dictionary<string, object?>();
        if (files is not null)
        {
            parameters[nameof(FormFieldFileUpload.Files)] = files;
        }
        if (filesChanged.HasDelegate)
        {
            parameters[nameof(FormFieldFileUpload.FilesChanged)] = filesChanged;
        }
        if (filesExpression is not null)
        {
            parameters[nameof(FormFieldFileUpload.FilesExpression)] = filesExpression;
        }
        if (multiple is not null)
        {
            parameters[nameof(FormFieldFileUpload.Multiple)] = multiple.Value;
        }
        if (accept is not null)
        {
            parameters[nameof(FormFieldFileUpload.Accept)] = accept;
        }
        if (maxFileSize is not null)
        {
            parameters[nameof(FormFieldFileUpload.MaxFileSize)] = maxFileSize.Value;
        }
        if (maxFileCount is not null)
        {
            parameters[nameof(FormFieldFileUpload.MaxFileCount)] = maxFileCount.Value;
        }
        if (showPreview is not null)
        {
            parameters[nameof(FormFieldFileUpload.ShowPreview)] = showPreview.Value;
        }
        if (disabled is not null)
        {
            parameters[nameof(FormFieldFileUpload.Disabled)] = disabled.Value;
        }
        if (required is not null)
        {
            parameters[nameof(FormFieldFileUpload.Required)] = required.Value;
        }
        if (onValidationError is not null)
        {
            parameters[nameof(FormFieldFileUpload.OnValidationError)] = onValidationError.Value;
        }
        if (dropzoneContent is not null)
        {
            parameters[nameof(FormFieldFileUpload.DropzoneContent)] = dropzoneContent;
        }
        if (inputClass is not null)
        {
            parameters[nameof(FormFieldFileUpload.InputClass)] = inputClass;
        }
        if (label is not null)
        {
            parameters[nameof(FormFieldFileUpload.Label)] = label;
        }
        if (helperText is not null)
        {
            parameters[nameof(FormFieldFileUpload.HelperText)] = helperText;
        }
        if (errorText is not null)
        {
            parameters[nameof(FormFieldFileUpload.ErrorText)] = errorText;
        }
        if (ariaLabel is not null)
        {
            parameters[nameof(FormFieldFileUpload.AriaLabel)] = ariaLabel;
        }
        if (splat is not null)
        {
            parameters["AdditionalAttributes"] = new Dictionary<string, object?>(splat);
        }

        return parameters;
    }

    /// <summary>取隐藏文件输入开标签（type="file" 指纹；Field 根为 data-slot="field" div）。</summary>
    private static string FileInputTag(string markup) =>
        Regex.Match(markup, """<input[^>]*type="file"[^>]*>""").Value;

    /// <summary>取 Field 根 div 开标签。</summary>
    private static string FieldRootTag(string markup) =>
        Regex.Match(markup, """<div[^>]*data-slot="field"[^>]*>""").Value;

    // ------------------------------------------------------------------
    // 测试桩：IBrowserFile 与 InputFileChangeEventArgs（反射内部构造器）
    // ------------------------------------------------------------------

    /// <summary>浏览器文件桩（先例 FileUploadTests.FakeBrowserFile，本文件自含）。</summary>
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

        public Stream OpenReadStream(long maxAllowedSize = 344106572800000, CancellationToken cancellationToken = default) =>
            new MemoryStream(_content);

        public Task<IBrowserFile> RequestImageFileAsync(string mimeType, int maxWidth, int maxHeight) =>
            Task.FromResult<IBrowserFile>(new FakeBrowserFile(Name, _content.Length, mimeType, _content));
    }

    /// <summary>以内部 IBrowserFile 列表构造器构造 InputFileChangeEventArgs（headless 无 JS 通道）。</summary>
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

    /// <summary>反射读底座的 inputSelections 列表末位（当前活动选择）。</summary>
    private static object CurrentSelection(FileUpload upload)
    {
        var field = typeof(FileUpload).GetField("inputSelections", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var list = (System.Collections.IList)field.GetValue(upload)!;
        return list[list.Count - 1]!;
    }

    /// <summary>反射直调底座私有 HandleFileSelected（InputFile OnChange 对端）
    /// 绕开框架"事件后自动 StateHasChanged"包装，需显式补封装层重渲染并排空批。</summary>
    private static async Task SelectFilesAsync(
        ComponentTestRenderer renderer, FormFieldFileUpload field, params IBrowserFile[] files)
    {
        var upload = renderer.FindComponent<FileUpload>();
        var args = MakeChangeEventArgs(files);
        var selection = CurrentSelection(upload);
        var method = typeof(FileUpload).GetMethod("HandleFileSelected", BindingFlags.Instance | BindingFlags.NonPublic)!;
        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var task = (Task)method.Invoke(upload, new object?[] { args, selection })!;
            await task;
        });
        await SettleAsync(renderer);

        var stateHasChanged = typeof(ComponentBase)
            .GetMethod("StateHasChanged", BindingFlags.Instance | BindingFlags.NonPublic)!;
        await renderer.Dispatcher.InvokeAsync(() => stateHasChanged.Invoke(field, null));
        await SettleAsync(renderer);
    }

    // ------------------------------------------------------------------
    // 快照与写入面
    // ------------------------------------------------------------------

    [Fact]
    public async Task 默认结构快照应与基线一致()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<FormFieldFileUpload>(FieldParams(
            label: "上传附件", helperText: "支持 PDF 与图片", ariaLabel: "选择附件", multiple: true));
        await SettleAsync(renderer);

        await Verify(Scrub(renderer.Markup()));
    }

    [Fact]
    public async Task 标签与帮助文本应落Field结构且未接线for与describedby()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<FormFieldFileUpload>(FieldParams(
            label: "上传附件", helperText: "支持 PDF 与图片", ariaLabel: "选择附件"));
        await SettleAsync(renderer);

        var markup = renderer.Markup();
        // FieldLabel 的 class 属性恒存在（怪癖），label 断言放宽到开标签内文本匹配。
        Assert.Matches("""<label[^>]*data-slot="field-label"[^>]*>上传附件</label>""", markup);
        // 未接线沿（ 保留）：label 无 for 属性、控件无 aria-describedby。
        Assert.DoesNotContain("for=", markup);
        Assert.DoesNotContain("aria-describedby", markup);
        // 描述元素仍带 description 后缀 id（仅元素自身）。
        Assert.Matches(@"id=""formfield-[0-9a-f]{32}-description""", markup);
        Assert.Contains("data-slot=\"field-description\"", markup);
        // 底座输入：生成 id + AriaLabel 透传。
        var input = FileInputTag(markup);
        Assert.Matches(@"id=""file-upload-[0-9a-f]{32}""", input);
        Assert.Contains("aria-label=\"选择附件\"", input);
    }

    [Fact]
    public async Task 多选与Accept与上限应透传落底座提示与输入属性()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<FormFieldFileUpload>(FieldParams(
            multiple: true, accept: "image/*", maxFileCount: 5));
        await SettleAsync(renderer);

        var markup = renderer.Markup();
        Assert.Contains("Max files: 5", markup);
        Assert.Contains("Accepted: image/*", markup);
        Assert.Contains("accept=\"image/*\"", FileInputTag(markup));
        Assert.Contains("multiple", FileInputTag(markup));
    }

    [Fact]
    public async Task MaxFileSize应透传落底座提示文案()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<FormFieldFileUpload>(FieldParams(maxFileSize: 1024L));
        await SettleAsync(renderer);

        Assert.Contains("Max size: 1 KB", renderer.Markup());
    }

    [Fact]
    public async Task 必填与禁用应透传落底座写入面()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<FormFieldFileUpload>(FieldParams(required: true, disabled: true));
        await SettleAsync(renderer);

        var markup = renderer.Markup();
        Assert.Contains("aria-required=\"true\"", markup);
        Assert.Contains("atom:opacity-50", markup);          // 拖放区禁用态类
        Assert.Contains("atom:cursor-not-allowed", markup);
        Assert.Contains(" disabled", FileInputTag(markup));  // 活动输入禁用
    }

    [Fact]
    public async Task DropzoneContent应经底座整体替换默认提示()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<FormFieldFileUpload>(FieldParams(
            dropzoneContent: builder => builder.AddMarkupContent(0, "<b>把协议丢进来</b>")));
        await SettleAsync(renderer);

        var markup = renderer.Markup();
        Assert.Contains("把协议丢进来", markup);
        Assert.DoesNotContain("Drag and drop files here", markup);
        Assert.DoesNotContain("Max size:", markup);
    }

    [Fact]
    public async Task InputClass与透传应分别落底座容器与输入元素()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<FormFieldFileUpload>(FieldParams(
            inputClass: "atom:custom-upload",
            splat: new Dictionary<string, object?> { ["data-testid"] = "field-upload" }));
        await SettleAsync(renderer);

        var markup = renderer.Markup();
        Assert.Contains("atom:custom-upload", markup);                 // InputClass → 底座容器
        Assert.Contains("data-testid=\"field-upload\"", FileInputTag(markup)); // splat → 输入
        // Field 根不吃 splat（splat 经 @attributes 只进底座输入）。
        Assert.DoesNotContain("data-testid", FieldRootTag(markup));
    }

    // ------------------------------------------------------------------
    // 错误覆写
    // ------------------------------------------------------------------

    [Fact]
    public async Task 错误文本手动覆写应落FieldError且隐藏帮助文本()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<FormFieldFileUpload>(FieldParams(
            label: "上传附件", helperText: "支持 PDF 与图片",
            errorText: "服务端驳回：文件含病毒"));
        await SettleAsync(renderer);

        var markup = Scrub(renderer.Markup());
        Assert.Contains("服务端驳回：文件含病毒", markup);
        Assert.Contains("data-slot=\"field-error\"", markup);
        Assert.Contains("data-invalid=\"true\"", FieldRootTag(markup));
        // 标记已过 Scrub 归一化：GUID 段为 <id>。
        Assert.Contains("id=\"formfield-<id>-error\"", markup);
        // 错误态下帮助文本隐藏。
        Assert.DoesNotContain("支持 PDF 与图片", markup);
    }

    // ------------------------------------------------------------------
    // 文件选择沿与 EditContext 校验沿
    // ------------------------------------------------------------------

    [Fact]
    public async Task 选择文件应经封装层回填并上报且消解校验错误()
    {
        var doc = new Doc();
        var renderer = CreateRenderer();
        var context = CreateValidatedContext(doc);
        var reports = new List<IReadOnlyList<FileUploadItem>>();

        Expression<Func<IReadOnlyList<FileUploadItem>?>> filesExpression = () => doc.Files;
        await renderer.MountAsync<FormFieldBaseTests.EditContextHost>(new Dictionary<string, object?>
        {
            [nameof(FormFieldBaseTests.EditContextHost.EditContext)] = context,
            [nameof(FormFieldBaseTests.EditContextHost.ChildContent)] = (RenderFragment)(builder =>
            {
                // Files 须回写模型（FormFieldSwitchTests 先例）：校验器读的是 doc.Files
                // 没有回写则再校验仍见空而错误不消解。上报按引用留存，供 Assert.Same。
                builder.OpenComponent<FormFieldFileUpload>(0);
                builder.AddAttribute(1, nameof(FormFieldFileUpload.Files), doc.Files);
                builder.AddAttribute(2, nameof(FormFieldFileUpload.FilesExpression), filesExpression);
                builder.AddAttribute(3, nameof(FormFieldFileUpload.FilesChanged),
                    EventCallback.Factory.Create<IReadOnlyList<FileUploadItem>>(new object(), l =>
                    {
                        reports.Add(l);
                        doc.Files = l;
                    }));
                builder.AddAttribute(4, nameof(FormFieldFileUpload.Label), "上传附件");
                builder.CloseComponent();
            }),
        });
        await SettleAsync(renderer);

        // 初始 Files 为空：广播校验后错误经 FieldError 呈现。
        await renderer.Dispatcher.InvokeAsync(async () => await context.ValidateAsync());
        await SettleAsync(renderer);
        var markup = renderer.Markup();
        Assert.Contains("请至少选择一个文件", markup);
        Assert.Contains("data-slot=\"field-error\"", markup);

        // 选择一枚文件：底座 FilesChanged → 封装层 HandleFilesChanged 两步沿
        // （Files 赋值 + FilesChanged 上报 + NotifyFieldChanged 触发再校验）。
        var field = renderer.FindComponent<FormFieldFileUpload>();
        await SelectFilesAsync(renderer, field,
            new FakeBrowserFile("resume.pdf", 2048, "application/pdf"));

        var report = Assert.Single(reports);
        Assert.Equal("resume.pdf", Assert.Single(report).Name);
        Assert.NotNull(field.Files);                                        // 封装层回填
        Assert.Same(Assert.Single(field.Files!), Assert.Single(report));    // 同一列表引用
        markup = renderer.Markup();
        Assert.DoesNotContain("请至少选择一个文件", markup);                 // 校验消解
        Assert.Contains("resume.pdf", markup);                              // 文件入列显形
        Assert.Contains("2.0 KB", markup);                                  // FormattedSize
    }

    [Fact]
    public async Task ClearFiles应经底座清空并上报空列表()
    {
        var reports = new List<IReadOnlyList<FileUploadItem>>();
        var renderer = CreateRenderer();

        var field = await renderer.MountAsync<FormFieldFileUpload>(FieldParams(
            multiple: true,
            filesChanged: EventCallback.Factory.Create<IReadOnlyList<FileUploadItem>>(
                new object(), l => reports.Add([.. l]))));
        await SettleAsync(renderer);

        await SelectFilesAsync(renderer, field,
            new FakeBrowserFile("a.bin", 16, "application/octet-stream"));
        Assert.Contains("a.bin", renderer.Markup());

        await renderer.Dispatcher.InvokeAsync(async () => await field.ClearFiles());
        await SettleAsync(renderer);

        Assert.DoesNotContain("a.bin", renderer.Markup());
        var last = Assert.Single(reports, l => l.Count == 0);
        Assert.Empty(last);
    }

    // ------------------------------------------------------------------
    // 缺省参数面
    // ------------------------------------------------------------------

    [Fact]
    public async Task 缺省参数面应与一致()
    {
        var renderer = CreateRenderer();

        // 缺省：Multiple=false、MaxFileSize=10MB、MaxFileCount=10、
        // ShowPreview=true、Disabled=false、Required=false、引用面全 null。
        var field = await renderer.MountAsync<FormFieldFileUpload>(new Dictionary<string, object?>());
        await SettleAsync(renderer);

        Assert.False(field.Multiple);
        Assert.Equal(10485760L, field.MaxFileSize);
        Assert.Equal(10, field.MaxFileCount);
        Assert.True(field.ShowPreview);
        Assert.False(field.Disabled);
        Assert.False(field.Required);
        Assert.Null(field.Accept);
        Assert.Null(field.InputClass);
        Assert.Null(field.Label);
        Assert.Null(field.HelperText);
        Assert.Null(field.ErrorText);
        Assert.Null(field.Files);
        Assert.Null(field.DropzoneContent);
        Assert.False(field.FilesChanged.HasDelegate);
        Assert.False(field.OnValidationError.HasDelegate);
    }
}
