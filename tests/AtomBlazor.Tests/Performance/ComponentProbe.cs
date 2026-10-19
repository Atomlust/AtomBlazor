using AtomBlazor.Primitives;
using System.Reflection;

namespace AtomBlazor.Tests.Performance;

// 不经浏览器渲染器来走处理路径；这些检查数的是工作量，
// 而非给跑测试的机器计时。公开 API 保持不变。
internal static class ComponentProbe
{
    private const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;

    internal static object? Call(object component, string method, params object?[] args) =>
        component.GetType().GetMethod(method, flags)!.Invoke(component, args);

    internal static T Field<T>(object component, string field) =>
        (T)component.GetType().GetField(field, flags)!.GetValue(component)!;

    internal static void SetField(object component, string field, object value) =>
        component.GetType().GetField(field, flags)!.SetValue(component, value);
}
