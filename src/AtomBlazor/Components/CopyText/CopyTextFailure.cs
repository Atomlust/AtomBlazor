namespace AtomBlazor;

/// <summary>
/// 复制没有发生的原因。
/// </summary>
/// <remarks>
/// 在 #466 之前，失败的复制是不可见的：组件展示它的已复制状态，而
/// 剪贴板仍然是空的。使用异步来源时，拒绝要常见得多，所以结果会被
/// 上报而不是被吞掉。
/// </remarks>
public enum CopyTextFailure
{
    /// <summary>
    /// 浏览器拒绝了写入。最常见的原因是瞬态用户激活已过期——
    /// 当文本在生产上耗时较长、而浏览器又不支持 promise 感知的
    /// <c>ClipboardItem</c> 时，就会发生这种情况。
    /// </summary>
    Refused = 0,

    /// <summary>
    /// 没有可复制的内容——既无 <c>Value</c>，func 也返回了 null 或空串。
    /// </summary>
    NoValue = 1,
}
