using System.Globalization;
using System.Net;
using System.Text;
using AtomBlazor.Primitives.Barcode;
using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 以 SVG 绘制的线性条形码。
/// </summary>
/// <remarks>
/// <para>
/// 编码在 C# 中通过 <see cref="BarcodeEncoder"/> 完成——无 JavaScript、无图片请求、
/// 无第三方依赖——于是符号就是渲染标记的一部分，能像其他矢量图一样
/// 打印、缩放和复制。这比二维码更为重要：条形码通常会被打印，
/// 而矢量在任何标签尺寸下都保持锐利。
/// </para>
/// <para>
/// 配色固定为白底深色而非跟随主题，在深色主题下也不变。
/// 扫码器测量条与空的对比度，反色符号在多数设备上根本
/// 读不出来。只有当结果不会被扫码，
/// 或已实测目标扫码器时，才覆盖 <see cref="Foreground"/> 与 <see cref="Background"/>。
/// </para>
/// <para>
/// <see cref="ShowValue"/> 默认开启。条下的文本行正是扫码失败时
/// 人们阅读的内容，且零售码制中它在护栏条两侧的落位
/// 是标准规定而非装饰。
/// </para>
/// </remarks>
public partial class Barcode : ComponentBase
{
    private const int minQuietZone = 0;
    private const int maxQuietZone = 40;

    /// <summary>文本行按模块宽度定尺寸，使其随符号一起缩放。</summary>
    private const double textHeightModules = 8;

    private const double textGapModules = 1.5;

    /// <summary>
    /// 页内 SVG 自身单位下的条区高度。任何值都行，因为
    /// preserveAspectRatio 为 none，由 CSS 决定渲染高度。
    /// </summary>
    private const double barAreaUnits = 100;

    /// <summary>独立文件获得的条高（以模块计），由此固定其比例。</summary>
    private const double standaloneBarUnits = 50;

    /// <summary>独立文件中每模块的像素数，使其打开时尺寸可用。</summary>
    private const int standalonePixelsPerModule = 3;

    private string? svg;
    private string? encodeError;
    private BarcodeSymbol? symbol;

    /// <summary>
    /// 获取或设置要编码的值。为空时不渲染任何内容。
    /// </summary>
    /// <remarks>
    /// 何为合法取决于 <see cref="Type"/>。
    /// <see cref="BarcodeEncoder.DescribeInput(BarcodeType)"/> 返回描述该要求的
    /// 一句话，可作表单提示。码制无法承载的值会显示消息而非符号。
    /// </remarks>
    [Parameter]
    public string? Value { get; set; }

    /// <summary>
    /// 获取或设置要采用的码制。
    /// </summary>
    /// <remarks>
    /// <see cref="BarcodeType.Code128"/> 是通用选择：任意 ASCII 文本，
    /// 且是线性码中密度最高的。只有当另一端扫码器
    /// 有所期望时才使用零售或邮政码制。
    /// </remarks>
    [Parameter]
    public BarcodeType Type { get; set; } = BarcodeType.Code128;

    /// <summary>
    /// 获取或设置对具有可选校验字符的码制是否追加校验字符。
    /// </summary>
    /// <remarks>
    /// 适用于 <see cref="BarcodeType.Code39"/>、<see cref="BarcodeType.Itf"/>、
    /// <see cref="BarcodeType.Codabar"/> 与 <see cref="BarcodeType.Msi"/>。默认关闭，
    /// 因为未被配置期望校验字符的扫码器会把它当作数据
    /// 的一部分报告。凡标准强制校验位之处，无论此处
    /// 如何设置都会追加。
    /// </remarks>
    [Parameter]
    public bool AddChecksum { get; set; }

    /// <summary>
    /// 获取或设置以 CSS 长度表示的单个模块宽度。
    /// </summary>
    /// <remarks>
    /// 这决定整个符号的宽度，因为条形码的宽度已由其数据固定。
    /// 模块打印宽度低于约 0.19mm 便超出多数扫码器的分辨能力，
    /// 长值因此需要更宽的标签或更致密的码制。
    /// </remarks>
    [Parameter]
    public string ModuleWidth { get; set; } = "2px";

    /// <summary>
    /// 获取或设置以 CSS 长度表示的条高。
    /// </summary>
    /// <remarks>
    /// 高度只影响对准扫码的难易——除了
    /// <see cref="BarcodeType.Postnet"/> 与 <see cref="BarcodeType.Rm4scc"/>，
    /// 它们的数据在条高里，压扁符号就会毁坏数据。
    /// </remarks>
    [Parameter]
    public string Height { get; set; } = "4rem";

