using AtomBlazor.Primitives;
using System.Reflection;
using AtomBlazor;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;

namespace AtomBlazor.Tests.Rendering;

/// <summary>
/// 自 Rendering/ApiAuditRegressionTests.cs（七规则机械改名：<c></c>→<c>Xxx</c>
/// 余下两处为 <c></c>→<c>AtomBlazor</c>、<c>AddComponents</c>→<c>AddAtomBlazorComponents</c>）。
/// 回归 API 审计暴露的缺陷面：包装件把 <c>aria-*</c> 送达真实控件、日期选择器的越界步长与被禁日期、
/// 星期名缓存失效、受控/非受控状态、Dock 面板 Id 约束与装饰图回退。
/// 断言文本逐行照搬，不做品牌改名带来的宽松化。
/// </summary>
public class ApiAuditRegressionTests
{
    private static readonly int[] replacementPageSizes = [42];
    [Theory]
    [InlineData("Combobox", "button")]
    [InlineData("DatePicker", "button")]
    [InlineData("DateRangePicker", "button")]
    [InlineData("DateTimePicker", "button")]
    [InlineData("TimePicker", "button")]
    [InlineData("MultiSelect", "button")]
    [InlineData("Select", "button")]
    [InlineData("NativeSelect", "select")]
    [InlineData("CheckboxGroup", "div")]
    [InlineData("FileUpload", "input")]
    [InlineData("InputOTP", "input")]
    public async Task WrapperNamesReachTheActualControl(string name, string tag)
    {
        var types = new Dictionary<string, Type>
        {
            ["Combobox"] = typeof(FormFieldCombobox<string>),
            ["DatePicker"] = typeof(FormFieldDatePicker),
            ["DateRangePicker"] = typeof(FormFieldDateRangePicker),
            ["DateTimePicker"] = typeof(FormFieldDateTimePicker),
            ["TimePicker"] = typeof(FormFieldTimePicker),
            ["MultiSelect"] = typeof(FormFieldMultiSelect<string>),
            ["Select"] = typeof(FormFieldSelect<string>),
            ["NativeSelect"] = typeof(FormFieldNativeSelect<string>),
            ["CheckboxGroup"] = typeof(FormFieldCheckboxGroup<string>),
            ["FileUpload"] = typeof(FormFieldFileUpload),
            ["InputOTP"] = typeof(FormFieldInputOTP)
        };
        await Run(async renderer =>
        {
            var parameters = new Dictionary<string, object?> { ["AriaLabel"] = "Audit label" };
            if (name == "Select")
            {
                parameters["Options"] = new[] { new SelectOption<string>("one", "One") };
            }
            await renderer.MountTypeAsync(types[name], parameters);
            Assert.Matches($"<{tag}\\b[^>]*aria-label=\"Audit label\"", renderer.Markup());
        });
    }

