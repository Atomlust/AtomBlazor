using AtomBlazor.Primitives;
using System.Reflection;
using AtomBlazor;
using Ganss.Xss;
using Xunit;

namespace AtomBlazor.Tests.Utilities;

/// <summary>
/// 自 Utilities/RichTextEditorSanitizerTests.cs（七规则机械改名 →AtomBlazor、
/// →RichTextEditor、注释汉化；断言字符串字面量逐字节保原样，方法名保英）。
/// <para>
/// <c>RichTextEditor</c> 会把每一个传入的 <c>Value</c> 过一遍 <c>HtmlSanitizer</c>。
/// 它的默认规则会剥掉 Quill 往返文档所需的内容：检查清单标记与数据 URL 图片。
/// 这些测试钉住所添加的例外，并保证例外保持在预期的窄范围内。
/// </para>
/// </summary>
public class RichTextEditorSanitizerTests
{
    private static readonly HtmlSanitizer sanitizer = ResolveSanitizer();

    private static HtmlSanitizer ResolveSanitizer()
    {
        var field = typeof(RichTextEditor).GetField("Sanitizer", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.False(field is null, " no longer has a private static `Sanitizer` field; update this test to reach the editor's sanitizer.");
        return (HtmlSanitizer)field!.GetValue(null)!;
    }

    [Fact]
    public void KeepsChecklistState() =>
        Assert.Contains("data-list=\"checked\"", sanitizer.Sanitize("<ul><li data-list=\"checked\">done</li></ul>"), StringComparison.Ordinal);

    [Fact]
    public void KeepsDataUrlImages()
    {
        const string html = "<p><img src=\"data:image/png;base64,iVBORw0KGgo=\"></p>";
        Assert.Contains("src=\"data:image/png;base64,iVBORw0KGgo=\"", sanitizer.Sanitize(html), StringComparison.Ordinal);
    }

    [Fact]
    public void DropsNonImageDataUrls() =>
        Assert.DoesNotContain("data:", sanitizer.Sanitize("<p><img src=\"data:text/html;base64,PHNjcmlwdD4=\"></p>"), StringComparison.Ordinal);

    [Fact]
    public void DropsDataUrlLinks() =>
        Assert.DoesNotContain("data:", sanitizer.Sanitize("<a href=\"data:image/svg+xml,%3Csvg%3E%3C/svg%3E\">x</a>"), StringComparison.Ordinal);

    [Fact]
    public void KeepsTablesAlignmentAndColour()
    {
        const string html = "<table><tbody><tr><td>a</td></tr></tbody></table>"
            + "<p style=\"text-align: center\"><span style=\"color: rgb(220, 38, 38); background-color: rgb(254, 240, 138)\">x</span> <code>y</code></p>";
        var result = sanitizer.Sanitize(html);

        Assert.Contains("<td>a</td>", result, StringComparison.Ordinal);
        Assert.Contains("text-align: center", result, StringComparison.Ordinal);
        // AngleSharp 会重新序列化颜色（rgb → 带 alpha 的 rgba），因此按通道匹配而非拼写。
        Assert.Matches(@"[^-]color:\s*rgba?\(220,\s*38,\s*38(,\s*1)?\)", result);
        Assert.Matches(@"background-color:\s*rgba?\(254,\s*240,\s*138(,\s*1)?\)", result);
        Assert.Contains("<code>y</code>", result, StringComparison.Ordinal);
    }

    [Fact]
    public void StillStripsScript() =>
        Assert.DoesNotContain("<script", sanitizer.Sanitize("<p>x</p><script>alert(1)</script><img src=x onerror=alert(1)>"), StringComparison.OrdinalIgnoreCase);
}
