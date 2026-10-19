// 与一致保留 32 处未守卫 interop 调用（L292–L1037 跨全篇的 InvokeAsync/InvokeVoidAsync，皆在 jsInitialized 门内由用户交互驱动，断线由电路生命周期兜底）；逐点 pragma 会打散与逐行对齐的行位，采文件级抑制
#pragma warning disable BL0016
using System.Text.Json;
using Ganss.Xss;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using AtomBlazor.Primitives.Services;

namespace AtomBlazor;

/// <summary>
/// 基于 Quill.js 构建、遵循 shadcn/ui 设计体系的富文本编辑器组件。
/// </summary>
public partial class RichTextEditor : ComponentBase, IAsyncDisposable
{
    // === 私有字段 ===
    private ElementReference _editorRef;
    private IJSObjectReference? _jsModule;
    private DotNetObjectReference<RichTextEditor>? _dotNetRef;
    private string _editorId = Guid.NewGuid().ToString("N");
    private bool _jsInitialized;
    private string? _lastKnownValue;
    private bool _pendingValueUpdate;

    // === 格式状态跟踪 ===
    private bool _isBold;
    private bool _isItalic;
    private bool _isUnderline;
    private bool _isStrike;
    private bool _isBulletList;
    private bool _isOrderedList;
    private bool _isBlockquote;
    private bool _isCodeBlock;
    private bool _isCode;
    private bool _isCheckList;
    private string _headerLevel = "";
    private string _align = "";
    private bool _canUndo;
    private bool _canRedo;

    // === 颜色弹出面板 ===
    private bool _textColorOpen;
    private bool _highlightOpen;

    /// <summary>提供的文字颜色色块：先灰色系，再每个色相取一个饱和色。</summary>
    private static readonly string[] textColors =
    [
        "#000000", "#404040", "#737373", "#a3a3a3", "#d4d4d4", "#ffffff", "#78350f",
        "#dc2626", "#ea580c", "#ca8a04", "#16a34a", "#0891b2", "#2563eb", "#7c3aed",
    ];

    /// <summary>提供的高亮色块：浅色调，保证文字依然清晰可读。</summary>
    private static readonly string[] highlightColors =
    [
        "#fef08a", "#fde68a", "#fed7aa", "#fecaca", "#fbcfe8", "#ddd6fe", "#c7d2fe",
        "#bfdbfe", "#a5f3fc", "#99f6e4", "#bbf7d0", "#d9f99d", "#e5e5e5", "#f5f5f4",
    ];

    // === 表格状态 ===
    private const int tablePickerSize = 6;
    private bool _isInTable;
    private bool _tableMenuOpen;
    private int _tablePickerRows;
    private int _tablePickerColumns;

    // === 链接对话框状态 ===
    private bool _linkDialogOpen;
    private string _linkUrl = "";
    private string? _linkUrlError;
    private bool _hasExistingLink;
    private EditorRange? _savedSelection;

    // === ShouldRender 跟踪 ===
    private bool _parametersChanged;
    private string? _lastValue;
    private bool _lastDisabled;
    private bool _lastReadOnly;
    private bool _lastLinkDialogOpen;
    private bool _lastTableMenuOpen;
    private bool _lastHasImageUploader;
    private bool _formatStateChanged;

    /// <summary>
    /// 用于防 XSS 的 HTML 净化器。可静态使用，线程安全。
    /// </summary>
    private static readonly HtmlSanitizer sanitizer = CreateSanitizer();

