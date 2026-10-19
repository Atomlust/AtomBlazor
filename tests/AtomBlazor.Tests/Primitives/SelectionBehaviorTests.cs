using AtomBlazor.Primitives;
using Xunit;

namespace AtomBlazor.Tests.Primitives;

/// <summary>
/// 自 DataGrid/SelectionBehaviorTests.cs。覆盖
/// <see cref="DataGridSelectionBehavior.Replace"/>：用户从文件管理器带来的单击、Shift+单击与
/// Ctrl+单击惯例，以及 Shift+单击所延伸的锚点行。断言本体逐行照搬。
/// </summary>
public class SelectionBehaviorTests
{
    private sealed class Row
    {
        public required string Name { get; init; }

        public override string ToString() => Name;
    }

    private static readonly int[] oneThenThree = { 1, 3 };

    private static List<Row> CreateRows() => new()
    {
        new Row { Name = "a" },
        new Row { Name = "b" },
        new Row { Name = "c" },
        new Row { Name = "d" },
        new Row { Name = "e" }
    };

    private static DataGridContext<Row> CreateContext(
        List<Row> rows,
        DataGridSelectionBehavior behavior,
        SelectionMode mode = SelectionMode.Multiple)
    {
        var state = new DataGridState<Row>();
        state.Selection.Mode = mode;

        return new DataGridContext<Row>(state)
        {
            ProcessedData = rows,
            SelectionMode = mode,
            SelectionBehavior = behavior
        };
    }

    private static string SelectedNames(DataGridContext<Row> context) =>
        string.Concat(context.State.Selection.SelectedItems
            .Select(r => r.Name)
            .OrderBy(n => n, StringComparer.Ordinal));

    // --- Toggle 保持既有行为 -------------------------------------------------------------

    [Fact]
    public void ToggleBehaviourFlipsOneRowAndLeavesTheRestAlone()
    {
        var rows = CreateRows();
        var context = CreateContext(rows, DataGridSelectionBehavior.Toggle);

        context.ApplyRowSelectionInput(rows[0], RowSelectionModifiers.None);
        context.ApplyRowSelectionInput(rows[2], RowSelectionModifiers.None);

        Assert.Equal("ac", SelectedNames(context));

        context.ApplyRowSelectionInput(rows[0], RowSelectionModifiers.None);

        Assert.Equal("c", SelectedNames(context));
    }

    [Fact]
    public void ToggleBehaviourIgnoresModifierKeys()
    {
        var rows = CreateRows();
        var context = CreateContext(rows, DataGridSelectionBehavior.Toggle);

        context.ApplyRowSelectionInput(rows[0], RowSelectionModifiers.None);
        context.ApplyRowSelectionInput(rows[3], new RowSelectionModifiers(Extend: true, Additive: false));

        // 范围选择本会选中 a、b、c、d；Toggle 只追加 d。
        Assert.Equal("ad", SelectedNames(context));
    }

    // --- Replace：普通单击 --------------------------------------------------------------

    [Fact]
    public void PlainClickReplacesTheWholeSelection()
    {
        var rows = CreateRows();
        var context = CreateContext(rows, DataGridSelectionBehavior.Replace);

        context.ApplyRowSelectionInput(rows[0], RowSelectionModifiers.None);
        context.ApplyRowSelectionInput(rows[3], new RowSelectionModifiers(Extend: true, Additive: false));
        Assert.Equal("abcd", SelectedNames(context));

        context.ApplyRowSelectionInput(rows[2], RowSelectionModifiers.None);

        Assert.Equal("c", SelectedNames(context));
    }

    [Fact]
    public void PlainClickOnTheOnlySelectedRowClearsTheSelection()
    {
        var rows = CreateRows();
        var context = CreateContext(rows, DataGridSelectionBehavior.Replace);

        context.ApplyRowSelectionInput(rows[1], RowSelectionModifiers.None);
        Assert.Equal("b", SelectedNames(context));

        context.ApplyRowSelectionInput(rows[1], RowSelectionModifiers.None);

        Assert.Equal("", SelectedNames(context));
    }

    [Fact]
    public void PlainClickOnASelectedRowInAWiderSelectionCollapsesToThatRow()
    {
        var rows = CreateRows();
        var context = CreateContext(rows, DataGridSelectionBehavior.Replace);

        context.ApplyRowSelectionInput(rows[0], RowSelectionModifiers.None);
        context.ApplyRowSelectionInput(rows[2], new RowSelectionModifiers(Extend: true, Additive: false));
        Assert.Equal("abc", SelectedNames(context));

        // b 已选中，但并非唯一选中项，所以此处收敛为该单行
        // 而非清空。
        context.ApplyRowSelectionInput(rows[1], RowSelectionModifiers.None);

        Assert.Equal("b", SelectedNames(context));
    }

    // --- Replace：Shift+单击 ----------------------------------------------------------

    [Fact]
    public void ShiftClickSelectsTheRangeFromTheAnchor()
    {
        var rows = CreateRows();
        var context = CreateContext(rows, DataGridSelectionBehavior.Replace);

        context.ApplyRowSelectionInput(rows[1], RowSelectionModifiers.None);
        context.ApplyRowSelectionInput(rows[3], new RowSelectionModifiers(Extend: true, Additive: false));

        Assert.Equal("bcd", SelectedNames(context));
    }

    [Fact]
    public void ShiftClickSelectsTheRangeWhenClickingUpwards()
    {
        var rows = CreateRows();
        var context = CreateContext(rows, DataGridSelectionBehavior.Replace);

        context.ApplyRowSelectionInput(rows[3], RowSelectionModifiers.None);
        context.ApplyRowSelectionInput(rows[1], new RowSelectionModifiers(Extend: true, Additive: false));

        Assert.Equal("bcd", SelectedNames(context));
    }

