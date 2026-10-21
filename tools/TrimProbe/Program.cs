using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Text.Json;
using AtomBlazor;
using AtomBlazor.Primitives;
using AtomBlazor.Primitives.Filtering;
using Ganss.Xss;
using Ical.Net;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;
using Markdig;
using Microsoft.AspNetCore.Components;
using TailwindMerge;
using FilterCondition = AtomBlazor.Primitives.Filtering.FilterCondition;
using FilterDefinition = AtomBlazor.Primitives.Filtering.FilterDefinition;
using FilterOperator = AtomBlazor.Primitives.Filtering.FilterOperator;
using IcalCalendar = Ical.Net.Calendar;

// 裁剪/AOT 冒烟入口：让链接器与 NativeAOT 运行时真实走过各库的 AOT 敏感路径。
// 任何一步抛出异常都会让进程以非零码退出，CI 以此作为门禁。

// ── 第三方依赖（与两库声明的版本保持一致，经 ProjectReference 传递） ──────────
var markdown = Markdown.ToHtml("# 标题\n\n**加粗** 与 `代码`");
Console.WriteLine($"Markdig OK: {markdown.Length} chars");

var sanitizer = new HtmlSanitizer();
var clean = sanitizer.Sanitize("<p onclick=\"evil()\">内容</p>");
Console.WriteLine($"HtmlSanitizer OK: {clean}");

var twMerge = new TwMerge();
Console.WriteLine($"TailwindMerge.NET OK: {twMerge.Merge("p-2 p-4")}");

var calendar = new IcalCalendar();
var evt = new CalendarEvent
{
    Summary = "团队会议",
    Start = new CalDateTime(new DateTime(2026, 10, 12, 9, 0, 0, DateTimeKind.Utc)),
    End = new CalDateTime(new DateTime(2026, 10, 12, 10, 0, 0, DateTimeKind.Utc))
};
// 冒烟工程有意使用被标注过时的多规则 API，以覆盖日历循环规则的裁剪路径
#pragma warning disable CS0618
evt.RecurrenceRules.Add(new RecurrencePattern(FrequencyType.Daily, 7) { Count = 3 });
calendar.Events.Add(evt);
#pragma warning restore CS0618
var occurrences = evt.GetOccurrences(evt.Start, new Ical.Net.Evaluation.EvaluationOptions { MaxUnmatchedIncrementsLimit = 1000 });
Console.WriteLine($"Ical.Net OK: {occurrences.Count()} occurrences");

// ── 表单 Schema 源生成 JSON（原生格式 + JSON Schema 子集） ───────────────────
const string nativeFormJson = """
{
  "title": "注册",
  "columns": 2,
  "fields": [
    { "name": "email", "label": "邮箱", "type": "email", "required": true },
    { "name": "age", "label": "年龄", "type": "number" }
  ]
}
""";
var nativeSchema = FormSchemaParser.FromJson(nativeFormJson);
if (nativeSchema.Fields.Count != 2 ||
    nativeSchema.Fields[0].Type != FieldType.Email ||
    !nativeSchema.Fields[0].Required)
{
    throw new InvalidOperationException("原生 FormSchema 解析结果不符合预期。");
}

const string jsonSchemaJson = """
{
  "type": "object",
  "title": "联系信息",
  "properties": {
    "email": { "type": "string", "format": "email", "title": "邮箱" },
    "age": { "type": "integer", "title": "年龄" }
  },
  "required": ["email"]
}
""";
var convertedSchema = FormSchemaParser.FromJson(jsonSchemaJson);
if (convertedSchema.Fields.Count != 2 ||
    convertedSchema.Fields.All(f => f.Name != "email") ||
    !convertedSchema.Fields.First(f => f.Name == "email").Required)
{
    throw new InvalidOperationException("JSON Schema 解析结果不符合预期。");
}

Console.WriteLine($"FormSchemaParser OK: {nativeSchema.Fields.Count + convertedSchema.Fields.Count} fields");

// ── 筛选定义：源生成 JSON 往返 + 反射求值 + 表达式编译 ────────────────────────
var filter = new FilterDefinition();
filter.Conditions.Add(new FilterCondition
{
    Field = "Name",
    Operator = FilterOperator.Contains,
    Value = "lic"
});
filter.Conditions.Add(new FilterCondition
{
    Field = "Age",
    Operator = FilterOperator.GreaterThan,
    Value = 30
});

