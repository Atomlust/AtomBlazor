using AtomBlazor.Primitives;
using AtomBlazor.Primitives.Filtering;
using System.Linq.Expressions;
using Xunit;
using PrimitivesFiltering = AtomBlazor.Primitives.Filtering;

namespace AtomBlazor.Tests.Primitives;

/// <summary>
/// Filtering(P) 基元测试（依赖先行波；全自写——Filters/FilterPresetTests.cs
/// 依赖 FilterBuilder 故不可移植，登记为待移植项）。
/// 覆盖：FilterDefinition/PrimitivesFiltering.FilterCondition 结构与深拷贝、FilterOperatorHelper 算子表与标签、
/// MatchesValue 各算子语义（含"不完整条件匹配全部"与"整日语义"两条怪癖）、
/// 日期预设与 InLast/InNext、ToFunc/ToExpression/ToExpressionForSelector、JSON 往返。
/// </summary>
public class PrimitiveFilteringTests
{
    private sealed class Row
    {
        public string Name { get; set; } = "";

        public int Age { get; set; }

        public bool Active { get; set; }

        public DateTime Created { get; set; }
    }

    private static readonly FilterField[] fields =
    [
        new() { Name = "Name", Label = "Name", Type = PrimitivesFiltering.FilterFieldType.Text },
        new() { Name = "Age", Label = "Age", Type = PrimitivesFiltering.FilterFieldType.Number },
        new() { Name = "Active", Label = "Active", Type = PrimitivesFiltering.FilterFieldType.Boolean },
        new() { Name = "Created", Label = "Created", Type = PrimitivesFiltering.FilterFieldType.Date }
    ];

    // CA1861：断言里重复出现的常量数组一律提为 static readonly（历波口径：不加 NoWarn）
    private static readonly PrimitivesFiltering.FilterOperator[] textOperators =
    [
        PrimitivesFiltering.FilterOperator.Equals, PrimitivesFiltering.FilterOperator.NotEquals, PrimitivesFiltering.FilterOperator.Contains,
        PrimitivesFiltering.FilterOperator.NotContains, PrimitivesFiltering.FilterOperator.StartsWith, PrimitivesFiltering.FilterOperator.EndsWith,
        PrimitivesFiltering.FilterOperator.IsEmpty, PrimitivesFiltering.FilterOperator.IsNotEmpty
    ];

    private static readonly PrimitivesFiltering.FilterOperator[] booleanOperators =
        [PrimitivesFiltering.FilterOperator.IsTrue, PrimitivesFiltering.FilterOperator.IsFalse];

    private static readonly bool[] andMatches = [true, false, false];

    private static readonly bool[] orMatches = [true, true, false];

    private static readonly bool[] ageMatches = [true, false];

    private static readonly string[] xyValues = ["x", "y"];

    private static readonly string[] abValues = ["a", "b"];

    private static PrimitivesFiltering.FilterCondition Cond(
        PrimitivesFiltering.FilterOperator op,
        object? value = null,
        object? valueEnd = null,
        string field = "Name") => new()
        {
            Field = field,
            Operator = op,
            Value = value,
            ValueEnd = valueEnd
        };

    // ── FilterDefinition 结构 ─────────────────────────────────────────

    [Fact]
    public void 空定义IsEmpty为真且条件计数为零()
    {
        var filter = new FilterDefinition();

        Assert.True(filter.IsEmpty);
        Assert.Equal(0, filter.TotalConditionCount);
        Assert.Equal(LogicalOperator.And, filter.Operator);
    }

