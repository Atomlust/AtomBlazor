using System.Diagnostics.CodeAnalysis;

namespace AtomBlazor.Components.Dialog.Internals;

/// <summary>
/// 渲染 <see cref="ComponentDialogData.ComponentType"/> 指定的对话框内容组件。
/// </summary>
/// <remarks>
/// <see cref="Microsoft.AspNetCore.Components.DynamicComponent"/> 由框架反射式赋值
/// <c>Type</c> 参数，修剪器无法为其 <c>DynamicallyAccessedMembers</c> 要求作保证；
/// 组件类型来自用户的泛型调用，随宿主程序集保留，因此仅在声明类内抑制该诊断。
/// </remarks>
[UnconditionalSuppressMessage("Trimming", "IL2111",
    Justification = "DynamicComponent 的参数赋值由框架反射完成；对话框组件类型经用户泛型调用保留。")]
public partial class ComponentDialog
{
}
