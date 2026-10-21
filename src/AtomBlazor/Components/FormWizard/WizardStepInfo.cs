namespace AtomBlazor;

/// <summary>
/// 保存向导步骤元数据的内部数据类。
/// 由 <see cref="WizardStep"/> 在注册时填充。
/// </summary>
internal sealed class WizardStepInfo
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Icon { get; set; }
    public bool IsOptional { get; set; }
    public bool Disabled { get; set; }
    public string[]? FieldNames { get; set; }
    public Func<bool>? Validator { get; set; }
    public Func<CancellationToken, Task<bool>>? AsyncValidator { get; set; }
}
