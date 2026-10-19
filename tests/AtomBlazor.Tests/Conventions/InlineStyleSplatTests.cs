using AtomBlazor.Primitives;
using System.Text.RegularExpressions;
using Xunit;

namespace AtomBlazor.Tests.Conventions;

/// <summary>
/// 防止元素自己写了 <c>style</c>、随后又把原始 <c>AdditionalAttributes</c> 展开（splat）上去。
/// <para>
/// Razor 按源码顺序应用属性，<c>@attributes</c> 展开就落在指令所在的位置，所以使用者的 <c>style</c>
/// 后到并覆盖先前写入的一切——让面板可伸缩的 flex 尺寸、grid 模板、隐藏已关闭菜单的
/// <c>display: none</c>。在合并值旁边展开 <c>InlineStyleMerge.WithoutStyle(...)</c> 可以两者都保住。
/// </para>
/// <para>
/// 以文本方式检查，是因为两个版本都能编译、都能渲染；只有顺序不同，
/// 而丢失只在使用者传入自己的 style 时才显现。
/// </para>
/// </summary>
public partial class InlineStyleSplatTests
{
    [Fact]
    public void AnElementWithItsOwnStyleDoesNotSplatTheRawAttributes()
    {
        var offenders = new List<string>();

        foreach (var file in SourceTree.ComponentSources.Where(f => f.Extension == ".razor"))
        {
            var text = File.ReadAllText(file.FullName);

            foreach (Match match in StyleThenSplat().Matches(text))
            {
                var line = text.Take(match.Index).Count(c => c == '\n') + 1;
                offenders.Add($"{SourceTree.RelativePath(file)}:{line}");
            }
        }

        Assert.True(offenders.Count == 0,
            "这些元素先写了自己的 style、随后又把 AdditionalAttributes 展开覆盖上去，"
            + "于是使用者传入的 style 会静默替换掉组件自己的。请把 "
            + "InlineStyleMerge.Merge(...) 渲染进 style 属性，并展开 "
            + $"InlineStyleMerge.WithoutStyle(AdditionalAttributes)。{Environment.NewLine}"
            + string.Join(Environment.NewLine, offenders));
    }

    [GeneratedRegex(@"<[^<>]*style=""@[^""]*""[^<>]*@attributes=""AdditionalAttributes""[^<>]*>", RegexOptions.Singleline)]
    private static partial Regex StyleThenSplat();
}