    private static HtmlSanitizer CreateSanitizer()
    {
        var sanitizer = new HtmlSanitizer();

        // Quill 用 data-list="checked|unchecked" 标记检查清单项；缺少该规则时
        // 绑定的 Value 回传后只会是普通项目符号。
        sanitizer.AllowedAttributes.Add("data-list");

        // 未经 ImageUploader 插入的图片是数据 URL，默认净化规则会将其剔除。
        // 此处仅放行 <img src> 上的图片数据；其他位置的 data: 仍一律拦截
        // 因为出现在 <a href> 上时它可能携带脚本。
        sanitizer.FilterUrl += (_, e) =>
        {
            if (e.SanitizedUrl == null
                && string.Equals(e.Tag.TagName, "IMG", StringComparison.OrdinalIgnoreCase)
                && e.OriginalUrl.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
            {
                e.SanitizedUrl = e.OriginalUrl;
            }
        };

        return sanitizer;
    }

    // === 参数 —— 值绑定 ===

    /// <summary>
    /// 获取或设置编辑器的 HTML 内容。
    /// </summary>
    [Parameter]
    public string? Value { get; set; }

    /// <summary>
    /// 获取或设置编辑器内容变更时调用的回调。
    /// </summary>
    [Parameter]
    public EventCallback<string?> ValueChanged { get; set; }

    /// <summary>
    /// 获取或设置编辑器内容的 Delta（JSON）表示。
    /// </summary>
    [Parameter]
    public string? DeltaValue { get; set; }

    /// <summary>
    /// 获取或设置 Delta 内容变更时调用的回调。
    /// </summary>
    [Parameter]
    public EventCallback<string?> DeltaValueChanged { get; set; }

    // === 参数 —— 工具栏 ===

    /// <summary>
    /// 获取或设置工具栏预设配置。
    /// </summary>
    [Parameter]
    public ToolbarPreset Toolbar { get; set; } = ToolbarPreset.Standard;

    /// <summary>
    /// 获取或设置自定义工具栏内容。
    /// </summary>
    [Parameter]
    public RenderFragment? ToolbarContent { get; set; }

    // === 参数 —— 图片 ===

    /// <summary>
    /// 接收用户添加的每一张图片——无论来自工具栏按钮、拖放还是粘贴——
    /// 返回要嵌入的 URL，返回 <c>null</c> 则拒绝该文件。请把图片存到你存放
    /// 上传物的位置并回传其地址。若无处理程序，图片会以数据 URL 的形式
    /// 内嵌在 HTML 里，这也是 Quill 的默认行为；小图尚可，但会膨胀值体量
    /// 并非入库时想要的效果。
    /// </summary>
    [Parameter]
    public Func<EditorImageUpload, Task<string?>>? ImageUploader { get; set; }

    /// <summary>
    /// 获取或设置允许流式传给 <see cref="ImageUploader"/> 的图片最大字节数。
    /// 超限文件在搬运任何数据之前即被拒绝。默认为 10 MB。
    /// </summary>
    [Parameter]
    public long MaxImageSize { get; set; } = 10 * 1024 * 1024;

    // === 参数 —— 外观 ===

    /// <summary>
    /// 获取或设置编辑器为空时显示的占位文本。
    /// </summary>
    [Parameter]
    public string? Placeholder { get; set; }

    /// <summary>
    /// 获取或设置编辑器的最小高度。
    /// </summary>
    [Parameter]
    public string MinHeight { get; set; } = "150px";

    /// <summary>
    /// 获取或设置编辑器的最大高度。超出后内容将滚动显示。
    /// </summary>
    [Parameter]
    public string? MaxHeight { get; set; }

    /// <summary>
    /// 获取或设置编辑器的固定高度。
    /// </summary>
    [Parameter]
    public string? Height { get; set; }

    /// <summary>
    /// 获取或设置容器的附加 CSS 类。
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置应用到根元素的附加 HTML 属性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>
    /// 获取或设置编辑器容器的 HTML id 属性。
    /// </summary>
    [Parameter]
    public string? Id { get; set; }

    // === 参数 —— 状态 ===

    /// <summary>
    /// 获取或设置编辑器是否禁用。
    /// </summary>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>
    /// 获取或设置编辑器是否只读。
    /// </summary>
    [Parameter]
    public bool ReadOnly { get; set; }

    // === 参数 —— 无障碍 ===

    /// <summary>
    /// 获取或设置编辑器的 ARIA 标签。
    /// </summary>
    [Parameter]
    public string? AriaLabel { get; set; }

    /// <summary>
    /// 获取或设置描述该编辑器的元素 ID。
    /// </summary>
    [Parameter]
    public string? AriaDescribedBy { get; set; }

    /// <summary>
    /// 获取或设置编辑器的值是否无效。
    /// </summary>
    [Parameter]
    public bool? AriaInvalid { get; set; }

    // === 参数 —— 事件 ===

    /// <summary>
    /// 获取或设置编辑器内容变更时调用的回调。
    /// </summary>
    [Parameter]
    public EventCallback<TextChangeEventArgs> OnTextChange { get; set; }

    /// <summary>
    /// 获取或设置选区变更时调用的回调。
    /// </summary>
    [Parameter]
    public EventCallback<SelectionChangeEventArgs> OnSelectionChange { get; set; }

    /// <summary>
    /// 获取或设置编辑器获得焦点时调用的回调。
    /// </summary>
    [Parameter]
    public EventCallback OnFocus { get; set; }

    /// <summary>
    /// 获取或设置编辑器失去焦点时调用的回调。
    /// </summary>
    [Parameter]
    public EventCallback OnBlur { get; set; }

    // === 生命周期方法 ===

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await InitializeJsAsync();
        }
    }

    protected override async Task OnParametersSetAsync()
    {
        _parametersChanged = true;

        var hasImageUploader = ImageUploader != null;
        if (_jsInitialized && _jsModule != null && hasImageUploader != _lastHasImageUploader)
        {
            _lastHasImageUploader = hasImageUploader;
            await _jsModule.InvokeVoidAsync("setImageUploader", _editorId, hasImageUploader);
        }

        // 若 Value 被外部修改，则更新编辑器
        if (_jsInitialized && Value != _lastKnownValue && !_pendingValueUpdate)
        {
            _pendingValueUpdate = true;
            try
            {
                await SetHtmlAsync(Value);
                _lastKnownValue = Value;
            }
            finally
            {
                _pendingValueUpdate = false;
            }
        }
    }

    private async Task InitializeJsAsync()
    {
        if (_jsInitialized)
        {
            return;
        }

        try
        {
            _jsModule = await JsModules.GetAsync(JS, "./_content/AtomBlazor/js/quill-interop.js");
            _dotNetRef = DotNetObjectReference.Create(this);

            var options = BuildEditorOptions();
            _lastHasImageUploader = ImageUploader != null;
            await _jsModule.InvokeVoidAsync("initializeEditor",
                _editorRef, _dotNetRef, _editorId, options);
            _jsInitialized = true;

            // 设置初始内容（已净化以防 XSS）
            if (!string.IsNullOrEmpty(Value))
            {
                var sanitized = sanitizer.Sanitize(Value);
                await _jsModule.InvokeVoidAsync("setHtml", _editorId, sanitized);
                _lastKnownValue = Value;
            }

            // 应用禁用状态
            if (Disabled)
            {
                await _jsModule.InvokeVoidAsync("disable", _editorId);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to initialize RichTextEditor JS: {ex.Message}");
        }
    }

    // === JSInvokable 回调 ===

    [JSInvokable]
    public async Task OnTextChangeCallback(TextChangeEventArgs args)
    {
        if (_canUndo != args.CanUndo || _canRedo != args.CanRedo)
        {
            _canUndo = args.CanUndo;
            _canRedo = args.CanRedo;
            _formatStateChanged = true;
            StateHasChanged();
        }

        _lastKnownValue = args.Html;
        Value = args.Html;
        await ValueChanged.InvokeAsync(args.Html);

        DeltaValue = args.Delta;
        await DeltaValueChanged.InvokeAsync(args.Delta);

        await OnTextChange.InvokeAsync(args);
    }

    /// <summary>
    /// 当设置了 <see cref="ImageUploader"/> 时，由 JavaScript 对用户选取、
    /// 拖放或粘贴的每张图片回调本方法。返回要嵌入的 URL，返回 <c>null</c> 则跳过。
    /// </summary>
    [JSInvokable]
    public async Task<string?> OnImageUploadCallback(IJSStreamReference stream, string fileName, string contentType, long size)
    {
        await using (stream)
        {
            if (ImageUploader == null || size > MaxImageSize)
            {
                return null;
            }

            await using var content = await stream.OpenReadStreamAsync(MaxImageSize);
            return await ImageUploader(new EditorImageUpload(fileName, contentType, size, content));
        }
    }

    [JSInvokable]
    public async Task OnSelectionChangeCallback(SelectionChangeEventArgs args)
    {
        // 依据选区更新格式状态
        if (args.Format != null)
        {
            UpdateFormatState(args.Format);
        }

        // 由选区推断聚焦/失焦（range 为 null 即已失焦）
        if (args.Range == null && args.OldRange != null)
        {
            await OnBlur.InvokeAsync();
        }
        else if (args.Range != null && args.OldRange == null)
        {
            await OnFocus.InvokeAsync();
        }

        await OnSelectionChange.InvokeAsync(args);
    }

    private static bool GetFormatBool(Dictionary<string, object?> format, string key)
    {
        if (!format.TryGetValue(key, out var value) || value == null)
        {
            return false;
        }

        if (value is bool b)
        {
            return b;
        }

        if (value is JsonElement je && je.ValueKind == JsonValueKind.True)
        {
            return true;
        }

        return false;
    }

    private static string GetFormatString(Dictionary<string, object?> format, string key)
    {
        if (!format.TryGetValue(key, out var value) || value == null)
        {
            return "";
        }

        if (value is string s)
        {
            return s;
        }

        if (value is JsonElement je)
        {
            if (je.ValueKind == JsonValueKind.String)
            {
                return je.GetString() ?? "";
            }
            if (je.ValueKind == JsonValueKind.Number)
            {
                return je.GetInt32().ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        return value.ToString() ?? "";
    }

    // === 工具栏动作 ===

    private async Task ToggleFormatAsync(string format, object? value = null)
    {
        if (_jsModule == null || !_jsInitialized || Disabled)
        {
            return;
        }

        // 切换：已激活则移除，否则应用
        var isActive = format switch
        {
            "bold" => _isBold,
            "italic" => _isItalic,
            "underline" => _isUnderline,
            "strike" => _isStrike,
            "code" => _isCode,
            "blockquote" => _isBlockquote,
            "code-block" => _isCodeBlock,
            "list" when value?.ToString() == "bullet" => _isBulletList,
            "list" when value?.ToString() == "ordered" => _isOrderedList,
            "list" when value?.ToString() == "check" => _isCheckList,
            _ => false
        };

        // 检查清单即 Quill 取值为 "unchecked"（或 "checked"）的列表格式
        // "check" 是它在工具栏上的名称。
        var applied = value?.ToString() == "check" ? "unchecked" : value;
        var newValue = isActive ? false : (applied ?? true);

        // 所有格式统一走 formatAndGetState，确保状态即时同步
        var formatState = await _jsModule.InvokeAsync<Dictionary<string, object?>>(
            "formatAndGetState", _editorId, format, newValue);
        UpdateFormatState(formatState);

        // 工具栏按钮点击后让编辑器重新聚焦
        await _jsModule.InvokeVoidAsync("focus", _editorId);
    }

    private void UpdateFormatState(Dictionary<string, object?> format)
    {
        if (format == null)
        {
            return;
        }

        _isBold = GetFormatBool(format, "bold");
        _isItalic = GetFormatBool(format, "italic");
        _isUnderline = GetFormatBool(format, "underline");
        _isStrike = GetFormatBool(format, "strike");
        _isBlockquote = GetFormatBool(format, "blockquote");
        _isCodeBlock = GetFormatBool(format, "code-block");
        _isCode = GetFormatBool(format, "code");

        var listValue = GetFormatString(format, "list");
        _isBulletList = listValue == "bullet";
        _isOrderedList = listValue == "ordered";
        _isCheckList = listValue is "checked" or "unchecked";

        _headerLevel = GetFormatString(format, "header");
        _align = GetFormatString(format, "align");

        // Quill 会报告光标所在单元格的行 id；只要有值就意味着“在表格内”。
        _isInTable = !string.IsNullOrEmpty(GetFormatString(format, "table"));

        // 标记格式状态已变更，供 ShouldRender 优化使用
        _formatStateChanged = true;
        StateHasChanged();
    }

    /// <summary>
    /// 基于被跟踪的状态变更判断组件是否需要重新渲染。
    /// 该优化可减少双向绑定带来的多余渲染周期。
    /// </summary>
    protected override bool ShouldRender()
    {
        if (_parametersChanged)
        {
            _parametersChanged = false;
            _lastValue = Value;
            _lastDisabled = Disabled;
            _lastReadOnly = ReadOnly;
            _lastLinkDialogOpen = _linkDialogOpen;
            _lastTableMenuOpen = _tableMenuOpen;
            _formatStateChanged = false;
            return true;
        }

        var valueChanged = _lastValue != Value;
        var disabledChanged = _lastDisabled != Disabled;
        var readOnlyChanged = _lastReadOnly != ReadOnly;
        var dialogChanged = _lastLinkDialogOpen != _linkDialogOpen;
        var tableMenuChanged = _lastTableMenuOpen != _tableMenuOpen;

        if (valueChanged || disabledChanged || readOnlyChanged || dialogChanged || tableMenuChanged || _formatStateChanged)
        {
            _lastValue = Value;
            _lastDisabled = Disabled;
            _lastReadOnly = ReadOnly;
            _lastLinkDialogOpen = _linkDialogOpen;
            _lastTableMenuOpen = _tableMenuOpen;
            _formatStateChanged = false;
            return true;
        }

        return false;
    }

    private async Task HandleHeaderChangeAsync(string? value)
    {
        if (_jsModule == null || !_jsInitialized || Disabled)
        {
            return;
        }

        _headerLevel = value ?? "";

        if (string.IsNullOrEmpty(value))
        {
            await _jsModule.InvokeVoidAsync("format", _editorId, "header", false);
        }
        else
        {
            await _jsModule.InvokeVoidAsync("format", _editorId, "header", int.Parse(value, System.Globalization.CultureInfo.InvariantCulture));
        }

        // 下拉选项变更后让编辑器重新聚焦
        await _jsModule.InvokeVoidAsync("focus", _editorId);
    }

    private async Task InsertLinkAsync()
    {
        if (_jsModule == null || !_jsInitialized || Disabled)
        {
            return;
        }

        // 打开对话框前先保存当前选区
        _savedSelection = await GetSelectionAsync();

        // 检查选区处是否已有链接
        var format = await _jsModule.InvokeAsync<Dictionary<string, object?>>("getFormat", _editorId);
        object? linkValue = null;
        _hasExistingLink = format != null && format.TryGetValue("link", out linkValue) && linkValue != null;

        if (_hasExistingLink)
        {
            if (linkValue is string existingUrl)
            {
                _linkUrl = existingUrl;
            }
            else if (linkValue is System.Text.Json.JsonElement je && je.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                _linkUrl = je.GetString() ?? "https://";
            }
            else
            {
                _linkUrl = "https://";
            }
        }
        else
        {
            _linkUrl = "https://";
        }

        _linkUrlError = null;
        _linkDialogOpen = true;
    }

    private void CloseLinkDialog()
    {
        _linkDialogOpen = false;
        _linkUrl = "";
        _linkUrlError = null;
        _savedSelection = null;
    }

    private void ValidateLinkUrl()
    {
        if (string.IsNullOrWhiteSpace(_linkUrl) || _linkUrl == "https://")
        {
            _linkUrlError = null;
        }
        else if (!IsValidUrl(_linkUrl))
        {
            _linkUrlError = "Please enter a valid URL";
        }
        else
        {
            _linkUrlError = null;
        }
    }

    private static bool IsValidUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || url == "https://")
        {
            return false;
        }

        return Uri.TryCreate(url, UriKind.Absolute, out var uri)
               && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }

    private async Task ApplyLinkAsync()
    {
        if (_jsModule == null || !_jsInitialized || !IsValidUrl(_linkUrl))
        {
            return;
        }

        // 应用链接前先恢复选区
        if (_savedSelection != null)
        {
            await SetSelectionAsync(_savedSelection.Index, _savedSelection.Length);
        }

        await _jsModule.InvokeVoidAsync("format", _editorId, "link", _linkUrl);

        CloseLinkDialog();
        await _jsModule.InvokeVoidAsync("focus", _editorId);
    }

    private async Task RemoveLinkAsync()
    {
        if (_jsModule == null || !_jsInitialized)
        {
            return;
        }

        // 移除链接前先恢复选区
        if (_savedSelection != null)
        {
            await SetSelectionAsync(_savedSelection.Index, _savedSelection.Length);
        }

        await _jsModule.InvokeVoidAsync("format", _editorId, "link", false);

        CloseLinkDialog();
        await _jsModule.InvokeVoidAsync("focus", _editorId);
    }

    // === 对齐、颜色、图片、历史 ===

    private string AlignIcon => _align switch
    {
        "center" => "align-center",
        "right" => "align-right",
        "justify" => "align-justify",
        _ => "align-left"
    };

    private async Task SetAlignAsync(string? value)
    {
        if (_jsModule == null || !_jsInitialized || Disabled)
        {
            return;
        }

        var state = await _jsModule.InvokeAsync<Dictionary<string, object?>>(
            "formatAndGetState", _editorId, "align", value is null ? false : value);
        UpdateFormatState(state);
        await _jsModule.InvokeVoidAsync("focus", _editorId);
    }

    private void HandleTextColorOpenChanged(bool open)
    {
        _textColorOpen = open;
        _formatStateChanged = true;
    }

    private void HandleHighlightOpenChanged(bool open)
    {
        _highlightOpen = open;
        _formatStateChanged = true;
    }

    private async Task SetColorAsync(string format, string? value)
    {
        _textColorOpen = false;
        _highlightOpen = false;
        _formatStateChanged = true;

        if (_jsModule == null || !_jsInitialized || Disabled)
        {
            return;
        }

        var state = await _jsModule.InvokeAsync<Dictionary<string, object?>>(
            "formatAndGetState", _editorId, format, value is null ? false : value);
        UpdateFormatState(state);
        await _jsModule.InvokeVoidAsync("focus", _editorId);
    }

    private async Task PickImageAsync()
    {
        if (_jsModule == null || !_jsInitialized || Disabled)
        {
            return;
        }

        await _jsModule.InvokeVoidAsync("pickImage", _editorId);
    }

    private sealed class HistoryState
    {
        public bool CanUndo { get; set; }
        public bool CanRedo { get; set; }
    }

    private async Task HistoryAsync(string action)
    {
        if (_jsModule == null || !_jsInitialized || Disabled)
        {
            return;
        }

        var state = await _jsModule.InvokeAsync<HistoryState>(action, _editorId);
        _canUndo = state.CanUndo;
        _canRedo = state.CanRedo;
        _formatStateChanged = true;
        StateHasChanged();
    }

    // === 表格工具栏 ===

    private void HandleTableMenuOpenChange(bool open)
    {
        if (!open)
        {
            SetTablePickerHover(0, 0);
        }
    }

    private void SetTablePickerHover(int rows, int columns)
    {
        if (_tablePickerRows == rows && _tablePickerColumns == columns)
        {
            return;
        }

        _tablePickerRows = rows;
        _tablePickerColumns = columns;
        _formatStateChanged = true;
    }

    private string TablePickerLabel => _tablePickerRows == 0
        ? Localizer["RichTextEditor.InsertTable"]
        : Localizer["RichTextEditor.TableSize", _tablePickerRows, _tablePickerColumns];

    private string TablePickerCellClass(int row, int column) => ClassNames.cn(
        "atom:h-5 atom:w-5 atom:rounded-sm atom:border atom:transition-colors atom:focus-visible:outline-none atom:focus-visible:ring-2 atom:focus-visible:ring-ring",
        row <= _tablePickerRows && column <= _tablePickerColumns
            ? "atom:border-primary atom:bg-primary/20"
            : "atom:border-input atom:bg-background"
    );

    private async Task InsertTableFromPickerAsync(int rows, int columns)
    {
        _tableMenuOpen = false;
        SetTablePickerHover(0, 0);
        await InsertTableAsync(rows, columns);
    }

    private async Task RunTableActionAsync(string action)
    {
        _tableMenuOpen = false;
        await TableActionAsync(action);
    }

    private async Task TableActionAsync(string action)
    {
        if (_jsModule == null || !_jsInitialized || Disabled)
        {
            return;
        }

        var state = await _jsModule.InvokeAsync<Dictionary<string, object?>>("tableAction", _editorId, action);
        UpdateFormatState(state);
    }

    // === 公开 API 方法 ===

    /// <summary>撤销上一次用户改动。程序写入 <see cref="Value"/> 的更新不可撤销。</summary>
    public Task UndoAsync() => HistoryAsync("undo");

    /// <summary>重做上一次被撤销的改动。</summary>
    public Task RedoAsync() => HistoryAsync("redo");

    /// <summary>
    /// 在光标处按 URL 插入图片。自行存好图片后用本方法插入；用户经工具栏、
    /// 拖放或粘贴添加的图片则走 <see cref="ImageUploader"/>。
    /// </summary>
    public async Task InsertImageAsync(string url)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);

        if (_jsModule != null && _jsInitialized && !Disabled)
        {
            await _jsModule.InvokeVoidAsync("insertImage", _editorId, url);
        }
    }

    /// <summary>
    /// 在光标处插入指定行数与列数的表格。
    /// 使用 Quill 内置表格模块；单元格是普通的 <c>&lt;td&gt;</c> 元素
    /// HTML 输出中是一个常规的 <c>&lt;table&gt;</c>。
    /// </summary>
    /// <param name="rows">行数，至少为 1。</param>
    /// <param name="columns">列数，至少为 1。</param>
    public async Task InsertTableAsync(int rows, int columns)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(rows, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(columns, 1);

        if (_jsModule == null || !_jsInitialized || Disabled)
        {
            return;
        }

        var state = await _jsModule.InvokeAsync<Dictionary<string, object?>>("insertTable", _editorId, rows, columns);
        UpdateFormatState(state);
    }

    /// <summary>在光标所在行上方插入一行。表格之外为空操作。</summary>
    public Task InsertRowAboveAsync() => TableActionAsync("insertRowAbove");

    /// <summary>在光标所在行下方插入一行。表格之外为空操作。</summary>
    public Task InsertRowBelowAsync() => TableActionAsync("insertRowBelow");

    /// <summary>在光标所在列左侧插入一列。表格之外为空操作。</summary>
    public Task InsertColumnLeftAsync() => TableActionAsync("insertColumnLeft");

    /// <summary>在光标所在列右侧插入一列。表格之外为空操作。</summary>
    public Task InsertColumnRightAsync() => TableActionAsync("insertColumnRight");

    /// <summary>删除光标所在行。表格之外为空操作。</summary>
    public Task DeleteRowAsync() => TableActionAsync("deleteRow");

    /// <summary>删除光标所在列。表格之外为空操作。</summary>
    public Task DeleteColumnAsync() => TableActionAsync("deleteColumn");

    /// <summary>删除光标所在表格。表格之外为空操作。</summary>
    public Task DeleteTableAsync() => TableActionAsync("deleteTable");

    /// <summary>
    /// 让编辑器获得焦点。
    /// </summary>
    public async Task FocusAsync()
    {
        if (_jsModule != null && _jsInitialized)
        {
            await _jsModule.InvokeVoidAsync("focus", _editorId);
        }
    }

    /// <summary>
    /// 让编辑器失去焦点。
    /// </summary>
    public async Task BlurAsync()
    {
        if (_jsModule != null && _jsInitialized)
        {
            await _jsModule.InvokeVoidAsync("blur", _editorId);
        }
    }

    /// <summary>
    /// 获取当前选区范围。
    /// </summary>
    public async Task<EditorRange?> GetSelectionAsync()
    {
        if (_jsModule != null && _jsInitialized)
        {
            return await _jsModule.InvokeAsync<EditorRange?>("getSelection", _editorId);
        }
        return null;
    }

    /// <summary>
    /// 设置选区范围。
    /// </summary>
    public async Task SetSelectionAsync(int index, int length = 0)
    {
        if (_jsModule != null && _jsInitialized)
        {
            await _jsModule.InvokeVoidAsync("setSelection", _editorId, index, length);
        }
    }

    /// <summary>
    /// 对当前选区应用格式。
    /// </summary>
    public async Task FormatAsync(string formatName, object? value = null)
    {
        if (_jsModule != null && _jsInitialized)
        {
            await _jsModule.InvokeVoidAsync("format", _editorId, formatName, value ?? true);
        }
    }

    /// <summary>
    /// 获取编辑器的纯文本内容。
    /// </summary>
    public async Task<string> GetTextAsync()
    {
        if (_jsModule != null && _jsInitialized)
        {
            return await _jsModule.InvokeAsync<string>("getText", _editorId) ?? "";
        }
        return "";
    }

    /// <summary>
    /// 获取编辑器内容的长度。
    /// </summary>
    public async Task<int> GetLengthAsync()
    {
        if (_jsModule != null && _jsInitialized)
        {
            return await _jsModule.InvokeAsync<int>("getLength", _editorId);
        }
        return 0;
    }

    /// <summary>
    /// 获取编辑器的 HTML 内容。
    /// </summary>
    public async Task<string> GetHtmlAsync()
    {
        if (_jsModule != null && _jsInitialized)
        {
            return await _jsModule.InvokeAsync<string>("getHtml", _editorId) ?? "";
        }
        return "";
    }

    /// <summary>
    /// 设置编辑器的 HTML 内容。
    /// HTML 已经过净化以防 XSS 攻击。
    /// </summary>
    public async Task SetHtmlAsync(string? html)
    {
        if (_jsModule != null && _jsInitialized)
        {
            var sanitized = string.IsNullOrEmpty(html) ? "" : sanitizer.Sanitize(html);
            await _jsModule.InvokeVoidAsync("setHtml", _editorId, sanitized);
        }
    }

    /// <summary>
    /// 获取编辑器的 Delta（JSON）内容。
    /// Delta 是 Quill 的原生文档格式，保留全部格式信息。
    /// </summary>
    public async Task<string> GetDeltaAsync()
    {
        if (_jsModule != null && _jsInitialized)
        {
            return await _jsModule.InvokeAsync<string>("getContents", _editorId) ?? "{}";
        }
        return "{}";
    }

    /// <summary>
    /// 使用 Delta（JSON）对象设置编辑器内容。
    /// 在 Quill 原生格式场景下推荐优先使用本方法。
    /// </summary>
    public async Task SetDeltaAsync(string? deltaJson)
    {
        if (_jsModule != null && _jsInitialized)
        {
            if (string.IsNullOrEmpty(deltaJson))
            {
                await _jsModule.InvokeVoidAsync("setContents", _editorId, "{\"ops\":[{\"insert\":\"\\n\"}]}");
            }
            else
            {
                await _jsModule.InvokeVoidAsync("setContents", _editorId, deltaJson);
            }
        }
    }

    // === 私有辅助方法 ===

    private object BuildEditorOptions() => new
    {
        placeholder = Placeholder ?? "",
        readOnly = Disabled || ReadOnly,
        hasImageUploader = ImageUploader != null
    };

    // === CSS 类 ===

    private string ContainerCssClass => ClassNames.cn(
        "atom:flex atom:flex-col atom:rounded-md atom:border atom:border-input atom:bg-background",
        "atom:focus-within:border-ring",
        ClassNames.when(AriaInvalid == true, "atom:border-destructive"),
        ClassNames.when(Disabled, "atom:opacity-50 atom:cursor-not-allowed"),
        Class
    );

    private static string ToolbarCssClass => ClassNames.cn(
        "atom:flex atom:flex-wrap atom:items-center atom:gap-1 atom:px-3 atom:py-2 atom:border-b atom:border-input atom:bg-muted/40"
    );

    private string EditorCssClass => ClassNames.cn(
        "atom:text-base atom:md:text-sm",
        ClassNames.when(Disabled, "atom:cursor-not-allowed")
    );

    private string EditorStyle
    {
        get
        {
            var styles = new List<string>();

            if (!string.IsNullOrEmpty(Height))
            {
                styles.Add($"height: {Height}");
                styles.Add("overflow-y: auto");
            }
            else
            {
                styles.Add($"min-height: {MinHeight}");
                if (!string.IsNullOrEmpty(MaxHeight))
                {
                    styles.Add($"max-height: {MaxHeight}");
                    styles.Add("overflow-y: auto");
                }
            }

            return string.Join("; ", styles);
        }
    }

    // === 释放 ===

    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        if (_jsModule != null && _jsInitialized)
        {
            try
            {
                await _jsModule.InvokeVoidAsync("disposeEditor", _editorId);
            }
            catch (Exception ex) when (ex is JSDisconnectedException or JSException or TaskCanceledException or ObjectDisposedException)
            {
                // Blazor Server 电路断开时属预期情况——可安全忽略
            }
            catch (ObjectDisposedException)
            {
                // 模块已释放——可安全忽略
            }
            catch (InvalidOperationException)
            {
                // JS interop 不可用（预渲染中）——可安全忽略
            }
        }
        _dotNetRef?.Dispose();
    }
}
