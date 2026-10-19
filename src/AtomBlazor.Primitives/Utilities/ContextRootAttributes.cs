using Microsoft.Extensions.Logging;

namespace AtomBlazor.Primitives.Utilities;

/// <summary>
/// 统一处理那些只负责提供上下文、自身不渲染任何元素的基元根组件
/// （Dialog、Sheet、Popover、HoverCard 及各门户）。
/// <para>
/// 这些根仍然声明一个 <c>CaptureUnmatchedValues</c> 参数，因为带样式的包装层会把自己的
/// <c>AdditionalAttributes</c> splat 到它们身上，而向没有此类参数的组件 splat 会抛出
/// <see cref="InvalidOperationException"/>。这些属性无处落地，于是被接受并按每个组件实例
/// 报告一次，而不是悄无声息地被丢弃。
/// </para>
/// </summary>
internal static class ContextRootAttributes
{
    /// <summary>
    /// 当有额外 HTML 属性被传给一个只供上下文的根时，记录一次性的警告。
    /// </summary>
    /// <param name="logger">用于上报警告的日志器。</param>
    /// <param name="alreadyWarned">本组件实例是否已经警告过。</param>
    /// <param name="componentName">根组件在标记中的名字，例如 <c>Dialog</c>。</param>
    /// <param name="suggestion">使用者应当把这些属性改放到哪里。</param>
    /// <param name="attributes">捕获到的额外属性，若有的话。</param>
    /// <returns>警告已记录后返回 <c>true</c>，供调用方保存。</returns>
    public static bool WarnOnce(
        ILogger logger,
        bool alreadyWarned,
        string componentName,
        string suggestion,
        IReadOnlyDictionary<string, object>? attributes)
    {
        ArgumentNullException.ThrowIfNull(logger);

        if (alreadyWarned || attributes is not { Count: > 0 })
        {
            return alreadyWarned;
        }

        noEffectWarning(logger, componentName, string.Join(", ", attributes.Keys), suggestion, null);

        return true;
    }

    private static readonly Action<ILogger, string, string, string, Exception?> noEffectWarning =
        LoggerMessage.Define<string, string, string>(
            LogLevel.Warning,
            new EventId(1, nameof(ContextRootAttributes)),
            "{ComponentName}：额外的 HTML 属性 [{AttributeNames}] 不会生效。" +
            "它自身不渲染任何元素\u2014\u2014只向子组件提供上下文。{Suggestion}");
}