    [Fact]
    public void RepeatedShiftClicksReExtendFromTheSameAnchor()
    {
        var rows = CreateRows();
        var context = CreateContext(rows, DataGridSelectionBehavior.Replace);

        context.ApplyRowSelectionInput(rows[0], RowSelectionModifiers.None);
        context.ApplyRowSelectionInput(rows[3], new RowSelectionModifiers(Extend: true, Additive: false));
        Assert.Equal("abcd", SelectedNames(context));

        // 锚点留在 a，于是可以收窄范围而非沿列表前移锚点。
        context.ApplyRowSelectionInput(rows[1], new RowSelectionModifiers(Extend: true, Additive: false));

        Assert.Equal("ab", SelectedNames(context));
    }

    [Fact]
    public void ShiftClickWithNoAnchorSelectsOnlyTheClickedRow()
    {
        var rows = CreateRows();
        var context = CreateContext(rows, DataGridSelectionBehavior.Replace);

        context.ApplyRowSelectionInput(rows[2], new RowSelectionModifiers(Extend: true, Additive: false));

        Assert.Equal("c", SelectedNames(context));
    }

    [Fact]
    public void ShiftClickAfterAStaleAnchorSelectsOnlyTheClickedRow()
    {
        var rows = CreateRows();
        var context = CreateContext(rows, DataGridSelectionBehavior.Replace);

        context.ApplyRowSelectionInput(rows[0], RowSelectionModifiers.None);

        // 选择之下页面变了：锚点已不在可见行之中。
        context.ProcessedData = rows.Skip(2).ToList();

        context.ApplyRowSelectionInput(rows[3], new RowSelectionModifiers(Extend: true, Additive: false));

        Assert.Equal("d", SelectedNames(context));
    }

    // --- Replace：Ctrl+单击 -----------------------------------------------------------

    [Fact]
    public void CtrlClickAddsARowWithoutDisturbingTheRest()
    {
        var rows = CreateRows();
        var context = CreateContext(rows, DataGridSelectionBehavior.Replace);

        context.ApplyRowSelectionInput(rows[0], RowSelectionModifiers.None);
        context.ApplyRowSelectionInput(rows[3], new RowSelectionModifiers(Extend: false, Additive: true));

        Assert.Equal("ad", SelectedNames(context));
    }

    [Fact]
    public void CtrlClickRemovesAnAlreadySelectedRow()
    {
        var rows = CreateRows();
        var context = CreateContext(rows, DataGridSelectionBehavior.Replace);

        context.ApplyRowSelectionInput(rows[0], RowSelectionModifiers.None);
        context.ApplyRowSelectionInput(rows[2], new RowSelectionModifiers(Extend: true, Additive: false));
        Assert.Equal("abc", SelectedNames(context));

        context.ApplyRowSelectionInput(rows[1], new RowSelectionModifiers(Extend: false, Additive: true));

        Assert.Equal("ac", SelectedNames(context));
    }

    [Fact]
    public void CtrlClickMovesTheAnchorSoAFollowingShiftClickExtendsFromIt()
    {
        var rows = CreateRows();
        var context = CreateContext(rows, DataGridSelectionBehavior.Replace);

        context.ApplyRowSelectionInput(rows[0], RowSelectionModifiers.None);
        context.ApplyRowSelectionInput(rows[2], new RowSelectionModifiers(Extend: false, Additive: true));
        Assert.Equal("ac", SelectedNames(context));

        // 锚点是 c 而非 a。延伸到 e 必须得 c、d、e——而不是 a 到 e。
        context.ApplyRowSelectionInput(rows[4], new RowSelectionModifiers(Extend: true, Additive: false));

        Assert.Equal("cde", SelectedNames(context));
    }

    // --- 选择模式 ----------------------------------------------------------------------

    [Fact]
    public void SingleModeTreatsEveryClickAsAPlainClick()
    {
        var rows = CreateRows();
        var context = CreateContext(rows, DataGridSelectionBehavior.Replace, SelectionMode.Single);

        context.ApplyRowSelectionInput(rows[0], RowSelectionModifiers.None);
        context.ApplyRowSelectionInput(rows[3], new RowSelectionModifiers(Extend: true, Additive: true));

        Assert.Equal("d", SelectedNames(context));
    }

    [Fact]
    public void NoneModeSelectsNothing()
    {
        var rows = CreateRows();
        var context = CreateContext(rows, DataGridSelectionBehavior.Replace, SelectionMode.None);

        context.ApplyRowSelectionInput(rows[0], RowSelectionModifiers.None);

        Assert.Equal("", SelectedNames(context));
    }

    // --- 复选框仍走 Toggle --------------------------------------------------------------

    [Fact]
    public void ToggleRowSelectionStaysToggleUnderReplaceSoCheckboxesKeepWorking()
    {
        var rows = CreateRows();
        var context = CreateContext(rows, DataGridSelectionBehavior.Replace);

        context.ToggleRowSelection(rows[0]);
        context.ToggleRowSelection(rows[2]);

        Assert.Equal("ac", SelectedNames(context));
    }

    // --- 回调 ------------------------------------------------------------------------

    [Fact]
    public void SelectionChangeIsReportedOncePerClick()
    {
        var rows = CreateRows();
        var context = CreateContext(rows, DataGridSelectionBehavior.Replace);
        var reported = new List<int>();
        context.OnSelectionChange = items => reported.Add(items.Count);

        context.ApplyRowSelectionInput(rows[0], RowSelectionModifiers.None);
        context.ApplyRowSelectionInput(rows[2], new RowSelectionModifiers(Extend: true, Additive: false));

        Assert.Equal(oneThenThree, reported);
    }
}