var filterJson = filter.ToJson();
var restoredFilter = FilterDefinitionExtensions.FromJson(filterJson);
var filterFields = new FilterField[]
{
    new() { Name = "Name", Label = "姓名", Type = FilterFieldType.Text },
    new() { Name = "Age", Label = "年龄", Type = FilterFieldType.Number }
};

var filterPredicate = restoredFilter.ToFunc<ProbeRow>(filterFields);
if (SampleRows().Count(filterPredicate) != 1)
{
    throw new InvalidOperationException("FilterDefinition.ToFunc 结果不符合预期。");
}

var filterExpression = restoredFilter.ToExpression<ProbeRow>(filterFields).Compile();
if (SampleRows().Count(filterExpression) != 1)
{
    throw new InvalidOperationException("FilterDefinition.ToExpression 结果不符合预期。");
}

Console.WriteLine($"FilterDefinition OK: {filterJson.Length} chars, {restoredFilter.TotalConditionCount} conditions");

// ── DataGrid 多列排序：强类型 IQueryable 路径 + EnumerableQuery 回退路径 ───────
var (sortedRows, fallbackRows) = RunQueryableSorting();
Console.WriteLine($"DataGrid.ApplyMultiSort OK: {sortedRows.Count} typed, {fallbackRows.Count} fallback");

// ── DataExtractor：字典 / JsonElement 无反射路径 + 保留成员的 POCO 路径 ────────
var dictionaryData = new object[]
{
    new Dictionary<string, object?> { ["name"] = "A", ["value"] = 10 },
    new Dictionary<string, object?> { ["name"] = "B", ["value"] = 20 },
    new Dictionary<string, object?> { ["name"] = "C", ["value"] = null }
};
var dictionaryValues = DataExtractor.ExtractValues(dictionaryData, "Value");
if (dictionaryValues.Count != 3 || (int?)dictionaryValues[1] != 20 || dictionaryValues[2] is not null)
{
    throw new InvalidOperationException("DataExtractor 字典路径结果不符合预期。");
}

using var chartDataDocument = JsonDocument.Parse(
    """[{"name":"A","value":1},{"name":"B","value":2},{"name":"C","value":3}]""");
var jsonValues = DataExtractor.ExtractValues(chartDataDocument.RootElement, "value");
if (jsonValues.Count != 3 ||
    jsonValues[2] is not JsonElement lastElement ||
    lastElement.GetInt32() != 3)
{
    throw new InvalidOperationException("DataExtractor JsonElement 路径结果不符合预期。");
}

RunPocoExtraction();
Console.WriteLine($"DataExtractor OK: {dictionaryValues.Count + jsonValues.Count + 3} values");

// ── 签名与拖拽公告：另一组源生成 JSON 上下文 ─────────────────────────────────
var strokes = new[]
{
    new SignatureStroke(new SignaturePoint[]
    {
        new(10, 12.5, 0),
        new(20, 30, 16.7)
    })
};
var strokesJson = JsonSerializer.Serialize(strokes, SignaturePadJsonContext.Default.Strokes);
if (!strokesJson.Contains("\"points\"", StringComparison.Ordinal) ||
    !strokesJson.Contains("16.7", StringComparison.Ordinal))
{
    throw new InvalidOperationException("SignaturePad JSON 序列化结果不符合预期。");
}

var announcementsJson = JsonSerializer.Serialize(
    new Dictionary<string, string> { ["moveUp"] = "上移", ["moveDown"] = "下移" },
    SortableJsonContext.Default.Announcements);
if (!announcementsJson.Contains("moveUp", StringComparison.Ordinal))
{
    throw new InvalidOperationException("Sortable JSON 序列化结果不符合预期。");
}

Console.WriteLine($"Signature/Sortable JSON OK: {strokesJson.Length + announcementsJson.Length} chars");

// ── 图表 option：源生成选项图 + object 槽位转换器（含 JsonElement/渐变/字典） ──
using var extraData = JsonDocument.Parse(
    """{"value":260,"itemStyle":{"color":"#f59e0b"}}""");