    [Fact]
    public void TotalConditionCount递归统计嵌套组条件数()
    {
        var filter = new FilterDefinition
        {
            Conditions = [Cond(PrimitivesFiltering.FilterOperator.Equals, "a"), Cond(PrimitivesFiltering.FilterOperator.Equals, "b")],
            Groups =
            [
                new FilterDefinition { Conditions = [Cond(PrimitivesFiltering.FilterOperator.Equals, "c")] },
                new FilterDefinition
                {
                    Conditions = [Cond(PrimitivesFiltering.FilterOperator.Equals, "d")],
                    Groups = [new FilterDefinition { Conditions = [Cond(PrimitivesFiltering.FilterOperator.Equals, "e")] }]
                }
            ]
        };

        Assert.Equal(5, filter.TotalConditionCount);
        Assert.False(filter.IsEmpty);
    }

    [Fact]
    public void 仅含空组时IsEmpty为假()
    {
        var filter = new FilterDefinition { Groups = [new FilterDefinition()] };

        Assert.False(filter.IsEmpty);
        Assert.Equal(0, filter.TotalConditionCount);
    }

    [Fact]
    public void Clone深拷贝条件与嵌套组但保留标量值()
    {
        var filter = new FilterDefinition
        {
            Operator = LogicalOperator.Or,
            Conditions = [Cond(PrimitivesFiltering.FilterOperator.In, xyValues)],
            Groups = [new FilterDefinition { Conditions = [Cond(PrimitivesFiltering.FilterOperator.Equals, "z")] }]
        };

        var clone = filter.Clone();

        Assert.Equal(LogicalOperator.Or, clone.Operator);
        Assert.NotSame(filter.Conditions[0], clone.Conditions[0]);
        Assert.NotSame(filter.Groups[0], clone.Groups[0]);
        Assert.Equal(filter.Conditions[0].Id, clone.Conditions[0].Id);
        Assert.Equal(filter.Conditions[0].Field, clone.Conditions[0].Field);
        Assert.Equal(filter.Conditions[0].Operator, clone.Conditions[0].Operator);
        Assert.Equal(xyValues, Assert.IsType<string[]>(clone.Conditions[0].Value));
    }

    // ── PrimitivesFiltering.FilterCondition ──────────────────────────────────────────────

    [Fact]
    public void 条件默认Id为八字符GUID片段且字段默认为空串()
    {
        var condition = new PrimitivesFiltering.FilterCondition();

        Assert.Equal(8, condition.Id.Length);
        Assert.Equal("", condition.Field);
        Assert.Equal(PrimitivesFiltering.FilterOperator.Equals, condition.Operator);
    }

    [Fact]
    public void 条件克隆数组值独立标量值沿用()
    {
        var values = new[] { "a", "b" };
        var condition = Cond(PrimitivesFiltering.FilterOperator.In, values);
        var clone = condition.Clone();

        Assert.NotSame(values, clone.Value);
        values[0] = "mutated";
        Assert.Equal("a", Assert.IsType<string[]>(clone.Value)[0]);
    }

    // ── FilterOperatorHelper ─────────────────────────────────────────

    [Fact]
    public void 文本类型算子表含字符串算子且不含区间算子()
    {
        var operators = FilterOperatorHelper.GetOperatorsForType(PrimitivesFiltering.FilterFieldType.Text);

        Assert.Equal(textOperators, operators);
        Assert.DoesNotContain(PrimitivesFiltering.FilterOperator.Between, operators);
    }

    [Fact]
    public void 数值类型算子表含区间与比较算子()
    {
        var operators = FilterOperatorHelper.GetOperatorsForType(PrimitivesFiltering.FilterFieldType.Number);

        Assert.Contains(PrimitivesFiltering.FilterOperator.Between, operators);
        Assert.Contains(PrimitivesFiltering.FilterOperator.GreaterOrEqual, operators);
        Assert.DoesNotContain(PrimitivesFiltering.FilterOperator.Contains, operators);
    }

    [Fact]
    public void 布尔类型算子表仅真假两算子() =>
        Assert.Equal(booleanOperators, FilterOperatorHelper.GetOperatorsForType(PrimitivesFiltering.FilterFieldType.Boolean));

