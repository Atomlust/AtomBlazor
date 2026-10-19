using AtomBlazor.Primitives;
using System.Reflection;
using AtomBlazor;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using Xunit;

namespace AtomBlazor.Tests.Components;

/// <summary>
/// 自写补全波：MarkdownEditor 组件的纯逻辑面——不启宿主、不经渲染器，直接实例化组件、
/// 反射驱动私有处理程序，断言参数到 HTML/CSS 的映射与回写语义：预览段的空值占位、
/// Markdown 转换与 XSS 净化、最小高度样式与 CSS 类合并、输入与 JSInvokable 回调的
/// Value/Changed 回写、标签切换与 GitHub 风格类串、Ctrl/Cmd 快捷键拦截位，以及
/// JS 模块未就绪时格式动作与释放路径的安静退场。
/// </summary>
/// <remarks>
/// 对本组件零测试；净化白名单面已由 Utilities/RichTextEditorSanitizerTests 件
/// 覆盖编辑器侧。方法名照 自写先例用中文。
/// </remarks>
public class MarkdownEditorTests
{
    private static readonly BindingFlags access = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    /// <summary>反射传 null 实参用：避免每次新建常量数组（CA1861）。</summary>
    private static readonly object?[] singleNull = [null];

    private static readonly string[] seenInput = ["改写后"];

    private static readonly string[] seenJsCallback = ["来自撤销"];

    private static string PrivateString(MarkdownEditor editor, string name)
    {
        var property = typeof(MarkdownEditor).GetProperty(name, access);
        Assert.NotNull(property);
        return (string)property!.GetValue(editor)!;
    }

    private static async Task InvokeAsync(MarkdownEditor editor, string name, params object?[] args)
    {
        var method = typeof(MarkdownEditor).GetMethod(name, access);
        Assert.NotNull(method);
        var result = method!.Invoke(editor, args);
        // 私有处理程序有 void 返回面（如 HandleTabChange），仅在 Task 时等待
        if (result is Task task)
        {
            await task;
        }
    }