    [Fact]
    public async Task EditableDateValidationReachesTheInput()
    {
        await Run(async renderer =>
        {
            await renderer.MountAsync<FormFieldDatePicker>(new()
            {
                ["Editable"] = true, ["AriaLabel"] = "Date", ["ErrorText"] = "Choose a date"
            });
            Assert.Matches("<input\\b[^>]*aria-invalid=\"true\"", renderer.Markup());
        });
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(60)]
    public async Task DateTimePickerRejectsInvalidSteps(int step)
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => Run(async renderer =>
            await renderer.MountAsync<DateTimePicker>(new() { ["MinuteStep"] = step })));
    }

    [Theory]
    [InlineData("MinDate")]
    [InlineData("MaxDate")]
    [InlineData("DisabledDates")]
    public async Task NowAndEmptyTimeSteppersCannotChooseBlockedDates(string restriction)
    {
        await Run(async renderer =>
        {
            var changes = 0;
            var parameters = new Dictionary<string, object?>
            {
                ["ValueChanged"] = EventCallback.Factory.Create<DateTime?>(this, _ => changes++)
            };
            parameters[restriction] = restriction switch
            {
                "MinDate" => DateTime.Today.AddDays(1),
                "MaxDate" => DateTime.Today.AddDays(-1),
                _ => (Func<DateTime, bool>)(_ => true)
            };
            var picker = await renderer.MountAsync<DateTimePicker>(parameters);
            await Invoke(picker, "SetNow");
            await Invoke(picker, "IncrementMinute");
            Assert.Null(picker.Value);
            Assert.Equal(0, changes);
        });
    }

    [Theory]
    [InlineData("Calendar")]
    [InlineData("DateRangePicker")]
    public async Task ReplacingDayNamesRefreshesTheCache(string name)
    {
        await Run(async renderer =>
        {
            var type = typeof(Calendar).Assembly.GetType($"AtomBlazor.{name}")!;
            var component = await renderer.MountTypeAsync(type, new() { ["CustomDayNames"] = Enumerable.Repeat("old", 7).ToArray() });
            var property = type.GetProperty("DayNames", BindingFlags.Instance | BindingFlags.NonPublic)!;
            Assert.All((string[])property.GetValue(component)!, day => Assert.Equal("old", day));
            await component.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                ["CustomDayNames"] = Enumerable.Repeat("new", 7).ToArray()
            }));
            Assert.All((string[])property.GetValue(component)!, day => Assert.Equal("new", day));
        });
    }

    [Fact]
    public void UnvaluedNavigationContextIsClosed()
    {
        using var root = new NavigationMenuContext(() => { }, true);
        Assert.False(new NavigationMenuItemContext(root, null).IsOpen);
    }

    [Fact]
    public async Task PaginationTemplateReceivesReplacementStateAndOptions()
    {
        await Run(async renderer =>
        {
            var pagination = await renderer.MountAsync<AtomBlazor.Primitives.TablePagination<object>>(new()
            {
                ["State"] = new PaginationState(),
                ["PaginationTemplate"] = (RenderFragment<AtomBlazor.Primitives.PaginationContext>)(context => builder =>
                    builder.AddContent(0, $"{context.State.PageSize}:{context.PageSizeOptions[0]}:{context.ShowPageInfo}"))
            });
            await pagination.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                ["State"] = new PaginationState { PageSize = 42 }, ["PageSizeOptions"] = replacementPageSizes, ["ShowPageInfo"] = false
            }));
            Assert.Contains("42:42:False", renderer.Markup(), StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task RequiredChipCannotBeDismissed()
    {
        await Run(async renderer =>
        {
            var set = await renderer.MountAsync<ChipSet<string>>(new() { ["Required"] = true, ["Value"] = "only" });
            await Invoke(set, "DismissCoreAsync", "only");
            var selected = typeof(ChipSet<string>).GetMethod("IsSelectedCore", BindingFlags.NonPublic | BindingFlags.Instance)!;
            Assert.True((bool)selected.Invoke(set, ["only"])!);
        });
    }

    [Fact]
    public async Task HeadingLevelChangesSemanticsWithoutChangingDefault()
    {
        await Run(async renderer =>
        {
            var heading = await renderer.MountAsync<SectionHeader>(new() { ["Title"] = "Nested section" });
            Assert.Contains("<h2", renderer.Markup(), StringComparison.Ordinal);
            await heading.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string, object?> { ["HeadingLevel"] = 3 }));
            Assert.Contains("<h3", renderer.Markup(), StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task ContextMenuReadsControlledStateAndReportsUncontrolledChanges()
    {
        await Run(async renderer =>
        {
            var reports = new List<bool>();
            var menu = await renderer.MountAsync<AtomBlazor.Primitives.ContextMenuPrimitive>(new()
            {
                ["OpenChanged"] = EventCallback.Factory.Create<bool>(this, reports.Add)
            });
            await menu.OpenAt(12, 34);
            await menu.Close();
            Assert.Collection(reports, value => Assert.True(value), value => Assert.False(value));
            await menu.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string, object?> { ["Open"] = true }));
            Assert.True(menu.IsOpen);
            await menu.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string, object?> { ["Open"] = false }));
            Assert.False(menu.IsOpen);
        });
    }

    [Theory]
    [InlineData("")]
    [InlineData("same")]
    public async Task DockRejectsMissingAndDuplicatePanelIds(string id)
    {
        var exception = await Record.ExceptionAsync(() => Run(async renderer =>
            await renderer.MountAsync<Dock>(new()
            {
                ["ChildContent"] = (RenderFragment)(builder =>
                {
                    builder.OpenComponent<DockPanel>(0);
                    builder.AddAttribute(1, "Id", id);
                    builder.CloseComponent();
                    builder.OpenComponent<DockPanel>(2);
                    builder.AddAttribute(3, "Id", id);
                    builder.CloseComponent();
                })
            })));
        if (id.Length == 0)
        {
            Assert.IsType<InvalidOperationException>(exception);
            Assert.Contains("non-empty", exception.Message, StringComparison.Ordinal);
        }
        else
        {
            Assert.IsType<InvalidOperationException>(exception);
            Assert.Contains("already registered", exception.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task DecorativeImageFallbackIsHiddenAndKeepsAttributes()
    {
        await Run(async renderer =>
        {
            var image = await renderer.MountAsync<Image>(new()
            {
                ["Alt"] = "", ["AdditionalAttributes"] = new Dictionary<string, object> { ["data-testid"] = "fallback" }
            });
            await Invoke(image, "HandleError");
            await image.SetParametersAsync(ParameterView.Empty);
            var markup = renderer.Markup();
            Assert.Contains("aria-hidden=\"true\"", markup, StringComparison.Ordinal);
            Assert.Contains("data-testid=\"fallback\"", markup, StringComparison.Ordinal);
            Assert.DoesNotMatch("<div\\b[^>]*role=\"img\"", markup);
        });
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("_self", false)]
    [InlineData("_blank", true)]
    public async Task ExternalIconOnlyAnnouncesANewTabWhenOneWillOpen(string? target, bool announces)
    {
        await Run(async renderer =>
        {
            await renderer.MountAsync<Link>(new() { ["Href"] = "/docs", ["Target"] = target, ["ShowExternalIcon"] = true });
            Assert.Equal(announces, renderer.Markup().Contains("opens in a new tab", StringComparison.Ordinal));
        });
    }

    private static Task Invoke(object target, string method, params object?[] args) =>
        (Task)target.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(target, args)!;

    private static async Task Run(Func<ComponentTestRenderer, Task> test)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton<IJSRuntime, NoopJavaScript>();
        services.AddAtomBlazorComponents();
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new ComponentTestRenderer(provider, NullLoggerFactory.Instance);
        await renderer.Dispatcher.InvokeAsync(() => test(renderer));
    }
}
