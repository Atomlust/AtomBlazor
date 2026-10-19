using System.Diagnostics.CodeAnalysis;

namespace AtomBlazor;

/// <summary>
/// 一次在 <c>PickList</c> 两个窗格之间的移动。
/// </summary>
/// <typeparam name="TValue">选项值的类型。</typeparam>
/// <param name="Values">被移动的值。</param>
/// <param name="ToTarget">
/// 移入已选窗格时为 <c>true</c>，移出回到可用窗格时为 <c>false</c>。
/// </param>
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "Type represents event arguments")]
public readonly record struct PickListMoveEventArgs<TValue>(IReadOnlyList<TValue> Values, bool ToTarget);
