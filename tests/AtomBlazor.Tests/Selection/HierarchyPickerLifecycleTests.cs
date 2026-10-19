using AtomBlazor.Primitives;
using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using AtomBlazor;
using AtomBlazor.Tests.Performance;
using AtomBlazor.Tests.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Xunit;

// CS0618：EditContext.Validate() 在 .NET 11 已标记过时（建议 ValidateAsync）。
// 用例正是断言同步 Validate() 的返回值为测试目的本身，故保留语句本体，仅在此文件抑制该诊断。
#pragma warning disable CS0618
namespace AtomBlazor.Tests.Selection;

public class HierarchyPickerLifecycleTests
{
    [Theory]
    [InlineData(false, "root")]
    [InlineData(false, "leaf")]
    [InlineData(true, "leaf")]
    public async Task TreeKeyboardExpansionPreservesSelectionAndEnterCommitsAccordingToMode(bool multiple, string target)
    {
        await using var provider = Services().BuildServiceProvider();
        await using var renderer = new ComponentTestRenderer(provider, NullLoggerFactory.Instance);
        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            await renderer.MountAsync<AtomBlazor.Primitives.PortalHost>(new());
            var picker = await renderer.MountAsync<TreeSelect<Node>>(new()
            {
                [nameof(TreeSelect<Node>.Items)] = nodes,
                [nameof(TreeSelect<Node>.ValueField)] = (Func<Node, string>)(n => n.Id),
                [nameof(TreeSelect<Node>.TextField)] = (Func<Node, string>)(n => n.Text),
                [nameof(TreeSelect<Node>.ChildrenProperty)] = (Func<Node, IEnumerable<Node>>)(n => n.Children),
                [nameof(TreeSelect<Node>.Multiple)] = multiple,
                [nameof(TreeSelect<Node>.Value)] = "leaf"
            });
            ComponentProbe.Call(picker, "SetOpen", true);
            await picker.SetParametersAsync(ParameterView.Empty);
            var tree = renderer.FindComponent<AtomBlazor.Primitives.TreeViewPrimitive>();
            Assert.Equal("true", tree.AdditionalAttributes!["data-tree-select"]);
            await tree.JsOnNodeExpand("root");
            await tree.JsOnNodeCollapse("root");
            await Task.Yield();
            Assert.True(ComponentProbe.Field<bool>(picker, "open"));
            Assert.Equal("leaf", picker.Value);
            Assert.Empty(picker.Values);

            if (multiple)
            {
                tree.JsOnNodeCheck(target);
                await Task.Yield();
                Assert.Contains(target, picker.Values);
                Assert.True(ComponentProbe.Field<bool>(picker, "open"));
                tree.JsOnNodeCheck(target);
                await Task.Yield();
                Assert.DoesNotContain(target, picker.Values);
                Assert.True(ComponentProbe.Field<bool>(picker, "open"));
            }
            else
            {
                await tree.JsOnNodeActivate(target, target == "root");
                await Task.Yield();
                Assert.Equal(target, picker.Value);
                Assert.False(ComponentProbe.Field<bool>(picker, "open"));
            }
        });
    }

    [Fact]
    public async Task TreeSearchStaysStableDuringCloseAndResetsOnReopen()
    {
        await using var provider = Services().BuildServiceProvider();
        await using var renderer = new ComponentTestRenderer(provider, NullLoggerFactory.Instance);
        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var picker = await renderer.MountAsync<TreeSelect<Node>>(new()
            {
                [nameof(TreeSelect<Node>.Items)] = nodes,
                [nameof(TreeSelect<Node>.ValueField)] = (Func<Node, string>)(n => n.Id),
                [nameof(TreeSelect<Node>.TextField)] = (Func<Node, string>)(n => n.Text),
                [nameof(TreeSelect<Node>.ChildrenProperty)] = (Func<Node, IEnumerable<Node>>)(n => n.Children)
            });
            ComponentProbe.Call(picker, "SetOpen", true);
            ComponentProbe.SetField(picker, "search", "Leaf");
            ComponentProbe.Call(picker, "SetOpen", false);
            Assert.False(ComponentProbe.Field<bool>(picker, "open"));
            Assert.Equal("Leaf", ComponentProbe.Field<string>(picker, "search"));
            ComponentProbe.Call(picker, "SetOpen", true);
            Assert.Empty(ComponentProbe.Field<string>(picker, "search"));
        });
    }

    [Theory]
    [InlineData(false, "")]
    [InlineData(true, "")]
    [InlineData(true, "Web")]
    public async Task ParentCheckboxCascadesAcrossCollapsedAndFilteredDescendants(bool leafOnly, string search)
    {
        await using var provider = Services().BuildServiceProvider();
        await using var renderer = new ComponentTestRenderer(provider, NullLoggerFactory.Instance);
        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            await renderer.MountAsync<AtomBlazor.Primitives.PortalHost>(new());
            var changes = 0;
            var nodes = new Node[]
            {
                new("engineering", "Engineering", [new("platform", "Platform", [new("runtime", "Runtime", []), new("tools", "Tools", [])]), new("web", "Web", [])]),
                new("design", "Design", [new("brand", "Brand", [])])
            };
            var picker = await renderer.MountAsync<TreeSelect<Node>>(new()
            {
                [nameof(TreeSelect<Node>.Items)] = nodes,
                [nameof(TreeSelect<Node>.ValueField)] = (Func<Node, string>)(n => n.Id),
                [nameof(TreeSelect<Node>.TextField)] = (Func<Node, string>)(n => n.Text),
                [nameof(TreeSelect<Node>.ChildrenProperty)] = (Func<Node, IEnumerable<Node>>)(n => n.Children),
                [nameof(TreeSelect<Node>.Multiple)] = true,
                [nameof(TreeSelect<Node>.LeafOnly)] = leafOnly,
                [nameof(TreeSelect<Node>.Values)] = new HashSet<string> { "runtime", "brand" },
                [nameof(TreeSelect<Node>.ValuesChanged)] = EventCallback.Factory.Create<HashSet<string>>(new object(), _ => changes++)
            });
            ComponentProbe.Call(picker, "SetOpen", true);
            ComponentProbe.SetField(picker, "search", search);
            await picker.SetParametersAsync(ParameterView.Empty);
            var tree = renderer.FindComponent<AtomBlazor.Primitives.TreeViewPrimitive>();
            var context = ComponentProbe.Field<AtomBlazor.Primitives.TreeViewContext>(tree, "context");
            Assert.True(context.IsIndeterminate("engineering"));
            Assert.False(context.IsExpanded("platform"));
            var before = changes;

            tree.JsOnNodeCheck("engineering");
            await Task.Yield();
            Assert.Contains("runtime", picker.Values);
            Assert.Contains("tools", picker.Values);
            Assert.Contains("web", picker.Values);
            Assert.Contains("brand", picker.Values);
            Assert.Equal(!leafOnly, picker.Values.Contains("engineering"));
            Assert.True(context.IsChecked("platform"));
            Assert.True(context.IsChecked("engineering"));
            Assert.False(context.IsIndeterminate("engineering"));
            Assert.Equal(before + 1, changes);

            // 仅叶子模式下重渲染选择器，必须保住已勾选分支的汇总状态。
            await picker.SetParametersAsync(ParameterView.Empty);
            tree.JsOnNodeCheck("web");
            await Task.Yield();
            Assert.DoesNotContain("web", picker.Values);
            Assert.Contains("runtime", picker.Values);
            Assert.True(context.IsIndeterminate("engineering"));
            tree.JsOnNodeCheck("engineering");
            await Task.Yield();
            tree.JsOnNodeCheck("engineering");
            await Task.Yield();
            Assert.DoesNotContain("runtime", picker.Values);
            Assert.DoesNotContain("tools", picker.Values);
            Assert.DoesNotContain("web", picker.Values);
            Assert.Contains("brand", picker.Values);
            Assert.False(context.IsIndeterminate("engineering"));
        });
    }

    [Fact]
    public async Task TreeSelectionEnforcesLeafOnlyDisabledAndReportsFieldChanges()
    {
        var services = Services();
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new ComponentTestRenderer(provider, NullLoggerFactory.Instance);
        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var model = new Model();
            var editContext = new EditContext(model);
            using var validation = editContext.EnableDataAnnotationsValidation(provider);
            var changes = 0;
            var picker = await renderer.MountAsync<TreeSelect<Node>>(new()
            {
                [nameof(TreeSelect<Node>.Items)] = nodes,
                [nameof(TreeSelect<Node>.ValueField)] = (Func<Node, string>)(n => n.Id),
                [nameof(TreeSelect<Node>.TextField)] = (Func<Node, string>)(n => n.Text),
                [nameof(TreeSelect<Node>.ChildrenProperty)] = (Func<Node, IEnumerable<Node>>)(n => n.Children),
                [nameof(TreeSelect<Node>.LeafOnly)] = true,
                [nameof(TreeSelect<Node>.ValueExpression)] = (Expression<Func<string?>>)(() => model.Value),
                [nameof(TreeSelect<Node>.ValueChanged)] = EventCallback.Factory.Create<string?>(new object(), value => { model.Value = value; changes++; })
            });
            typeof(TreeSelect<Node>).GetProperty("EditContext", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.SetValue(picker, editContext);
            ComponentProbe.Call(picker, "OnParametersSet");
            await (Task)ComponentProbe.Call(picker, "SelectAsync", "root")!;
            Assert.Equal(0, changes);
            await (Task)ComponentProbe.Call(picker, "SelectAsync", "leaf")!;
            Assert.Equal("leaf", model.Value);
            Assert.True(editContext.IsModified());
            Assert.True(editContext.Validate());
            ComponentProbe.Call(picker, "SetOpen", true);
            var selectionChanges = changes;
            await (Task)ComponentProbe.Call(picker, "SelectAsync", "leaf")!;
            Assert.True(ComponentProbe.Field<bool>(picker, "open"));
            Assert.Equal(selectionChanges, changes);
            ComponentProbe.Call(picker, "NodeClicked", nodes[0].Children[0]);
            Assert.False(ComponentProbe.Field<bool>(picker, "open"));
            await (Task)ComponentProbe.Call(picker, "ClearAsync")!;
            Assert.Null(model.Value);
            Assert.False(editContext.Validate());
            var before = changes;
            await picker.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string, object?> { [nameof(picker.Disabled)] = true }));
            await (Task)ComponentProbe.Call(picker, "SelectAsync", "leaf")!;
            Assert.Equal(before, changes);
        });
    }

    [Fact]
    public async Task CascaderDerivesPreselectedPathAndClearEmitsNull()
    {
        await using var provider = Services().BuildServiceProvider();
        await using var renderer = new ComponentTestRenderer(provider, NullLoggerFactory.Instance);
        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var value = (string?)"leaf";
            var picker = await renderer.MountAsync<Cascader<Node>>(new()
            {
                [nameof(Cascader<Node>.Items)] = nodes,
                [nameof(Cascader<Node>.ValueField)] = (Func<Node, string>)(n => n.Id),
                [nameof(Cascader<Node>.TextField)] = (Func<Node, string>)(n => n.Text),
                [nameof(Cascader<Node>.ChildrenProperty)] = (Func<Node, IEnumerable<Node>>)(n => n.Children),
                [nameof(Cascader<Node>.Value)] = value,
                [nameof(Cascader<Node>.ValueChanged)] = EventCallback.Factory.Create<string?>(new object(), selected => value = selected)
            });
            Assert.Equal("Root / Leaf", ComponentProbe.Call(picker, "PathLabel", "leaf"));
            await (Task)ComponentProbe.Call(picker, "SelectAsync", null, true)!;
            Assert.Null(value);
            Assert.False(ComponentProbe.Field<bool>(picker, "open"));
        });
    }

    private static ServiceCollection Services()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton<IJSRuntime, NoopJavaScript>();
        services.AddAtomBlazorComponents();
        return services;
    }

    private static readonly Node[] nodes = [new("root", "Root", [new("leaf", "Leaf", [])])];
    public sealed record Node(string Id, string Text, Node[] Children);
    private sealed class Model
    {
        [Required] public string? Value { get; set; }
    }
}