var chartOption = new EChartsOption
{
    XAxis = new EChartsAxisOption { Type = "category", Data = ["一季度", "二季度", "三季度"] },
    YAxis = new EChartsAxisOption { Type = "value" },
    Color =
    [
        new EChartsLinearGradient
        {
            X = 0,
            Y = 0,
            X2 = 0,
            Y2 = 1,
            ColorStops =
            [
                new EChartsColorStop { Offset = 0, Color = "#4f46e5" },
                new EChartsColorStop { Offset = 1, Color = "#9333ea" }
            ]
        },
        "#10b981"
    ],
    Series =
    {
        new EChartsSeriesOption
        {
            Type = "line",
            Name = "营收",
            Smooth = true,
            Data =
            [
                120,
                180.5,
                null,
                new Dictionary<string, object?>
                {
                    ["value"] = 220,
                    ["itemStyle"] = new Dictionary<string, object> { ["color"] = "#4f46e5" }
                },
                extraData.RootElement.Clone(),
                new DateTime(2026, 10, 21, 9, 30, 0, DateTimeKind.Utc),
                Guid.Parse("6f9619ff-8b86-d011-b42d-00c04fc964ff"),
                DayOfWeek.Monday
            ]
        }
    }
};

var chartJson = ChartOptionSerializer.Serialize(chartOption);
using (var parsedChart = JsonDocument.Parse(chartJson))
{
    var root = parsedChart.RootElement;
    if (!root.TryGetProperty("series", out var series) ||
        series.GetArrayLength() != 1 ||
        !series[0].TryGetProperty("data", out var seriesData) ||
        seriesData.GetArrayLength() != 8 ||
        !root.TryGetProperty("color", out var color) ||
        color.GetArrayLength() != 2)
    {
        throw new InvalidOperationException("图表 option 序列化结果不符合预期。");
    }
}

Console.WriteLine($"ChartOptionSerializer OK: {chartJson.Length} chars");

Console.WriteLine("AOT smoke OK: all paths executed.");

// ── 辅助类型与本地函数 ───────────────────────────────────────────────────────

// 探针有意触发内存集合的 Queryable 绑定（DataGrid 的实际用法），并在
// NativeAOT 运行时验证其可用性；分析器警告由下方的无条件抑制承担。
[UnconditionalSuppressMessage("Trimming", "IL2026: 内存集合绑定到 Queryable 可能引用被裁剪的扩展方法",
    Justification = "该路径就是探针的验证目标：NativeAOT 构建产物中真实执行排序与回退，成功即证明相关代码未被裁剪。")]
[UnconditionalSuppressMessage("AOT", "IL3050: 内存集合绑定到 Queryable 可能依赖动态代码",
    Justification = "该路径就是探针的验证目标：NativeAOT 构建产物中真实执行排序与回退，成功即证明无需动态代码生成。")]
static (List<ProbeRow> Typed, List<ProbeRow> Fallback) RunQueryableSorting()
{
    var sortRows = SampleRows();
    var sortColumns = new IDataGridColumn<ProbeRow>[]
    {
        CreateAgeColumn(strongTyped: true),
        CreateNameColumn(strongTyped: true)
    };
    var sortDefinitions = new SortDefinition[]
    {
        new() { ColumnId = "Age", Direction = SortDirection.Descending },
        new() { ColumnId = "Name", Direction = SortDirection.Ascending }
    };

    var typed = sortRows.AsQueryable().ApplyMultiSort(sortDefinitions, sortColumns).ToList();
    if (typed.Count != 3 ||
        typed[0].Name != "Carol" ||
        typed[1].Name != "Alice" ||
        typed[2].Name != "Bob")
    {
        throw new InvalidOperationException(
            $"强类型多列排序结果不符合预期：{string.Join(", ", typed.Select(r => r.Name))}");
    }

    // 列未提供强类型排序时，ApplyMultiSort 会走 EnumerableQuery 回退（AOT 抑制路径）。
    var fallbackColumns = new IDataGridColumn<ProbeRow>[] { CreateNameColumn(strongTyped: false) };
    var fallback = sortRows.AsQueryable()
        .ApplyMultiSort(
            new SortDefinition[] { new() { ColumnId = "Name", Direction = SortDirection.Ascending } },
            fallbackColumns)
        .ToList();
    if (fallback.Count != 3 || fallback[0].Name != "Alice")
    {
        throw new InvalidOperationException(
            $"内存排序回退结果不符合预期：{string.Join(", ", fallback.Select(r => r.Name))}");
    }

    return (typed, fallback);
}

static List<ProbeRow> SampleRows() =>
[
    new ProbeRow { Name = "Alice", Age = 34 },
    new ProbeRow { Name = "Bob", Age = 25 },
    new ProbeRow { Name = "Carol", Age = 40 }
];

// 模拟宿主保留 POCO 成员：DataExtractor 的 POCO 回退依赖运行时反射发现属性。
[DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(ProbeRow))]
static void RunPocoExtraction()
{
    var names = DataExtractor.ExtractStringValues(SampleRows(), "Name");
    if (names.Count != 3 || names[0] != "Alice" || names[2] != "Carol")
    {
        throw new InvalidOperationException("DataExtractor POCO 路径结果不符合预期。");
    }
}