    [Fact]
    public void 枚举类型算子表含In与NotIn()
    {
        var operators = FilterOperatorHelper.GetOperatorsForType(PrimitivesFiltering.FilterFieldType.Enum);

        Assert.Contains(PrimitivesFiltering.FilterOperator.In, operators);
        Assert.Contains(PrimitivesFiltering.FilterOperator.NotIn, operators);
        Assert.DoesNotContain(PrimitivesFiltering.FilterOperator.Contains, operators);
    }

    [Fact]
    public void 未知字段类型返回空算子数组而非抛异常() =>
        Assert.Empty(FilterOperatorHelper.GetOperatorsForType((PrimitivesFiltering.FilterFieldType)99));

    [Fact]
    public void 算子标签默认取内置英文文案()
    {
        Assert.Equal("contains", FilterOperatorHelper.GetOperatorLabel(PrimitivesFiltering.FilterOperator.Contains));
        Assert.Equal("is any of", FilterOperatorHelper.GetOperatorLabel(PrimitivesFiltering.FilterOperator.In));
    }

    [Theory]
    [InlineData(PrimitivesFiltering.FilterOperator.GreaterThan, "is after")]
    [InlineData(PrimitivesFiltering.FilterOperator.LessThan, "is before")]
    public void 日期字段类型改用语序化标签(PrimitivesFiltering.FilterOperator op, string expected)
    {
        Assert.Equal(expected, FilterOperatorHelper.GetOperatorLabel(op, PrimitivesFiltering.FilterFieldType.Date));
        // 非日期字段类型回落通用文案
        Assert.Equal(op == PrimitivesFiltering.FilterOperator.GreaterThan ? "greater than" : "less than",
            FilterOperatorHelper.GetOperatorLabel(op, PrimitivesFiltering.FilterFieldType.Number));
    }

    [Fact]
    public void 本地化键命中则返回本地化值未命中回落英文()
    {
        var localized = FilterOperatorHelper.GetOperatorLabel(
            PrimitivesFiltering.FilterOperator.Contains, PrimitivesFiltering.FilterFieldType.Text, key => key == "FilterBuilder.OperatorContains" ? "包含" : key);

        Assert.Equal("包含", localized);

        var fallback = FilterOperatorHelper.GetOperatorLabel(
            PrimitivesFiltering.FilterOperator.Contains, PrimitivesFiltering.FilterFieldType.Text, key => key);

        Assert.Equal("contains", fallback);
    }

    [Fact]
    public void 算子形态判定三件套()
    {
        Assert.True(FilterOperatorHelper.IsValuelessOperator(PrimitivesFiltering.FilterOperator.IsEmpty));
        Assert.True(FilterOperatorHelper.IsValuelessOperator(PrimitivesFiltering.FilterOperator.IsNotEmpty));
        Assert.True(FilterOperatorHelper.IsValuelessOperator(PrimitivesFiltering.FilterOperator.IsTrue));
        Assert.True(FilterOperatorHelper.IsValuelessOperator(PrimitivesFiltering.FilterOperator.IsFalse));
        Assert.False(FilterOperatorHelper.IsValuelessOperator(PrimitivesFiltering.FilterOperator.Equals));

        Assert.True(FilterOperatorHelper.IsRangeOperator(PrimitivesFiltering.FilterOperator.Between));
        Assert.False(FilterOperatorHelper.IsRangeOperator(PrimitivesFiltering.FilterOperator.GreaterThan));

        Assert.True(FilterOperatorHelper.IsDatePresetOperator(PrimitivesFiltering.FilterOperator.DateIs));
        Assert.True(FilterOperatorHelper.IsDatePresetOperator(PrimitivesFiltering.FilterOperator.DateIsNot));
        Assert.False(FilterOperatorHelper.IsDatePresetOperator(PrimitivesFiltering.FilterOperator.InLast));
    }

