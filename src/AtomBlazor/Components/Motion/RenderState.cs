namespace AtomBlazor;

/// <summary>
/// 由 <see cref="RenderStateProvider"/> 级联提供的渲染态。
/// </summary>
/// <param name="IsInteractive">静态预渲染阶段为 false，首次交互渲染完成后为 true。</param>
public sealed record RenderState(bool IsInteractive);
