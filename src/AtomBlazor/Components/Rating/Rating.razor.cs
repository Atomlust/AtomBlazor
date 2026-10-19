using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace AtomBlazor;

/// <summary>
/// 支持自定义图标与半星评价的评分组件。
/// </summary>
public partial class Rating : ComponentBase
{
    private readonly string _instanceId = Guid.NewGuid().ToString("N")[..8];
    private double _hoverValue;

    /// <summary>
    /// 获取或设置当前评分值。
    /// </summary>
    [Parameter]
    public double Value { get; set; }

    /// <summary>
    /// 获取或设置值变化时调用的回调。
    /// </summary>
    [Parameter]
    public EventCallback<double> ValueChanged { get; set; }

    /// <summary>
    /// 获取或设置评分最大值。
    /// </summary>
    [Parameter]
    public int Max { get; set; } = 5;

    /// <summary>
    /// 获取或设置是否允许半星评分。
    /// </summary>
    [Parameter]
    public bool AllowHalf { get; set; }

    /// <summary>
    /// 获取或设置点击当前值时是否清空评分。
    /// </summary>
    [Parameter]
    public bool AllowClear { get; set; } = true;

    /// <summary>
    /// 获取或设置评分是否只读（仅展示）。
    /// </summary>
    [Parameter]
    public bool ReadOnly { get; set; }

    /// <summary>
    /// 获取或设置评分是否禁用。
    /// </summary>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>
    /// 获取或设置图标类型。
    /// </summary>
    [Parameter]
    public RatingIcon Icon { get; set; } = RatingIcon.Star;

    /// <summary>
    /// 获取或设置激活（填充）图标的颜色。
    /// </summary>
    [Parameter]
    public string? ActiveColor { get; set; }

    /// <summary>
    /// 获取或设置未激活（空）图标的颜色。
    /// </summary>
    [Parameter]
    public string? InactiveColor { get; set; }

    /// <summary>
    /// 获取或设置自定义图标模板。
    /// </summary>
    [Parameter]
    public RenderFragment<RatingIconContext>? IconTemplate { get; set; }

    /// <summary>
    /// 获取或设置附加 CSS 类。
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置应用到根元素的其他 HTML 属性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>
    /// 获取或设置评分组的 ARIA 标签。
    /// </summary>
    [Parameter]
    public string? AriaLabel { get; set; }

    /// <summary>
    /// 获取或设置图标尺寸。
    /// </summary>
    [Parameter]
    public RatingSize Size { get; set; } = RatingSize.Default;

    private string CssClass => ClassNames.cn(
        "atom:inline-flex atom:items-center atom:gap-1",
        // #459：radiogroup 是唯一的 Tab 停靠点，而不是每颗星。gap-1 下无需偏移。
        "atom:rounded-sm atom:focus-visible:outline-none atom:focus-visible:ring-2 atom:focus-visible:ring-ring",
        (Disabled || ReadOnly) ? "atom:cursor-default" : "atom:cursor-pointer",
        Disabled ? "atom:opacity-50" : null,
        Class
    );

    private string IconClass => ClassNames.cn(
        Size switch
        {
            RatingSize.Small => "atom:h-4 atom:w-4",
            RatingSize.Large => "atom:h-8 atom:w-8",
            _ => "atom:h-6 atom:w-6"
        },
        "atom:transition-transform",
        !(Disabled || ReadOnly) ? "atom:hover:scale-110" : null
    );

    private string GetIconContainerClass(int index)
    {
        var isActive = GetFillPercentage(index) > 0;
        return ClassNames.cn(
            "atom:relative atom:inline-flex",
            !(Disabled || ReadOnly) ? "atom:cursor-pointer" : null
        );
    }

    private double GetFillPercentage(int index)
    {
        var currentValue = _hoverValue > 0 ? _hoverValue : Value;

        if (currentValue >= index)
        {
            return 1;
        }

        if (AllowHalf && currentValue >= index - 0.5)
        {
            return 0.5;
        }

        return 0;
    }

    private async Task HandleClick(int index)
    {
        if (Disabled || ReadOnly)
        {
            return;
        }

        double newValue;

        if (AllowHalf)
        {
            // 点击相同值时在半星与整星之间切换，或清空
            if (Value == index)
            {
                newValue = AllowClear ? 0 : index;
            }
            else if (Value == index - 0.5)
            {
                newValue = index;
            }
            else
            {
                newValue = index;
            }
        }
        else
        {
            // 简单点击行为
            if (AllowClear && Value == index)
            {
                newValue = 0;
            }
            else
            {
                newValue = index;
            }
        }

        if (newValue != Value)
        {
            Value = newValue;
            await ValueChanged.InvokeAsync(newValue);
        }
    }

    private void HandleMouseEnter(int index)
    {
        if (Disabled || ReadOnly)
        {
            return;
        }

        if (AllowHalf)
        {
            // 判断悬停在图标的左半还是右半
            // 这是简化实现——严格来说需要 JS 才能取得精确位置
            _hoverValue = index;
        }
        else
        {
            _hoverValue = index;
        }
    }

    private void HandleMouseLeave() => _hoverValue = 0;

    private async Task HandleKeyDown(KeyboardEventArgs e)
    {
        if (Disabled || ReadOnly)
        {
            return;
        }

        var step = AllowHalf ? 0.5 : 1.0;
        var newValue = Value;

        switch (e.Key)
        {
            case "ArrowRight":
            case "ArrowUp":
                newValue = Math.Min(Max, Value + step);
                break;
            case "ArrowLeft":
            case "ArrowDown":
                newValue = Math.Max(0, Value - step);
                break;
            case "Home":
                newValue = 0;
                break;
            case "End":
                newValue = Max;
                break;
            case "1":
            case "2":
            case "3":
            case "4":
            case "5":
            case "6":
            case "7":
            case "8":
            case "9":
                var numValue = int.Parse(e.Key, CultureInfo.InvariantCulture);
                if (numValue <= Max)
                {
                    newValue = numValue;
                }
                break;
            case "0":
                if (AllowClear)
                {
                    newValue = 0;
                }
                break;
        }

        if (newValue != Value)
        {
            Value = newValue;
            await ValueChanged.InvokeAsync(newValue);
        }
    }
}

/// <summary>
/// 定义 Rating 组件图标的尺寸。
/// </summary>
public enum RatingSize
{
    /// <summary>
    /// 小尺寸（16px）。
    /// </summary>
    Small,

    /// <summary>
    /// 默认尺寸（24px）。
    /// </summary>
    Default,

    /// <summary>
    /// 大尺寸（32px）。
    /// </summary>
    Large
}
