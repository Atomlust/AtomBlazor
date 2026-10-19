using AtomBlazor.Primitives;
using System.Reflection;
using System.Text.Json;
using AtomBlazor;
using AtomBlazor.Primitives.Services;
using AtomBlazor.Tests.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Xunit;

namespace AtomBlazor.Tests.Components;

/// <summary>
/// 自写补全波：RichTextEditor 组件的纯逻辑面——不启宿主、不经渲染器、不触真 JS interop
/// 直接实例化组件、反射驱动回调与状态机，断言：事件 DTO 与记录型的默认面和相等语义、
/// ShouldRender 跟踪位的状态机、JSInvokable 文本/选区回调的回写与聚焦推断、图片上传
/// 回调的准入判定（以记录型假流替代真流），以及 JS 未初始化时公开取值/设值面与释放
/// 路径的回落默认与安静无操作。
/// </summary>
/// <remarks>
/// 对本组件仅有 Sanitizer 白名单件（Utilities/RichTextEditorSanitizerTests）
/// 工具栏预设展开在 razor 标记内、依赖渲染宿主，本波不覆盖。方法名照 自写先例用中文。
/// </remarks>
public class RichTextEditorTests
{
    private static readonly BindingFlags access = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private static readonly string[] expectedTextCallbackSeen = ["<p>丙</p>", "d"];

    private static bool GetBool(RichTextEditor editor, string name)
    {
        var field = typeof(RichTextEditor).GetField(name, access);
        Assert.NotNull(field);
        return (bool)field!.GetValue(editor)!;
    }

    private static string GetString(RichTextEditor editor, string name)
    {
        var field = typeof(RichTextEditor).GetField(name, access);
        Assert.NotNull(field);
        return (string)field!.GetValue(editor)!;
    }

    private static void SetField(RichTextEditor editor, string name, object value)
    {
        var field = typeof(RichTextEditor).GetField(name, access);
        Assert.NotNull(field);
        field!.SetValue(editor, value);
    }

    private static bool ShouldRender(RichTextEditor editor)
    {
        var method = typeof(RichTextEditor).GetMethod("ShouldRender", access);
        Assert.NotNull(method);
        return (bool)method!.Invoke(editor, null)!;
    }

    private static async Task Invoke(RichTextEditor editor, string name, params object?[] args)
    {
        var method = typeof(RichTextEditor).GetMethod(name, access);
        Assert.NotNull(method);
        var result = method!.Invoke(editor, args);
        Assert.NotNull(result);
        if (result is Task task)
        {
            await task;
        }
        else if (result is ValueTask valueTask)
        {
            await valueTask;
        }
    }

