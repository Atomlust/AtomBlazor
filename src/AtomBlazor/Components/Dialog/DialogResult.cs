namespace AtomBlazor;

/// <summary>
/// 表示一次对话框交互的结果。
/// </summary>
/// <remarks>
/// 对话框结果指示对话框是否被取消，并可能
/// 携带对话框提供的附加数据。
/// </remarks>
public class DialogResult
{
    /// <summary>
    /// 初始化 <see cref="DialogResult"/> 类的新实例。
    /// </summary>
    /// <param name="cancelled">
    /// 如果对话框被取消则为 <see langword="true"/>；否则为 <see langword="false"/>。
    /// </param>
    /// <param name="data">
    /// 对话框返回的可选数据。
    /// </param>
    protected DialogResult(bool cancelled, object? data = null)
    {
        Cancelled = cancelled;
        Data = data;
    }

    /// <summary>
    /// 获取一个值，该值指示对话框是否被取消。
    /// </summary>
    /// <value>
    /// 如果用户关闭或取消了对话框，则为 <see langword="true"/>；
    /// 否则为 <see langword="false"/>。
    /// </value>
    public bool Cancelled { get; }

    /// <summary>
    /// 获取对话框返回的原始数据。
    /// </summary>
    /// <remarks>
    /// 具体的数据类型取决于对话框的实现。
    /// 该值仅供内部使用。使用者应优先使用
    /// 强类型封装或 <see cref="GetData{T}"/>。
    /// </remarks>
    internal object? Data { get; }

    /// <summary>
    /// 尝试以指定类型检索对话框数据。
    /// </summary>
    /// <typeparam name="T">期望的数据类型。</typeparam>
    /// <returns>
    /// 如果类型兼容，则为转换为 <typeparamref name="T"/> 的数据；
    /// 否则为 <typeparamref name="T"/> 的默认值。
    /// </returns>
    public T? GetData<T>()
        => Data is T typed ? typed : default;

    /// <summary>
    /// 创建一个已取消的对话框结果。
    /// </summary>
    /// <returns>表示已取消对话框的 <see cref="DialogResult"/>。</returns>
    public static DialogResult Cancel()
        => new(true);

    /// <summary>
    /// 创建一个成功的对话框结果。
    /// </summary>
    /// <param name="data">对话框返回的可选数据。</param>
    /// <returns>表示成功对话框的 <see cref="DialogResult"/>。</returns>
    public static DialogResult Ok(object? data = null)
        => new(false, data);
}
