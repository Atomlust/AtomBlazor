using System.Globalization;
using System.Linq.Expressions;
using AtomBlazor.Primitives.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;

namespace AtomBlazor;

/// <summary>当日时刻输入：小时、分钟与可选秒分段，支持键盘自旋编辑。</summary>
public partial class TimeInput : ComponentBase, IAsyncDisposable
{
    private readonly Dictionary<string, string> parts = new() { ["Hour"] = "", ["Minute"] = "", ["Second"] = "" };
    private readonly SegmentedInputValidation validation;
    private ElementReference root;
    private IJSObjectReference? module;
    private TimeSpan? lastValue;
    private bool lastHour12;
    private bool lastShowSeconds;
    private bool initialized;
    private bool disposed;
    private bool open;
    private bool pm;

    public TimeInput()
    {
        validation = new(() => { if (!disposed) { _ = InvokeAsync(StateHasChanged); } });
    }

    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private ILocalizer Localizer { get; set; } = default!;
    [CascadingParameter] private EditContext? EditContext { get; set; }

    /// <summary>所选的当日时刻。分段不完整或非法时为 null。</summary>
    [Parameter] public TimeSpan? Value { get; set; }
    /// <summary>所选时刻变化时触发。</summary>
    [Parameter] public EventCallback<TimeSpan?> ValueChanged { get; set; }
    /// <summary>标识绑定的表单字段，用于校验与表单回传。</summary>
    [Parameter] public Expression<Func<TimeSpan?>>? ValueExpression { get; set; }
    /// <summary>当日时刻的闭下界。</summary>
    [Parameter] public TimeSpan? MinTime { get; set; }
    /// <summary>当日时刻的闭上界。不支持跨日界。</summary>
    [Parameter] public TimeSpan? MaxTime { get; set; }
    /// <summary>控制分隔符与默认 12/24 小时制。</summary>
    [Parameter] public CultureInfo? Culture { get; set; }
    /// <summary>设定后覆盖文化的时分制。</summary>
    [Parameter] public TimeFormat? Format { get; set; }
    /// <summary>显示秒分段。</summary>
    [Parameter] public bool ShowSeconds { get; set; }
    /// <summary>自旋按键与弹层选项的分钟步进，取值 1 到 59；手输分钟不要求是步进倍数。</summary>
    [Parameter] public int MinuteStep { get; set; } = 1;
    /// <summary>是否显示可选的时钟弹层按钮。</summary>
    [Parameter] public bool ShowPicker { get; set; } = true;
    /// <summary>禁用编辑与本控件的表单提交。</summary>
    [Parameter] public bool Disabled { get; set; }
    /// <summary>禁止数字分段编辑但保持其可聚焦。</summary>
    [Parameter] public bool ReadOnly { get; set; }
    /// <summary>把每个可见分段标记为必填。</summary>
    [Parameter] public bool Required { get; set; }
    /// <summary>小时段的 id，供外部标签使用。</summary>
    [Parameter] public string? Id { get; set; }
    /// <summary>承载 24 小时制时刻的隐藏表单字段的 name。</summary>
    [Parameter] public string? Name { get; set; }
    /// <summary>组的可访问名称。</summary>
    [Parameter] public string? AriaLabel { get; set; }
    /// <summary>帮助或校验文本的 id。</summary>
    [Parameter] public string? AriaDescribedBy { get; set; }
    /// <summary>输入容器的附加类。</summary>
    [Parameter] public string? Class { get; set; }
    /// <summary>输入容器的附加属性。</summary>
    [Parameter(CaptureUnmatchedValues = true)] public Dictionary<string, object>? AdditionalAttributes { get; set; }

    private CultureInfo EffectiveCulture => Culture ?? CultureInfo.CurrentCulture;
    private bool Hour12 => Format == TimeFormat.Hour12 || (Format == null && EffectiveCulture.DateTimeFormat.ShortTimePattern.Contains('h'));
    private string AmLabel => string.IsNullOrEmpty(EffectiveCulture.DateTimeFormat.AMDesignator) ? "AM" : EffectiveCulture.DateTimeFormat.AMDesignator;
    private string PmLabel => string.IsNullOrEmpty(EffectiveCulture.DateTimeFormat.PMDesignator) ? "PM" : EffectiveCulture.DateTimeFormat.PMDesignator;
    private string[] Segments => ShowSeconds ? ["Hour", "Minute", "Second"] : ["Hour", "Minute"];
    private int Minimum(string segment) => segment == "Hour" && Hour12 ? 1 : 0;
    private int Maximum(string segment) => segment == "Hour" ? Hour12 ? 12 : 23 : 59;
    private IEnumerable<int> PickerValues(string segment)
    {
        var values = Enumerable.Range(Minimum(segment), Maximum(segment) - Minimum(segment) + 1);
        return segment == "Minute" ? values.Where(n => n % MinuteStep == 0 || n == DateTimeSegments.Number(parts[segment])) : values;
    }
    private string CssClass => ClassNames.cn(
        "atom:flex atom:h-10 atom:w-fit atom:items-center atom:gap-1 atom:rounded-md atom:border atom:border-input atom:bg-background atom:px-3 atom:text-base atom:md:text-sm atom:focus-within:ring-2 atom:focus-within:ring-ring atom:aria-[invalid=true]:border-destructive",
        Disabled ? "atom:opacity-50 atom:cursor-not-allowed" : null, Class);

