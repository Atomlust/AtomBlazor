using AtomBlazor.Primitives.Utilities;
using Microsoft.AspNetCore.Components;
using Xunit;

namespace AtomBlazor.Tests.Primitives;

/// <summary>
/// UseControllableState 的 null 语义：默认值只负责初始化非受控状态，
/// 受控 null（清空选择）与非受控显式写入的 null 都必须原样生效；
/// 受控判定只依据显式标志或变化回调，绝不依据值是否为 null。
/// </summary>
public class UseControllableStateTests
{
    private static EventCallback<T> Callback<T>(Action<T> handler) =>
        EventCallback.Factory.Create<T>(new object(), handler);

    [Fact]
    public void 受控null应原样生效而非回落到默认值()
    {
        var state = new UseControllableState<string?>("默认")
        {
            ControlledValue = null,
            IsControlled = true,
        };

        Assert.Null(state.Value);
    }

    [Fact]
    public async Task 非受控清空应生效而非回落到默认值()
    {
        var state = new UseControllableState<string?>("默认");
        Assert.Equal("默认", state.Value);

        await state.SetValueAsync(null);

        Assert.Null(state.Value);
    }

    [Fact]
    public async Task 重置只影响非受控状态()
    {
        var uncontrolled = new UseControllableState<string?>("默认");
        await uncontrolled.SetValueAsync("新值");
        uncontrolled.Reset();
        Assert.Equal("默认", uncontrolled.Value);

        var controlled = new UseControllableState<string?>("默认")
        {
            ControlledValue = "父级",
            IsControlled = true,
        };
        controlled.Reset();
        Assert.Equal("父级", controlled.Value);
    }

    [Fact]
    public async Task 受控SetValue只上报不写回内部值()
    {
        var reported = "未触发";
        var state = new UseControllableState<string?>("默认")
        {
            ControlledValue = "父级",
            IsControlled = true,
            OnValueChanged = Callback<string?>(value => reported = value ?? "null"),
        };

        await state.SetValueAsync("子级");

        Assert.Equal("父级", state.Value);
        Assert.Equal("子级", reported);
    }

    [Fact]
    public void Create应依据回调而非值是否为null推断受控()
    {
        var withCallback = UseControllableStateExtensions.Create<string?>(
            controlledValue: null,
            onValueChanged: Callback<string?>(_ => { }),
            defaultValue: "默认");

        Assert.True(withCallback.IsControlled);
        Assert.Null(withCallback.Value);

        var withoutCallback = UseControllableStateExtensions.Create<string?>(
            controlledValue: "父级",
            onValueChanged: default,
            defaultValue: "默认");

        Assert.False(withoutCallback.IsControlled);
        Assert.Equal("默认", withoutCallback.Value);
    }

    [Fact]
    public void 显式受控标志应优先于回调推断()
    {
        var state = UseControllableStateExtensions.Create<string?>(
            controlledValue: "父级",
            onValueChanged: Callback<string?>(_ => { }),
            defaultValue: "默认",
            isControlled: false);

        Assert.False(state.IsControlled);
        Assert.Equal("默认", state.Value);
    }
}
