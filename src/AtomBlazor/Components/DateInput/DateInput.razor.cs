using System.Globalization;
using System.Linq.Expressions;
using AtomBlazor.Primitives.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;

namespace AtomBlazor;

/// <summary>格里历日期输入：按文化特定顺序逐段编辑，配合分段键盘自旋。</summary>
public partial class DateInput : ComponentBase, IAsyncDisposable
{
    private readonly Dictionary<string, string> parts = new() { ["Year"] = "", ["Month"] = "", ["Day"] = "" };
    private readonly SegmentedInputValidation validation;
    private ElementReference root;
    private IJSObjectReference? module;
    private DateTime? lastValue;
    private bool initialized;
    private bool disposed;
    private bool open;

    public DateInput()
    {
        validation = new(() => { if (!disposed) { _ = InvokeAsync(StateHasChanged); } });
    }

    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private ILocalizer Localizer { get; set; } = default!;
    [CascadingParameter] private EditContext? EditContext { get; set; }

    /// <summary>所选日期，不含时间部分。分段不完整或非法时为 null。</summary>
    [Parameter] public DateTime? Value { get; set; }
    /// <summary>所选日期变化时触发。</summary>
    [Parameter] public EventCallback<DateTime?> ValueChanged { get; set; }
    /// <summary>标识绑定的表单字段，用于校验与表单回传。</summary>
    [Parameter] public Expression<Func<DateTime?>>? ValueExpression { get; set; }
    /// <summary>可选日期的闭下界。</summary>
    [Parameter] public DateTime? MinDate { get; set; }
    /// <summary>可选日期的闭上界。</summary>
    [Parameter] public DateTime? MaxDate { get; set; }
    /// <summary>控制分段顺序与分隔符。默认为当前文化。</summary>
    [Parameter] public CultureInfo? Culture { get; set; }
    /// <summary>是否显示可选的日历按钮。</summary>
    [Parameter] public bool ShowCalendar { get; set; } = true;
    /// <summary>禁用编辑与本控件的表单提交。</summary>
    [Parameter] public bool Disabled { get; set; }
    /// <summary>禁止编辑但保持控件可聚焦。</summary>
    [Parameter] public bool ReadOnly { get; set; }
    /// <summary>把每一段标记为必填；模型校验可据此强制要求完整值。</summary>
    [Parameter] public bool Required { get; set; }
    /// <summary>首段的 id，供外部标签使用。</summary>
    [Parameter] public string? Id { get; set; }
    /// <summary>ISO 格式隐藏表单字段的 name。</summary>
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
    private string FirstSegment => DateTimeSegments.DateOrder(EffectiveCulture)[0];
    private string LastSegment => DateTimeSegments.DateOrder(EffectiveCulture)[^1];
    private int MaxDay => DateTimeSegments.Number(parts["Year"]) is > 0 and <= 9999 and var year
        && DateTimeSegments.Number(parts["Month"]) is > 0 and <= 12 and var month
        ? DateTime.DaysInMonth(year, month) : 31;
    private string CssClass => ClassNames.cn(
        "atom:flex atom:h-10 atom:w-fit atom:items-center atom:gap-1 atom:rounded-md atom:border atom:border-input atom:bg-background atom:px-3 atom:text-base atom:md:text-sm atom:focus-within:ring-2 atom:focus-within:ring-ring atom:aria-[invalid=true]:border-destructive",
        Disabled ? "atom:opacity-50 atom:cursor-not-allowed" : null, Class);

    protected override void OnParametersSet()
    {
        if (MinDate?.Date > MaxDate?.Date) { throw new ArgumentException("MinDate must not exceed MaxDate."); }
        validation.Update(EditContext, ValueExpression);
        if (!initialized || Value != lastValue)
        {
            initialized = true;
            Synchronize(Value);
        }
        var parsed = DateTimeSegments.Date(parts["Year"], parts["Month"], parts["Day"], MinDate, MaxDate);
        validation.SetError(parsed == null && parts.Values.Any(p => p.Length > 0) ? Localizer["DateInput.Invalid"] : null);
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

    private void Synchronize(DateTime? value)
    {
        lastValue = value;
        parts["Year"] = value?.Year.ToString("D4", CultureInfo.InvariantCulture) ?? "";
        parts["Month"] = value?.Month.ToString("D2", CultureInfo.InvariantCulture) ?? "";
        parts["Day"] = value?.Day.ToString("D2", CultureInfo.InvariantCulture) ?? "";
    }

    private async Task EditAsync(string segment, string text)
    {
        if (Disabled || ReadOnly || disposed || !parts.ContainsKey(segment)) { return; }
        parts[segment] = text;
        var next = DateTimeSegments.Date(parts["Year"], parts["Month"], parts["Day"], MinDate, MaxDate);
        validation.SetError(next == null && parts.Values.Any(p => p.Length > 0) ? Localizer["DateInput.Invalid"] : null);
        await PublishAsync(next);
    }

    private async Task SelectAsync(DateTime? value)
    {
        if (Disabled || ReadOnly || disposed) { return; }
        if (value.HasValue && ((MinDate.HasValue && value.Value.Date < MinDate.Value.Date) || (MaxDate.HasValue && value.Value.Date > MaxDate.Value.Date))) { return; }
        open = false;
        Synchronize(value?.Date);
        validation.SetError(null);
        await PublishAsync(value?.Date);
    }

    private async Task PublishAsync(DateTime? value)
    {
        var changed = Value != value;
        Value = lastValue = value;
        if (changed) { await ValueChanged.InvokeAsync(value); }
        validation.NotifyFieldChanged();
    }

    /// <summary>清空所有分段与绑定值。</summary>
    public Task ClearAsync() => InvokeAsync(async () => { await SelectAsync(null); StateHasChanged(); });

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