    private static T PrivateField<T>(MarkdownEditor editor, string name)
    {
        var field = typeof(MarkdownEditor).GetField(name, access);
        Assert.NotNull(field);
        return (T)field!.GetValue(editor)!;
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public void 值为空或纯空白时预览渲染占位段落(string? value)
    {
        var editor = new MarkdownEditor { Value = value };

        var html = PrivateString(editor, "RenderedHtml");

        Assert.Equal("<p class=\"text-muted-foreground italic\">Nothing to preview</p>", html);
    }

    [Fact]
    public void 预览把markdown转成标题加粗与列表()
    {
        var editor = new MarkdownEditor { Value = "# 标题\n\n**粗** 与 *斜*\n\n- 甲\n- 乙" };

        var html = PrivateString(editor, "RenderedHtml");

        Assert.Contains("<h1>标题</h1>", html, StringComparison.Ordinal);
        Assert.Contains("<strong>粗</strong>", html, StringComparison.Ordinal);
        Assert.Contains("<em>斜</em>", html, StringComparison.Ordinal);
        Assert.Contains("<li>甲</li>", html, StringComparison.Ordinal);
    }

    [Fact]
    public void 预览剥除脚本与事件属性以防xss()
    {
        var editor = new MarkdownEditor { Value = "<script>alert(1)</script><img src=\"x\" onerror=\"alert(1)\">正文" };

        var html = PrivateString(editor, "RenderedHtml");

        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("onerror", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("正文", html, StringComparison.Ordinal);
    }

    [Fact]
    public void 最小高度样式仅在设置时下发()
    {
        var bare = new MarkdownEditor();
        Assert.Null(PrivateStringOrNull(bare, "MinHeightStyle"));

        var sized = new MarkdownEditor { MinHeight = "200px" };
        Assert.Equal("min-height:200px", PrivateString(sized, "MinHeightStyle"));
    }

    private static string? PrivateStringOrNull(MarkdownEditor editor, string name)
    {
        var property = typeof(MarkdownEditor).GetProperty(name, access);
        Assert.NotNull(property);
        return (string?)property!.GetValue(editor);
    }

    [Fact]
    public void 容器类并入附加类无效态与禁用态()
    {
        var plain = PrivateString(new MarkdownEditor(), "ContainerCssClass");
        Assert.Contains("atom:border-input", plain, StringComparison.Ordinal);

        var invalid = PrivateString(new MarkdownEditor { AriaInvalid = true, Class = "extra" }, "ContainerCssClass");
        Assert.Contains("atom:border-destructive", invalid, StringComparison.Ordinal);
        Assert.Contains("extra", invalid, StringComparison.Ordinal);

        var disabled = PrivateString(new MarkdownEditor { Disabled = true }, "ContainerCssClass");
        Assert.Contains("atom:opacity-50", disabled, StringComparison.Ordinal);
    }

    [Fact]
    public void 默认一百五十像素高度且自定义后不再内置()
    {
        var bareTextarea = PrivateString(new MarkdownEditor(), "TextareaCssClass");
        var barePreview = PrivateString(new MarkdownEditor(), "PreviewCssClass");
        Assert.Contains("atom:min-h-[150px]", bareTextarea, StringComparison.Ordinal);
        Assert.Contains("atom:min-h-[150px]", barePreview, StringComparison.Ordinal);

        var sized = new MarkdownEditor { MinHeight = "20rem" };
        Assert.DoesNotContain("atom:min-h-[150px]", PrivateString(sized, "TextareaCssClass"), StringComparison.Ordinal);
        Assert.DoesNotContain("atom:min-h-[150px]", PrivateString(sized, "PreviewCssClass"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task 输入事件回写值并触发变更回调()
    {
        var seen = new List<string?>();
        var editor = new MarkdownEditor
        {
            ValueChanged = EventCallback.Factory.Create<string?>(new object(), v => seen.Add(v)),
        };

        await InvokeAsync(editor, "HandleInput", new ChangeEventArgs { Value = "改写后" });

        Assert.Equal("改写后", editor.Value);
        Assert.Equal(seenInput, seen);

        // 无回调时仅改值、不抛异常
        var bare = new MarkdownEditor();
        await InvokeAsync(bare, "HandleInput", new ChangeEventArgs { Value = null });
        Assert.Null(bare.Value);
    }

    [Fact]
    public async Task JSInvokable内容变更回调同样回写()
    {
        var seen = new List<string?>();
        var editor = new MarkdownEditor
        {
            ValueChanged = EventCallback.Factory.Create<string?>(new object(), v => seen.Add(v)),
        };

        await editor.OnContentChanged("来自撤销");

        Assert.Equal("来自撤销", editor.Value);
        Assert.Equal(seenJsCallback, seen);
    }

    [Fact]
    public async Task 标签切换空值保旧值且类串随激活态翻转()
    {
        var editor = new MarkdownEditor();

        await InvokeAsync(editor, "HandleTabChange", singleNull);
        Assert.Equal("write", PrivateField<string>(editor, "_activeTab"));

        await InvokeAsync(editor, "HandleTabChange", "preview");
        Assert.Equal("preview", PrivateField<string>(editor, "_activeTab"));

        Assert.Contains("atom:bg-background", PrivateGetTabClass(editor, "preview"), StringComparison.Ordinal);
        var inactive = PrivateGetTabClass(editor, "write");
        Assert.Contains("atom:bg-transparent", inactive, StringComparison.Ordinal);
        Assert.Contains("atom:text-muted-foreground", inactive, StringComparison.Ordinal);
    }

    private static string PrivateGetTabClass(MarkdownEditor editor, string tabValue)
    {
        var method = typeof(MarkdownEditor).GetMethod("GetTabClass", access);
        Assert.NotNull(method);
        return (string)method!.Invoke(editor, new object[] { tabValue })!;
    }

    [Theory]
    [InlineData("b")]
    [InlineData("i")]
    [InlineData("u")]
    public async Task 控制键加粗斜体下划线快捷键拦截键默认(string key)
    {
        var editor = new MarkdownEditor();

        await InvokeAsync(editor, "HandleKeyDown", new KeyboardEventArgs { Key = key, CtrlKey = true });

        Assert.True(PrivateField<bool>(editor, "_shouldPreventKeydown"));
    }

    [Theory]
    [InlineData("b", false, false)]
    [InlineData("x", true, false)]
    [InlineData("Enter", false, false)]
    public async Task 非快捷键组合不拦截键默认(string key, bool ctrl, bool expected)
    {
        var editor = new MarkdownEditor();

        await InvokeAsync(editor, "HandleKeyDown", new KeyboardEventArgs { Key = key, CtrlKey = ctrl });

        Assert.Equal(expected, PrivateField<bool>(editor, "_shouldPreventKeydown"));
    }

    [Theory]
    [InlineData("ApplyBold")]
    [InlineData("ApplyItalic")]
    [InlineData("ApplyUnderline")]
    [InlineData("ApplyBulletList")]
    [InlineData("ApplyNumberedList")]
    public async Task JS模块未就绪时格式动作安静退场(string method)
    {
        var editor = new MarkdownEditor { Value = "原文" };

        await InvokeAsync(editor, method);

        Assert.Equal("原文", editor.Value);
    }

    [Fact]
    public async Task 未初始化时释放安全完成()
    {
        var editor = new MarkdownEditor();

        await editor.DisposeAsync();

        Assert.Null(PrivateField<IJSObjectReference?>(editor, "_module"));
    }
}
