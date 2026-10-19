namespace AtomBlazor.Primitives;

/// <summary>描述一次拟议的重排，此时消费方的集合尚未被改动。</summary>
/// <param name="Item">被移动的项目。</param>
/// <param name="OldIndex">其原始的从零起算位置。</param>
/// <param name="NewIndex">其拟议的从零起算位置。</param>
public sealed record SortableMoveContext<TItem>(TItem Item, int OldIndex, int NewIndex);