    /// <summary>
    /// 获取或设置以 CSS 颜色表示的条颜色。
    /// </summary>
    [Parameter]
    public string Foreground { get; set; } = "#18181b";

    /// <summary>
    /// 获取或设置以 CSS 颜色表示的符号底色，含静区。
    /// </summary>
    [Parameter]
    public string Background { get; set; } = "#ffffff";

    /// <summary>
    /// 获取或设置两侧浅色边距宽度（以模块计）。默认为码制所要求的值。
    /// </summary>
    /// <remarks>
    /// 扫码器需要这条边距来定位符号起止，它也是打印条形码
    /// 读不出来最常见的原因。只有当标签四周本就空白时
    /// 才调低它。
    /// </remarks>
    [Parameter]
    public int? QuietZone { get; set; }

    /// <summary>
    /// 获取或设置是否把值打印在条下方。
    /// </summary>
    [Parameter]
    public bool ShowValue { get; set; } = true;

    /// <summary>
    /// 获取或设置打印行中是否包含校验字符。
    /// </summary>
    /// <remarks>
    /// 默认关闭：校验位是给扫码器用的，打印出来可能让手动
    /// 键入号码的人犯迷糊。零售码制忽略此项并总是打印其校验位，
    /// 因为那里完整编号就是产品代码。
    /// </remarks>
    [Parameter]
    public bool ShowChecksum { get; set; }

    /// <summary>
    /// 获取或设置在条形码上方显示的标签。
    /// </summary>
    [Parameter]
    public string? Label { get; set; }

    /// <summary>
    /// 获取或设置符号的无障碍名称。
    /// </summary>
    /// <remarks>
    /// 默认为码制与已编码值。设置它说明该码标识的是什么——
    /// "订单 4051 的条形码"要比单独的编号数字更好。
    /// </remarks>
    [Parameter]
    public string? AriaLabel { get; set; }

    /// <summary>
    /// 获取或设置在已编码符号改变时调用的回调。
    /// </summary>
    /// <remarks>
    /// 参数为新符号；值为空或无效时为 <see langword="null"/>。在标记中
    /// 显示已编码值请用本回调而非 <see cref="Symbol"/>：父组件
    /// 在子组件重渲染前就构建好自己的标记，在那里读 <see cref="Symbol"/> 得到
    /// 的是上一个符号。
    /// </remarks>
    [Parameter]
    public EventCallback<BarcodeSymbol?> OnEncoded { get; set; }

    /// <summary>
    /// 获取或设置为根元素追加的 CSS 类。
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置摊散到根元素上的附加特性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>
    /// 获取已编码的符号；值为空或无效时为 <see langword="null"/>。
    /// </summary>
    /// <remarks>
    /// 请在事件处理器或 <c>OnAfterRender</c> 中读取，那里它是最新的。
    /// 从父组件标记读取会拿到上一个符号——那里请用 <see cref="OnEncoded"/>。
    /// </remarks>
    public BarcodeSymbol? Symbol => symbol;

    private string RootClass => ClassNames.cn("atom:inline-flex atom:flex-col atom:items-start", Class);

    private int EffectiveQuietZone =>
        Math.Clamp(QuietZone ?? symbol?.QuietZoneModules ?? 10, minQuietZone, maxQuietZone);

    private string EffectiveAriaLabel =>
        AriaLabel ?? Localizer["Barcode.AriaLabel", Type.ToString(), symbol?.Value ?? string.Empty];

    /// <summary>
    /// 返回已渲染的 SVG 标记；未绘制任何内容时为 <see langword="null"/>。
    /// </summary>
    /// <returns>可直接写入文件、打印或交给下载器的 SVG 文档。</returns>
    /// <remarks>
    /// 这不是页面上的标记。文件周围没有 CSS，因此它有固定的
    /// 比例，打印行画在其内部而非旁边。
    /// </remarks>
    public string? GetSvg() => symbol is null ? null : BuildStandaloneSvg(symbol);

