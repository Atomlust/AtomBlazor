using AtomBlazor.Primitives;
using AtomBlazor.Primitives.Services;
using AtomBlazor.Primitives.Utilities;
using AtomBlazor.Tests.Rendering;
using AtomBlazor.Primitives.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AtomBlazor.Tests.Keyboard;

/// <summary>
/// KeyboardShortcutService 的注册/释放语义与 interop 调用契约测试，
/// 以及 KeyboardShortcut 纯逻辑（组合键解析、规范化、显示格式化、事件匹配）
/// 的表驱动单测。JS 侧只登记键串，按键归一化的另一半在 keyboard-shortcuts.js，
/// 两侧以 GetNormalizedKey 的输出为共同契约。
/// </summary>
public class KeyboardShortcutServiceTests
{
    private static (KeyboardShortcutService Service, NoopJavaScript Js) CreateService()
    {
        var js = new NoopJavaScript();
        return (new KeyboardShortcutService(js), js);
    }

    // ------------------------------------------------------------------
    // KeyboardShortcut.Parse：表驱动
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("Ctrl+N", "ctrl+n")]
    [InlineData("ctrl+s", "ctrl+s")]
    [InlineData("CONTROL+shift+T", "ctrl+shift+t")]
    [InlineData("Cmd+S", "ctrl+s")]                 // Mac Cmd 归入 Ctrl
    [InlineData("Command+S", "ctrl+s")]
    [InlineData("Shift+Delete", "shift+delete")]
    [InlineData("Alt+Left", "alt+arrowleft")]       // 方向键别名归一化
    [InlineData("Ctrl+Shift+Arrow-Up", "ctrl+shift+arrow-up")] // 带连字符的键名保持原样
    [InlineData("Opt+F1", "alt+f1")]
    [InlineData("Win+E", "meta+e")]
    [InlineData("Ctrl+Alt+Del", "ctrl+alt+delete")]
    [InlineData("Space", "space")]
    [InlineData("Ctrl+Space", "ctrl+space")]        // 空格键的三种写法之一
    [InlineData("Ctrl+Spacebar", "ctrl+space")]
    [InlineData("Esc", "escape")]
    [InlineData("Shift+Esc", "shift+escape")]
    [InlineData("Ctrl+Shift+P", "ctrl+shift+p")]
    [InlineData("Return", "enter")]
    [InlineData("Back", "backspace")]
    [InlineData("PgUp", "pageup")]
    public void Parse应把快捷键字符串规范化为稳定的比较键串(string input, string expectedNormalized)
    {
        var shortcut = KeyboardShortcut.Parse(input);

        Assert.Equal(expectedNormalized, shortcut.GetNormalizedKey());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Ctrl")]          // 只有修饰键
    [InlineData("Ctrl+Shift")]
    [InlineData("Ctrl+A+B")]      // 两个主键
    public void Parse对非法输入应抛ArgumentException(string? input) =>
        Assert.Throws<ArgumentException>(() => KeyboardShortcut.Parse(input!));

    [Theory]
    [InlineData("Ctrl+N", true)]
    [InlineData("bad+bad+bad+key+extra!", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void TryParse不应抛出并按结果填出参(string? input, bool shouldParse)
    {
        var ok = KeyboardShortcut.TryParse(input, out var shortcut);

        Assert.Equal(shouldParse, ok);
        Assert.Equal(shouldParse, shortcut is not null);
        if (shouldParse)
        {
            Assert.Equal("ctrl+n", shortcut!.GetNormalizedKey());
        }
    }

    [Theory]
    [InlineData("Ctrl+N", "Ctrl+N")]
    [InlineData("Ctrl+Shift+T", "Ctrl+Shift+T")]
    [InlineData("alt+enter", "Alt+Enter")]
    [InlineData("f1", "F1")]
    [InlineData("win+e", "Win+E")]
    [InlineData("ctrl+pageup", "Ctrl+Pageup")]
    public void ToDisplayString应产出人类可读形式(string input, string expectedDisplay) =>
        Assert.Equal(expectedDisplay, KeyboardShortcut.Parse(input).ToDisplayString());

    [Fact]
    public void ToDisplayString的Mac符号形态应使用Unicode修饰符()
    {
        var shortcut = KeyboardShortcut.Parse("Ctrl+Shift+Opt+Meta+T");

        Assert.Equal("\u2318\u21e7\u2325\u2303T", shortcut.ToDisplayString(useMacSymbols: true));
    }

    [Fact]
    public void 相等性应基于规范化键串而非书写形式()
    {
        var a = KeyboardShortcut.Parse("Ctrl+Shift+T");
        var b = KeyboardShortcut.Parse("control+SHIFT+t");

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.NotEqual(a, KeyboardShortcut.Parse("Ctrl+T"));
    }

    [Theory]
    [InlineData("Ctrl+S", "s", true, false, false, false, true)]   // Ctrl 命中
    [InlineData("Ctrl+S", "s", false, false, false, true, true)]   // Meta 等价（Mac Cmd）
    [InlineData("Ctrl+S", "S", true, false, false, false, true)]   // 主键不区分大小写
    [InlineData("Ctrl+S", "s", false, false, false, false, false)] // 缺修饰键
    [InlineData("Ctrl+S", "s", true, true, false, false, false)]   // 多余 Shift
    [InlineData("Shift+Delete", "Delete", false, true, false, false, true)]
    public void Matches应按修饰键组合与主键判定匹配(
        string shortcut, string key, bool ctrl, bool shift, bool alt, bool meta, bool expected) =>
        Assert.Equal(expected, KeyboardShortcut.Parse(shortcut).Matches(key, ctrl, shift, alt, meta));

    [Fact]
    public void ToString应等于默认显示字符串() =>
        Assert.Equal("Ctrl+N", KeyboardShortcut.Parse("Ctrl+N").ToString());

    // ------------------------------------------------------------------
    // 服务：注册与 interop 契约
    // ------------------------------------------------------------------

    [Fact]
    public async Task 首次注册应初始化模块并派发registerShortcut()
    {
        var (service, js) = CreateService();

        await service.RegisterAsync("Ctrl+S", () => Task.CompletedTask);

        Assert.Contains(js.Invocations, i => i.Identifier == "import");
        var initialize = Assert.Single(js.Invocations, i => i.Identifier == "keyboardShortcuts.initialize");
        Assert.NotNull(initialize.Args![0]); // DotNetObjectReference

        var register = Assert.Single(js.Invocations, i => i.Identifier == "keyboardShortcuts.registerShortcut");
        Assert.Equal("ctrl+s", register.Args![0]);
    }

    [Fact]
    public async Task 二次注册不应重复初始化且句柄释放应派发unregisterShortcut()
    {
        var (service, js) = CreateService();

        var first = await service.RegisterAsync("Ctrl+S", () => Task.CompletedTask);
        var second = await service.RegisterAsync("Ctrl+Shift+S", () => Task.CompletedTask);

        Assert.Single(js.Invocations, i => i.Identifier == "keyboardShortcuts.initialize");

        first.Dispose();
        var unregister = Assert.Single(js.Invocations, i => i.Identifier == "keyboardShortcuts.unregisterShortcut");
        Assert.Equal("ctrl+s", unregister.Args![0]);

        // 句柄释放幂等。
        first.Dispose();
        Assert.Single(js.Invocations, i => i.Identifier == "keyboardShortcuts.unregisterShortcut");

        second.Dispose();
    }

    [Fact]
    public async Task Unregister应按注册ID注销并派发对应键串()
    {
        var (service, js) = CreateService();

        await service.RegisterAsync("Ctrl+K", () => Task.CompletedTask, "command-palette");

        service.Unregister("command-palette");

        var unregister = Assert.Single(js.Invocations, i => i.Identifier == "keyboardShortcuts.unregisterShortcut");
        Assert.Equal("ctrl+k", unregister.Args![0]);

        // 未知 ID 静默无操作。
        service.Unregister("nope");
        Assert.Single(js.Invocations, i => i.Identifier == "keyboardShortcuts.unregisterShortcut");
    }

    [Fact]
    public async Task 同键串重复注册应覆盖旧登记且新回调生效()
    {
        var (service, _) = CreateService();
        var calls = new List<string>();

        await service.RegisterAsync("Ctrl+S", () => { calls.Add("old"); return Task.CompletedTask; }, "old");
        await service.RegisterAsync("Ctrl+S", () => { calls.Add("new"); return Task.CompletedTask; }, "new");

        await service.HandleShortcutAsync("ctrl+s");

        Assert.Equal(["new"], calls);
    }

    // ------------------------------------------------------------------
    // 服务：触发、挂起与非法输入
    // ------------------------------------------------------------------

    [Fact]
    public async Task HandleShortcutAsync应在命中时调用回调且对未命中与非法输入静默()
    {
        var (service, _) = CreateService();
        var hit = 0;
        await service.RegisterAsync("Ctrl+N", () => { hit++; return Task.CompletedTask; });

        await service.HandleShortcutAsync("ctrl+n");
        Assert.Equal(1, hit);

        await service.HandleShortcutAsync("ctrl+unset");   // 未登记
        await service.HandleShortcutAsync("");             // 空白
        await service.HandleShortcutAsync(new string('x', 51)); // 超长注入串
        Assert.Equal(1, hit);
    }

    [Fact]
    public async Task Suspend与Resume应暂停并恢复回调派发()
    {
        var (service, _) = CreateService();
        var hit = 0;
        await service.RegisterAsync("Ctrl+N", () => { hit++; return Task.CompletedTask; });

        Assert.False(service.IsSuspended);
        service.Suspend();
        Assert.True(service.IsSuspended);

        await service.HandleShortcutAsync("ctrl+n");
        Assert.Equal(0, hit);

        service.Resume();
        await service.HandleShortcutAsync("ctrl+n");
        Assert.Equal(1, hit);
    }

    [Fact]
    public async Task 回调抛错应被吞下而不传播回JS()
    {
        var (service, _) = CreateService();
        await service.RegisterAsync("Ctrl+N", () => throw new InvalidOperationException("boom"));

        await service.HandleShortcutAsync("ctrl+n"); // 不得外抛
    }

    [Fact]
    public async Task 非法快捷键串应在派发前抛出且不留登记()
    {
        var (service, js) = CreateService();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.RegisterAsync("Ctrl+Shift", () => Task.CompletedTask));

        Assert.DoesNotContain(js.Invocations, i => i.Identifier == "keyboardShortcuts.registerShortcut");
    }

    // ------------------------------------------------------------------
    // 服务：释放与 DI
    // ------------------------------------------------------------------

    [Fact]
    public async Task 释放应派发JS侧dispose并拒绝后续注册且二次释放不重复派发()
    {
        var (service, js) = CreateService();
        await service.RegisterAsync("Ctrl+S", () => Task.CompletedTask);

        await service.DisposeAsync();

        Assert.Single(js.Invocations, i => i.Identifier == "keyboardShortcuts.dispose");

        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            service.RegisterAsync("Ctrl+T", () => Task.CompletedTask));

        await service.DisposeAsync();
        Assert.Single(js.Invocations, i => i.Identifier == "keyboardShortcuts.dispose");
    }

    [Fact]
    public async Task 释放后挂起中的回调派发应静默()
    {
        var (service, _) = CreateService();
        var hit = 0;
        await service.RegisterAsync("Ctrl+N", () => { hit++; return Task.CompletedTask; });

        await service.DisposeAsync();
        await service.HandleShortcutAsync("ctrl+n");

        Assert.Equal(0, hit);
    }

    [Fact]
    public void AddAtomBlazorPrimitives应把快捷键服务注册为Scoped()
    {
        var services = new ServiceCollection();
        services.AddAtomBlazorPrimitives();

        var descriptor = Assert.Single(services, d => d.ServiceType == typeof(IKeyboardShortcutService));
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
        Assert.Equal(typeof(KeyboardShortcutService), descriptor.ImplementationType);
    }
}
