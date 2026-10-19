namespace AtomBlazor;

/// <summary>
/// <see cref="Step"/> 注册时交给其 <see cref="Stepper"/> 的元数据，
/// 好让步进器无需伸进子组件就能画出指示器。
/// </summary>
internal sealed class StepInfo
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Icon { get; set; }
    public StepState? State { get; set; }
    public bool Disabled { get; set; }
    public bool Optional { get; set; }
    public bool HasContent { get; set; }
}