    [Fact]
    public void 算子选项按类型产出标签配对()
    {
        var options = FilterOperatorHelper.GetOperatorOptions(PrimitivesFiltering.FilterFieldType.Boolean).ToList();

        Assert.Equal(2, options.Count);
        Assert.Equal(PrimitivesFiltering.FilterOperator.IsTrue, options[0].Value);
        Assert.Equal("is true", options[0].Text);
    }

    // ── MatchesValue 算子语义 ─────────────────────────────────────────

    [Theory]
    [InlineData("Active", true)]
    [InlineData("active", false)]
    [InlineData("Pending", false)]
    public void Equals算子区分大小写后回落字符串比较(string candidate, bool expected) =>
        Assert.Equal(expected, Cond(PrimitivesFiltering.FilterOperator.Equals, "Active").MatchesValue(candidate));

    [Fact]
    public void 字符串三算子一律大小写不敏感()
    {
        Assert.True(Cond(PrimitivesFiltering.FilterOperator.Contains, "ANN").MatchesValue("Joanna"));
        Assert.True(Cond(PrimitivesFiltering.FilterOperator.NotContains, "ann").MatchesValue("Bob"));
        Assert.True(Cond(PrimitivesFiltering.FilterOperator.StartsWith, "jo").MatchesValue("Joanna"));
        Assert.True(Cond(PrimitivesFiltering.FilterOperator.EndsWith, "NNA").MatchesValue("Joanna"));
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("   ", true)]
    [InlineData("x", false)]
    public void IsEmpty算子把空白串也视为空(string? candidate, bool expected)
    {
        Assert.Equal(expected, Cond(PrimitivesFiltering.FilterOperator.IsEmpty).MatchesValue(candidate));
        Assert.Equal(!expected, Cond(PrimitivesFiltering.FilterOperator.IsNotEmpty).MatchesValue(candidate));
    }

    [Fact]
    public void 布尔算子把null归入IsFalse()
    {
        Assert.True(Cond(PrimitivesFiltering.FilterOperator.IsTrue).MatchesValue(true));
        Assert.False(Cond(PrimitivesFiltering.FilterOperator.IsTrue).MatchesValue(false));
        Assert.True(Cond(PrimitivesFiltering.FilterOperator.IsFalse).MatchesValue(false));
        Assert.True(Cond(PrimitivesFiltering.FilterOperator.IsFalse).MatchesValue(null));
    }

    [Theory]
    [InlineData(PrimitivesFiltering.FilterOperator.GreaterThan, 8, false)]
    [InlineData(PrimitivesFiltering.FilterOperator.GreaterThan, 12, true)]
    [InlineData(PrimitivesFiltering.FilterOperator.LessThan, 8, true)]
    [InlineData(PrimitivesFiltering.FilterOperator.GreaterOrEqual, 10, true)]
    [InlineData(PrimitivesFiltering.FilterOperator.LessOrEqual, 10, true)]
    [InlineData(PrimitivesFiltering.FilterOperator.NotEquals, 10, false)]
    public void 数值比较算子按数值语义而非字符串语义(PrimitivesFiltering.FilterOperator op, int candidate, bool expected) =>
        Assert.Equal(expected, Cond(op, 10).MatchesValue(candidate, PrimitivesFiltering.FilterFieldType.Number));

    [Fact]
    public void Between算子含端点且缺ValueEnd时判为不完整匹配全部()
    {
        Assert.True(Cond(PrimitivesFiltering.FilterOperator.Between, 5, 10).MatchesValue(5, PrimitivesFiltering.FilterFieldType.Number));
        Assert.True(Cond(PrimitivesFiltering.FilterOperator.Between, 5, 10).MatchesValue(10, PrimitivesFiltering.FilterFieldType.Number));
        Assert.False(Cond(PrimitivesFiltering.FilterOperator.Between, 5, 10).MatchesValue(11, PrimitivesFiltering.FilterFieldType.Number));

        // 怪癖：Between 缺任一端点即"不完整条件"，匹配全部（与 ToExpression 行为对齐）
        Assert.True(Cond(PrimitivesFiltering.FilterOperator.Between, 5, null).MatchesValue(999, PrimitivesFiltering.FilterFieldType.Number));
    }

