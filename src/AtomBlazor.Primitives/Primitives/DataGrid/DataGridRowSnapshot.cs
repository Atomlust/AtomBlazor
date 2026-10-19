using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace AtomBlazor.Primitives;

/// <summary>
/// 记录项的公共可写属性的值，以便撤销一次编辑。
/// </summary>
/// <remarks>
/// 行编辑直接绑定到项本身，这正是编辑模板可以使用普通 <c>@bind-Value</c>
/// 的原因。也就是说，取消时必须把旧值写回，快照正是用来保存这些旧值的。
/// 只捕获既可读又可写的公共实例属性——计算属性没有需要恢复的内容，
/// 而私有字段不可能被编辑模板改动。
/// </remarks>
/// <typeparam name="TData">数据项的类型。</typeparam>
[SuppressMessage("Design", "CA1000:Do not declare static members on generic types",
    Justification = "Capture is the only way to make a snapshot, and the type argument is already " +
                    "implied by the item being passed in.")]
public sealed class DataGridRowSnapshot<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TData> where TData : class
{
    private static readonly PropertyInfo[] properties = typeof(TData)
        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Where(p => p.CanRead && p.CanWrite && p.GetIndexParameters().Length == 0)
        .ToArray();

    private readonly Dictionary<string, object?> values;

    private DataGridRowSnapshot(TData item, Dictionary<string, object?> values)
    {
        Item = item;
        this.values = values;
    }

    /// <summary>
    /// 获取快照所取自的项。
    /// </summary>
    public TData Item { get; }

    /// <summary>
    /// 对项的当前值拍摄快照。
    /// </summary>
    /// <param name="item">要记录的项。</param>
    /// <returns>快照。</returns>
    /// <exception cref="ArgumentNullException">当 item 为 null 时抛出。</exception>
    public static DataGridRowSnapshot<TData> Capture(TData item)
    {
        ArgumentNullException.ThrowIfNull(item);

        var values = new Dictionary<string, object?>(properties.Length, StringComparer.Ordinal);
        foreach (var property in properties)
        {
            try
            {
                values[property.Name] = property.GetValue(item);
            }
            catch (TargetInvocationException)
            {
                // A property that throws when read has no value worth restoring. Skipping it is
                // better than failing the edit before the user has typed anything.
            }
        }

        return new DataGridRowSnapshot<TData>(item, values);
    }

    /// <summary>
    /// 把记录的值写回项上。
    /// </summary>
    /// <remarks>
    /// 恢复会写回快照来源的同一个实例，因此任何仍持有该行引用的其他方
    /// 也会看到值被还原。
    /// </remarks>
    public void Restore()
    {
        foreach (var property in properties)
        {
            if (!values.TryGetValue(property.Name, out var value))
            {
                continue;
            }

            try
            {
                property.SetValue(Item, value);
            }
            catch (TargetInvocationException)
            {
                // A setter that rejects a value it previously held leaves that property as the
                // user left it. Better than abandoning the rest of the restore half-done.
            }
        }
    }

    /// <summary>
    /// 把捕获的可写属性值复制到另一个实例上。与 Restore 不同，
    /// setter 失败会向上抛出，使调用者不会把部分应用的编辑误认为成功。
    /// 引用值直接赋值而不克隆；嵌套数据请使用独立的编辑模型。
    /// </summary>
    /// <param name="target">接收这些值的记录。</param>
    public void ApplyTo(TData target)
    {
        ArgumentNullException.ThrowIfNull(target);
        foreach (var property in properties)
        {
            if (values.TryGetValue(property.Name, out var value))
            {
                property.SetValue(target, value);
            }
        }
    }

    /// <summary>
    /// 获取是否有某个记录值与项的当前值不同。
    /// </summary>
    /// <returns>自快照以来项被改动过时为 true。</returns>
    public bool HasChanges()
    {
        foreach (var property in properties)
        {
            if (!values.TryGetValue(property.Name, out var original))
            {
                continue;
            }

            object? current;
            try
            {
                current = property.GetValue(Item);
            }
            catch (TargetInvocationException)
            {
                continue;
            }

            if (!Equals(original, current))
            {
                return true;
            }
        }

        return false;
    }
}