    protected override void OnParametersSet()
    {
        if (MinuteStep is < 1 or > 59) { throw new ArgumentOutOfRangeException(nameof(MinuteStep)); }
        if (MinTime < TimeSpan.Zero || MinTime >= TimeSpan.FromDays(1) || MaxTime < TimeSpan.Zero || MaxTime >= TimeSpan.FromDays(1) || MinTime > MaxTime)
        {
            throw new ArgumentException("Time bounds must be within one day and ordered from minimum to maximum.");
        }
        if (Value < TimeSpan.Zero || Value >= TimeSpan.FromDays(1)) { throw new ArgumentOutOfRangeException(nameof(Value)); }
        validation.Update(EditContext, ValueExpression);
        if (!initialized || Value != lastValue || Hour12 != lastHour12 || ShowSeconds != lastShowSeconds)
        {
            initialized = true;
            lastHour12 = Hour12;
            lastShowSeconds = ShowSeconds;
            Synchronize(Value);
        }
        var parsed = DateTimeSegments.Time(parts["Hour"], parts["Minute"], parts["Second"], ShowSeconds, Hour12, pm, MinTime, MaxTime);
        validation.SetError(parsed == null && Segments.Any(p => parts[p].Length > 0) ? Localizer["TimeInput.Invalid"] : null);
        if (Disabled || ReadOnly) { open = false; }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) { return; }
 // 有意偏差（记录在案）：按实例直连 segmented-input.js 模块路径
        // 本仓库统一改走共享 components-core.js barrel（camelCase 点分标识符）
        // 规则见 PrimitiveJsModuleTests。
        module = await ComponentModules.GetCoreAsync(JS);
        if (!disposed)
        {
            // BL0016（.NET 11）：与一致不加 try/catch 守卫——已按 disposed 标志
            // 拦在门内，断线时由电路生命周期兜底。
#pragma warning disable BL0016
            await module.InvokeVoidAsync("segmentedInput.initialize", root);
#pragma warning restore BL0016
        }
    }

    private void Synchronize(TimeSpan? value)
    {
        lastValue = value;
        pm = value?.Hours >= 12;
        parts["Hour"] = value.HasValue ? (Hour12 ? ((value.Value.Hours + 11) % 12) + 1 : value.Value.Hours).ToString("D2", CultureInfo.InvariantCulture) : "";
        parts["Minute"] = value?.Minutes.ToString("D2", CultureInfo.InvariantCulture) ?? "";
        parts["Second"] = value?.Seconds.ToString("D2", CultureInfo.InvariantCulture) ?? "";
    }

    private async Task EditAsync(string segment, string text)
    {
        if (Disabled || ReadOnly || disposed || !parts.ContainsKey(segment)) { return; }
        parts[segment] = text;
        await PublishAsync();
    }

    private async Task SetPeriodAsync(bool value)
    {
        if (Disabled || ReadOnly || disposed) { return; }
        pm = value;
        await PublishAsync();
    }

    private async Task PublishAsync()
    {
        var next = DateTimeSegments.Time(parts["Hour"], parts["Minute"], parts["Second"], ShowSeconds, Hour12, pm, MinTime, MaxTime);
        validation.SetError(next == null && Segments.Any(p => parts[p].Length > 0) ? Localizer["TimeInput.Invalid"] : null);
        var changed = Value != next;
        Value = lastValue = next;
        if (changed) { await ValueChanged.InvokeAsync(next); }
        validation.NotifyFieldChanged();
    }

    /// <summary>清空所有分段与绑定值。</summary>
    public async Task ClearAsync()
    {
        await InvokeAsync(async () =>
        {
            if (Disabled || ReadOnly || disposed) { return; }
            Synchronize(null);
            open = false;
            await PublishAsync();
            StateHasChanged();
        });
    }

    public async ValueTask DisposeAsync()
    {
        disposed = true;
        validation.Dispose();
        if (module != null)
        {
            try { await module.InvokeVoidAsync("segmentedInput.dispose", root); }
            catch (Exception ex) when (ex is JSDisconnectedException or JSException or TaskCanceledException or ObjectDisposedException) { }
        }
        GC.SuppressFinalize(this);
    }
}
