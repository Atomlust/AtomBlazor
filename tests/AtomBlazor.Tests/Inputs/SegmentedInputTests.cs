using AtomBlazor.Primitives;
using System.Globalization;
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

// CS0618：EditContext.Validate() 在 .NET 11 已标记过时（建议 ValidateAsync）。
// 用例正是断言同步 Validate() 的返回值为测试目的本身，故保留语句本体，仅在此文件抑制该诊断。
#pragma warning disable CS0618
namespace AtomBlazor.Tests.Inputs;

public class SegmentedInputTests
{
    [Theory]
    [InlineData("en-US", "Month,Day,Year")]
    [InlineData("en-GB", "Day,Month,Year")]
    [InlineData("ja-JP", "Year,Month,Day")]
    public void DateSegmentsFollowCulture(string culture, string expected) =>
        Assert.Equal(expected, string.Join(",", DateTimeSegments.DateOrder(CultureInfo.GetCultureInfo(culture))));

    [Fact]
    public void DatePatternLiteralsDoNotBecomeSegments()
    {
        var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        culture.DateTimeFormat.ShortDatePattern = "yyyy 'day' MM/dd";
        Assert.Equal(["Year", "Month", "Day"], DateTimeSegments.DateOrder(culture));
    }

    [Fact]
    public void DatesRespectLeapYearsAndInclusiveBounds()
    {
        Assert.Equal(new DateTime(2024, 2, 29), DateTimeSegments.Date("2024", "02", "29", null, null));
        Assert.Null(DateTimeSegments.Date("2025", "02", "29", null, null));
        Assert.Null(DateTimeSegments.Date("20", "02", "28", null, null));
        var limit = new DateTime(2024, 2, 29, 18, 0, 0);
        Assert.Equal(limit.Date, DateTimeSegments.Date("2024", "02", "29", limit, limit));
        Assert.Null(DateTimeSegments.Date("2024", "03", "01", null, limit));
    }

    [Fact]
    public void MidnightNoonAndBoundsAreUnambiguous()
    {
        Assert.Equal(TimeSpan.Zero, DateTimeSegments.Time("12", "00", "", false, true, false, null, null));
        Assert.Equal(TimeSpan.FromHours(12), DateTimeSegments.Time("12", "00", "", false, true, true, null, null));
        Assert.Null(DateTimeSegments.Time("24", "00", "", false, false, false, null, null));
        Assert.Null(DateTimeSegments.Time("11", "59", "", false, true, true, null, TimeSpan.FromHours(17)));
        Assert.Null(DateTimeSegments.Time("11", "00", "", true, false, false, null, null));
    }

