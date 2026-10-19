using AtomBlazor.Primitives;
using Xunit;

namespace AtomBlazor.Tests.Primitives;

/// <summary>
/// 覆盖行编辑背后的快照机制：行编辑直接绑定到条目实例，所以取消编辑
/// 必须把原值写回同一个实例。
/// </summary>
public class RowEditingTests
{
    private sealed class Employee
    {
        public string Name { get; set; } = "";

        public int Salary { get; set; }

        public DateTime? Reviewed { get; set; }

        public string Initials => Name.Length > 0 ? Name[..1] : "";

        public string WriteOnly { private get; set; } = "";

        public string ReadWriteOnly() => WriteOnly;
    }

    private sealed class Throwing
    {
        private string backing = "";

        public string Safe { get; set; } = "";

        public string Explodes
        {
            get => throw new InvalidOperationException(backing);
            set => throw new InvalidOperationException(value);
        }
    }

    [Fact]
    public void RestorePutsEveryChangedValueBack()
    {
        var employee = new Employee { Name = "Ana", Salary = 100, Reviewed = new DateTime(2026, 1, 1) };
        var snapshot = DataGridRowSnapshot<Employee>.Capture(employee);

        employee.Name = "Bo";
        employee.Salary = 200;
        employee.Reviewed = null;

        snapshot.Restore();

        Assert.Equal("Ana", employee.Name);
        Assert.Equal(100, employee.Salary);
        Assert.Equal(new DateTime(2026, 1, 1), employee.Reviewed);
    }

    [Fact]
    public void RestoreWritesBackToTheSameInstance()
    {
        // 其他持有该行引用的代码也必须看到值一并回退，
        // 这就是快照选择原地还原而非交回副本的原因。
        var employee = new Employee { Name = "Ana" };
        var alias = employee;
        var snapshot = DataGridRowSnapshot<Employee>.Capture(employee);

        employee.Name = "Bo";
        snapshot.Restore();

        Assert.Same(employee, snapshot.Item);
        Assert.Equal("Ana", alias.Name);
    }

    [Fact]
    public void HasChangesIsFalseUntilSomethingIsEdited()
    {
        var employee = new Employee { Name = "Ana", Salary = 100 };
        var snapshot = DataGridRowSnapshot<Employee>.Capture(employee);

        Assert.False(snapshot.HasChanges());

        employee.Salary = 101;

        Assert.True(snapshot.HasChanges());
    }

    [Fact]
    public void HasChangesGoesBackToFalseAfterARestore()
    {
        var employee = new Employee { Name = "Ana" };
        var snapshot = DataGridRowSnapshot<Employee>.Capture(employee);

        employee.Name = "Bo";
        snapshot.Restore();

        Assert.False(snapshot.HasChanges());
    }

    [Fact]
    public void EditingToTheSameValueIsNotAChange()
    {
        var employee = new Employee { Name = "Ana" };
        var snapshot = DataGridRowSnapshot<Employee>.Capture(employee);

        employee.Name = "Bo";
        employee.Name = "Ana";

        Assert.False(snapshot.HasChanges());
    }

    [Fact]
    public void AComputedPropertyIsIgnoredBecauseThereIsNothingToRestore()
    {
        var employee = new Employee { Name = "Ana" };
        var snapshot = DataGridRowSnapshot<Employee>.Capture(employee);

        employee.Name = "Bo";

        // Initials 没有 setter，所以靠还原 Name 才能把它纠正。
        snapshot.Restore();

        Assert.Equal("A", employee.Initials);
    }

    [Fact]
    public void APropertyThatThrowsDoesNotAbandonTheRestore()
    {
        var item = new Throwing { Safe = "before" };
        var snapshot = DataGridRowSnapshot<Throwing>.Capture(item);

        item.Safe = "after";
        snapshot.Restore();

        Assert.Equal("before", item.Safe);
    }

    [Fact]
    public void CaptureRejectsANullItem() =>
        Assert.Throws<ArgumentNullException>(() => DataGridRowSnapshot<Employee>.Capture(null!));

    [Fact]
    public void TwoSnapshotsOfTheSameItemAreIndependent()
    {
        // 第一次编辑提交后再开始第二次编辑，不得倒退到提交点之前。
        var employee = new Employee { Name = "Ana" };
        var first = DataGridRowSnapshot<Employee>.Capture(employee);

        employee.Name = "Bo";
        var second = DataGridRowSnapshot<Employee>.Capture(employee);
        employee.Name = "Cy";

        second.Restore();
        Assert.Equal("Bo", employee.Name);

        first.Restore();
        Assert.Equal("Ana", employee.Name);
    }
}
