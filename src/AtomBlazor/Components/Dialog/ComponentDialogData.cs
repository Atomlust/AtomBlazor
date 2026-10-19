using System.Diagnostics.CodeAnalysis;

namespace AtomBlazor;

public sealed class ComponentDialogData : DialogData, IDialogReference
{
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    public required Type ComponentType { get; init; }

    public Dictionary<string, object?> Parameters { get; init; } = new();

    public DialogOpenOptions Options { get; init; } = new();

    internal Func<DialogResult, Task>? CloseCallback { get; init; }

    public Task CloseAsync(DialogResult result)
        => CloseCallback?.Invoke(result) ?? Task.CompletedTask;

    public Task CancelAsync()
        => CloseCallback?.Invoke(DialogResult.Cancel()) ?? Task.CompletedTask;
}