    [Fact]
    public void In与NotIn算子以字符串序列为准空序列匹配全部()
    {
        Assert.True(Cond(PrimitivesFiltering.FilterOperator.In, abValues).MatchesValue("b"));
        Assert.False(Cond(PrimitivesFiltering.FilterOperator.NotIn, abValues).MatchesValue("b"));

        // 空集合：In 匹配全部、NotIn 全部排除（注释明确对齐 ToExpression）
        Assert.True(Cond(PrimitivesFiltering.FilterOperator.In, Array.Empty<string>()).MatchesValue("zzz"));
        Assert.False(Cond(PrimitivesFiltering.FilterOperator.NotIn, Array.Empty<string>()).MatchesValue("zzz"));
    }

    [Fact]
    public void 值缺失的条件判为不完整并匹配全部()
    {
        // 怪癖：用户尚未填值的条件不参与过滤，避免编辑中整表清空
        Assert.True(Cond(PrimitivesFiltering.FilterOperator.Equals, null).MatchesValue("anything"));
        Assert.True(Cond(PrimitivesFiltering.FilterOperator.Contains, null).MatchesValue("anything"));

        // 无值算子与日期预设算子永不判为不完整
        Assert.False(Cond(PrimitivesFiltering.FilterOperator.IsEmpty).MatchesValue("x"));
        Assert.False(Cond(PrimitivesFiltering.FilterOperator.DateIs, DatePreset.Today).MatchesValue(DateTime.Today.AddYears(-5)));
    }

    [Fact]
    public void 日期字段的Equals按整日区间判定()
    {
        var day = new DateTime(2026, 3, 10);

        Assert.True(Cond(PrimitivesFiltering.FilterOperator.Equals, day).MatchesValue(day.AddHours(0), PrimitivesFiltering.FilterFieldType.Date));
        Assert.True(Cond(PrimitivesFiltering.FilterOperator.Equals, day).MatchesValue(day.AddHours(23), PrimitivesFiltering.FilterFieldType.Date));
        Assert.False(Cond(PrimitivesFiltering.FilterOperator.Equals, day).MatchesValue(day.AddDays(1), PrimitivesFiltering.FilterFieldType.Date));
    }

    [Fact]
    public void 日期字段的GreaterThan退化为次日零点之后()
    {
        var day = new DateTime(2026, 3, 10);

        // 怪癖：日期选择器不含时间分量，故 > 实为 >= 次日零点（同日 23:00 不算大于）
        Assert.False(Cond(PrimitivesFiltering.FilterOperator.GreaterThan, day).MatchesValue(day.AddHours(23), PrimitivesFiltering.FilterFieldType.Date));
        Assert.True(Cond(PrimitivesFiltering.FilterOperator.GreaterThan, day).MatchesValue(day.AddDays(1), PrimitivesFiltering.FilterFieldType.Date));
    }

    [Fact]
    public void 非日期值无法解析为DateTime时日期算子返回假()
    {
        Assert.False(Cond(PrimitivesFiltering.FilterOperator.DateIs, DatePreset.Today).MatchesValue("not-a-date"));
        Assert.False(Cond(PrimitivesFiltering.FilterOperator.InLast, 3, InLastPeriod.Days).MatchesValue("not-a-date"));
    }

    // ── 日期预设与相对区间 ────────────────────────────────────────────

    [Fact]
    public void Today预设命中当日并可由DateIsNot取反()
    {
        var today = DateTime.Today.AddHours(9);

        Assert.True(Cond(PrimitivesFiltering.FilterOperator.DateIs, DatePreset.Today).MatchesValue(today));
        Assert.False(Cond(PrimitivesFiltering.FilterOperator.DateIs, DatePreset.Today).MatchesValue(today.AddDays(-1)));
        Assert.True(Cond(PrimitivesFiltering.FilterOperator.DateIsNot, DatePreset.Today).MatchesValue(today.AddDays(-1)));
    }

