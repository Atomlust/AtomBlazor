using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using AtomBlazor.Primitives;

using AtomBlazor.Primitives.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using System.Runtime.CompilerServices;
using Microsoft.JSInterop;

namespace AtomBlazor;

public partial class DataGrid<TData> where TData : class
{
    private DataGridEditBuffer<TData>? editBuffer;
    private string? editingColumnId;
    private TData? editModel;
    private TData? cellCheckpoint;
    private readonly ConditionalWeakTable<TData, CellFocusIdentity> cellFocusIds = new();
    private string? returnFocusId;
    private string? pendingFocusId;
    private bool cellHadDraft;
    private bool savingEdits;
    private bool focusEditor;
    private string? editError;
    private IDisposable? editValidation;

    [Inject]
    private IServiceProvider ServiceProvider { get; set; } = default!;

    /// <summary>
    /// 将源行克隆为一个独立的编辑模型。Cell 与 Batch 模式下为必需。
    /// 可编辑的嵌套对象也要一并克隆。稳定的 ItemKey 值不得被编辑。
    /// </summary>
    [Parameter]
    public Func<TData, TData>? EditItemFactory { get; set; }

    /// <summary>持久化一个批处理事务。拒绝后设置 Cancel 可保留所有草稿。</summary>
    [Parameter]
    public EventCallback<DataGridBatchCommitContext<TData>> OnBatchCommit { get; set; }

    /// <summary>在待处理的批次被丢弃之后发生。</summary>
    [Parameter]
    public EventCallback OnBatchCancel { get; set; }

    /// <summary>获取待处理的批次草稿，而不修改源记录。</summary>
    public IReadOnlyList<DataGridEditChange<TData>> PendingChanges => editBuffer?.Changes ?? [];

    /// <summary>获取当前是否正在运行某个保存回调。</summary>
    public bool IsSavingEdits => savingEdits;

    /// <summary>获取 Cell 或 Batch 模式下当前正在编辑的列。</summary>
    public string? EditingColumnId => editingColumnId;

    private bool UsesEditBuffer => EditMode is DataGridEditMode.Cell or DataGridEditMode.Batch;

    private TData DisplayItem(TData item) => editBuffer?.GetDisplayItem(item) ?? item;

    private bool IsCellEditable(IDataGridColumn<TData> column) => UsesEditBuffer && column.EditTemplate != null;

    /// <summary>开始编辑一个可编辑的可见列。未提交的无效编辑会阻止导航离开。</summary>
    public async Task StartCellEditAsync(TData item, string columnId)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (!UsesEditBuffer || savingEdits)
        {
            return;
        }

        var column = _columns.Find(c => c.ColumnId == columnId && c.Visible && c.EditTemplate != null)
            ?? throw new ArgumentException("The column must be visible and have an EditTemplate.", nameof(columnId));
        if (IsEditing(item) && editingColumnId == column.ColumnId)
        {
            return;
        }

        if (_editingItem != null && !await CommitEditAsync())
        {
            return;
        }

        var factory = EditItemFactory ?? throw new InvalidOperationException("Cell and Batch editing require EditItemFactory.");
        editBuffer ??= new DataGridEditBuffer<TData>(factory, ItemKey);
        cellHadDraft = !ReferenceEquals(editBuffer.GetDisplayItem(item), item);
        var draft = editBuffer.Begin(item);
        var checkpoint = factory(draft);
        if (checkpoint == null || ReferenceEquals(checkpoint, draft) || ReferenceEquals(checkpoint, item))
        {
            throw new InvalidOperationException("EditItemFactory must return an independent copy for cancellation.");
        }

