using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Components;
using System.Globalization;

namespace AtomBlazor;

/// <summary>
/// 用于构建列表项的灵活、可组合的条目组件。
/// </summary>
/// <remarks>
/// Item 组件为列表项提供结构，支持变体、尺寸，以及通过 AsChild 参数的多态渲染。
/// </remarks>
[UnconditionalSuppressMessage("Trimming", "IL2111: Method with DynamicallyAccessedMembers is accessed via reflection",
    Justification = "DynamicComponent.Type 由 razor 生成代码经反射赋值；候选类型是本类的私有嵌套 ComponentBase，始终存在。")]
public partial class Item : ComponentBase
{
    /// <summary>
    /// 获取或设置条目的视觉风格变体。
    /// </summary>
    [Parameter]
    public ItemVariant Variant { get; set; } = ItemVariant.Default;

    /// <summary>
    /// 获取或设置条目的尺寸。
    /// </summary>
    [Parameter]
    public ItemSize Size { get; set; } = ItemSize.Default;

    /// <summary>
    /// 获取或设置渲染所用的元素类型（如 "a"、"button"）。
    /// 设置后组件将渲染为该元素而非 div。
    /// </summary>
    [Parameter]
    public string? AsChild { get; set; }

    /// <summary>
    /// 获取或设置渲染为链接元素时的 href 属性。
    /// </summary>
    [Parameter]
    public string? Href { get; set; }

    /// <summary>
    /// 获取或设置应用到条目上的额外 CSS 类。
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置在条目内部渲染的内容。
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// 获取或设置应用到元素的额外属性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>
    /// 获取条目元素计算后的 CSS 类。
    /// </summary>
    private string CssClass => ClassNames.cn(
        // 基础条目样式
        "atom:group atom:relative atom:flex atom:items-center atom:gap-3 atom:rounded-lg",
        // 仅在可交互时附加过渡
        IsInteractive ? "atom:transition-colors" : null,
        // 变体专属样式
        Variant switch
        {
            ItemVariant.Outline => IsInteractive
                ? "atom:border atom:border-border atom:bg-background atom:hover:bg-accent"
                : "atom:border atom:border-border atom:bg-background",
            ItemVariant.Muted => IsInteractive
                ? "atom:bg-muted atom:hover:bg-muted/80"
                : "atom:bg-muted",
            _ => IsInteractive ? "atom:hover:bg-accent" : null
        },
        // 尺寸专属样式
        Size switch
        {
            ItemSize.Sm => "atom:px-3 atom:py-2 atom:text-sm",
            _ => "atom:px-4 atom:py-3"
        },
        Class
    );

    /// <summary>
    /// 获取此条目是否可交互（可点击/可悬停）。
    /// </summary>
    private bool IsInteractive => !string.IsNullOrEmpty(AsChild) ||
                                   (AdditionalAttributes?.ContainsKey("onclick") == true);

    /// <summary>
    /// 根据 AsChild 参数获取要渲染的元素类型。
    /// </summary>
    private Type GetElementType()
    {
        return AsChild?.ToLower(CultureInfo.InvariantCulture) switch
        {
            "a" => typeof(AnchorElement),
            "button" => typeof(ButtonElement),
            _ => typeof(DivElement)
        };
    }

    /// <summary>
    /// 获取传递给动态元素的属性。
    /// </summary>
    private Dictionary<string, object> GetElementAttributes()
    {
        var attributes = new Dictionary<string, object>
        {
            ["class"] = CssClass,
            ["data-slot"] = "item",
            ["ChildContent"] = (object?)ChildContent!
        };

        if (!string.IsNullOrEmpty(Href) && AsChild == "a")
        {
            attributes["href"] = Href;
        }

        if (AdditionalAttributes != null)
        {
            foreach (var attr in AdditionalAttributes)
            {
                attributes[attr.Key] = attr.Value;
            }
        }

        return attributes;
    }

    // DynamicComponent 的辅助渲染组件：分别以 div / a / button 承载条目内容与属性。
    private sealed class DivElement : ComponentBase
    {
        [Parameter] public string? @class { get; set; }
        [Parameter] public string? DataSlot { get; set; }
        [Parameter] public RenderFragment? ChildContent { get; set; }
        [Parameter(CaptureUnmatchedValues = true)] public Dictionary<string, object>? Attributes { get; set; }

        protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "div");
            builder.AddAttribute(1, "class", @class);
            builder.AddAttribute(2, "data-slot", DataSlot);
            builder.AddMultipleAttributes(3, Attributes);
            builder.AddContent(4, ChildContent);
            builder.CloseElement();
        }
    }

    private sealed class AnchorElement : ComponentBase
    {
        [Parameter] public string? @class { get; set; }
        [Parameter] public string? DataSlot { get; set; }
        [Parameter] public string? href { get; set; }
        [Parameter] public RenderFragment? ChildContent { get; set; }
        [Parameter(CaptureUnmatchedValues = true)] public Dictionary<string, object>? Attributes { get; set; }

        protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "a");
            builder.AddAttribute(1, "class", @class);
            builder.AddAttribute(2, "data-slot", DataSlot);
            if (!string.IsNullOrEmpty(href))
            {
                builder.AddAttribute(3, "href", href);
            }
            builder.AddMultipleAttributes(4, Attributes);
            builder.AddContent(5, ChildContent);
            builder.CloseElement();
        }
    }

    private sealed class ButtonElement : ComponentBase
    {
        [Parameter] public string? @class { get; set; }
        [Parameter] public string? DataSlot { get; set; }
        [Parameter] public RenderFragment? ChildContent { get; set; }
        [Parameter(CaptureUnmatchedValues = true)] public Dictionary<string, object>? Attributes { get; set; }

        protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "button");
            builder.AddAttribute(1, "type", "button");
            builder.AddAttribute(2, "class", @class);
            builder.AddAttribute(3, "data-slot", DataSlot);
            builder.AddMultipleAttributes(4, Attributes);
            builder.AddContent(5, ChildContent);
            builder.CloseElement();
        }
    }
}
