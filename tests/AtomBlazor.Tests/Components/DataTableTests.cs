using AtomBlazor.Primitives;
using System.Collections.Generic;
using AtomBlazor;
using AtomBlazor.Primitives.Extensions;
using AtomBlazor.Tests.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Xunit;

namespace AtomBlazor.Tests.Components;

/// <summary>
/// DataTable 组件层测试。 ui/tests 的 DataTable 断言依赖 FilterBuilder
/// 无法机械，故覆盖可独立验证的渲染契约与纯逻辑面：列注册与渲染、数据行渲染、空/加载态、
/// 选择模式映射（复选框出现）、工具栏与全局搜索、列可见性切换回调、CellTemplate 自定义单元。
/// </summary>
public class DataTableTests
{
    private sealed class Person
    {
        public string Name { get; set; } = "";

        public int Age { get; set; }
    }

    private static List<Person> Sample() => new()
    {
        new Person { Name = "Charlie", Age = 30 },
        new Person { Name = "Alice", Age = 25 },
        new Person { Name = "Bob", Age = 35 },
    };

    private static ComponentTestRenderer CreateRenderer()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(new NoopJavaScript());
        // 静默日志：为组件内 [Inject] ILogger<T> 提供空实现。
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddAtomBlazorPrimitives();
        services.AddAtomBlazorComponents();
        return new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance);
    }

    private static RenderFragment ColumnsFragment(bool withCellTemplate = false) => builder =>
    {
        builder.OpenComponent<DataTableColumn<Person, string>>(0);
        builder.AddAttribute(1, nameof(DataTableColumn<Person, string>.Header), "Name");
        // 注意：TValue 为无约束泛型，Property 的 CLR 类型为 Func<Person,string>（注解型 ? 不装箱）
        // 故此处显式以非可空委托传入，与运行期真实类型一致。
        builder.AddAttribute(2, nameof(DataTableColumn<Person, string>.Property), (Func<Person, string>)(p => p.Name));
        builder.AddAttribute(3, nameof(DataTableColumn<Person, string>.Sortable), true);
        builder.CloseComponent();

        builder.OpenComponent<DataTableColumn<Person, int>>(10);
        builder.AddAttribute(11, nameof(DataTableColumn<Person, int>.Header), "Age");
        builder.AddAttribute(12, nameof(DataTableColumn<Person, int>.Property), (Func<Person, int>)(p => p.Age));
        builder.AddAttribute(13, nameof(DataTableColumn<Person, int>.Sortable), true);
        if (withCellTemplate)
        {
            builder.AddAttribute(14, nameof(DataTableColumn<Person, int>.CellTemplate),
                (RenderFragment<Person>)(p => b => b.AddContent(0, $"AGE:{p.Age}")));
        }
        builder.CloseComponent();
    };

    private static async Task<ComponentTestRenderer> MountAsync(
        Dictionary<string, object?>? parameters = null, bool withCellTemplate = false)
    {
        var renderer = CreateRenderer();
        var p = parameters ?? new Dictionary<string, object?>();
        p.TryAdd(nameof(DataTable<Person>.Data), Sample());
        p.TryAdd(nameof(DataTable<Person>.Columns), ColumnsFragment(withCellTemplate));
        await renderer.MountAsync<DataTable<Person>>(p);
        // 列组件在父级首次渲染末尾的 @Columns 中注册；再触发一次重渲染以让
        // 已注册的 _columns 参与行/表头渲染（等价于真实应用中参数变化沿）。
        await renderer.RerenderRootAsync(0, new Dictionary<string, object?>());
        return renderer;
    }

    [Fact]
    public async Task 数据行与列头应随数据渲染()
    {
        var renderer = await MountAsync();
        var markup = renderer.Markup();

        Assert.Contains("Charlie", markup);
        Assert.Contains("Alice", markup);
        Assert.Contains("Bob", markup);
        // 列头文案
        Assert.Contains("Name", markup);
        Assert.Contains("Age", markup);
        // 单元指纹（atom: 前缀 Tailwind 类）
        Assert.Contains("atom:caption-bottom", markup);
    }

    [Fact]
    public async Task 无选择模式时不应出现复选框()
    {
        var renderer = await MountAsync();
        var markup = renderer.Markup();
        // 复选框由 渲染为 role=checkbox
        Assert.DoesNotContain("role=\"checkbox\"", markup);
    }

    [Fact]
    public async Task 多选模式应渲染表头与行内复选框()
    {
        var renderer = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(DataTable<Person>.SelectionMode)] = DataTableSelectionMode.Multiple,
        });
        var markup = renderer.Markup();
        Assert.Contains("role=\"checkbox\"", markup);
    }

    [Fact]
    public async Task 空数据应回退到空状态()
    {
        var renderer = CreateRenderer();
        var p = new Dictionary<string, object?>
        {
            [nameof(DataTable<Person>.Data)] = new List<Person>(),
            [nameof(DataTable<Person>.Columns)] = ColumnsFragment(),
        };
        await renderer.MountAsync<DataTable<Person>>(p);
        var markup = renderer.Markup();
        Assert.Contains("No results", markup);
    }

    [Fact]
    public async Task 加载态应渲染加载占位()
    {
        var renderer = CreateRenderer();
        var p = new Dictionary<string, object?>
        {
            [nameof(DataTable<Person>.Data)] = Sample(),
            [nameof(DataTable<Person>.Columns)] = ColumnsFragment(),
            [nameof(DataTable<Person>.IsLoading)] = true,
        };
        await renderer.MountAsync<DataTable<Person>>(p);
        var markup = renderer.Markup();
        Assert.Contains("Loading", markup);
    }

    [Fact]
    public async Task 工具栏应提供全局搜索输入框与列切换()
    {
        var renderer = await MountAsync();
        var markup = renderer.Markup();
        // 搜索输入框占位（Input 默认 type=text，占位来自 Localizer["DataTable.Search"]）
        Assert.Contains("Search...", markup);
        // 列切换按钮（PopoverTrigger 内的 Button）
        Assert.Contains("Columns", markup);
    }

    [Fact]
    public async Task CellTemplate应自定义单元内容()
    {
        var renderer = await MountAsync(withCellTemplate: true);
        var markup = renderer.Markup();
        Assert.Contains("AGE:30", markup);
        Assert.Contains("AGE:25", markup);
        Assert.Contains("AGE:35", markup);
    }
}