        _editingItem = item;
        editModel = draft;
        cellCheckpoint = checkpoint;
        editingColumnId = columnId;
        returnFocusId = CellTriggerId(item, columnId);
        _editContext = new EditContext(draft);
        editValidation = _editContext.EnableDataAnnotationsValidation(ServiceProvider);
        editError = null;
        focusEditor = true;
        _stateVersion++;
        StateHasChanged();
    }

    private string CellTriggerId(TData item, string columnId) =>
        $"{gridId}-{cellFocusIds.GetValue(item, _ => new CellFocusIdentity()).Id}-{columnId}";

    private sealed class CellFocusIdentity
    {
        internal string Id { get; } = Guid.NewGuid().ToString("N");
    }

    private async Task<bool> CommitBufferedCellAsync()
    {
        if (savingEdits || _editingItem == null || editModel == null)
        {
            return !savingEdits;
        }

        if (_editContext != null && !await _editContext.ValidateAsync())
        {
            focusEditor = true;
            _stateVersion++;
            StateHasChanged();
            return false;
        }

        if (ItemKey != null && !Equals(ItemKey(_editingItem), ItemKey(editModel)))
        {
            editError = Localizer["DataGrid.ImmutableKey"];
            _stateVersion++;
            StateHasChanged();
            return false;
        }

        if (EditMode == DataGridEditMode.Batch)
        {
            ClearEditState();
            StateHasChanged();
            return true;
        }

        savingEdits = true;
        _stateVersion++;
        StateHasChanged();
        try
        {
            var context = new DataGridRowCommitContext<TData> { Item = editModel, OriginalItem = _editingItem };
            await OnRowCommit.InvokeAsync(context);
            if (context.Cancel)
            {
                editError = Localizer["DataGrid.SaveRejected"];
                return false;
            }

            ApplyDrafts([new DataGridEditChange<TData> { Item = editModel, OriginalItem = _editingItem }]);
            editBuffer!.Discard(_editingItem);
            ClearEditState();
            await RefreshDataAsync();
            return true;
        }
        catch (Exception ex)
        {
            // A failed remote save must not discard the draft or terminate a server circuit.
            DataGridLog.RowCommitFailed(Logger, ex);
            editError = Localizer["DataGrid.SaveFailed"];
            return false;
        }
        finally
        {
            savingEdits = false;
            _stateVersion++;
            StateHasChanged();
        }
    }

    /// <summary>用一个批处理回调校验并保存所有已暂存的行。</summary>
    /// <returns>若所有变更都被接受则返回 true；若校验或持久化失败则返回 false。</returns>
    [UnconditionalSuppressMessage("Trimming", "IL2026: Validator.TryValidateObject/ValidationContext 依赖反射发现被验证实例类型以解析 DisplayNameAttribute，修剪下不自述 trim-safe；DataGrid 编辑校验路径受控且类型来自用户模型，沿用行为",
        Justification = "校验仅对当前编辑模型实例做 DataAnnotations 校验，目标类型由调用方提供并随宿主程序集保留，属受控路径。")]
    public async Task<bool> CommitBatchAsync()
    {
        if (EditMode != DataGridEditMode.Batch || savingEdits)
        {
            return false;
        }

        if (_editingItem != null && !await CommitEditAsync())
        {
            return false;
        }

        var changes = PendingChanges;
        if (changes.Count == 0)
        {
            return true;
        }

        foreach (var change in changes)
        {
            if (ItemKey != null && !Equals(ItemKey(change.OriginalItem), ItemKey(change.Item)))
            {
                editError = Localizer["DataGrid.InvalidBatch"];
                _stateVersion++;
                StateHasChanged();
                return false;
            }

            // 异步版本会运行 IAsyncValidatableObject 与 AsyncValidationAttribute，
            // 与单元格编辑所用的 EditContext.ValidateAsync 保持一致。
            if (!await Validator.TryValidateObjectAsync(
                    change.Item,
                    new ValidationContext(change.Item, ServiceProvider, null),
                    [],
                    validateAllProperties: true,
                    CancellationToken.None))
            {
                editError = Localizer["DataGrid.InvalidBatch"];
                _stateVersion++;
                StateHasChanged();
                return false;
            }
        }

        savingEdits = true;
        editError = null;
        _stateVersion++;
        StateHasChanged();
        try
        {
            var context = new DataGridBatchCommitContext<TData> { Changes = changes };
            await OnBatchCommit.InvokeAsync(context);
            if (context.Cancel)
            {
                editError = Localizer["DataGrid.SaveRejected"];
                return false;
            }

            ApplyDrafts(changes);

            editBuffer!.Clear();
            await RefreshDataAsync();
            return true;
        }
        catch (Exception ex)
        {
            DataGridLog.BatchCommitFailed(Logger, ex);
            editError = Localizer["DataGrid.SaveFailed"];
            return false;
        }
        finally
        {
            savingEdits = false;
            _stateVersion++;
            StateHasChanged();
        }
    }

    private void ApplyDrafts(IReadOnlyList<DataGridEditChange<TData>> changes)
    {
        if (ItemKey != null && changes.Any(c => !Equals(ItemKey(c.OriginalItem), ItemKey(c.Item))))
        {
            throw new InvalidOperationException("The row key cannot change during persistence.");
        }
        var originals = changes.Select(c => DataGridRowSnapshot<TData>.Capture(c.OriginalItem)).ToArray();
        try
        {
            foreach (var change in changes)
            {
                DataGridRowSnapshot<TData>.Capture(change.Item).ApplyTo(change.OriginalItem);
            }
        }
        catch
        {
            foreach (var original in originals)
            {
                original.Restore();
            }
            throw;
        }
    }

    /// <summary>丢弃当前活动的以及已暂存的批次草稿。源记录保持不变。</summary>
    public async Task CancelBatchAsync()
    {
        if (savingEdits)
        {
            return;
        }

        editBuffer?.Clear();
        ClearEditState();
        editError = null;
        await OnBatchCancel.InvokeAsync();
    }

    private async Task FocusCellEditorAsync()
    {
        if (!focusEditor && pendingFocusId == null)
        {
            return;
        }

        var editor = focusEditor;
        var restore = pendingFocusId;
        focusEditor = false;
        pendingFocusId = null;
        try
        {
            var module = await JsModules.GetAsync(Js, "./_content/AtomBlazor/js/datagrid-editing.js");
            await module.InvokeVoidAsync("focusEditor", containerRef, editor ? null : restore);
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException or InvalidOperationException or TaskCanceledException)
        {
            // Navigation and prerendering can remove the editor before focus is applied.
        }
    }
}
