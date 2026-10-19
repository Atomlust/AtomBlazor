using AtomBlazor.Primitives;
using System.Text.Json;
using System.Text.RegularExpressions;
using AtomBlazor.Tests.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Xunit;
using PTree = AtomBlazor.Primitives.TreeViewPrimitive;
using PTreeItem = AtomBlazor.Primitives.TreeItemPrimitive;
using PTreeViewContext = AtomBlazor.Primitives.TreeViewContext;
using PTreeViewState = AtomBlazor.Primitives.TreeViewState;

namespace AtomBlazor.Tests.Components;

/// <summary>
/// TreeView 基元测试：上下文纯状态沿（展开/折叠、注册表层级查询、
/// 注册期自动展开、None/Single/Multiple 选择模式、级联勾选向下铺开与向上汇总、
/// 不确定态与禁用豁免、CheckStrictly 独立、TreeViewState 序列化往返）、
/// 渲染层 aria 层级（role=tree/treeitem、aria-expanded/selected/checked/level/
/// setsize/posinset、data-state/data-value、roving tabindex）、受控参数同步沿
/// （含过期重渲染不回退用户操作的守卫）、DefaultExpandAll/DefaultExpandDepth、
/// 事件回调沿、treeKeyboard.initialize/dispose interop 载荷与 JSInvokable 入口。
/// 拖放（TreeDrop）headless 不可模拟，仅覆盖 TreeDropEventArgs/TreeDropPosition
/// 的事件参数构造；键盘导航的 DOM 行为由 node 侧 tree-keyboard-module 测试覆盖。
/// </summary>
public class TreeViewTests
{
    /// <summary>
    /// 归一化上下文自增 id（tree-N 及其派生的 -node- 作用域 id），使快照可确定。
    /// </summary>
    private static string Scrub(string markup) =>
        Regex.Replace(markup, "tree-[0-9]+", "tree-<n>");

