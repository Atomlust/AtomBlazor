using Ganss.Xss;
using Markdig;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using AtomBlazor.Primitives.Services;

namespace AtomBlazor;

/// <summary>
/// Markdown 编辑器组件，带工具栏与预览功能。
/// 设计灵感来自 GitHub 的评论编辑器。
/// </summary>
public partial class MarkdownEditor : ComponentBase, IAsyncDisposable
{
    private IJSObjectReference? _module;
    private DotNetObjectReference<MarkdownEditor>? _dotNetRef;
    private ElementReference _textareaRef;
    private string _activeTab = "write";
    private bool _shouldPreventKeydown;

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = default!;

    /// <summary>
    /// 获取或设置 Markdown 内容值。
    /// </summary>
    [Parameter]
    public string? Value { get; set; }

    /// <summary>
    /// 获取或设置值变更时调用的回调。
    /// </summary>
    [Parameter]
    public EventCallback<string?> ValueChanged { get; set; }

    /// <summary>
    /// 获取或设置编辑器为空时显示的占位文本。
    /// </summary>
    [Parameter]
    public string? Placeholder { get; set; }

    /// <summary>
    /// 获取或设置编辑器是否禁用。
    /// </summary>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>
    /// 获取或设置应用到编辑器容器的附加 CSS 类。
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置应用到根元素的附加 HTML 属性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>
    /// 获取或设置 textarea 元素的 HTML id 属性。
    /// </summary>
    [Parameter]
    public string? Id { get; set; }

    /// <summary>
    /// 获取或设置 textarea 的 ARIA 标签。
    /// </summary>
    [Parameter]
    public string? AriaLabel { get; set; }

    /// <summary>
    /// 获取或设置描述该 textarea 的元素 ID。
    /// </summary>
    [Parameter]
    public string? AriaDescribedBy { get; set; }

    /// <summary>
    /// 获取或设置 textarea 的值是否无效。
    /// </summary>
    [Parameter]
    public bool? AriaInvalid { get; set; }

    /// <summary>
    /// 获取或设置编辑器 textarea 与预览区的最小高度（如 "200px"、"10rem"）。
    /// 默认为 "150px"。
    /// </summary>
    [Parameter]
    public string? MinHeight { get; set; }

    /// <summary>
    /// 用于将 Markdown 转换为 HTML 的 Markdig 管道。
    /// </summary>
    private static readonly MarkdownPipeline pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .UseAutoLinks()
        .Build();

    /// <summary>
    /// 用于防 XSS 的 HTML 净化器。可静态使用，线程安全。
    /// </summary>
    private static readonly HtmlSanitizer sanitizer = new();

    /// <summary>
    /// 获取由 Markdown 内容渲染出的 HTML。
    /// HTML 已经过净化以防 XSS 攻击。
    /// </summary>
    private string RenderedHtml => string.IsNullOrWhiteSpace(Value)
        ? "<p class=\"text-muted-foreground italic\">Nothing to preview</p>"
        : sanitizer.Sanitize(Markdown.ToHtml(Value, pipeline));

    /// <summary>
    /// 获取编辑器容器的 CSS 类。
    /// </summary>
    private string ContainerCssClass => ClassNames.cn(
        "atom:flex atom:flex-col atom:rounded-md atom:border atom:border-input atom:bg-background",
        "atom:focus-within:ring-2 atom:focus-within:ring-ring atom:focus-within:ring-offset-2",
        ClassNames.when(AriaInvalid == true, "atom:border-destructive atom:ring-destructive/20"),
        ClassNames.when(Disabled, "atom:opacity-50 atom:cursor-not-allowed"),
        Class
    );

    /// <summary>
    /// 获取用于覆盖 min-height 的行内样式。
    /// </summary>
    private string? MinHeightStyle => MinHeight != null ? $"min-height:{MinHeight}" : null;

