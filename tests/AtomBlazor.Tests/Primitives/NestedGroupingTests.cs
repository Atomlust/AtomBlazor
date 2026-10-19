using AtomBlazor.Primitives;
using Xunit;

namespace AtomBlazor.Tests.Primitives;

/// <summary>
/// 自 DataGrid/NestedGroupingTests.cs。覆盖多级分组背后的状态：
/// 能唯一标识嵌套分组而不相撞的路径，以及决定嵌套层序的层级列表。
/// </summary>
public class NestedGroupingTests
{
    private static readonly string[] deptThenStatus = { "dept", "status" };

    private static readonly string[] statusThenDept = { "status", "dept" };

    private static readonly string[] deptThenYear = { "dept", "year" };

    private static readonly string[] statusYearDept = { "status", "year", "dept" };

    private static readonly string[] yearOnly = { "year" };

    // --- GroupPath（分组路径） ------------------------------------------------------------

    [Fact]
    public void TwoPathsWithTheSameKeysAreEqual()
    {
        var left = new GroupPath("Sales", "Active");
        var right = new GroupPath("Sales", "Active");

        Assert.Equal(left, right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    [Fact]
    public void TheSameLeafKeyUnderDifferentParentsIsADifferentPath()
    {
        // 这正是路径存在的全部理由：若用扁平键集，"Active" 会把
        // 每个部门下的该分组一并折叠掉。
        var underSales = new GroupPath("Sales", "Active");
        var underSupport = new GroupPath("Support", "Active");

        Assert.NotEqual(underSales, underSupport);
    }

    [Fact]
    public void APathTracksItsOwnDepthAndLeafKey()
    {
        var path = new GroupPath("Sales", "Active", "2026");

        Assert.Equal(2, path.Depth);
        Assert.Equal("2026", path.Key);
        Assert.Equal(new GroupPath("Sales", "Active"), path.Parent());
    }

    [Fact]
    public void AppendReturnsAChildPathAndLeavesTheOriginalAlone()
    {
        var parent = new GroupPath("Sales");
        var child = parent.Append("Active");

        Assert.Equal(new GroupPath("Sales", "Active"), child);
        Assert.Equal(new GroupPath("Sales"), parent);
    }

    [Fact]
    public void ANullKeyIsALegitimateSegment()
    {
        var withNull = new GroupPath("Sales", null);

        Assert.Equal(new GroupPath("Sales", null), withNull);
        Assert.NotEqual(new GroupPath("Sales"), withNull);
    }

    [Fact]
    public void RootIsAnAncestorOfEveryPath()
    {
        Assert.True(GroupPath.Root.IsAncestorOfOrSelf(new GroupPath("Sales", "Active")));
        Assert.Equal(GroupPath.Root, new GroupPath("Sales").Parent());
    }

    [Fact]
    public void AncestryOnlyMatchesAPrefix()
    {
        var sales = new GroupPath("Sales");

        Assert.True(sales.IsAncestorOfOrSelf(new GroupPath("Sales", "Active")));
        Assert.True(sales.IsAncestorOfOrSelf(sales));
        Assert.False(sales.IsAncestorOfOrSelf(new GroupPath("Support", "Active")));
        Assert.False(new GroupPath("Sales", "Active").IsAncestorOfOrSelf(sales));
    }

    // --- Levels（层级） -----------------------------------------------------------------

    [Fact]
    public void SetGroupsKeepsTheGivenOrder()
    {
        var state = new DataGridGroupState();

        state.SetGroups(new[] { Level("dept"), Level("status") });

        Assert.Equal(deptThenStatus, state.ActiveGroups.Select(g => g.ColumnId));
        Assert.Equal(2, state.Depth);
        Assert.Equal("dept", state.ActiveGroup!.ColumnId);
    }

    [Fact]
    public void SetGroupsDropsARepeatedColumn()
    {
        // 按同一列分组两次，会产生一层每个分组恰好只含
        // 一个子分组的层级。
        var state = new DataGridGroupState();

        state.SetGroups(new[] { Level("dept"), Level("status"), Level("dept") });

        Assert.Equal(deptThenStatus, state.ActiveGroups.Select(g => g.ColumnId));
    }

    [Fact]
    public void AddGroupNestsInsideTheExistingLevels()
    {
        var state = new DataGridGroupState();
        state.SetGroup(Level("dept"));

        Assert.True(state.AddGroup(Level("status")));

        Assert.Equal(deptThenStatus, state.ActiveGroups.Select(g => g.ColumnId));
    }

    [Fact]
    public void AddGroupIgnoresAColumnThatAlreadyGroups()
    {
        var state = new DataGridGroupState();
        state.SetGroup(Level("dept"));

        Assert.False(state.AddGroup(Level("dept")));
        Assert.Single(state.ActiveGroups);
    }

    [Fact]
    public void RemoveGroupKeepsTheOtherLevelsInOrder()
    {
        var state = new DataGridGroupState();
        state.SetGroups(new[] { Level("dept"), Level("status"), Level("year") });

        Assert.True(state.RemoveGroup("status"));

        Assert.Equal(deptThenYear, state.ActiveGroups.Select(g => g.ColumnId));
    }

    [Fact]
    public void MoveGroupChangesWhichColumnGroupsOutermost()
    {
        var state = new DataGridGroupState();
        state.SetGroups(new[] { Level("dept"), Level("status") });

        Assert.True(state.MoveGroup("status", 0));

        Assert.Equal(statusThenDept, state.ActiveGroups.Select(g => g.ColumnId));
    }

    [Fact]
    public void MoveGroupClampsAnIndexPastTheEnd()
    {
        var state = new DataGridGroupState();
        state.SetGroups(new[] { Level("dept"), Level("status"), Level("year") });

        state.MoveGroup("dept", 99);

        Assert.Equal(statusYearDept, state.ActiveGroups.Select(g => g.ColumnId));
    }

    [Fact]
    public void GetLevelReportsWhereAColumnGroups()
    {
        var state = new DataGridGroupState();
        state.SetGroups(new[] { Level("dept"), Level("status") });

        Assert.Equal(0, state.GetLevel("dept"));
        Assert.Equal(1, state.GetLevel("status"));
        Assert.Equal(-1, state.GetLevel("salary"));
        Assert.True(state.IsGroupedBy("status"));
        Assert.False(state.IsGroupedBy("salary"));
    }

    [Fact]
    public void ChangingTheLevelsClearsCollapsedState()
    {
        // 一旦新增一层，已存的每条路径都差一层才能标识分组
        // 留着它们就会折叠错误的分组。
        var state = new DataGridGroupState();
        state.SetGroup(Level("dept"));
        state.Toggle(new GroupPath("Sales"));
        Assert.True(state.IsCollapsed(new GroupPath("Sales")));

        state.AddGroup(Level("status"));

        Assert.Empty(state.CollapsedPaths);
    }

    // --- Collapse（折叠） ---------------------------------------------------------------

    [Fact]
    public void CollapsingOneNestedGroupLeavesItsSiblingUnderAnotherParentAlone()
    {
        var state = new DataGridGroupState();
        state.SetGroups(new[] { Level("dept"), Level("status") });

        state.Toggle(new GroupPath("Sales", "Active"));

        Assert.True(state.IsCollapsed(new GroupPath("Sales", "Active")));
        Assert.False(state.IsCollapsed(new GroupPath("Support", "Active")));
    }

    [Fact]
    public void AGroupInsideACollapsedParentIsHiddenButNotItselfCollapsed()
    {
        // 把两者区分开，才能让子树在父级收起时
        // 仍记住自身的展开状态。
        var state = new DataGridGroupState();
        state.SetGroups(new[] { Level("dept"), Level("status") });

        state.Toggle(new GroupPath("Sales"));

        Assert.False(state.IsCollapsed(new GroupPath("Sales", "Active")));
        Assert.True(state.IsHiddenByCollapse(new GroupPath("Sales", "Active")));
        Assert.False(state.IsHiddenByCollapse(new GroupPath("Support", "Active")));
    }

    [Fact]
    public void AnAncestorAtAnyDepthHidesADescendant()
    {
        var state = new DataGridGroupState();
        state.SetGroups(new[] { Level("dept"), Level("status"), Level("year") });

        state.Toggle(new GroupPath("Sales"));

        Assert.True(state.IsHiddenByCollapse(new GroupPath("Sales", "Active", "2026")));
    }

    [Fact]
    public void ATopLevelGroupIsNeverHiddenByCollapse()
    {
        var state = new DataGridGroupState();
        state.SetGroup(Level("dept"));
        state.Toggle(new GroupPath("Sales"));

        Assert.False(state.IsHiddenByCollapse(new GroupPath("Sales")));
    }

    [Fact]
    public void ExpandAllClearsEveryLevel()
    {
        var state = new DataGridGroupState();
        state.SetGroups(new[] { Level("dept"), Level("status") });
        state.CollapseAll(new[] { new GroupPath("Sales"), new GroupPath("Sales", "Active") });

        state.ExpandAll();

        Assert.Empty(state.CollapsedPaths);
    }

    // --- 单层兼容性 ----------------------------------------------------------------------

    [Fact]
    public void TheKeyBasedApiStillCollapsesATopLevelGroup()
    {
        var state = new DataGridGroupState();
        state.SetGroup(Level("dept"));

        state.Toggle("Sales");

        Assert.True(state.IsCollapsed("Sales"));
        Assert.True(state.IsCollapsed(new GroupPath("Sales")));
        Assert.Contains("Sales", state.CollapsedKeys);
    }

    [Fact]
    public void SetGroupReplacesEveryLevel()
    {
        var state = new DataGridGroupState();
        state.SetGroups(new[] { Level("dept"), Level("status") });

        state.SetGroup(Level("year"));

        Assert.Equal(yearOnly, state.ActiveGroups.Select(g => g.ColumnId));
    }

    [Fact]
    public void VersionAdvancesWheneverTheLevelsChange()
    {
        var state = new DataGridGroupState();
        var start = state.Version;

        state.SetGroup(Level("dept"));
        var afterSet = state.Version;
        state.AddGroup(Level("status"));
        var afterAdd = state.Version;
        state.RemoveGroup("status");

        Assert.True(afterSet > start);
        Assert.True(afterAdd > afterSet);
        Assert.True(state.Version > afterAdd);
    }

    [Fact]
    public void TogglingCollapseDoesNotAdvanceTheVersion()
    {
        // 版本号告诉表格其分组形状变了。折叠改变的是渲染内容
        // 而非形状，推进它会迫发一次无谓的数据刷新。
        var state = new DataGridGroupState();
        state.SetGroup(Level("dept"));
        var version = state.Version;

        state.Toggle(new GroupPath("Sales"));

        Assert.Equal(version, state.Version);
    }

    private static GroupDefinition Level(string columnId) =>
        new() { ColumnId = columnId, GroupSortDirection = SortDirection.Ascending };
}
