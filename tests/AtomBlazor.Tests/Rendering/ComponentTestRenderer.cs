using AtomBlazor.Primitives;
using System.Text;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace AtomBlazor.Tests.Rendering;

// 渲染器内部实现细节全部收敛在本适配器中；测试只使用组件的公开方法。
#pragma warning disable BL0006
internal sealed class ComponentTestRenderer(IServiceProvider services, ILoggerFactory loggerFactory)
    : Renderer(services, loggerFactory)
{
    private readonly List<int> roots = [];
    public override Dispatcher Dispatcher { get; } = Dispatcher.CreateDefault();
    protected override Task UpdateDisplayAsync(in RenderBatch renderBatch) => Task.CompletedTask;
    protected override void HandleException(Exception exception) => System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception).Throw();

    internal Task<T> MountAsync<T>(Dictionary<string, object?> parameters) where T : IComponent
    {
        // .NET 11 的 Renderer 强制 Dispatcher 线程亲和：挂载与渲染批处理必须发生在 Dispatcher 上。
        return Dispatcher.InvokeAsync(async () =>
        {
            var component = (T)InstantiateComponent(typeof(T));
            var id = AssignRootComponentId(component);
            roots.Add(id);
            await RenderRootComponentAsync(id, ParameterView.FromDictionary(parameters));
            return component;
        });
    }

    internal Task<IComponent> MountTypeAsync(Type type, Dictionary<string, object?> parameters)
    {
        return Dispatcher.InvokeAsync(async () =>
        {
            var component = InstantiateComponent(type);
            var id = AssignRootComponentId(component);
            roots.Add(id);
            await RenderRootComponentAsync(id, ParameterView.FromDictionary(parameters));
            return component;
        });
    }

    /// <summary>
    /// 用新的参数视图重渲染第 <paramref name="rootOrdinal"/> 个根组件，
    /// 用于测试参数变化沿（例如 IsOpen 的打开沿与关闭沿）。
    /// 参数视图中未出现的参数保持组件上的当前值。
    /// </summary>
    internal Task RerenderRootAsync(int rootOrdinal, Dictionary<string, object?> parameters)
    {
        return Dispatcher.InvokeAsync(() =>
            RenderRootComponentAsync(roots[rootOrdinal], ParameterView.FromDictionary(parameters)));
    }

    /// <summary>
    /// 将渲染树序列化为类 HTML 标记，使测试可以断言组件实际写入 DOM 的内容：
    /// 特性落在了哪个元素上、id 是否被写出、ARIA 特性是否携带了值。
    /// 渲染器本身从不生成文档，而这些测试所守护的缺陷，恰恰是仅凭组件状态
    /// 无法看到的。
    /// </summary>
    internal string Markup()
    {
        var builder = new StringBuilder();

        foreach (var id in roots)
        {
            AppendFrames(builder, GetCurrentRenderTreeFrames(id));
        }

        return builder.ToString();
    }

    private void AppendFrames(StringBuilder builder, ArrayRange<RenderTreeFrame> frames)
    {
        var index = 0;

        while (index < frames.Count)
        {
            index = AppendFrame(builder, frames, index);
        }
    }

    private int AppendFrame(StringBuilder builder, ArrayRange<RenderTreeFrame> frames, int index)
    {
        var frame = frames.Array[index];

        switch (frame.FrameType)
        {
            case RenderTreeFrameType.Element:
            {
                var end = index + frame.ElementSubtreeLength;
                var child = index + 1;

                builder.Append('<').Append(frame.ElementName);

                while (child < end && frames.Array[child].FrameType is RenderTreeFrameType.Attribute
                       or RenderTreeFrameType.ElementReferenceCapture)
                {
                    if (frames.Array[child].FrameType == RenderTreeFrameType.Attribute)
                    {
                        AppendAttribute(builder, frames.Array[child]);
                    }

                    child++;
                }

                builder.Append('>');

                while (child < end)
                {
                    child = AppendFrame(builder, frames, child);
                }

                builder.Append("</").Append(frame.ElementName).Append('>');
                return end;
            }

            case RenderTreeFrameType.Text:
                builder.Append(frame.TextContent);
                return index + 1;

            case RenderTreeFrameType.Markup:
                builder.Append(frame.MarkupContent);
                return index + 1;

            case RenderTreeFrameType.Component:
                AppendFrames(builder, GetCurrentRenderTreeFrames(frame.ComponentId));
                return index + frame.ComponentSubtreeLength;

            case RenderTreeFrameType.Region:
            {
                var end = index + frame.RegionSubtreeLength;
                var child = index + 1;

                while (child < end)
                {
                    child = AppendFrame(builder, frames, child);
                }

                return end;
            }

            default:
                return index + 1;
        }
    }

    private static void AppendAttribute(StringBuilder builder, RenderTreeFrame frame)
    {
        switch (frame.AttributeValue)
        {
            case null:
                return;

            // 事件处理器没有标记形式；写出委托只会增加噪音。
            case Delegate:
            case EventCallback:
                return;

            case bool flag:
                if (flag)
                {
                    builder.Append(' ').Append(frame.AttributeName);
                }

                return;

            default:
                builder.Append(' ').Append(frame.AttributeName)
                    .Append("=\"").Append(frame.AttributeValue).Append('"');
                return;
        }
    }

    /// <summary>
    /// 针对已渲染的事件处理器触发一次 DOM 事件，从而按浏览器实际操作的方式
    /// 测试键盘与指针行为。<paramref name="occurrence"/> 用于在多个携带同一
    /// 处理器的元素之间按渲染顺序选取。
    /// </summary>
    internal Task DispatchAsync(string attributeName, EventArgs args, int occurrence = 0)
    {
        var handlers = new List<ulong>();

        foreach (var id in roots)
        {
            CollectHandlers(GetCurrentRenderTreeFrames(id), attributeName, handlers);
        }

        if (handlers.Count <= occurrence)
        {
            throw new InvalidOperationException(
                $"No rendered '{attributeName}' handler at index {occurrence}; found {handlers.Count}.");
        }

        return DispatchEventAsync(handlers[occurrence], new EventFieldInfo(), args);
    }

    private void CollectHandlers(ArrayRange<RenderTreeFrame> frames, string attributeName, List<ulong> handlers)
    {
        for (var i = 0; i < frames.Count; i++)
        {
            var frame = frames.Array[i];

            if (frame.FrameType == RenderTreeFrameType.Attribute
                && frame.AttributeEventHandlerId != 0
                && string.Equals(frame.AttributeName, attributeName, StringComparison.Ordinal))
            {
                handlers.Add(frame.AttributeEventHandlerId);
            }
            else if (frame.FrameType == RenderTreeFrameType.Component)
            {
                CollectHandlers(GetCurrentRenderTreeFrames(frame.ComponentId), attributeName, handlers);
            }
        }
    }

    internal T FindComponent<T>() where T : IComponent
    {
        var pending = new Stack<int>(roots);
        while (pending.TryPop(out var id))
        {
            var frames = GetCurrentRenderTreeFrames(id);
            for (var i = 0; i < frames.Count; i++)
            {
                var frame = frames.Array[i];
                if (frame.FrameType == RenderTreeFrameType.Component)
                {
                    if (frame.Component is T match)
                    {
                        return match;
                    }
                    pending.Push(frame.ComponentId);
                }
            }
        }
        throw new InvalidOperationException($"No rendered {typeof(T).Name} component found.");
    }
}
#pragma warning restore BL0006

internal sealed class NoopJavaScript : IJSRuntime, IJSObjectReference
{
    /// <summary>
    /// 按调用顺序记录每次 interop 的标识符与参数，供断言派发内容之用。
    /// 所有派发都发生在渲染器 Dispatcher 线程上，普通 List 即可。
    /// </summary>
    internal List<(string Identifier, object?[]? Args)> Invocations { get; } = [];

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => InvokeAsync<TValue>(identifier, CancellationToken.None, args);
    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
    {
        Invocations.Add((identifier, args));
        return ValueTask.FromResult(typeof(TValue) == typeof(IJSObjectReference) ? (TValue)(object)this : default!);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