    [Fact]
    public void Yesterday与Tomorrow预设按整日窗口划分()
    {
        Assert.True(Cond(PrimitivesFiltering.FilterOperator.DateIs, DatePreset.Yesterday)
            .MatchesValue(DateTime.Today.AddDays(-1).AddHours(12)));
        Assert.False(Cond(PrimitivesFiltering.FilterOperator.DateIs, DatePreset.Yesterday)
            .MatchesValue(DateTime.Today.AddHours(12)));
        Assert.True(Cond(PrimitivesFiltering.FilterOperator.DateIs, DatePreset.Tomorrow)
            .MatchesValue(DateTime.Today.AddDays(1).AddHours(12)));
    }

    [Fact]
    public void 年月预设按自然年月边界划分()
    {
        var thisYear = new DateTime(DateTime.Today.Year, 6, 15);

        Assert.True(Cond(PrimitivesFiltering.FilterOperator.DateIs, DatePreset.ThisYear).MatchesValue(thisYear));
        Assert.False(Cond(PrimitivesFiltering.FilterOperator.DateIs, DatePreset.ThisYear).MatchesValue(thisYear.AddYears(-1)));
        Assert.True(Cond(PrimitivesFiltering.FilterOperator.DateIs, DatePreset.LastYear).MatchesValue(thisYear.AddYears(-1)));
    }

    [Fact]
    public void 未知预设值回落Today窗口()
    {
        Assert.True(Cond(PrimitivesFiltering.FilterOperator.DateIs, "NotARealPreset").MatchesValue(DateTime.Today.AddHours(3)));
        Assert.False(Cond(PrimitivesFiltering.FilterOperator.DateIs, "NotARealPreset").MatchesValue(DateTime.Today.AddDays(-3)));
    }

    [Fact]
    public void InLast按相对区间向前取()
    {
        Assert.True(Cond(PrimitivesFiltering.FilterOperator.InLast, 3, InLastPeriod.Days)
            .MatchesValue(DateTime.Now.AddDays(-2)));
        Assert.False(Cond(PrimitivesFiltering.FilterOperator.InLast, 3, InLastPeriod.Days)
            .MatchesValue(DateTime.Now.AddDays(-5)));
    }

    [Fact]
    public void InNext按相对区间向后取且要求不早于当前时刻()
    {
        Assert.True(Cond(PrimitivesFiltering.FilterOperator.InNext, 3, InLastPeriod.Days)
            .MatchesValue(DateTime.Now.AddDays(2)));
        Assert.False(Cond(PrimitivesFiltering.FilterOperator.InNext, 3, InLastPeriod.Days)
            .MatchesValue(DateTime.Now.AddDays(-1)));
    }

    [Fact]
    public void 相对区间周期可由字符串或序号给出()
    {
        Assert.True(Cond(PrimitivesFiltering.FilterOperator.InLast, 3, "Days").MatchesValue(DateTime.Now.AddDays(-1)));
        Assert.True(Cond(PrimitivesFiltering.FilterOperator.InLast, 3, 0).MatchesValue(DateTime.Now.AddDays(-1)));
    }

    // ── ToFunc ───────────────────────────────────────────────────────

    [Fact]
    public void 空过滤器的谓词恒真()
    {
        var predicate = new FilterDefinition().ToFunc<Row>(fields);

        Assert.True(predicate(new Row()));
    }