    private static (ComponentTestRenderer Renderer, NoopJavaScript Js) CreateRenderer()
    {
        var js = new NoopJavaScript();
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(js);
        return (new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance), js);
    }

    /// <summary>让调度器空转两轮，冲刷 OnAfterRender/StateHasChanged 队列。</summary>
    private static async Task FlushAsync(ComponentTestRenderer renderer)
    {
        for (var i = 0; i < 2; i++)
        {
            await renderer.Dispatcher.InvokeAsync(() => { });
        }
    }

    private static async Task<(ComponentTestRenderer Renderer, NoopJavaScript Js, PTree Tree)> MountAsync(
        Dictionary<string, object?>? parameters = null, RenderFragment? childContent = null)
    {
        var (renderer, js) = CreateRenderer();
        var dict = parameters ?? [];
        if (childContent != null)
        {
            dict = new Dictionary<string, object?>(dict) { [nameof(PTree.ChildContent)] = childContent };
        }
        var component = await renderer.Dispatcher.InvokeAsync(() =>
            renderer.MountTypeAsync(typeof(PTree), dict));
        await FlushAsync(renderer);
        return (renderer, js, (PTree)component);
    }

    private static (string Identifier, object?[] Args) Single(NoopJavaScript js, string identifier)
    {
        var invocation = Assert.Single(js.Invocations, i => i.Identifier == identifier);
        Assert.NotNull(invocation.Args);
        return (invocation.Identifier, invocation.Args!);
    }

    /// <summary>在 Dispatcher 上执行同步操作（上下文变更会触发订阅组件的 StateHasChanged）。</summary>
    private static Task ActAsync(ComponentTestRenderer renderer, Action act) =>
        renderer.Dispatcher.InvokeAsync(act);

    /// <summary>在 Dispatcher 上执行异步操作（如 JSInvokable 入口）。</summary>
    private static Task ActAsync(ComponentTestRenderer renderer, Func<Task> act) =>
        renderer.Dispatcher.InvokeAsync(act);

    // ------------------------------------------------------------------
    // 片段构造
    // ------------------------------------------------------------------

    /// <summary>扁平两根节点。</summary>
    private static RenderFragment FlatFamily() => builder =>
    {
        var seq = 0;
        foreach (var value in new[] { "a", "b" })
        {
            builder.OpenComponent<PTreeItem>(seq++);
            builder.AddAttribute(seq++, "Value", value);
            builder.CloseComponent();
        }
    };

    /// <summary>根 a（子 a1/a2）+ 根 b。</summary>
    private static RenderFragment NestedFamily() => builder =>
    {
        builder.OpenComponent<PTreeItem>(0);
        builder.AddAttribute(1, "Value", "a");
        builder.AddAttribute(2, "ChildContent", (RenderFragment)(inner =>
        {
            var iseq = 0;
            inner.OpenComponent<PTreeItem>(iseq++);
            inner.AddAttribute(iseq++, "Value", "a1");
            inner.CloseComponent();
            inner.OpenComponent<PTreeItem>(iseq++);
            inner.AddAttribute(iseq++, "Value", "a2");
            inner.CloseComponent();
        }));
        builder.CloseComponent();
        builder.OpenComponent<PTreeItem>(3);
        builder.AddAttribute(4, "Value", "b");
        builder.CloseComponent();
    };

    /// <summary>根 p（子 p1 禁用可选/p2）。</summary>
    private static RenderFragment CascadeFamily(bool p1Disabled = false) => builder =>
    {
        builder.OpenComponent<PTreeItem>(0);
        builder.AddAttribute(1, "Value", "p");
        builder.AddAttribute(2, "ChildContent", (RenderFragment)(inner =>
        {
            var iseq = 0;
            inner.OpenComponent<PTreeItem>(iseq++);
            inner.AddAttribute(iseq++, "Value", "p1");
            if (p1Disabled)
            {
                inner.AddAttribute(iseq++, "Disabled", true);
            }
            inner.CloseComponent();
            inner.OpenComponent<PTreeItem>(iseq++);
            inner.AddAttribute(iseq++, "Value", "p2");
            inner.CloseComponent();
        }));
        builder.CloseComponent();
    };

    // ------------------------------------------------------------------
    // 上下文纯状态沿
    // ------------------------------------------------------------------

    [Fact]
    public void 展开折叠沿只在真实翻转时通知()
    {
        var context = new PTreeViewContext();
        var notifications = 0;
        context.OnStateChanged += () => notifications++;

        context.ExpandNode("a");
        Assert.Equal(1, notifications);
        Assert.True(context.IsExpanded("a"));

        context.ExpandNode("a");
        Assert.Equal(1, notifications); // 已展开不重复通知

        context.CollapseNode("a");
        Assert.Equal(2, notifications);
        Assert.False(context.IsExpanded("a"));

        context.CollapseNode("a");
        Assert.Equal(2, notifications); // 已折叠不重复通知

        context.ToggleExpanded("a");
        Assert.True(context.IsExpanded("a"));
        Assert.Equal(3, notifications);
        context.ToggleExpanded("a");
        Assert.False(context.IsExpanded("a"));
        Assert.Equal(4, notifications);

        context.SetExpandedValues(["x", "y"]);
        Assert.Equal(5, notifications);
        Assert.True(context.IsExpanded("x") && context.IsExpanded("y"));
    }

    [Fact]
    public void 注册表结构查询应给出层级序位与祖先链()
    {
        var context = new PTreeViewContext();
        context.RegisterNode("root", null, 0, false);
        context.RegisterNode("c1", "root", 1, false);
        context.RegisterNode("c2", "root", 1, true);
        context.RegisterNode("g1", "c1", 2, false);

        Assert.Equal(["c1", "c2"], context.GetChildValues("root"));
        Assert.Equal(["c1", "g1", "c2"], context.GetAllDescendants("root"));
        Assert.Equal(["c1", "root"], context.GetAncestors("g1")); // 自底向上：父级在前
        Assert.Equal(["c2"], context.GetSiblingValues("c1"));
        Assert.Equal(2, context.GetNodeDepth("g1"));
        Assert.Equal(2, context.GetSetSize("c1"));
        Assert.Equal(1, context.GetPosInSet("c1"));
        Assert.Equal(2, context.GetPosInSet("c2"));
        Assert.True(context.HasChildren("root"));
        Assert.False(context.HasChildren("g1"));
        Assert.True(context.IsNodeRegistered("c2"));
        Assert.False(context.IsNodeRegistered("missing"));
        Assert.Equal("root", context.GetParentValue("c1"));
        Assert.Null(context.GetParentValue("root"));
        Assert.True(context.IsFirstEnabledRoot("root"));

        context.UpdateNodeDisabled("c2", false);
        Assert.False(context.IsFirstEnabledRoot("x")); // 未知值
        context.RegisterNode("r2", null, 0, false);
        Assert.False(context.IsFirstEnabledRoot("r2")); // 首个可用根仍是 root

        context.UnregisterNode("g1");
        Assert.False(context.IsNodeRegistered("g1"));
        Assert.False(context.HasChildren("c1"));
        Assert.DoesNotContain("g1", context.GetAllDescendants("root"));
    }

    [Fact]
    public void 注册期自动展开直接改状态且不触发通知()
    {
        var context = new PTreeViewContext();
        var notifications = 0;
        context.OnStateChanged += () => notifications++;
        context.AutoExpandAll = true;

        context.RegisterNode("a", null, 0, false);
        context.RegisterNode("a1", "a", 1, false);

        Assert.True(context.IsExpanded("a"));
        Assert.True(context.IsExpanded("a1"));
        Assert.Equal(0, notifications); // 渲染中途注册不惊动订阅方

        // 深度阈值：只展开深度 < 2 的节点
        var depthContext = new PTreeViewContext { AutoExpandDepth = 2 };
        depthContext.RegisterNode("x", null, 0, false);
        depthContext.RegisterNode("x1", "x", 1, false);
        depthContext.RegisterNode("x11", "x1", 2, false);
        Assert.True(depthContext.IsExpanded("x"));
        Assert.True(depthContext.IsExpanded("x1"));
        Assert.False(depthContext.IsExpanded("x11"));
    }

    [Fact]
    public void 选择模式沿None不选Single替换Multiple切换()
    {
        var context = new PTreeViewContext();
        context.RegisterNode("a", null, 0, false);
        context.RegisterNode("b", null, 0, false);
        context.RegisterNode("d", null, 0, true);

        // None：吞掉选择
        context.State.SelectionMode = TreeSelectionMode.None;
        context.SelectNode("a");
        Assert.Empty(context.State.SelectedValues);

        // Single：替换
        context.State.SelectionMode = TreeSelectionMode.Single;
        context.SelectNode("a");
        Assert.Equal(["a"], context.State.SelectedValues);
        context.SelectNode("b");
        Assert.Equal(["b"], context.State.SelectedValues);

        // 禁用节点不选中
        context.SelectNode("d");
        Assert.Equal(["b"], context.State.SelectedValues);

        // Multiple：切换
        context.State.SelectionMode = TreeSelectionMode.Multiple;
        context.SelectNode("a");
        Assert.Equal(["b", "a"], context.State.SelectedValues);
        context.SelectNode("a");
        Assert.Equal(["b"], context.State.SelectedValues);

        // 受控设置
        context.SetSelectedValues(["x"]);
        Assert.True(context.IsSelected("x"));
    }

    [Fact]
    public void 级联勾选向下铺开并向上汇总()
    {
        var context = new PTreeViewContext();
        context.State.Checkable = true;
        context.RegisterNode("p", null, 0, false);
        context.RegisterNode("p1", "p", 1, false);
        context.RegisterNode("p2", "p", 1, false);

        // 向下级联：勾父带子
        context.ToggleChecked("p");
        Assert.True(context.IsChecked("p"));
        Assert.True(context.IsChecked("p1") && context.IsChecked("p2"));

        // 向上级联：取消一个子级把父级拉下
        context.ToggleChecked("p1");
        Assert.False(context.IsChecked("p1"));
        Assert.False(context.IsChecked("p"));

        // 再勾上：兄弟全勾则父级自动勾上
        context.ToggleChecked("p1");
        Assert.True(context.IsChecked("p1"));
        Assert.True(context.IsChecked("p"));

        // 受控设置
        context.SetCheckedValues(["p2"]);
        Assert.True(context.IsChecked("p2"));
        Assert.False(context.IsChecked("p"));
    }

    [Fact]
    public void 不确定态判定与禁用后代豁免()
    {
        var context = new PTreeViewContext();
        context.State.Checkable = true;
        context.RegisterNode("p", null, 0, false);
        context.RegisterNode("p1", "p", 1, false);
        context.RegisterNode("p2", "p", 1, false);

        context.ToggleChecked("p1");
        Assert.True(context.IsIndeterminate("p"));   // 一勾一空
        Assert.False(context.IsIndeterminate("p1")); // 叶子无不确定态

        // p2 禁用后只剩 p1 参与判定 → 全勾 → 无不确定态
        context.RegisterNode("q", null, 0, false);
        context.RegisterNode("q1", "q", 1, true);
        context.RegisterNode("q2", "q", 1, false);
        context.ToggleChecked("q2");
        Assert.True(context.IsChecked("q2"));
        Assert.True(context.IsChecked("q")); // 向上级联：唯一可用子级勾上则父级勾上
        Assert.False(context.IsIndeterminate("q")); // 禁用的 q1 被豁免（非 mixed）

        // 取消父级时禁用后代不被卷入
        context.ToggleChecked("q");
        Assert.False(context.IsChecked("q"));
        Assert.False(context.IsChecked("q2"));
        Assert.False(context.IsChecked("q1")); // 禁用项保持未勾

        // 禁用节点自身不可勾选
        context.ToggleChecked("q1");
        Assert.False(context.IsChecked("q1"));
    }

    [Fact]
    public void CheckStrictly时各复选框独立()
    {
        var context = new PTreeViewContext();
        context.State.Checkable = true;
        context.State.CheckStrictly = true;
        context.RegisterNode("p", null, 0, false);
        context.RegisterNode("p1", "p", 1, false);
        context.RegisterNode("p2", "p", 1, false);

        context.ToggleChecked("p");
        Assert.True(context.IsChecked("p"));
        Assert.False(context.IsChecked("p1")); // 不向下级联

        context.ToggleChecked("p1");           // 子级独立勾选
        Assert.True(context.IsChecked("p1"));
        Assert.True(context.IsChecked("p"));   // 不向上级联改写

        context.ToggleChecked("p1");           // 子级独立取消
        Assert.False(context.IsChecked("p1"));
        Assert.True(context.IsChecked("p"));

        Assert.False(context.IsIndeterminate("p")); // 严格模式无不确定态
    }

    [Fact]
    public void 全部展开与兄弟展开沿()
    {
        var context = new PTreeViewContext();
        context.RegisterNode("a", null, 0, false);
        context.RegisterNode("a1", "a", 1, false);
        context.RegisterNode("b", null, 0, false);
        context.RegisterNode("b1", "b", 1, false);
        context.RegisterNode("b11", "b1", 2, false);

        context.ExpandSiblings("a"); // a 的兄弟 b 与自身都被铺开（均含子级）
        Assert.True(context.IsExpanded("a"));
        Assert.True(context.IsExpanded("b"));
        Assert.False(context.IsExpanded("b1")); // 只到兄弟层，不下钻后代

        context.ExpandAllWithChildren();
        Assert.True(context.IsExpanded("a"));
        Assert.True(context.IsExpanded("b"));
        Assert.True(context.IsExpanded("b1"));
        Assert.False(context.IsExpanded("b11")); // 叶子不进集合
    }

    [Fact]
    public void TreeViewState序列化往返应保留集合与开关()
    {
        var state = new PTreeViewState
        {
            ExpandedValues = ["a", "b"],
            SelectedValues = ["a"],
            CheckedValues = ["b"],
            SelectionMode = TreeSelectionMode.Multiple,
            Checkable = true,
            CheckStrictly = true,
            ShowLines = true,
            ShowIcons = false,
        };

        var restored = JsonSerializer.Deserialize<PTreeViewState>(JsonSerializer.Serialize(state))!;

        Assert.Equal(["a", "b"], restored.ExpandedValues.OrderBy(v => v));
        Assert.Equal(["a"], restored.SelectedValues);
        Assert.Equal(["b"], restored.CheckedValues);
        Assert.Equal(TreeSelectionMode.Multiple, restored.SelectionMode);
        Assert.True(restored.Checkable);
        Assert.True(restored.CheckStrictly);
        Assert.True(restored.ShowLines);
        Assert.False(restored.ShowIcons);
    }

    [Fact]
    public void 拖放事件参数应携带源目标与位置()
    {
        var args = new TreeDropEventArgs<string>("源", "目标", TreeDropPosition.Inside);

        Assert.Equal("源", args.SourceNode);
        Assert.Equal("目标", args.TargetNode);
        Assert.Equal(TreeDropPosition.Inside, args.Position);

        Assert.Equal(0, (int)TreeDropPosition.Before);
        Assert.Equal(1, (int)TreeDropPosition.After);
        Assert.Equal(2, (int)TreeDropPosition.Inside);
    }

    // ------------------------------------------------------------------
    // 渲染层结构与 aria
    // ------------------------------------------------------------------

    [Fact]
    public async Task 默认树快照应与基线一致()
    {
        var (renderer, _, _) = await MountAsync(childContent: FlatFamily());
        await Verify(Scrub(renderer.Markup()));
    }

    [Fact]
    public async Task 展开级联勾选树快照应与基线一致()
    {
        var (renderer, _, tree) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(PTree.SelectionMode)] = TreeSelectionMode.Multiple,
            [nameof(PTree.Checkable)] = true,
            [nameof(PTree.ExpandedValues)] = new HashSet<string> { "a" },
            [nameof(PTree.CheckedValues)] = new HashSet<string> { "a1" },
            [nameof(PTree.ShowLines)] = true,
            [nameof(PTree.AriaLabel)] = "示例树",
        }, NestedFamily());

        // 首轮渲染 a 的特性先于 a1/a2 注册计算（上游同拍）；
        // 一次良性状态变化让全部条目按注册后的级联状态重算（a → mixed）。
        await ActAsync(renderer, () => tree.Context.ExpandNode("b"));
        await FlushAsync(renderer);

        await Verify(Scrub(renderer.Markup()));
    }

    [Fact]
    public async Task 树结构应携带完整aria层级()
    {
        var (renderer, _) = CreateRenderer();
        await renderer.MountAsync<PTree>(new Dictionary<string, object?>
        {
            [nameof(PTree.ChildContent)] = NestedFamily(),
            [nameof(PTree.SelectionMode)] = TreeSelectionMode.Single,
            [nameof(PTree.AriaLabel)] = "层级树",
            [nameof(PTree.ExpandedValues)] = new HashSet<string> { "a" },
        });
        await FlushAsync(renderer);

        var markup = renderer.Markup();
        Assert.Contains("role=\"tree\"", markup);
        Assert.Contains("aria-label=\"层级树\"", markup);
        Assert.Equal(4, Regex.Count(markup, "role=\"treeitem\""));
        Assert.Contains("aria-expanded=\"true\"", markup);  // a 已展开（叶子不出 aria-expanded）
        Assert.Contains("aria-selected=\"false\"", markup); // Single 模式出 aria-selected
        Assert.Contains("aria-level=\"1\"", markup);
        Assert.Contains("aria-level=\"2\"", markup);
        Assert.Contains("aria-setsize=\"2\"", markup);
        Assert.Contains("aria-posinset=\"1\"", markup);
        Assert.Contains("data-value=\"a\"", markup);
        Assert.Contains("data-state=\"open\"", markup);
        Assert.Contains("data-state=\"closed\"", markup);
        Assert.Contains("data-has-children=\"true\"", markup);
        Assert.Contains("role=\"group\"", markup);
    }

    [Fact]
    public async Task 展开状态沿应切换数据状态与分组可见性()
    {
        var (renderer, _, tree) = await MountAsync(childContent: NestedFamily());
        var context = tree.Context;

        // a 未展开（叶子不出 aria-expanded；b 为叶子）
        Assert.Contains("data-state=\"closed\"", renderer.Markup());
        // 非级联勾选模式下，折叠节点的分组整段离场
        Assert.DoesNotContain("role=\"group\"", renderer.Markup());
        Assert.DoesNotContain("aria-expanded=\"true\"", renderer.Markup());

        await ActAsync(renderer, () => context.ExpandNode("a"));
        await FlushAsync(renderer);

        Assert.Contains("data-state=\"open\"", renderer.Markup());
        Assert.Contains("aria-expanded=\"true\"", renderer.Markup());
        // 折叠沿：折叠后的组在非级联勾选模式下整段离场（不是隐藏）
        await ActAsync(renderer, () => context.CollapseNode("a"));
        await FlushAsync(renderer);
        Assert.Contains("data-state=\"closed\"", renderer.Markup());
        Assert.DoesNotContain("role=\"group\"", renderer.Markup());
    }

    [Fact]
    public async Task 游走tabindex应从首个可用根转移到激活节点()
    {
        var (renderer, _, tree) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(PTree.SelectionMode)] = TreeSelectionMode.Single,
        }, FlatFamily());

        // 初始：首个可用根 a = tabindex 0，b = -1（渲染序：tabindex 先于 data-value）
        Assert.Matches("tabindex=\"0\"[^>]*data-value=\"a\"", renderer.Markup());
        Assert.Matches("tabindex=\"-1\"[^>]*data-value=\"b\"", renderer.Markup());

        await ActAsync(renderer, () => tree.JsOnNodeActivate("b"));
        await FlushAsync(renderer);

        Assert.Matches("tabindex=\"0\"[^>]*data-value=\"b\"", renderer.Markup());
        Assert.Matches("tabindex=\"-1\"[^>]*data-value=\"a\"", renderer.Markup());
    }

    [Fact]
    public async Task 级联勾选树在折叠时仍保留子级渲染()
    {
        var (renderer, _, tree) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(PTree.Checkable)] = true,
        }, NestedFamily());

        // a 未展开，但 Checkable 且非 Strict → 子级以 display:none 保留在 DOM
        Assert.Contains("display:none", renderer.Markup());
        Assert.Contains("data-value=\"a1\"", renderer.Markup());

        await ActAsync(renderer, () => tree.Context.ToggleChecked("a"));
        await FlushAsync(renderer);

        Assert.True(tree.Context.IsChecked("a1"));
        Assert.True(tree.Context.IsChecked("a2"));
        Assert.Contains("aria-checked=\"true\"", renderer.Markup());
    }

    [Fact]
    public async Task 勾选状态沿应驱动mixed与true()
    {
        var (renderer, _, tree) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(PTree.Checkable)] = true,
            [nameof(PTree.ExpandedValues)] = new HashSet<string> { "a" },
        }, NestedFamily());

        await ActAsync(renderer, () => tree.Context.ToggleChecked("a1"));
        await FlushAsync(renderer);

        // a1 勾上 → a 半选（渲染序：aria-checked 先于 data-value）
        Assert.Matches("aria-checked=\"mixed\"[^>]*data-value=\"a\"", renderer.Markup());
        Assert.Matches("aria-checked=\"true\"[^>]*data-value=\"a1\"", renderer.Markup());

        await ActAsync(renderer, () => tree.Context.ToggleChecked("a2"));
        await FlushAsync(renderer);

        // a1/a2 全勾 → a 全勾
        Assert.Matches("aria-checked=\"true\"[^>]*data-value=\"a\"", renderer.Markup());
    }

    [Fact]
    public async Task 选中沿应驱动aria选中与data选中标记()
    {
        var (renderer, _, tree) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(PTree.SelectionMode)] = TreeSelectionMode.Single,
        }, FlatFamily());

        Assert.Matches("data-value=\"a\"[^>]*data-selected=\"false\"", renderer.Markup());

        await ActAsync(renderer, () => tree.Context.SelectNode("a"));
        await FlushAsync(renderer);

        Assert.Matches("data-value=\"a\"[^>]*data-selected=\"true\"", renderer.Markup());
        Assert.Matches("aria-selected=\"true\"[^>]*data-value=\"a\"", renderer.Markup());

        // None 模式不出 aria-selected
        var (noneRenderer, _) = CreateRenderer();
        await noneRenderer.MountAsync<PTree>(new Dictionary<string, object?>
        {
            [nameof(PTree.ChildContent)] = FlatFamily(),
        });
        await FlushAsync(noneRenderer);
        Assert.DoesNotContain("aria-selected", noneRenderer.Markup());
    }

    [Fact]
    public async Task 禁用节点应携带aria禁用标记且不被选中()
    {
        var (renderer, _, tree) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(PTree.SelectionMode)] = TreeSelectionMode.Single,
        }, builder =>
        {
            var seq = 0;
            builder.OpenComponent<PTreeItem>(seq++);
            builder.AddAttribute(seq++, "Value", "a");
            builder.AddAttribute(seq++, "Disabled", true);
            builder.CloseComponent();
        });

        Assert.Contains("aria-disabled=\"true\"", renderer.Markup());

        await ActAsync(renderer, () => tree.Context.SelectNode("a"));
        await FlushAsync(renderer);

        Assert.DoesNotContain("data-selected=\"true\"", renderer.Markup());
    }

    [Fact]
    public async Task TreeItem脱离TreeView应抛中文异常()
    {
        var (renderer, _) = CreateRenderer();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            renderer.MountAsync<PTreeItem>(new Dictionary<string, object?> { ["Value"] = "a" }));
        Assert.Equal("TreeItemPrimitive 必须用在 TreeView 组件内部。", ex.Message);
    }

    // ------------------------------------------------------------------
    // 受控参数同步沿
    // ------------------------------------------------------------------

    [Fact]
    public async Task 受控参数应灌入上下文并在参数变化时跟进()
    {
        var (renderer, _, tree) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(PTree.SelectionMode)] = TreeSelectionMode.Single,
            [nameof(PTree.SelectedValue)] = "a",
            [nameof(PTree.ExpandedValues)] = new HashSet<string> { "a" },
        }, FlatFamily());

        Assert.True(tree.Context.IsSelected("a"));
        Assert.True(tree.Context.IsExpanded("a"));

        await renderer.RerenderRootAsync(0, new Dictionary<string, object?>
        {
            [nameof(PTree.SelectionMode)] = TreeSelectionMode.Single,
            [nameof(PTree.SelectedValue)] = "b",
            [nameof(PTree.ExpandedValues)] = new HashSet<string> { "b" },
        });
        await FlushAsync(renderer);

        Assert.True(tree.Context.IsSelected("b"));
        Assert.False(tree.Context.IsSelected("a"));
        Assert.True(tree.Context.IsExpanded("b"));
        Assert.False(tree.Context.IsExpanded("a"));
    }

    [Fact]
    public async Task 过期重渲染不得回退用户刚发起的选中()
    {
        var (renderer, _, tree) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(PTree.SelectionMode)] = TreeSelectionMode.Single,
            [nameof(PTree.SelectedValue)] = "a",
        }, FlatFamily());

        // 用户操作：上下文切到 b
        await ActAsync(renderer, () => tree.Context.SelectNode("b"));
        await FlushAsync(renderer);
        Assert.True(tree.Context.IsSelected("b"));

        // 中间渲染（参数字典里不再出现 SelectedValue，组件参数保持旧值 "a"）
        // 不得把上下文拉回 "a"——lastSyncedSelectedValue 守卫生效。
        await renderer.RerenderRootAsync(0, new Dictionary<string, object?>());
        await FlushAsync(renderer);

        Assert.True(tree.Context.IsSelected("b"));
    }

    [Fact]
    public async Task DefaultExpandAll应展开全部有子节点()
    {
        var (renderer, _, tree) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(PTree.DefaultExpandAll)] = true,
        }, NestedFamily());

        Assert.True(tree.Context.IsExpanded("a"));
        Assert.Contains("data-state=\"open\"", renderer.Markup());
    }

    [Fact]
    public async Task DefaultExpandDepth应只展开到指定深度()
    {
        var (renderer, _, tree) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(PTree.DefaultExpandDepth)] = 1,
        }, NestedFamily());

        Assert.True(tree.Context.IsExpanded("a"));  // 深度 0 < 1
        Assert.False(tree.Context.IsExpanded("a1")); // 深度 1 不展开
    }

    [Fact]
    public async Task ExpandAll与CollapseAll公共方法应沿上下文生效()
    {
        // 受控给出初始展开，让 a 的子级完成注册（ExpandAll 作用于已注册节点）
        var (renderer, _, tree) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(PTree.ExpandedValues)] = new HashSet<string> { "a" },
        }, NestedFamily());

        await ActAsync(renderer, tree.ExpandAll);
        await FlushAsync(renderer);
        Assert.True(tree.Context.IsExpanded("a"));

        await ActAsync(renderer, tree.CollapseAll);
        await FlushAsync(renderer);
        Assert.False(tree.Context.IsExpanded("a"));
        Assert.Contains("data-state=\"closed\"", renderer.Markup());
    }

    // ------------------------------------------------------------------
    // 事件回调沿
    // ------------------------------------------------------------------

    [Fact]
    public async Task 上下文变化应沿事件回调回填参数()
    {
        var selected = new List<string?>();
        var expanded = new List<HashSet<string>>();
        var checkedValues = new List<HashSet<string>>();
        var (renderer, _, tree) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(PTree.SelectionMode)] = TreeSelectionMode.Single,
            [nameof(PTree.SelectedValueChanged)] = EventCallback.Factory.Create<string?>(new object(), v => selected.Add(v)),
            [nameof(PTree.ExpandedValuesChanged)] = EventCallback.Factory.Create<HashSet<string>>(new object(), v => expanded.Add(v)),
            [nameof(PTree.Checkable)] = true,
            [nameof(PTree.CheckedValuesChanged)] = EventCallback.Factory.Create<HashSet<string>>(new object(), v => checkedValues.Add(v)),
        }, NestedFamily());

        await ActAsync(renderer, () => tree.Context.SelectNode("a"));
        await FlushAsync(renderer);
        Assert.Equal("a", selected[^1]);

        await ActAsync(renderer, () => tree.Context.ExpandNode("b"));
        await FlushAsync(renderer);
        Assert.Contains("b", expanded[^1]); // 每次状态变化都会回灌展开集

        await ActAsync(renderer, () => tree.Context.ToggleChecked("a1"));
        await FlushAsync(renderer);
        Assert.Contains("a1", checkedValues[^1]);
    }

    // ------------------------------------------------------------------
    // JSInvokable 入口与 interop 载荷
    // ------------------------------------------------------------------

    [Fact]
    public async Task 首次渲染应携带elementRef与dotNetRef发起treeKeyboard初始化()
    {
        var (renderer, js, tree) = await MountAsync(childContent: FlatFamily());

        var initialize = Single(js, "treeKeyboard.initialize");
        Assert.IsType<Microsoft.AspNetCore.Components.ElementReference>(initialize.Args[0]);
        var dotNetRef = Assert.IsType<DotNetObjectReference<PTree>>(initialize.Args[1]!);
        Assert.Equal(tree.Context.Id, initialize.Args[2]);

        // 释放沿：treeKeyboard.dispose 携带同一实例 id
        await renderer.Dispatcher.InvokeAsync(tree.DisposeAsync);
        var dispose = Single(js, "treeKeyboard.dispose");
        Assert.Equal(tree.Context.Id, dispose.Args[0]);
        dotNetRef.Dispose();
    }

    [Fact]
    public async Task JsOnNodeActivate应选中并可经ExpandOnClick翻面()
    {
        var clicks = new List<string>();
        var expands = new List<string>();
        var collapses = new List<string>();
        var (renderer, _, tree) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(PTree.SelectionMode)] = TreeSelectionMode.Single,
            [nameof(PTree.ExpandOnClick)] = true,
            [nameof(PTree.OnNodeClick)] = EventCallback.Factory.Create<string>(new object(), v => clicks.Add(v)),
            [nameof(PTree.OnNodeExpand)] = EventCallback.Factory.Create<string>(new object(), v => expands.Add(v)),
            [nameof(PTree.OnNodeCollapse)] = EventCallback.Factory.Create<string>(new object(), v => collapses.Add(v)),
        }, NestedFamily());

        await ActAsync(renderer, () => tree.JsOnNodeActivate("a", hasChildren: true));
        await FlushAsync(renderer);

        Assert.True(tree.Context.IsSelected("a"));
        Assert.Equal("a", tree.Context.FocusedNodeValue);
        Assert.True(tree.Context.IsExpanded("a")); // 点击有子节点 → 展开
        Assert.Equal(["a"], expands);
        Assert.Empty(collapses);
        Assert.Equal(["a"], clicks);

        // 再点一次 → 折叠
        await ActAsync(renderer, () => tree.JsOnNodeActivate("a", hasChildren: true));
        await FlushAsync(renderer);
        Assert.False(tree.Context.IsExpanded("a"));
        Assert.Equal(["a"], collapses);
    }

    [Fact]
    public async Task JsOnNodeExpand与Collapse应驱动上下文与回调()
    {
        var expands = new List<string>();
        var collapses = new List<string>();
        var (renderer, _, tree) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(PTree.OnNodeExpand)] = EventCallback.Factory.Create<string>(new object(), v => expands.Add(v)),
            [nameof(PTree.OnNodeCollapse)] = EventCallback.Factory.Create<string>(new object(), v => collapses.Add(v)),
        }, NestedFamily());

        await ActAsync(renderer, () => tree.JsOnNodeExpand("a"));
        await FlushAsync(renderer);
        Assert.True(tree.Context.IsExpanded("a"));
        Assert.Equal("a", tree.Context.FocusedNodeValue);
        Assert.Equal(["a"], expands);

        await ActAsync(renderer, () => tree.JsOnNodeCollapse("a"));
        await FlushAsync(renderer);
        Assert.False(tree.Context.IsExpanded("a"));
        Assert.Equal(["a"], collapses);
    }

    [Fact]
    public async Task JsOnNodeCheck应切换勾选且JsOnExpandSiblings应铺开兄弟()
    {
        var (renderer, _, tree) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(PTree.Checkable)] = true,
        }, NestedFamily());

        await ActAsync(renderer, () => tree.JsOnNodeCheck("a1"));
        await FlushAsync(renderer);
        Assert.True(tree.Context.IsChecked("a1"));

        await ActAsync(renderer, () => tree.JsOnExpandSiblings("a"));
        await FlushAsync(renderer);
        Assert.True(tree.Context.IsExpanded("a"));  // 自身有子级 → 铺开
        Assert.False(tree.Context.IsExpanded("b")); // 叶子兄弟不进展开集
    }
}