    /// <summary>
    /// 获取 textarea 的 CSS 类。
    /// </summary>
    private string TextareaCssClass => ClassNames.cn(
        "atom:flex-1 atom:w-full atom:resize-y atom:border-0 atom:bg-transparent",
        MinHeight == null ? "atom:min-h-[150px]" : null,
        "atom:px-3 atom:py-2 atom:text-sm atom:placeholder:text-muted-foreground",
        "atom:focus:outline-none",
        "atom:disabled:cursor-not-allowed"
    );

    /// <summary>
    /// 获取预览区的 CSS 类。
    /// </summary>
    private string PreviewCssClass => ClassNames.cn(
        "atom:flex-1 atom:w-full atom:px-3 atom:py-2 atom:overflow-auto",
        MinHeight == null ? "atom:min-h-[150px]" : null,
        "prose prose-sm dark:prose-invert atom:max-w-none",
        "atom:[&_h1]:text-2xl atom:[&_h1]:font-bold atom:[&_h1]:mb-2",
        "atom:[&_h2]:text-xl atom:[&_h2]:font-bold atom:[&_h2]:mb-2",
        "atom:[&_h3]:text-lg atom:[&_h3]:font-bold atom:[&_h3]:mb-2",
        "atom:[&_p]:mb-2 atom:[&_ul]:list-disc atom:[&_ul]:ms-4 atom:[&_ol]:list-decimal atom:[&_ol]:ms-4",
        "atom:[&_li]:mb-1 atom:[&_strong]:font-bold atom:[&_em]:italic atom:[&_u]:underline"
    );

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            try
            {
                _dotNetRef = DotNetObjectReference.Create(this);
                _module = await JsModules.GetAsync(JSRuntime, "./_content/AtomBlazor/js/markdown-editor.js");

                // 初始化列表续行行为与撤销/重做
                await _module.InvokeVoidAsync("initializeListContinuation", _textareaRef, _dotNetRef);
            }
            catch (JSException)
            {
                // JS 模块不可用，继续运行但不带 JS 特性
            }
        }
    }

    /// <summary>
    /// 由 JavaScript 在内容因撤销/重做而变化时回调。
    /// </summary>
    [JSInvokable]
    public async Task OnContentChanged(string value)
    {
        Value = value;

        if (ValueChanged.HasDelegate)
        {
            await ValueChanged.InvokeAsync(value);
        }
    }

    /// <summary>
    /// 处理 textarea 中的输入变更。
    /// </summary>
    private async Task HandleInput(ChangeEventArgs args)
    {
        var newValue = args.Value?.ToString();
        Value = newValue;

        if (ValueChanged.HasDelegate)
        {
            await ValueChanged.InvokeAsync(newValue);
        }
    }

    /// <summary>
    /// 处理 textarea 中的键盘快捷键。
    /// </summary>
    private async Task HandleKeyDown(KeyboardEventArgs e)
    {
        _shouldPreventKeydown = false;

        // 处理 Ctrl/Cmd 快捷键
        if (e.CtrlKey || e.MetaKey)
        {
            switch (e.Key.ToLowerInvariant())
            {
                case "b":
                    _shouldPreventKeydown = true;
                    await ApplyBold();
                    break;
                case "i":
                    _shouldPreventKeydown = true;
                    await ApplyItalic();
                    break;
                case "u":
                    _shouldPreventKeydown = true;
                    await ApplyUnderline();
                    break;
            }
        }
        // 说明：回车键的列表续行由 OnAfterRenderAsync 中
        // 初始化的 JS 事件监听器处理
    }

    /// <summary>
    /// 对选中文本应用加粗格式。
    /// </summary>
    private async Task ApplyBold()
    {
        if (_module == null || Disabled)
        {
            return;
        }

        try
        {
            var newValue = await _module.InvokeAsync<string>(
                "insertFormatting", _textareaRef, "**", "**", "bold text");
            await UpdateValue(newValue);
        }
        catch (JSException)
        {
            // 忽略 JS 错误
        }
    }

    /// <summary>
    /// 对选中文本应用斜体格式。
    /// </summary>
    private async Task ApplyItalic()
    {
        if (_module == null || Disabled)
        {
            return;
        }

        try
        {
            var newValue = await _module.InvokeAsync<string>(
                "insertFormatting", _textareaRef, "*", "*", "italic text");
            await UpdateValue(newValue);
        }
        catch (JSException)
        {
            // 忽略 JS 错误
        }
    }

    /// <summary>
    /// 对选中文本应用下划线格式。
    /// </summary>
    private async Task ApplyUnderline()
    {
        if (_module == null || Disabled)
        {
            return;
        }

        try
        {
            var newValue = await _module.InvokeAsync<string>(
                "insertFormatting", _textareaRef, "<u>", "</u>", "underlined text");
            await UpdateValue(newValue);
        }
        catch (JSException)
        {
            // 忽略 JS 错误
        }
    }

    /// <summary>
    /// 对当前行应用标题格式。
    /// </summary>
    private async Task ApplyHeading(int level)
    {
        if (_module == null || Disabled)
        {
            return;
        }

        try
        {
            var prefix = level switch
            {
                1 => "# ",
                2 => "## ",
                3 => "### ",
                _ => ""
            };

            if (string.IsNullOrEmpty(prefix))
            {
                // 移除标题（还原为普通段落）
                var newValue = await _module.InvokeAsync<string>(
                    "removeLinePrefix", _textareaRef);
                await UpdateValue(newValue);
            }
            else
            {
                var newValue = await _module.InvokeAsync<string>(
                    "insertLinePrefix", _textareaRef, prefix);
                await UpdateValue(newValue);
            }
        }
        catch (JSException)
        {
            // 忽略 JS 错误
        }
    }

    /// <summary>
    /// 对当前行应用无序列表格式。
    /// </summary>
    private async Task ApplyBulletList()
    {
        if (_module == null || Disabled)
        {
            return;
        }

        try
        {
            var newValue = await _module.InvokeAsync<string>(
                "insertLinePrefix", _textareaRef, "- ");
            await UpdateValue(newValue);
        }
        catch (JSException)
        {
            // 忽略 JS 错误
        }
    }

    /// <summary>
    /// 对当前行应用有序列表格式。
    /// </summary>
    private async Task ApplyNumberedList()
    {
        if (_module == null || Disabled)
        {
            return;
        }

        try
        {
            var newValue = await _module.InvokeAsync<string>(
                "insertLinePrefix", _textareaRef, "1. ");
            await UpdateValue(newValue);
        }
        catch (JSException)
        {
            // 忽略 JS 错误
        }
    }

    /// <summary>
    /// 更新值并通知父组件。
    /// </summary>
    private async Task UpdateValue(string newValue)
    {
        Value = newValue;

        if (ValueChanged.HasDelegate)
        {
            await ValueChanged.InvokeAsync(newValue);
        }

        StateHasChanged();
    }

    /// <summary>
    /// 处理标签页切换。
    /// </summary>
    private void HandleTabChange(string? tab)
    {
        if (tab != null)
        {
            _activeTab = tab;
        }
    }

    /// <summary>
    /// 根据标签页的激活状态获取其 CSS 类（GitHub 风格）。
    /// </summary>
    private string GetTabClass(string tabValue)
    {
        var isActive = _activeTab == tabValue;
        return ClassNames.cn(
            // 重置 TabsTrigger 默认样式
            "atom:!shadow-none atom:!ring-0 atom:!ring-offset-0",
            // 基础样式
            "atom:px-3 atom:py-1 atom:text-sm atom:font-medium atom:transition-colors atom:rounded-sm",
            // 激活态为白色背景，非激活态为透明
            isActive
                ? "atom:bg-background atom:text-foreground atom:shadow-sm"
                : "atom:bg-transparent atom:text-muted-foreground atom:hover:text-foreground"
        );
    }

    public async ValueTask DisposeAsync()
    {
        if (_module != null)
        {
            try
            {
                // 清理列表续行监听器与编辑器数据
                await _module.InvokeVoidAsync("disposeListContinuation", _textareaRef);
            }
            catch (Exception ex) when (ex is JSDisconnectedException or JSException or TaskCanceledException or ObjectDisposedException)
            {
                // 电路已断开，忽略
            }
        }

        _dotNetRef?.Dispose();
        GC.SuppressFinalize(this);
    }
}