    [Fact]
    public void And组合要求全部条件命中Or组合任一命中即可()
    {
        var rows = new[]
        {
            new Row { Name = "Joanna", Age = 31 },
            new Row { Name = "Joanna", Age = 22 },
            new Row { Name = "Bob", Age = 31 }
        };

        var and = new FilterDefinition
        {
            Conditions =
            [
                Cond(PrimitivesFiltering.FilterOperator.StartsWith, "jo"),
                Cond(PrimitivesFiltering.FilterOperator.GreaterThan, 30, field: "Age")
            ]
        }.ToFunc<Row>(fields);

        var or = new FilterDefinition
        {
            Operator = LogicalOperator.Or,
            Conditions =
            [
                Cond(PrimitivesFiltering.FilterOperator.StartsWith, "jo"),
                Cond(PrimitivesFiltering.FilterOperator.LessThan, 25, field: "Age")
            ]
        }.ToFunc<Row>(fields);

        Assert.Equal(andMatches, rows.Select(and));
        Assert.Equal(orMatches, rows.Select(or));
    }

    [Fact]
    public void 谓词按属性名大小写不敏感解析()
    {
        var predicate = new FilterDefinition
        {
            Conditions = [Cond(PrimitivesFiltering.FilterOperator.Equals, "Joanna", field: "nAmE")]
        }.ToFunc<Row>(fields);

        Assert.True(predicate(new Row { Name = "Joanna" }));
        Assert.False(predicate(new Row { Name = "Bob" }));
    }

    [Fact]
    public void 列名不在字段元数据表内时整条条件被跳过而非判假()
    {
        // 怪癖（EvaluateGroup L203-206）：提供了 fieldTypeMap 时，未登记的列名直接
        // continue 跳过；条件全部被跳过则 results 为空、整组返回 true——即"未知列不过滤"。
        var predicate = new FilterDefinition
        {
            Conditions = [Cond(PrimitivesFiltering.FilterOperator.Equals, "x", field: "Missing")]
        }.ToFunc<Row>(fields);

        Assert.True(predicate(new Row { Name = "Joanna" }));
    }

    [Fact]
    public void 不给字段元数据时未知列名走反射取属性失败判假()
    {
        // 无 fieldTypeMap（传空字段集）时不再跳过，改走 GetProperty 反射；属性缺失即判假。
        var predicate = new FilterDefinition
        {
            Conditions = [Cond(PrimitivesFiltering.FilterOperator.Equals, "x", field: "Missing")]
        }.ToFunc<Row>([]);

        Assert.False(predicate(new Row { Name = "Joanna" }));
    }

    // ── ToExpression ─────────────────────────────────────────────────

    [Fact]
    public void 空过滤器的表达式编译后恒真()
    {
        var lambda = new FilterDefinition().ToExpression<Row>(fields);
        var compiled = lambda.Compile();

        Assert.True(compiled(new Row()));
        Assert.Equal("x => True", lambda.ToString());
    }

    [Fact]
    public void 表达式与谓词对同一数据集给出一致结果()
    {
        var rows = new[]
        {
            new Row { Name = "Joanna", Age = 31, Active = true },
            new Row { Name = "Bob", Age = 22, Active = false }
        };

        var filter = new FilterDefinition
        {
            Conditions = [Cond(PrimitivesFiltering.FilterOperator.GreaterOrEqual, 30, field: "Age")]
        };

        var byFunc = rows.Select(filter.ToFunc<Row>(fields)).ToList();
        var byExpr = rows.Select(filter.ToExpression<Row>(fields).Compile()).ToList();

        Assert.Equal(ageMatches, byFunc);
        Assert.Equal(byFunc, byExpr);
    }

    [Fact]
    public void 选择器表达式按投影值过滤并剥离装箱转换节点()
    {
        var condition = Cond(PrimitivesFiltering.FilterOperator.GreaterThan, 30, field: "Age");
        Expression<Func<Row, object>> selector = r => r.Age;

        var lambda = condition.ToExpressionForSelector<Row>(selector, PrimitivesFiltering.FilterFieldType.Number);
        var compiled = lambda.Compile();

        Assert.True(compiled(new Row { Age = 31 }));
        Assert.False(compiled(new Row { Age = 22 }));
    }

