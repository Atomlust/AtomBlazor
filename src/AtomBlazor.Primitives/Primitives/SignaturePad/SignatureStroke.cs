namespace AtomBlazor.Primitives;

/// <summary>
/// 一段连续的笔画：指针按下到抬起之间捕获的全部内容。
/// </summary>
/// <param name="Points">构成该笔画的采样点，按捕获顺序排列。</param>
public sealed record SignatureStroke(IReadOnlyList<SignaturePoint> Points);
