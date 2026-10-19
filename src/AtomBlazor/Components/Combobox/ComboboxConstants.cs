namespace AtomBlazor;

/// <summary>
/// Combobox 组件族共享的、与类型无关的常量。
/// </summary>
internal static class ComboboxConstants
{
    /// <summary>
    /// 父组件用来标记其隐藏的、不渲染任何内容的注册通道的级联标志名，
    /// 让 <c>ComboboxItem</c> 子组件在初始加载时注册自己的显示文本而不产出可交互的 DOM。
    /// 放在非泛型类型上，才能在不牵扯父组件类型参数的前提下被
    /// <see cref="Microsoft.AspNetCore.Components.CascadingParameterAttribute"/> 的 Name 引用。
    /// </summary>
    public const string RegistrationScopeName = "ComboboxRegistrationOnly";
}
