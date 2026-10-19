using AtomBlazor.Primitives;
using System.Globalization;
using AtomBlazor.Primitives.Gantt;
using Xunit;

namespace AtomBlazor.Tests.Primitives;

/// <summary>
/// 自写补全波：GanttSource 的快照语义与参数缺省。构建即取源之快照：表读一遍、
/// 选择器读一次，此后源集之变不涉已成之图；诸缺省（Day 缩放、Major 吸附、上卷
/// 开启、无进度即零、文本 null 作空串）逐项落定，参数校验各归其位。
/// </summary>
public class GanttSourceTests
{
    private sealed record Task(string Id, string? Parent, string Text, DateTimeOffset Start, DateTimeOffset End, double Progress = 0);

    private static DateTimeOffset On(int year, int month, int day) =>
        new(new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Unspecified), TimeSpan.Zero);

    private static GanttSource<Task> Source(IEnumerable<Task> tasks) => new()
    {
        Items = tasks,
        Id = t => t.Id,
        ParentId = t => t.Parent,
        Text = t => t.Text,
        Start = t => t.Start,
        End = t => t.End,
        Progress = t => t.Progress,
        Labels = new GanttLabels { Culture = CultureInfo.InvariantCulture },
    };

    [Fact]
    public void BuildRejectsANullSource() =>
        Assert.Throws<ArgumentNullException>(() => GanttBuilder.Build<Task>(null!));

    [Fact]
    public void RejectsATaskWhoseIdentifierIsBlank()
    {
        var source = Source([new Task(string.Empty, null, "Blank", On(2026, 3, 2), On(2026, 3, 3))]);

        Assert.Throws<ArgumentException>(() => GanttBuilder.Build(source));
    }

    [Fact]
    public void ATaskThatNamesItselfAsParentSitsAtTheTop()
    {
        // 自环不是环死遍历的那种环：Link 认父时便排除了自身，
        // 故该任务落到顶层而非抛出。
        var chart = GanttBuilder.Build(Source([new Task("a", "a", "A", On(2026, 3, 2), On(2026, 3, 3))]));

        Assert.Equal(0, chart.Rows[0].Depth);
        Assert.False(chart.Rows[0].IsSummary);
    }

    [Fact]
    public void ReadsTheItemExactlyOncePerTask()
    {
        var passes = 0;
        var tasks = new[]
        {
            new Task("a", null, "A", On(2026, 3, 2), On(2026, 3, 3)),
            new Task("b", null, "B", On(2026, 3, 3), On(2026, 3, 4)),
        };

        IEnumerable<Task> Observed()
        {
            passes++;
            foreach (var task in tasks)
            {
                yield return task;
            }
        }

        var chart = GanttBuilder.Build(Source(Observed()));

        Assert.Equal(2, chart.TaskCount);
        Assert.Equal(1, passes);
    }

    [Fact]
    public void TheChartIsASnapshotTakenAtBuild()
    {
        var tasks = new List<Task> { new("a", null, "A", On(2026, 3, 2), On(2026, 3, 3)) };
        var chart = GanttBuilder.Build(Source(tasks));

        tasks.Add(new Task("b", null, "B", On(2026, 3, 3), On(2026, 3, 4)));

        Assert.Single(chart.Rows);
        Assert.Equal(1, chart.TaskCount);
    }

    [Fact]
    public void OmittedSelectorsFallBackToPlainDefaults()
    {
        var source = new GanttSource<Task>
        {
            Items =
            [
                new Task("a", null, "A", On(2026, 3, 2), On(2026, 3, 3)),
                new Task("b", null, "B", On(2026, 3, 3), On(2026, 3, 4)),
            ],
            Id = t => t.Id,
            Text = t => t.Text,
            Start = t => t.Start,
            End = t => t.End,
        };

        var chart = GanttBuilder.Build(source);

        // 无父读取器则皆在顶层；无进度读取器则零；无收拢集则全敞。
        Assert.All(chart.Rows, r =>
        {
            Assert.Equal(0, r.Depth);
            Assert.Equal(0, r.Progress);
            Assert.True(r.IsExpanded);
        });

        // 缺省 Day 缩放加 Major 吸附：展开为整月。
        Assert.Equal(On(2026, 3, 1), chart.Axis.Start);
        Assert.Equal(On(2026, 4, 1), chart.Axis.End);
    }

    [Fact]
    public void NegativeProgressClampsToZero()
    {
        var chart = GanttBuilder.Build(Source([new Task("a", null, "A", On(2026, 3, 2), On(2026, 3, 3), -3.0)]));

        Assert.Equal(0, chart.Rows[0].Progress);
    }

    [Fact]
    public void ATaskWithoutTextReadsAsAnEmptyName()
    {
        var source = Source([new Task("a", null, null!, On(2026, 3, 2), On(2026, 3, 3))]);

        var chart = GanttBuilder.Build(source);

        Assert.Equal(string.Empty, chart.Rows[0].Text);
    }

    [Fact]
    public void AnExplicitRangeOutrunsTheTasksOnBothEnds()
    {
        var source = new GanttSource<Task>
        {
            Items = [new Task("a", null, "A", On(2026, 3, 10), On(2026, 3, 12))],
            Id = t => t.Id,
            Text = t => t.Text,
            Start = t => t.Start,
            End = t => t.End,
            RangeStart = On(2026, 1, 1),
            RangeEnd = On(2026, 6, 1),
        };

        var chart = GanttBuilder.Build(source);

        Assert.Equal(On(2026, 1, 1), chart.Axis.Start);
        Assert.Equal(On(2026, 6, 1), chart.Axis.End);

        // 任务仍落在其槽上：1 月 31 格加 2 月 28 格，3 月 10 日便是第 68 格起、两格之长。
        Assert.Equal(68, chart.Rows[0].OffsetSlots, 6);
        Assert.Equal(2, chart.Rows[0].LengthSlots, 6);
    }

    [Fact]
    public void CollapsingALeafHidesNothing()
    {
        // 收拢指名的是其子不在屏上者；叶无子，故名单里有它也照样满员。
        var source = new GanttSource<Task>
        {
            Items =
            [
                new Task("a", null, "A", On(2026, 3, 2), On(2026, 3, 3)),
                new Task("b", null, "B", On(2026, 3, 3), On(2026, 3, 4)),
            ],
            Id = t => t.Id,
            ParentId = t => t.Parent,
            Text = t => t.Text,
            Start = t => t.Start,
            End = t => t.End,
            Collapsed = new HashSet<string> { "b" },
        };

        // 收拢指名的是其子被藏者；叶无子可藏，落下的只是展开旗标本身。
        var chart = GanttBuilder.Build(source);

        Assert.Equal(2, chart.Rows.Count);
        Assert.True(chart.Rows[0].IsExpanded);
        Assert.False(chart.Rows[1].IsExpanded);
    }

    [Fact]
    public void ASummaryRollsUpItsWholeBranchNotJustItsChildren()
    {
        // 上卷自底向上递归：孙辈的日期经子辈汇入祖辈，无须逐层亲设。
        var chart = GanttBuilder.Build(Source(
        [
            new Task("top", null, "Top", On(2026, 3, 1), On(2026, 3, 2)),
            new Task("mid", "top", "Mid", On(2026, 3, 3), On(2026, 3, 5)),
            new Task("leaf", "mid", "Leaf", On(2026, 3, 10), On(2026, 3, 20), 1.0),
        ]));

        var top = chart.Rows[0];
        Assert.Equal(On(2026, 3, 10), top.Start);
        Assert.Equal(On(2026, 3, 20), top.End);
        Assert.Equal(1.0, top.Progress, 6);
    }
}
