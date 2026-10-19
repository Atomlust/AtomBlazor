namespace AtomBlazor;

/// <summary>
/// 定义 Input 组件的输入类型。
/// </summary>
/// <remarks>
/// 与 HTML input 的 type 属性一一对应。每种类型提供不同的输入行为、
/// 校验规则与移动端键盘布局。
/// </remarks>
public enum InputType
{
    /// <summary>
    /// 单行文本输入（默认）。
    /// 接受任意文本字符。
    /// </summary>
    Text,

    /// <summary>
    /// 电子邮箱输入。
    /// 提供邮箱格式校验，移动端键盘带 @ 键。
    /// </summary>
    Email,

    /// <summary>
    /// 密码输入，字符被遮蔽。
    /// 出于安全考虑文本不直接显示（以圆点或星号呈现）。
    /// </summary>
    Password,

    /// <summary>
    /// 数字输入。
    /// 移动端显示数字键盘，并允许浏览器自带的步进调节按钮。
    /// </summary>
    Number,

    /// <summary>
    /// 电话号码输入。
    /// 针对号码录入优化，显示 tel: 键盘布局。
    /// </summary>
    Tel,

    /// <summary>
    /// URL 输入。
    /// 提供 URL 格式校验，移动端键盘带 .com 键。
    /// </summary>
    Url,

    /// <summary>
    /// 搜索词输入。
    /// 显示搜索图标，并可能提供最近搜索建议。
    /// </summary>
    Search,

    /// <summary>
    /// 日期选择输入。
    /// 在受支持的浏览器中显示原生日期选择器。
    /// </summary>
    Date,

    /// <summary>
    /// 时间选择输入。
    /// 在受支持的浏览器中显示原生时间选择器。
    /// </summary>
    Time,

    /// <summary>
    /// 文件上传输入。
    /// 允许用户从设备中选择文件。
    /// </summary>
    File
}
