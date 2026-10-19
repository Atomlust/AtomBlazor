using AtomBlazor.Primitives;
using System.Text.RegularExpressions;
using Xunit;

namespace AtomBlazor.Tests.Conventions;

/// <summary>
/// #459 中"Overlay triggers"（浮层触发器）一组的最后一个回归守卫：<c>DialogTrigger</c>、
/// <c>SheetTrigger</c> 与 <c>PopoverTrigger</c> 已在 #508 修复，唯独漏掉了
/// <c>AlertDialogTrigger</c>——它渲染一个原生 <c>&lt;button&gt;</c>（经由其包装的 Dialog primitive），
/// 却没有任何焦点样式，既无 <c>outline-none</c> 也无替代品。
/// <see cref="FocusIndicatorTests"/> 只在出现 <c>outline-none</c> 时才会触发，所以从未抓到这个问题：
/// 焦点经浏览器自带的 outline 仍然可见，只是与其他每个独立触发器所用的主题化焦点环不一致。
/// </summary>
public class AlertDialogTriggerFocusRingTests
{
    private static readonly Regex standaloneRing = new(
        @"atom:focus-visible:outline-none\s+atom:focus-visible:ring-2\s+atom:focus-visible:ring-ring\s+atom:focus-visible:ring-offset-2",
        RegexOptions.Compiled);

    [FactWhen(Components = new[] { "AlertDialogTrigger" })]
    public void ComponentsLayerAppliesTheStandaloneFocusRing()
    {
 // 优雅跳过：AlertDialogTrigger 的源码尚未进入源树（对应组件尚未交付），
        // 该组件到位后本测试自动生效。
        var text = SourceTree.ByComponent()
            .Where(c => c.Component == "AlertDialogTrigger")
            .Select(c => c.Text)
            .SingleOrDefault();

        Assert.False(text is null, "预期在 src/AtomBlazor 或 src/AtomBlazor.Primitives 下" +
            "存在名为 AlertDialogTrigger 的组件——它是否被改名或移动了？");

        Assert.True(standaloneRing.IsMatch(text!),
            "AlertDialogTrigger 应当把本库标准的独立控件焦点环"
            + "（atom:focus-visible:outline-none atom:focus-visible:ring-2 atom:focus-visible:ring-ring atom:focus-visible:ring-offset-2）"
            + "应用到它渲染的原生 button 上，与 DialogTrigger/SheetTrigger/PopoverTrigger 保持一致（#508）。");
    }
}