    /// <inheritdoc />
    protected override async Task OnParametersSetAsync()
    {
        var previous = svg;

        svg = null;
        encodeError = null;
        symbol = null;

        if (!string.IsNullOrEmpty(Value))
        {
            try
            {
                symbol = BarcodeEncoder.Encode(Value, Type, AddChecksum);
                svg = BuildSvg(symbol);
            }
            catch (ArgumentException error)
            {
                // 码制无法承载的值是内容问题而非缺陷。错在哪里要写在
                // 标记里而不是抛异常——抛异常在 Blazor Server 上会
                // 击穿 circuit。
                //
                // 原样显示。BarcodeFormatException.Message 就是编码器自己的那句话、
                // 别无其他——这里曾从框架拼接消息中剥掉参数名后缀，
                // 那会在 WebAssembly 上印出裸的资源键 Arg_ParamName_Name，
                // 那里该资源已被裁剪掉。
                encodeError = error.Message;
            }
        }

        if (OnEncoded.HasDelegate && !string.Equals(previous, svg, StringComparison.Ordinal))
        {
            await OnEncoded.InvokeAsync(symbol);
        }
    }

    /// <summary>
    /// 获取属于条上方的打印文本段，若码制有的话。
    /// </summary>
    /// <remarks>
    /// 只有 ISBN 和 ISSN 如此：人们引用的编号在上方，
    /// 产品代码的 13 位数字在下方。
    /// </remarks>
    private BarcodeTextGroup? AboveGroup =>
        symbol is null || !ShowValue
            ? null
            : TextGroupsToDraw(symbol).Where(IsFullWidth).Cast<BarcodeTextGroup?>().FirstOrDefault();

    private IReadOnlyList<BarcodeTextGroup> BelowGroups =>
        symbol is null || !ShowValue
            ? []
            : [.. TextGroupsToDraw(symbol).Where(g => !IsFullWidth(g) || AboveGroup is null)];

    /// <summary>
    /// 获取某文本段是否横跨整个符号，这正是 ISBN 与 ISSN 行的标志。
    /// </summary>
    private bool IsFullWidth(BarcodeTextGroup group) =>
        symbol is not null
        && symbol.Type is BarcodeType.Isbn or BarcodeType.Issn
        && group.Start <= 0
        && group.End >= symbol.ModuleCount;

    private int TotalWidth => (symbol?.ModuleCount ?? 0) + (EffectiveQuietZone * 2);

    private string SymbolWidthStyle =>
        FormattableString.Invariant($"calc({ModuleWidth} * {TotalWidth})");

    /// <summary>
    /// 获取打印行的字号。它跟随模块宽度、随符号一起变大，
    /// 但两端都有钳制：不会小到无法阅读，
    /// 在粗模块的邮政码上也不会大到淹没条。
    /// </summary>
    private string TextSizeStyle =>
        FormattableString.Invariant($"clamp(0.625rem, calc({ModuleWidth} * 5), 1rem)");

    /// <summary>
    /// 把模块位置转换为整个符号（含静区）的百分比。
    /// </summary>
    /// <param name="modules">位置；<paramref name="span"/> 置位时则为宽度。</param>
    /// <param name="span">
    /// <see langword="true"/> 表示按宽度处理该值，
    /// 而不是自符号左端起的位置。
    /// </param>
    private string Percent(double modules, bool span = false)
    {
        if (TotalWidth == 0)
        {
            return "0%";
        }

        var value = span ? modules : modules + EffectiveQuietZone;
        return FormattableString.Invariant($"{value / TotalWidth * 100:0.###}%");
    }

    /// <summary>
    /// 构建条。文本以 HTML 画在其外围而非其中。
    /// </summary>
    private string BuildSvg(BarcodeSymbol code)
    {
        var quiet = EffectiveQuietZone;
        var width = code.ModuleCount + (quiet * 2);
        var builder = new StringBuilder(2048);

        // preserveAspectRatio 为 none，因为条形码的宽度来自数据、
        // 高度自由：条应拉伸到 Height 指定的高度而无黑边。
        builder.Append(CultureInfo.InvariantCulture, $"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {width} {barAreaUnits}\"");
        builder.Append(" preserveAspectRatio=\"none\" role=\"img\"");
        builder.Append(CultureInfo.InvariantCulture, $" aria-label=\"{WebUtility.HtmlEncode(EffectiveAriaLabel)}\"");
        builder.Append(CultureInfo.InvariantCulture, $" style=\"display:block;width:100%;height:{WebUtility.HtmlEncode(Height)}\">");

        builder.Append(CultureInfo.InvariantCulture, $"<rect width=\"{width}\" height=\"{barAreaUnits}\" fill=\"{WebUtility.HtmlEncode(Background)}\"/>");

        builder.Append(CultureInfo.InvariantCulture, $"<g fill=\"{WebUtility.HtmlEncode(Foreground)}\" shape-rendering=\"crispEdges\">");
        foreach (var bar in code.Bars)
        {
            AppendBar(builder, bar, quiet, 0);
        }

        builder.Append("</g></svg>");
        return builder.ToString();
    }

