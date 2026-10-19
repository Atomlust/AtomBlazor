using Microsoft.Extensions.Logging;

namespace AtomBlazor.Primitives.Utilities;

/// <summary>
/// AsChild 触发器在其内部没有任何组件读取它的 <see cref="TriggerContext"/> 时，
/// 如何在所记录的警告里描述自己。
/// </summary>
/// <param name="Name">触发器在标记中的写法，例如 <c>CollapsibleTrigger</c>。</param>
/// <param name="Action">因此做不到的事，例如 <c>折叠或展开此面板</c>。</param>
/// <param name="Element">AsChild 关闭时触发器渲染的元素，例如 <c>&lt;button&gt;</c>。</param>
public sealed record AsChildTriggerDescription(string Name, string Action, string Element);

/// <summary>
/// 针对 AsChild 唯一会静默失败的方式的开发期警告，适用于任何级联
/// <see cref="TriggerContext"/> 的触发器。
/// </summary>
/// <remarks>
/// <para>
/// AsChild 开启时，触发器不渲染自己的元素，也不挂任何处理器。它级联一个
/// <see cref="TriggerContext"/>，由子组件自己去接线。从未读取该上下文的子组件——
/// 文本、图标、普通 <c>span</c>，甚至一个裸 <c>button</c>——会留下一个什么都打不开、
/// 什么都切不了的局面，而给到触发器上的 class 与属性也没有元素可落。
/// 一切都不报错，也就无人出声。这里出声。
/// </para>
/// <para>
/// 库里每一个带 AsChild 模式的触发器都在用它；它是公开的，库外的触发器也能以同样方式告警。
/// 在触发器的 <c>OnAfterRender</c> 里、首帧时调用，传入最近一次渲染级联的上下文。
/// 子组件在渲染时、同一个渲染批次里读取上下文，届时结论已经落定；只查首帧意味着
/// 每个触发器至多警告一次。
/// </para>
/// <para>
/// 消费情况由上下文自己记录：对它的任何一次成员读取都会标记"已消费"，
/// 而只在事件处理器内部读取它的子组件可以用 <see cref="TriggerContext.NotifyConsumed"/>
/// 主动声明。除 Development 环境外一律不记日志。
/// </para>
/// </remarks>
public static class AsChildDiagnostics
{
    private static readonly Action<ILogger, string, string, string, string, Exception?> logUnconsumed =
        LoggerMessage.Define<string, string, string, string>(
            LogLevel.Warning,
            new EventId(1, "TriggerContextUnconsumed"),
            "{Trigger} 设置了 AsChild=\"true\"，但其内部没有任何组件读取它的 TriggerContext，" +
            "因此没有任何东西能够{Action}。在此模式下，触发器不渲染自己的元素，也不挂任何事件处理器。" +
            "要么把 AsChild 设为 \"false\"，让触发器在内容外渲染自己的{Element}——文本、普通标记或 " +
            "LucideIcon 这样的裸图标正需要这样做；要么在其内放入 Button——它会读取上下文并充当触发器。" +
            "{Ignored} 只有在事件处理器内部才读取上下文的自定义子组件，可调用 " +
            "TriggerContext.NotifyConsumed() 主动声明。此警告仅在 Development 环境记录。");

    /// <summary>
    /// 当 AsChild 触发器渲染出的上下文没有任何组件读取时，仅在 Development 环境记录一条警告。
    /// </summary>
    /// <typeparam name="TTrigger">触发器本身，用于命名日志类别。</typeparam>
    /// <param name="services">应用的服务提供器，用于取环境与日志器。</param>
    /// <param name="trigger">触发器如何描述自己。</param>
    /// <param name="asChild">触发器是否处于 AsChild 模式。</param>
    /// <param name="context">触发器最近一次渲染级联出去的上下文，若有的话。</param>
    /// <param name="attributes">触发器被赋予、而 AsChild 模式会丢弃的那些属性。</param>
    /// <remarks>
    /// 传入触发器本会写到自己元素上的每一个属性，class 也在其列。只有有值的属性会被点名，
    /// 所以带样式的触发器应当交出页面设置的那个 class，而不是它自己的样式——
    /// 否则读起来倒像是页面写了它的内置样式。日志器只在确有内容可记时才解析，
    /// 因此用法正确的触发器——绝大多数——根本不会碰日志器。
    /// </remarks>
    /// <example>
    /// <code>
    /// private static readonly AsChildTriggerDescription Description =
    ///     new("MyTrigger", "打开此面板", "&lt;button&gt;");
    ///
    /// protected override void OnAfterRender(bool firstRender)
    /// {
    ///     if (firstRender)
    ///     {
    ///         AsChildDiagnostics.WarnIfUnconsumed&lt;MyTrigger&gt;(
    ///             Services, Description, AsChild, lastTriggerContext, AdditionalAttributes);
    ///     }
    /// }
    /// </code>
    /// </example>
    public static void WarnIfUnconsumed<TTrigger>(
        IServiceProvider services,
        AsChildTriggerDescription trigger,
        bool asChild,
        TriggerContext? context,
        IReadOnlyDictionary<string, object>? attributes)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(trigger);

        if (!asChild || context is null || context.WasConsumed)
        {
            return;
        }

        if (!DevelopmentEnvironment.IsDevelopment(services))
        {
            return;
        }

        if (services.GetService(typeof(ILoggerFactory)) is not ILoggerFactory factory)
        {
            return;
        }

        logUnconsumed(factory.CreateLogger<TTrigger>(), trigger.Name, trigger.Action, trigger.Element, Ignored(attributes), null);
    }

    /// <summary>
    /// 把 AsChild 模式丢弃的属性点名，作为追加到警告后的一句话。
    /// </summary>
    /// <param name="attributes">触发器被赋予的属性。</param>
    /// <returns>带前导空格的一句话；什么都没丢时为空串。</returns>
    /// <remarks>
    /// 只有带值的属性才算数。带样式的触发器在 AsChild 模式下交给基元的是 null class
    /// 而不是自己的样式，所以这里点到的只有页面设置的那些。
    /// </remarks>
    internal static string Ignored(IReadOnlyDictionary<string, object>? attributes)
    {
        if (attributes is null)
        {
            return string.Empty;
        }

        var names = attributes
            .Where(a => a.Value is not null && a.Value is not string { Length: 0 })
            .Select(a => a.Key)
            .ToList();

        return names.Count == 0
            ? string.Empty
            : $" 它还被赋予了 {string.Join(", ", names)}。AsChild=\"true\" 时没有元素可承载" +
              (names.Count == 1 ? "它，因此被忽略。" : "它们，因此被忽略。");
    }
}