    [Fact]
    public async Task InvalidDateRetainsDraftClearsBoundValueAndBlocksFormValidation()
    {
        await using var provider = Services().BuildServiceProvider();
        await using var renderer = new ComponentTestRenderer(provider, NullLoggerFactory.Instance);
        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var model = new Model { Date = new DateTime(2024, 2, 29) };
            var context = new EditContext(model);
            var notifications = 0;
            context.OnFieldChanged += (_, _) => notifications++;
            RenderFragment content = builder =>
            {
                builder.OpenComponent<DateInput>(0);
                builder.AddAttribute(1, nameof(DateInput.Value), model.Date);
                builder.AddAttribute(2, nameof(DateInput.ValueExpression), (Expression<Func<DateTime?>>)(() => model.Date));
                builder.AddAttribute(3, nameof(DateInput.ValueChanged), EventCallback.Factory.Create<DateTime?>(model, v => model.Date = v));
                builder.AddAttribute(4, nameof(DateInput.ShowCalendar), false);
                builder.CloseComponent();
            };
            await renderer.MountAsync<CascadingValue<EditContext>>(new()
            {
                [nameof(CascadingValue<EditContext>.Value)] = context,
                [nameof(CascadingValue<EditContext>.ChildContent)] = content
            });
            var input = renderer.FindComponent<DateInput>();
            await (Task)ComponentProbe.Call(input, "EditAsync", "Year", "2025")!;
            Assert.Null(model.Date);
            Assert.False(context.Validate());
            Assert.Equal("29", ComponentProbe.Field<Dictionary<string, string>>(input, "parts")["Day"]);
            await input.SetParametersAsync(ParameterView.Empty);
            Assert.Equal("2025", ComponentProbe.Field<Dictionary<string, string>>(input, "parts")["Year"]);
            await (Task)ComponentProbe.Call(input, "EditAsync", "Day", "28")!;
            Assert.Equal(new DateTime(2025, 2, 28), model.Date);
            Assert.True(context.Validate());
            await input.ClearAsync();
            Assert.Null(model.Date);
            Assert.True(context.Validate());
            Assert.Equal(3, notifications);
        });
    }

    [Fact]
    public async Task TimeFormatChangesKeepTheInstantAndDisabledInputRejectsEdits()
    {
        await using var provider = Services().BuildServiceProvider();
        await using var renderer = new ComponentTestRenderer(provider, NullLoggerFactory.Instance);
        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var input = await renderer.MountAsync<TimeInput>(new()
            {
                [nameof(TimeInput.Value)] = new TimeSpan(23, 15, 30),
                [nameof(TimeInput.Format)] = TimeFormat.Hour12,
                [nameof(TimeInput.ShowSeconds)] = true,
                [nameof(TimeInput.ShowPicker)] = false
            });
            Assert.Equal("11", ComponentProbe.Field<Dictionary<string, string>>(input, "parts")["Hour"]);
            await input.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string, object?> { [nameof(TimeInput.Format)] = TimeFormat.Hour24 }));
            Assert.Equal("23", ComponentProbe.Field<Dictionary<string, string>>(input, "parts")["Hour"]);
            await (Task)ComponentProbe.Call(input, "EditAsync", "Second", "45")!;
            Assert.Equal(new TimeSpan(23, 15, 45), input.Value);
            await input.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string, object?> { [nameof(TimeInput.Disabled)] = true }));
            await (Task)ComponentProbe.Call(input, "EditAsync", "Hour", "04")!;
            await input.ClearAsync();
            Assert.Equal(new TimeSpan(23, 15, 45), input.Value);
        });
    }

    [Theory]
    [InlineData(0, "AM", "PM", 12)]
    [InlineData(12, "PM", "AM", 0)]
    [InlineData(14, "PM", "AM", 2)]
    public async Task PeriodSelectReflectsTheBoundTimeAndPreservesMinutes(int hour, string initialPeriod, string selectedPeriod, int expectedHour)
    {
        await using var provider = Services().BuildServiceProvider();
        await using var renderer = new ComponentTestRenderer(provider, NullLoggerFactory.Instance);
        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            TimeSpan? changed = null;
            var input = await renderer.MountAsync<TimeInput>(new()
            {
                [nameof(TimeInput.Value)] = new TimeSpan(hour, 30, 0),
                [nameof(TimeInput.Format)] = TimeFormat.Hour12,
                [nameof(TimeInput.ShowPicker)] = false,
                [nameof(TimeInput.ValueChanged)] = EventCallback.Factory.Create<TimeSpan?>(this, value => changed = value)
            });
            var select = renderer.FindComponent<Select<string>>();
            Assert.Equal(initialPeriod, select.Value);
            await select.ValueChanged.InvokeAsync(selectedPeriod);
            Assert.Equal(new TimeSpan(expectedHour, 30, 0), input.Value);
            Assert.Equal(input.Value, changed);

            await input.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(TimeInput.ReadOnly)] = true
            }));
            Assert.True(select.Disabled);
            await select.ValueChanged.InvokeAsync(initialPeriod);
            Assert.Equal(new TimeSpan(expectedHour, 30, 0), input.Value);
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

    private sealed class Model
    {
        public DateTime? Date { get; set; }
    }
}