    private static void AppendBar(StringBuilder builder, BarcodeBar bar, int quiet, double offset)
    {
        var top = offset + (bar.Top * barAreaUnits);
        var height = (bar.Bottom - bar.Top) * barAreaUnits;

        builder.Append(CultureInfo.InvariantCulture, $"<rect x=\"{bar.Start + quiet}\" y=\"{top.ToString("0.###", CultureInfo.InvariantCulture)}\" width=\"{bar.Width}\" height=\"{height.ToString("0.###", CultureInfo.InvariantCulture)}\"/>");
    }

    /// <summary>
    /// 构建独立文档：条与打印行同处，比例真实。
    /// </summary>
    /// <remarks>
    /// 这不是页面上的标记。页面上条会拉伸到
    /// <see cref="Height"/> 指定的高度，打印行以 HTML 位于其旁，使文本
    /// 在任何宽高比下都保持可读。文件周围没有 CSS，因此它有固定、
    /// 不变形的形状，文本画在其内部。
    /// </remarks>
    private string BuildStandaloneSvg(BarcodeSymbol code)
    {
        var quiet = EffectiveQuietZone;
        var width = code.ModuleCount + (quiet * 2);
        var groups = ShowValue ? TextGroupsToDraw(code) : [];
        var above = groups.Any(IsFullWidth) ? textHeightModules + textGapModules : 0;
        var below = groups.Count > 0 ? textHeightModules + textGapModules : 0;
        var height = standaloneBarUnits + above + below;

        var builder = new StringBuilder(2048);
        builder.Append(CultureInfo.InvariantCulture, $"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {width} {Round(height)}\"");
        builder.Append(CultureInfo.InvariantCulture, $" width=\"{width * standalonePixelsPerModule}\" height=\"{Round(height * standalonePixelsPerModule)}\" role=\"img\"");
        builder.Append(CultureInfo.InvariantCulture, $" aria-label=\"{WebUtility.HtmlEncode(EffectiveAriaLabel)}\">");

        builder.Append(CultureInfo.InvariantCulture, $"<rect width=\"{width}\" height=\"{Round(height)}\" fill=\"{WebUtility.HtmlEncode(Background)}\"/>");

        builder.Append(CultureInfo.InvariantCulture, $"<g fill=\"{WebUtility.HtmlEncode(Foreground)}\" shape-rendering=\"crispEdges\">");
        foreach (var bar in code.Bars)
        {
            var top = above + (bar.Top * standaloneBarUnits);
            var barHeight = (bar.Bottom - bar.Top) * standaloneBarUnits;
            builder.Append(CultureInfo.InvariantCulture, $"<rect x=\"{bar.Start + quiet}\" y=\"{Round(top)}\" width=\"{bar.Width}\" height=\"{Round(barHeight)}\"/>");
        }

        builder.Append("</g>");

        if (groups.Count > 0)
        {
            builder.Append(CultureInfo.InvariantCulture, $"<g fill=\"{WebUtility.HtmlEncode(Foreground)}\" font-family=\"ui-monospace, SFMono-Regular, Menlo, monospace\" font-size=\"{Round(textHeightModules)}\" text-anchor=\"middle\">");

            foreach (var group in groups)
            {
                var isAbove = above > 0 && IsFullWidth(group);
                var baseline = isAbove
                    ? textHeightModules
                    : above + standaloneBarUnits + textGapModules + textHeightModules;
                var centre = ((group.Start + group.End) / 2) + quiet;

                builder.Append(CultureInfo.InvariantCulture, $"<text x=\"{Round(centre)}\" y=\"{Round(baseline)}\">{WebUtility.HtmlEncode(group.Text)}</text>");
            }

            builder.Append("</g>");
        }

        builder.Append("</svg>");
        return builder.ToString();
    }

    private static string Round(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    /// <summary>
    /// 当校验字符只是为扫码器而加时，从打印行中略去。
    /// </summary>
    private IReadOnlyList<BarcodeTextGroup> TextGroupsToDraw(BarcodeSymbol code)
    {
        var groups = code.TextGroups;

        if (ShowChecksum || !AddChecksum || groups.Count != 1)
        {
            return groups;
        }

        var text = groups[0].Text;

        // Code 39 在两颗星号之间打印，因此校验字符是倒数第二个。
        var trimmed = code.Type == BarcodeType.Code39 && text.Length > 2
            ? text[..^2] + text[^1]
            : text.Length > 1 ? text[..^1] : text;

        return [groups[0] with { Text = trimmed }];
    }
}