    [Fact]
    public void 选择器参数类型不匹配时抛ArgumentException()
    {
        var condition = Cond(PrimitivesFiltering.FilterOperator.Equals, "x");
        Expression<Func<string, object>> selector = s => s;

        var ex = Assert.Throws<ArgumentException>(
            () => condition.ToExpressionForSelector<Row>(selector));

        Assert.Equal("selector", ex.ParamName);
    }

    [Fact]
    public void 选择器为null时抛ArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(
            () => Cond(PrimitivesFiltering.FilterOperator.Equals, "x").ToExpressionForSelector<Row>(null!));
    }

    // ── JSON 往返 ────────────────────────────────────────────────────

    [Fact]
    public void 过滤器JSON往返保留算子字段与条件()
    {
        var filter = new FilterDefinition
        {
            Operator = LogicalOperator.Or,
            Conditions = [Cond(PrimitivesFiltering.FilterOperator.Equals, "Active"), Cond(PrimitivesFiltering.FilterOperator.GreaterThan, 10, field: "Age")],
            Groups = [new FilterDefinition { Conditions = [Cond(PrimitivesFiltering.FilterOperator.IsEmpty)] }]
        };

        var round = FilterDefinitionExtensions.FromJson(filter.ToJson());

        Assert.Equal(LogicalOperator.Or, round.Operator);
        Assert.Equal(2, round.Conditions.Count);
        Assert.Equal("Active", round.Conditions[0].Value);
        Assert.Equal(PrimitivesFiltering.FilterOperator.GreaterThan, round.Conditions[1].Operator);
        Assert.Single(round.Groups);
        Assert.Equal(PrimitivesFiltering.FilterOperator.IsEmpty, round.Groups[0].Conditions[0].Operator);
    }

    [Fact]
    public void Version属性被JsonIgnore故往返后归零()
    {
        var filter = new FilterDefinition { Version = 42 };
        var round = FilterDefinitionExtensions.FromJson(filter.ToJson());

        Assert.Equal(0, round.Version);
    }

    [Fact]
    public void 多态值经自定义转换器往返为字符串数组与日期时间()
    {
        var filter = new FilterDefinition
        {
            Conditions =
            [
                Cond(PrimitivesFiltering.FilterOperator.In, abValues),
                Cond(PrimitivesFiltering.FilterOperator.Equals, new DateTime(2026, 3, 10, 0, 0, 0, DateTimeKind.Utc), field: "Created")
            ]
        };

        var round = FilterDefinitionExtensions.FromJson(filter.ToJson());

        Assert.Equal(abValues, Assert.IsType<string[]>(round.Conditions[0].Value));
        Assert.Equal(new DateTime(2026, 3, 10, 0, 0, 0, DateTimeKind.Utc),
            Assert.IsType<DateTime>(round.Conditions[1].Value));
    }

    [Fact]
    public void 相对区间与日期预设经JSON往返为字符串仍可被求值()
    {
        var filter = new FilterDefinition
        {
            Conditions =
            [
                Cond(PrimitivesFiltering.FilterOperator.InLast, 3, InLastPeriod.Days),
                Cond(PrimitivesFiltering.FilterOperator.DateIs, DatePreset.Today)
            ]
        };

        var round = FilterDefinitionExtensions.FromJson(filter.ToJson());

        Assert.Equal("Days", round.Conditions[0].ValueEnd);
        Assert.Equal("Today", round.Conditions[1].Value);

        // 字符串形态仍被 EvaluateInLast / EvaluateDatePreset 解析
        Assert.True(round.Conditions[0].MatchesValue(DateTime.Now.AddDays(-1)));
        Assert.True(round.Conditions[1].MatchesValue(DateTime.Today.AddHours(9)));
    }

    [Fact]
    public void 空JSON串反序列化回空过滤器()
    {
        var round = FilterDefinitionExtensions.FromJson("{}");

        Assert.True(round.IsEmpty);
    }
}
