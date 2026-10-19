namespace AtomBlazor;

/// <summary>
/// 定义动态表单字段的字段类型。每种类型映射到特定的输入组件。
/// </summary>
public enum FieldType
{
    /// <summary>单行文本输入。</summary>
    Text,

    /// <summary>电子邮件输入，移动端唤起邮箱键盘。</summary>
    Email,

    /// <summary>密码输入，字符遮蔽显示。</summary>
    Password,

    /// <summary>URL 输入。</summary>
    Url,

    /// <summary>电话号码输入，移动端唤起电话键盘。</summary>
    Phone,

    /// <summary>带增减控制的数字输入。</summary>
    Number,

    /// <summary>货币格式的数字输入。</summary>
    Currency,

    /// <summary>多行文本输入。</summary>
    Textarea,

    /// <summary>带格式化工具栏的富文本编辑器。</summary>
    RichText,

    /// <summary>样式化下拉选择器。</summary>
    Select,

    /// <summary>可搜索、可过滤的组合框。</summary>
    Combobox,

    /// <summary>多选下拉选择器。</summary>
    MultiSelect,

    /// <summary>原生 HTML select 元素。</summary>
    NativeSelect,

    /// <summary>单个复选框（布尔值）。</summary>
    Checkbox,

    /// <summary>开关切换（布尔值）。</summary>
    Switch,

    /// <summary>用于多选的复选框组。</summary>
    CheckboxGroup,

    /// <summary>用于单选的单选按钮组。</summary>
    RadioGroup,

    /// <summary>日期选择器。</summary>
    Date,

    /// <summary>带开始与结束日期的日期范围选择器。</summary>
    DateRange,

    /// <summary>时间选择器。</summary>
    Time,

    /// <summary>日期时间选择器。</summary>
    DateTime,

    /// <summary>颜色选择器。</summary>
    Color,

    /// <summary>文件上传。</summary>
    File,

    /// <summary>一次性密码（OTP）输入。</summary>
    OTP,

    /// <summary>单值滑块。</summary>
    Slider,

    /// <summary>双滑块的范围选择器。</summary>
    RangeSlider,

    /// <summary>管理字符串列表的标签/芯片输入。</summary>
    Tags,

    /// <summary>自定义字段类型，委托给已注册的渲染器。</summary>
    Custom
}
