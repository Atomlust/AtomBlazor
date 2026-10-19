namespace AtomBlazor.Primitives.Filtering;

/// <summary>
/// 提供 <see cref="FilterFieldType"/> 与可用 <see cref="FilterOperator"/> 值之间的映射，
/// 以及运算符的显示标签。
/// </summary>
public static class FilterOperatorHelper
{
    private static readonly Dictionary<FilterFieldType, FilterOperator[]> operatorsByType = new()
    {
        [FilterFieldType.Text] = new[]
        {
            FilterOperator.Equals,
            FilterOperator.NotEquals,
            FilterOperator.Contains,
            FilterOperator.NotContains,
            FilterOperator.StartsWith,
            FilterOperator.EndsWith,
            FilterOperator.IsEmpty,
            FilterOperator.IsNotEmpty
        },
        [FilterFieldType.Number] = new[]
        {
            FilterOperator.Equals,
            FilterOperator.NotEquals,
            FilterOperator.GreaterThan,
            FilterOperator.LessThan,
            FilterOperator.GreaterOrEqual,
            FilterOperator.LessOrEqual,
            FilterOperator.Between,
            FilterOperator.IsEmpty,
            FilterOperator.IsNotEmpty
        },
        [FilterFieldType.Date] = new[]
        {
            FilterOperator.Equals,
            FilterOperator.NotEquals,
            FilterOperator.GreaterThan,
            FilterOperator.LessThan,
            FilterOperator.Between,
            FilterOperator.InLast,
            FilterOperator.InNext,
            FilterOperator.DateIs,
            FilterOperator.DateIsNot,
            FilterOperator.IsEmpty,
            FilterOperator.IsNotEmpty
        },
        [FilterFieldType.DateTime] = new[]
        {
            FilterOperator.Equals,
            FilterOperator.NotEquals,
            FilterOperator.GreaterThan,
            FilterOperator.LessThan,
            FilterOperator.Between,
            FilterOperator.InLast,
            FilterOperator.InNext,
            FilterOperator.DateIs,
            FilterOperator.DateIsNot,
            FilterOperator.IsEmpty,
            FilterOperator.IsNotEmpty
        },
        [FilterFieldType.Boolean] = new[]
        {
            FilterOperator.IsTrue,
            FilterOperator.IsFalse
        },
        [FilterFieldType.Enum] = new[]
        {
            FilterOperator.Equals,
            FilterOperator.NotEquals,
            FilterOperator.In,
            FilterOperator.NotIn,
            FilterOperator.IsEmpty,
            FilterOperator.IsNotEmpty
        }
    };

    private static readonly Dictionary<FilterOperator, string> operatorLabels = new()
    {
        [FilterOperator.Equals] = "equals",
        [FilterOperator.NotEquals] = "not equals",
        [FilterOperator.IsEmpty] = "is empty",
        [FilterOperator.IsNotEmpty] = "is not empty",
        [FilterOperator.Contains] = "contains",
        [FilterOperator.NotContains] = "not contains",
        [FilterOperator.StartsWith] = "starts with",
        [FilterOperator.EndsWith] = "ends with",
        [FilterOperator.GreaterThan] = "greater than",
        [FilterOperator.LessThan] = "less than",
        [FilterOperator.GreaterOrEqual] = "greater or equal",
        [FilterOperator.LessOrEqual] = "less or equal",
        [FilterOperator.Between] = "between",
        [FilterOperator.InLast] = "in the last",
        [FilterOperator.InNext] = "in the next",
        [FilterOperator.In] = "is any of",
        [FilterOperator.NotIn] = "is none of",
        [FilterOperator.IsTrue] = "is true",
        [FilterOperator.IsFalse] = "is false",
        [FilterOperator.DateIs] = "is",
        [FilterOperator.DateIsNot] = "is not"
    };

    private static readonly Dictionary<FilterOperator, string> dateOperatorLabels = new()
    {
        [FilterOperator.GreaterThan] = "is after",
        [FilterOperator.LessThan] = "is before",
    };

    /// <summary>
    /// 获取给定字段类型可用的运算符。
    /// </summary>
    public static FilterOperator[] GetOperatorsForType(FilterFieldType type) =>
        operatorsByType.TryGetValue(type, out var operators) ? operators : Array.Empty<FilterOperator>();

    /// <summary>
    /// 获取运算符的显示标签，可按特定字段类型调整。
    /// </summary>
    /// <param name="op">要打标签的运算符。</param>
    /// <param name="fieldType">字段类型，适用时用于选择日期专用的措辞。</param>
    /// <param name="localize">
    /// 可选的键解析器（例如 <c>ILocalizer</c> 索引器）。以形如
    /// <c>FilterBuilder.Operator{Op}</c>（日期变体为 <c>FilterBuilder.Operator{Op}Date</c>）的键调用。
    /// 若原样返回键——即"未找到"的约定——则使用内置的英文标签。
    /// 这层间接允许本地化器（位于 Components 层）驱动标签，
    /// 而 Primitives 层无需依赖它。
    /// </param>
    public static string GetOperatorLabel(
        FilterOperator op,
        FilterFieldType? fieldType = null,
        Func<string, string>? localize = null)
    {
        var isDateVariant = fieldType is FilterFieldType.Date or FilterFieldType.DateTime
            && dateOperatorLabels.ContainsKey(op);
        var key = isDateVariant
            ? $"FilterBuilder.Operator{op}Date"
            : $"FilterBuilder.Operator{op}";

        if (localize is not null)
        {
            var localized = localize(key);
            if (!string.Equals(localized, key, StringComparison.Ordinal))
            {
                return localized;
            }
        }

        if (isDateVariant)
        {
            return dateOperatorLabels[op];
        }

        return operatorLabels.TryGetValue(op, out var label) ? label : op.ToString();
    }

    /// <summary>
    /// 获取给定字段类型各运算符的 <see cref="SelectOption{TValue}"/> 条目。
    /// </summary>
    /// <param name="type">要列出其运算符的字段类型。</param>
    /// <param name="localize">可选的键解析器——参见 <see cref="GetOperatorLabel"/>。</param>
    public static IEnumerable<SelectOption<FilterOperator>> GetOperatorOptions(
        FilterFieldType type,
        Func<string, string>? localize = null)
    {
        return GetOperatorsForType(type)
            .Select(op => new SelectOption<FilterOperator>(op, GetOperatorLabel(op, type, localize)));
    }

    /// <summary>
    /// 若运算符无需值输入（运算符本身即隐含其值）则返回 true。
    /// </summary>
    public static bool IsValuelessOperator(FilterOperator op)
    {
        return op is FilterOperator.IsEmpty
            or FilterOperator.IsNotEmpty
            or FilterOperator.IsTrue
            or FilterOperator.IsFalse;
    }

    /// <summary>
    /// 若运算符需要两个值（例如 Between）则返回 true。
    /// </summary>
    public static bool IsRangeOperator(FilterOperator op) =>
        op is FilterOperator.Between;

    /// <summary>
    /// 若运算符使用 <see cref="DatePreset"/> 下拉框作为值输入则返回 true。
    /// </summary>
    public static bool IsDatePresetOperator(FilterOperator op) =>
        op is FilterOperator.DateIs or FilterOperator.DateIsNot;
}
