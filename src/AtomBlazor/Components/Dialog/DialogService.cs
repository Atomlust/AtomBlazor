
using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 提供用于显示和管理对话框的功能。
/// </summary>
/// <remarks>
/// 在 DI 中作为限定作用域（DI）注册，以在 Blazor Server 应用程序中获得恰当的隔离。
/// 所有对话框操作都会以一个 <see cref="DialogResult"/> 作为结果。
/// </remarks>
public class DialogService
{
    private readonly List<DialogData> dialogs = new();

    /// <summary>
    /// 当对话框集合发生更改时发生。
    /// </summary>
    public event Action? OnChange;

    /// <summary>
    /// 获取当前待处理对话框的列表。
    /// </summary>
    public IReadOnlyList<DialogData> Dialogs => dialogs.AsReadOnly();

    /// <summary>
    /// 显示一个带单个确认按钮的警告对话框。
    /// </summary>
    /// <returns>
    /// 在用户关闭对话框时完成的任务（完成源）。
    /// </returns>
    public Task<DialogResult> AlertAsync(
        string title,
        string? description = null,
        AlertDialogOptions? options = null)
    {
        var data = new AlertDialogData
        {
            Title = title,
            Description = description,
            Options = options ?? new AlertDialogOptions()
        };

        dialogs.Add(data);
        OnChange?.Invoke();

        return data.Completion;
    }

    /// <summary>
    /// 显示一个确认对话框。
    /// </summary>
    /// <param name="title">对话框的标题。</param>
    /// <param name="description">可选的对话框描述或消息。</param>
    /// <param name="options">可选的自定义选项。</param>
    /// <returns>
    /// 解析为 <see cref="ConfirmDialogResult"/> 的任务（完成源），
    /// 指示用户是否确认了该操作。
    /// </returns>
    public async Task<ConfirmDialogResult> ConfirmAsync(
        string title,
        string? description = null,
        ConfirmDialogOptions? options = null)
    {
        var data = new ConfirmDialogData
        {
            Title = title,
            Description = description,
            Options = options ?? new ConfirmDialogOptions()
        };

        dialogs.Add(data);
        OnChange?.Invoke();

        var result = await data.Completion;
        return new ConfirmDialogResult(result);
    }


    /// <summary>
    /// 显示一个确认对话框。
    /// </summary>
    /// <param name="title">对话框的标题。</param>
    /// <param name="description">可选的对话框描述或消息。</param>
    /// <param name="options">可选的自定义选项。</param>
    /// <returns>
    /// 如果用户确认则解析为 <see langword="true"/> 的任务（完成源），否则为 <see langword="false"/>。
    /// </returns>
    [Obsolete("请改用 ConfirmAsync，它会返回含有更丰富信息的 ConfirmDialogResult。")]
    public async Task<bool> Confirm(
        string title,
        string? description = null,
        ConfirmDialogOptions? options = null)
    {
        var result = await ConfirmAsync(title, description, options);
        return result.Confirmed;
    }

    /// <summary>
    /// 显示一个从用户处收集文本输入的提示输入对话框。
    /// </summary>
    /// <param name="title">对话框的标题。</param>
    /// <param name="description">可选的对话框描述或消息。</param>
    /// <param name="options">可选的自定义选项。</param>
    /// <returns>
    /// 解析为 <see cref="PromptDialogResult"/> 的任务（完成源），
    /// 在确认时包含输入的值。
    /// </returns>
    public async Task<PromptDialogResult> PromptAsync(
        string title,
        string? description = null,
        PromptDialogOptions? options = null)
    {
        var data = new PromptDialogData
        {
            Title = title,
            Description = description,
            Options = options ?? new PromptDialogOptions()
        };

        dialogs.Add(data);
        OnChange?.Invoke();

        var result = await data.Completion;
        return new PromptDialogResult(result);
    }

    /// <summary>
    /// 在对话框中打开一个自定义组件。
    /// </summary>
    public Task<DialogResult> OpenAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TComponent>(
        DialogOpenOptions? options = null)
        where TComponent : IComponent
        => OpenAsync<TComponent>(new Dictionary<string, object?>(), options);

    /// <summary>
    /// 在对话框中打开一个带有指定参数的自定义组件。
    /// </summary>
    public Task<DialogResult> OpenAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TComponent>(
        Dictionary<string, object?> parameters,
        DialogOpenOptions? options = null)
        where TComponent : IComponent
    {
        var id = Guid.NewGuid().ToString();
        var data = new ComponentDialogData
        {
            Id = id,
            Title = options?.Title ?? string.Empty,
            Description = options?.Description,
            ComponentType = typeof(TComponent),
            Parameters = parameters,
            Options = options ?? new DialogOpenOptions(),
            CloseCallback = result =>
            {
                Resolve(id, result);
                return Task.CompletedTask;
            }
        };

        dialogs.Add(data);
        OnChange?.Invoke();

        return data.Completion;
    }

    /// <summary>
    /// 以指定的结果解析对话框，并将其从集合中移除。
    /// </summary>
    internal void Resolve(string id, DialogResult result)
    {
        var dialog = dialogs.FirstOrDefault(d => d.Id == id);
        if (dialog is null)
        {
            return;
        }

        dialogs.Remove(dialog);
        dialog.SetResult(result);
        OnChange?.Invoke();
    }

    /// <summary>
    /// 取消所有待处理的对话框。
    /// </summary>
    internal void CancelAll()
    {
        foreach (var dialog in dialogs.ToList())
        {
            dialog.SetResult(DialogResult.Cancel());
        }

        dialogs.Clear();
        OnChange?.Invoke();
    }
}