    /// <summary>
    /// 挂进自研轻量渲染器取得实例：面内会触 StateHasChanged 的回调（文本回调的撤销位
    /// 翻转、格式状态刷新）需要已分配的 render handle，照 tests 既有渲染宿主先例挂载
    /// NoopJavaScript 令 JS 初始化安静失败，组件保持未初始化态。
    /// </summary>
    private static async Task<(ComponentTestRenderer Renderer, RichTextEditor Editor)> MountedAsync()
    {
        var js = new NoopJavaScript();
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(js);
        services.AddSingleton<IPortalService>(new PortalService(NullLogger<PortalService>.Instance));
        services.AddSingleton<IPositioningService>(new PositioningService(js));
        services.AddSingleton<IFocusManager>(new FocusManager(js));
        services.AddSingleton<INativeOverlayService>(new NativeOverlayService(js, new OverlayRenderingOptions()));
        services.AddSingleton<ILocalizer, DefaultLocalizer>();
        services.AddSingleton(typeof(Microsoft.Extensions.Logging.ILogger<>), typeof(NullLogger<>));
        var renderer = new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance);
        var editor = await renderer.MountAsync<RichTextEditor>(new Dictionary<string, object?>());
        await renderer.Dispatcher.InvokeAsync(() => Task.CompletedTask);
        return (renderer, editor);
    }

    /// <summary>记录型假流引用：替代真 JS stream，跟踪 OpenReadStream 与释放。</summary>
    private sealed class FakeStreamReference : IJSStreamReference
    {
        public FakeStreamReference(string identifier = "fake-stream")
        {
            Identifier = identifier;
        }

        public bool Opened { get; private set; }

        public bool Disposed { get; private set; }

        public string Identifier { get; }

        public long Length => 3L;

        public ValueTask<Stream> OpenReadStreamAsync(long maxAllowedSize = default, CancellationToken cancellationToken = default)
        {
            Opened = true;
            return new ValueTask<Stream>(new MemoryStream([1, 2, 3]));
        }

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }

    [Fact]
    public void 图片上传记录型按字段取值相等()
    {
        using var content = new MemoryStream([1, 2, 3]);
        var a = new EditorImageUpload("photo.png", "image/png", 3L, content);
        var b = new EditorImageUpload("photo.png", "image/png", 3L, content);
        var c = new EditorImageUpload("other.png", "image/png", 3L, content);

        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
        Assert.Equal("photo.png", a.FileName);
        Assert.Equal(3L, a.Size);
    }

    [Fact]
    public void 选区与两个事件参数对象默认面全空()
    {
        var range = new EditorRange();
        Assert.Equal(0, range.Index);
        Assert.Equal(0, range.Length);
        range.Index = 4;
        range.Length = 7;
        Assert.Equal((4, 7), (range.Index, range.Length));

        var selection = new SelectionChangeEventArgs();
        Assert.Null(selection.Range);
        Assert.Null(selection.OldRange);
        Assert.Null(selection.Source);
        Assert.Null(selection.Format);

        var text = new TextChangeEventArgs();
        Assert.Null(text.Delta);
        Assert.Null(text.Html);
        Assert.Null(text.Source);
        Assert.Equal(0, text.Length);
        Assert.False(text.CanUndo);
        Assert.False(text.CanRedo);
    }

    [Fact]
    public void 工具栏预设按由简到繁排序五档齐备()
    {
        var values = new[]
        {
            ToolbarPreset.None, ToolbarPreset.Simple, ToolbarPreset.Standard,
            ToolbarPreset.Full, ToolbarPreset.Custom,
        };
        Assert.Equal(values.OrderBy(v => (int)v).ToArray(), values);
        Assert.Equal(5, values.Length);
    }

    [Fact]
    public void 参数变更旗标首次放行随后清零()
    {
        var editor = new RichTextEditor();
        SetField(editor, "_parametersChanged", true);

        Assert.True(ShouldRender(editor));
        Assert.False(GetBool(editor, "_parametersChanged"));
        Assert.False(ShouldRender(editor));
    }

    [Theory]
    [InlineData("value")]
    [InlineData("disabled")]
    [InlineData("formatState")]
    public void 值禁用或格式状态变化触发重渲染(string what)
    {
        var editor = new RichTextEditor();
        ShouldRender(editor); // 先立基线（_lastValue 等与当前对齐）

        switch (what)
        {
            case "value":
                editor.Value = "<p>新</p>";
                break;
            case "disabled":
                editor.Disabled = true;
                break;
            default:
                SetField(editor, "_formatStateChanged", true);
                break;
        }

        Assert.True(ShouldRender(editor));
        Assert.False(ShouldRender(editor));
    }

    [Fact]
    public void 无任何变化时不放行重渲染()
    {
        var editor = new RichTextEditor();

        Assert.False(ShouldRender(editor));
        Assert.False(ShouldRender(editor));
    }

    [Fact]
    public async Task 文本回调回写值与Delta并同步撤销位()
    {
        var (renderer, editor) = await MountedAsync();

        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            await Invoke(editor, "OnTextChangeCallback", new TextChangeEventArgs
            {
                Html = "<p>甲</p>",
                Delta = "{\"ops\":[]}",
                CanUndo = true,
                CanRedo = false,
            });

            Assert.Equal("<p>甲</p>", editor.Value);
            Assert.Equal("{\"ops\":[]}", editor.DeltaValue);
            Assert.True(GetBool(editor, "_canUndo"));
            Assert.False(GetBool(editor, "_canRedo"));

            await Invoke(editor, "OnTextChangeCallback", new TextChangeEventArgs { Html = "<p>乙</p>", CanUndo = true });
            Assert.Equal("<p>乙</p>", editor.Value);

            // 重做位独立翻转
            await Invoke(editor, "OnTextChangeCallback", new TextChangeEventArgs { Html = "<p>乙</p>", CanUndo = true, CanRedo = true });
            Assert.True(GetBool(editor, "_canRedo"));
        });

        // 注：格式旗标在置位当刻即被同步渲染批次（ShouldRender）消费，其语义由
        // ShouldRender 状态机用例直接以字段驱动覆盖，这里不再断言旗标本体。
    }

    [Fact]
    public async Task 文本回调把参数递交给变更委托()
    {
        var seen = new List<string?>();
        var delivered = new List<TextChangeEventArgs>();
        var editor = new RichTextEditor
        {
            ValueChanged = EventCallback.Factory.Create<string?>(new object(), v => seen.Add(v)),
            DeltaValueChanged = EventCallback.Factory.Create<string?>(new object(), v => seen.Add(v)),
            OnTextChange = EventCallback.Factory.Create<TextChangeEventArgs>(new object(), a => delivered.Add(a)),
        };
        var args = new TextChangeEventArgs { Html = "<p>丙</p>", Delta = "d", Source = "user" };

        await Invoke(editor, "OnTextChangeCallback", args);

        Assert.Equal(expectedTextCallbackSeen, seen);
        Assert.Single(delivered);
        Assert.Same(args, delivered[0]);
    }

    [Theory]
    [InlineData(true, false, "focus")]
    [InlineData(false, true, "blur")]
    [InlineData(false, false, "none")]
    [InlineData(true, true, "none")]
    public async Task 选区回调按新旧范围推断聚焦失焦(bool hasRange, bool hasOldRange, string expected)
    {
        var log = new List<string>();
        var editor = new RichTextEditor
        {
            OnFocus = EventCallback.Factory.Create(new object(), () => log.Add("focus")),
            OnBlur = EventCallback.Factory.Create(new object(), () => log.Add("blur")),
            OnSelectionChange = EventCallback.Factory.Create<SelectionChangeEventArgs>(new object(), _ => log.Add("change")),
        };
        var args = new SelectionChangeEventArgs
        {
            Range = hasRange ? new EditorRange { Index = 1 } : null,
            OldRange = hasOldRange ? new EditorRange { Index = 0 } : null,
        };

        await Invoke(editor, "OnSelectionChangeCallback", args);

        var focusLog = log.Where(x => x != "change").ToList();
        Assert.Equal(expected == "none" ? Array.Empty<string>() : new[] { expected }, focusLog);
        Assert.Contains("change", log);
    }

    [Fact]
    public async Task 选区格式字典刷新状态位并点亮格式旗标()
    {
        var (renderer, editor) = await MountedAsync();
        using var headerDoc = JsonDocument.Parse("2");
        var args = new SelectionChangeEventArgs
        {
            Format = new Dictionary<string, object?>
            {
                ["bold"] = true,
                ["list"] = "unchecked",
                ["header"] = headerDoc.RootElement,
                ["align"] = "center",
                ["table"] = "row-1",
            },
        };

        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            await Invoke(editor, "OnSelectionChangeCallback", args);

            Assert.True(GetBool(editor, "_isBold"));
            Assert.True(GetBool(editor, "_isCheckList"));
            Assert.False(GetBool(editor, "_isBulletList"));
            Assert.Equal("2", GetString(editor, "_headerLevel"));
            Assert.Equal("center", GetString(editor, "_align"));
            Assert.True(GetBool(editor, "_isInTable"));
        });
    }

    [Fact]
    public async Task 未配置上传处理程序时拒图并释放流()
    {
        var editor = new RichTextEditor();
        var stream = new FakeStreamReference();

        var result = await editor.OnImageUploadCallback(stream, "photo.png", "image/png", 3L);

        Assert.Null(result);
        Assert.False(stream.Opened);
        Assert.True(stream.Disposed);
    }

    [Fact]
    public async Task 超限尺寸在搬运任何数据前被拒()
    {
        var editor = new RichTextEditor { MaxImageSize = 10L };
        var called = false;
        editor.ImageUploader = _ => { called = true; return Task.FromResult<string?>("https://cdn/x.png"); };
        var stream = new FakeStreamReference();

        var result = await editor.OnImageUploadCallback(stream, "big.png", "image/png", 11L);

        Assert.Null(result);
        Assert.False(called);
        Assert.False(stream.Opened);
        Assert.True(stream.Disposed);
    }

    [Fact]
    public async Task 上传处理程序收到完整元数据并回传嵌入地址()
    {
        var editor = new RichTextEditor();
        EditorImageUpload? captured = null;
        editor.ImageUploader = async upload =>
        {
            captured = upload;
            using var memory = new MemoryStream();
            await upload.Content.CopyToAsync(memory);
            return upload.FileName + "?" + memory.Length;
        };
        var stream = new FakeStreamReference();

        var result = await editor.OnImageUploadCallback(stream, "photo.png", "image/png", 3L);

        Assert.NotNull(captured);
        Assert.Equal("photo.png", captured!.FileName);
        Assert.Equal("image/png", captured.ContentType);
        Assert.Equal(3L, captured.Size);
        Assert.Equal("photo.png?3", result);
        Assert.True(stream.Opened);
        Assert.True(stream.Disposed);
    }

    [Fact]
    public async Task JS未初始化时公开取值面回落默认设值面安静()
    {
        var editor = new RichTextEditor();

        Assert.Equal(string.Empty, await editor.GetTextAsync());
        Assert.Equal(0, await editor.GetLengthAsync());
        Assert.Equal(string.Empty, await editor.GetHtmlAsync());
        Assert.Equal("{}", await editor.GetDeltaAsync());
        Assert.Null(await editor.GetSelectionAsync());

        // 设值面在未初始化时不发任何 interop、不抛异常
        await editor.SetHtmlAsync("<p>x</p>");
        await editor.SetDeltaAsync("{\"ops\":[]}");
        await editor.SetSelectionAsync(0, 1);
    }

    [Fact]
    public async Task JS未初始化时释放安全完成()
    {
        var editor = new RichTextEditor();

        await editor.DisposeAsync();

        Assert.Null(PrivateField(editor, "_jsModule"));
    }

    private static object? PrivateField(RichTextEditor editor, string name)
    {
        var field = typeof(RichTextEditor).GetField(name, access);
        Assert.NotNull(field);
        return field!.GetValue(editor);
    }
}
