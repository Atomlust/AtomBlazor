namespace AtomBlazor;

/// <summary>
/// 为 AtomBlazor 组件提供本地化字符串。
/// </summary>
/// <remarks>
/// <para>
/// 组件通过此接口以点分键（如 <c>"DataGrid.Loading"</c>、<c>"Combobox.Placeholder"</c>）
/// 解析所有界面外壳字符串（占位符、按钮标签、aria-label、空状态）。
/// </para>
/// <para>
/// 库默认注册 <see cref="DefaultLocalizer"/>，为全部键提供英文默认值。要自定义字符串，
/// 既可以在启动时配置默认实现，也可以用自定义实现替换其注册。
/// </para>
/// <example>
/// <code>
/// // 模式 A：启动时配置默认值
/// builder.Services.AddAtomBlazorComponents(localizer =>
/// {
///     localizer.Set("DataGrid.Loading", "Laden...");
///     localizer.Set("Pagination.Next", "Weiter");
/// });
///
/// // 模式 B：结合 IStringLocalizer 的自定义实现
/// public class AppLocalizer(IStringLocalizer&lt;SharedResources&gt; loc) : DefaultLocalizer
/// {
///     public override string this[string key] =>
///         loc[key] is { ResourceNotFound: false } found ? found.Value : base[key];
///
///     public override string this[string key, params object[] arguments] =>
///         loc[key, arguments] is { ResourceNotFound: false } found ? found.Value : base[key, arguments];
/// }
///
/// builder.Services.AddAtomBlazorComponents();
/// builder.Services.AddScoped&lt;ILocalizer, AppLocalizer&gt;();
/// </code>
/// </example>
/// </remarks>
public interface ILocalizer
{
    /// <summary>
    /// 获取指定键的本地化字符串。
    /// </summary>
    /// <param name="key">点分记法的本地化键（如 <c>"DataGrid.Loading"</c>）。</param>
    /// <returns>本地化字符串；未找到时返回键本身。</returns>
    public string this[string key] { get; }

    /// <summary>
    /// 获取指定键的本地化字符串，并用提供的参数格式化。
    /// </summary>
    /// <param name="key">点分记法的本地化键（如 <c>"DataGrid.ShowingRange"</c>）。</param>
    /// <param name="arguments">要替换进字符串的格式参数。</param>
    /// <returns>格式化后的本地化字符串；未找到时返回键本身。</returns>
    public string this[string key, params object[] arguments] { get; }
}