static ProbeColumn CreateNameColumn(bool strongTyped)
{
    Func<IQueryable<ProbeRow>, bool, IOrderedQueryable<ProbeRow>>? orderBy = null;
    Func<IOrderedQueryable<ProbeRow>, bool, IOrderedQueryable<ProbeRow>>? thenBy = null;
    if (strongTyped)
    {
        orderBy = (query, descending) => descending
            ? query.OrderByDescending(row => row.Name)
            : query.OrderBy(row => row.Name);
        thenBy = (query, descending) => descending
            ? query.ThenByDescending(row => row.Name)
            : query.ThenBy(row => row.Name);
    }

    return new ProbeColumn(
        "Name",
        row => row.Name,
        (x, y) => string.Compare(x.Name, y.Name, StringComparison.Ordinal),
        (Expression<Func<ProbeRow, object?>>)(row => row.Name),
        orderBy,
        thenBy);
}

static ProbeColumn CreateAgeColumn(bool strongTyped)
{
    Func<IQueryable<ProbeRow>, bool, IOrderedQueryable<ProbeRow>>? orderBy = null;
    Func<IOrderedQueryable<ProbeRow>, bool, IOrderedQueryable<ProbeRow>>? thenBy = null;
    if (strongTyped)
    {
        orderBy = (query, descending) => descending
            ? query.OrderByDescending(row => row.Age)
            : query.OrderBy(row => row.Age);
        thenBy = (query, descending) => descending
            ? query.ThenByDescending(row => row.Age)
            : query.ThenBy(row => row.Age);
    }

    return new ProbeColumn(
        "Age",
        row => row.Age,
        (x, y) => x.Age.CompareTo(y.Age),
        (Expression<Func<ProbeRow, object?>>)(row => row.Age),
        orderBy,
        thenBy);
}

internal sealed class ProbeRow
{
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
}

internal sealed class ProbeColumn : IDataGridColumn<ProbeRow>
{
    private readonly Func<ProbeRow, object?> valueSelector;
    private readonly Comparison<ProbeRow> comparison;
    private readonly LambdaExpression sortExpression;
    private readonly Func<IQueryable<ProbeRow>, bool, IOrderedQueryable<ProbeRow>>? orderBy;
    private readonly Func<IOrderedQueryable<ProbeRow>, bool, IOrderedQueryable<ProbeRow>>? thenBy;

    public ProbeColumn(
        string columnId,
        Func<ProbeRow, object?> valueSelector,
        Comparison<ProbeRow> comparison,
        LambdaExpression sortExpression,
        Func<IQueryable<ProbeRow>, bool, IOrderedQueryable<ProbeRow>>? orderBy,
        Func<IOrderedQueryable<ProbeRow>, bool, IOrderedQueryable<ProbeRow>>? thenBy)
    {
        ColumnId = columnId;
        this.valueSelector = valueSelector;
        this.comparison = comparison;
        this.sortExpression = sortExpression;
        this.orderBy = orderBy;
        this.thenBy = thenBy;
    }

    public string ColumnId { get; }
    public string? Title => ColumnId;
    public bool Sortable => true;
    public bool Filterable => false;
    public bool Visible => true;
    public string? Width => null;
    public bool Hideable => true;
    public bool Resizable => true;
    public bool Reorderable => true;
    public ColumnPinning Pinned => ColumnPinning.None;

    public object? GetValue(ProbeRow item) => valueSelector(item);

    public int Compare(ProbeRow x, ProbeRow y) => comparison(x, y);

    public LambdaExpression? GetSortExpression() => sortExpression;

    public IOrderedQueryable<ProbeRow>? ApplyOrderBy(IQueryable<ProbeRow> source, bool descending) =>
        orderBy?.Invoke(source, descending);

    public IOrderedQueryable<ProbeRow>? ApplyThenBy(IOrderedQueryable<ProbeRow> source, bool descending) =>
        thenBy?.Invoke(source, descending);

    public LambdaExpression? GetFilterExpression() => null;

    public RenderFragment<DataGridCellContext<ProbeRow>>? CellTemplate => null;

    public RenderFragment<DataGridHeaderContext<ProbeRow>>? HeaderTemplate => null;

    public string? CellClass => null;

    public string? HeaderClass => null;

    public bool NoWrap => false;

    public AggregateFunction Aggregate => AggregateFunction.None;
}
