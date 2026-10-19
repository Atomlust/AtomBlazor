using System.Globalization;
using System.Net;
using System.Text;
using AtomBlazor.Primitives.QrCode;
using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 以 SVG 绘制的二维码。
/// </summary>
/// <remarks>
/// <para>
/// 编码在 C# 中通过 <see cref="QrEncoder"/> 完成——无 JavaScript、无图片请求、
/// 无第三方依赖——于是二维码就是渲染标记的一部分，能像其他矢量图一样
/// 打印、缩放和复制。设置 <see cref="Value"/> 即可得到可用的码；其余都是呈现方式。
/// </para>
/// <para>
/// 默认配色是白底深色而非跟随主题，在深色主题下也保持不变。
/// 扫码器期望浅色底上的深色码，拒读反色码的扫码器多不胜数的，
/// 跟随主题无异于用可用的码换页面更整洁。确信这笔交换值得时，
/// 再覆盖 <see cref="Foreground"/> 与 <see cref="Background"/>。
/// </para>
/// <para>
/// 二维码承载的文本只有相机能读。凡值没有在页面别处写明的地方，
/// 请开启 <see cref="ShowValue"/> 或设置 <see cref="AriaLabel"/>。
/// </para>
/// </remarks>
public partial class QrCode : ComponentBase
{
    private const int minQuietZone = 0;
    private const int maxQuietZone = 16;
    private const double minImageSize = 0.05;
    private const double maxImageSize = 0.3;

    /// <summary>仍适合作为无障碍名称一部分朗读的最长值。</summary>
    private const int ariaLabelValueLimit = 60;

    /// <summary>
    /// SVG 自身宽高所用的每模块像素数，即文件单独打开时的尺寸；
    /// viewBox 使其保持矢量，因此这只设定起始大小。
    /// </summary>
    private const int standaloneModulePixels = 8;

    private string? svg;
    private string? encodeError;
    private QrMatrix? matrix;

    /// <summary>
    /// 获取或设置要编码的文本。为空时不渲染任何内容。
    /// </summary>
    /// <remarks>
    /// 任意文本皆可，但众所周知的前缀才让二维码被扫到时触发动作：
    /// <c>https://…</c> 打开网页、<c>mailto:…</c> 起草邮件、<c>tel:…</c> 拨号、
    /// <c>WIFI:T:WPA;S:name;P:secret;;</c> 加入网络，vCard 则添加联系人。
    /// </remarks>
    [Parameter]
    public string? Value { get; set; }

    /// <summary>
    /// 获取或设置二维码可丢失或被遮盖多少仍然能被读出。
    /// </summary>
    /// <remarks>
    /// 等级越高，同样文本下码越大。二维码将印得很小
    /// 或印在易折皱之物上时请调高。设置 <see cref="Image"/> 会自行
    /// 将其升至 <see cref="QrErrorCorrection.Quartile"/>，因为中心图就是一种损坏。
    /// </remarks>
    [Parameter]
    public QrErrorCorrection ErrorCorrection { get; set; } = QrErrorCorrection.Medium;

    /// <summary>
    /// 获取或设置使用的最小二维码版本，1 到 40。每升一版每边加四个模块。
    /// </summary>
    /// <remarks>
    /// 调高它可让一组二维码彼此保持同尺寸，而不是各自缩到
    /// 适配自身的值。文本需要更多空间时编码器仍会继续升版。
    /// </remarks>
    [Parameter]
    public int MinVersion { get; set; } = 1;

    /// <summary>
    /// 获取或设置以 CSS 长度表示的渲染宽高。
    /// </summary>
    /// <remarks>
    /// 码是矢量的，因此这只设定绘制多大。每设备像素低于约 2 个模块
    /// 时手机相机就开始吃力，长文本因此需要更大的尺寸。
    /// </remarks>
    [Parameter]
    public string Size { get; set; } = "12rem";

    /// <summary>
    /// 获取或设置以 CSS 颜色表示的深色模块颜色。
    /// </summary>
    [Parameter]
    public string Foreground { get; set; } = "#18181b";

    /// <summary>
    /// 获取或设置以 CSS 颜色表示的码底色，含静区。
    /// </summary>
    /// <remarks>
    /// 用 <c>transparent</c> 可让页面透出。只在浅色纯净的背景上这样做：
    /// 静区是扫码器找到二维码边缘的方式。
    /// </remarks>
    [Parameter]
    public string Background { get; set; } = "#ffffff";

    /// <summary>
    /// 获取或设置码周浅色边距的宽度（以模块计）。
    /// </summary>
    /// <remarks>
    /// 四模块是规范最小值也是默认值。扫码器需要这条边距才能找到码，
    /// 因此只有当周围页面本就浅淡空旷时才低于四。
    /// </remarks>
    [Parameter]
    public int QuietZone { get; set; } = 4;

    /// <summary>
    /// 获取或设置每个深色模块绘制的形状。
    /// </summary>
    [Parameter]
    public QrCodeModuleShape ModuleShape { get; set; } = QrCodeModuleShape.Square;

    /// <summary>
    /// 获取或设置放在码中心的图片，以 URL 或 data URL 表示。
    /// </summary>
    /// <remarks>
    /// 图片会遮盖模块，这就是设置它会把纠错等级至少抬到
    /// <see cref="QrErrorCorrection.Quartile"/> 的原因。<see cref="ImageSize"/> 宜小，
    /// 上线前请用真实扫码测试。
    /// </remarks>
    [Parameter]
    public string? Image { get; set; }

    /// <summary>
    /// 获取或设置中心图的宽度，按码宽比例，0.05 到 0.3。
    /// </summary>
    [Parameter]
    public double ImageSize { get; set; } = 0.2;

    /// <summary>
    /// 获取或设置中心图后方衬块的颜色。默认为 <see cref="Background"/>。
    /// </summary>
    [Parameter]
    public string? ImageBackground { get; set; }

    /// <summary>
    /// 获取或设置是否把值也以文本写在码下方。
    /// </summary>
    /// <remarks>
    /// 这是读取码内容的无障碍途径，也是手边没有相机的人唯一可用的途径。
    /// 除非值已在页面别处出现，否则请开启。
    /// </remarks>
    [Parameter]
    public bool ShowValue { get; set; }

    /// <summary>
    /// 获取或设置在码上方显示的标签。
    /// </summary>
    [Parameter]
    public string? Label { get; set; }

    /// <summary>
    /// 获取或设置二维码的无障碍名称。
    /// </summary>
    /// <remarks>
    /// 当值短到适合朗读时默认为该值，否则仅为 "QR code"。
    /// 设置它以说明扫码会做什么——
    /// "加入访客网络的二维码"要优于直接念出编码字符串。
    /// </remarks>
    [Parameter]
    public string? AriaLabel { get; set; }

    /// <summary>
    /// 获取或设置在编码网格改变时调用的回调。
    /// </summary>
    /// <remarks>
    /// 参数为新网格；值空或放不下时为 <see langword="null"/>。在标记中
    /// 显示版本或编码模式时请用本回调而非 <see cref="Matrix"/>：父组件
    /// 在子组件重渲染前就构建好自己的标记，在那里读 <see cref="Matrix"/>
    /// 得到的是上一个网格。
    /// </remarks>
    [Parameter]
    public EventCallback<QrMatrix?> OnEncoded { get; set; }

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
    /// 获取已编码的网格；值为空或放不下时为 <see langword="null"/>。
    /// </summary>
    /// <remarks>
    /// 请在事件处理器或 <c>OnAfterRender</c> 中读取，那里它是最新的。
    /// 从父组件标记读取会拿到上一个网格，因为父组件在子组件重渲染前
    /// 就构建好标记——那里请用 <see cref="OnEncoded"/>。网格不含静区。
    /// </remarks>
    public QrMatrix? Matrix => matrix;

    private string RootClass => ClassNames.cn("atom:inline-flex atom:flex-col atom:items-center", Class);

    private int EffectiveQuietZone => Math.Clamp(QuietZone, minQuietZone, maxQuietZone);

    private double EffectiveImageSize => Math.Clamp(ImageSize, minImageSize, maxImageSize);

    /// <summary>
    /// 获取实际使用的纠错等级，中心图需要余量时会抬高。
    /// </summary>
    private QrErrorCorrection EffectiveErrorCorrection =>
        string.IsNullOrEmpty(Image) || ErrorCorrection >= QrErrorCorrection.Quartile
            ? ErrorCorrection
            : QrErrorCorrection.Quartile;

    private string EffectiveAriaLabel =>
        AriaLabel
        ?? (Value is not null && Value.Length <= ariaLabelValueLimit
            ? Localizer["QrCode.AriaLabelWithValue", Value]
            : Localizer["QrCode.AriaLabel"]);

    /// <summary>
    /// 返回已渲染的 SVG 标记；未绘制任何内容时为 <see langword="null"/>。
    /// </summary>
    /// <returns>可直接写入文件或交给下载器的 SVG 文档。</returns>
    /// <remarks>
    /// 这是与组件渲染相同的标记，以独立文档形式给出。它不带 CSS
    /// 类，因此无论落到何处都保有自己的颜色。
    /// </remarks>
    public string? GetSvg() => svg;

    /// <inheritdoc />
    protected override async Task OnParametersSetAsync()
    {
        var previous = svg;

        svg = null;
        encodeError = null;
        matrix = null;

        if (!string.IsNullOrEmpty(Value))
        {
            try
            {
                matrix = QrEncoder.Encode(Value, EffectiveErrorCorrection, Math.Clamp(MinVersion, 1, 40));
                svg = BuildSvg(matrix);
            }
            catch (ArgumentException)
            {
                // 值超出版本 40 容量是内容问题而非缺陷。在标记中说明，
                // 而不是抛异常——那在 Blazor Server 上会击穿 circuit。
                encodeError = Localizer["QrCode.TooLong"];
            }
        }

        // 仅在真正变化时触发，避免因此重渲染的父组件再弹回这里。
        if (OnEncoded.HasDelegate && !string.Equals(previous, svg, StringComparison.Ordinal))
        {
            await OnEncoded.InvokeAsync(matrix);
        }
    }

    private string BuildSvg(QrMatrix code)
    {
        var quiet = EffectiveQuietZone;
        var span = code.Size + (quiet * 2);
        var size = WebUtility.HtmlEncode(Size);
        var builder = new StringBuilder(1024);

        // width 与 height 属性无单位，即按像素计，使文件单独打开时
        // 具有固有尺寸。尺寸写在 style 中，那里 rem 之类的 CSS 长度有效，
        // 并且在页面上也会压过属性值。
        builder.Append(CultureInfo.InvariantCulture, $"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {span} {span}\"");
        builder.Append(CultureInfo.InvariantCulture, $" width=\"{span * standaloneModulePixels}\" height=\"{span * standaloneModulePixels}\" role=\"img\"");
        builder.Append(CultureInfo.InvariantCulture, $" aria-label=\"{WebUtility.HtmlEncode(EffectiveAriaLabel)}\"");
        builder.Append(CultureInfo.InvariantCulture, $" style=\"width:{size};height:auto;max-width:100%\">");

        builder.Append(CultureInfo.InvariantCulture, $"<rect width=\"{span}\" height=\"{span}\" fill=\"{WebUtility.HtmlEncode(Background)}\"/>");

        var foreground = WebUtility.HtmlEncode(Foreground);

        // 无论模块形状如何，定位图案保持实心，扫码器仍能找到码。
        builder.Append(CultureInfo.InvariantCulture, $"<path fill=\"{foreground}\" shape-rendering=\"crispEdges\" d=\"{BuildSquarePath(code, quiet, finderOnly: ModuleShape != QrCodeModuleShape.Square)}\"/>");

        if (ModuleShape != QrCodeModuleShape.Square)
        {
            builder.Append(CultureInfo.InvariantCulture, $"<g fill=\"{foreground}\">");
            AppendShapedModules(builder, code, quiet);
            builder.Append("</g>");
        }

        AppendCentreImage(builder, span);

        builder.Append("</svg>");
        return builder.ToString();
    }

    /// <summary>
    /// 构建横向连续段合并后的路径，以降低大二维码的节点数量。
    /// </summary>
    /// <param name="code">已编码的网格。</param>
    /// <param name="quiet">静区宽度（以模块计）。</param>
    /// <param name="finderOnly">
    /// <see langword="true"/> 表示只绘制三个定位图案，其余留给塑形
    /// 通道。
    /// </param>
    private static string BuildSquarePath(QrMatrix code, int quiet, bool finderOnly)
    {
        var builder = new StringBuilder(code.Size * code.Size / 2);

        for (var y = 0; y < code.Size; y++)
        {
            var x = 0;
            while (x < code.Size)
            {
                if (!code[x, y] || (finderOnly && !IsFinderPattern(code.Size, x, y)))
                {
                    x++;
                    continue;
                }

                var run = 0;
                while (x + run < code.Size
                    && code[x + run, y]
                    && (!finderOnly || IsFinderPattern(code.Size, x + run, y)))
                {
                    run++;
                }

                builder.Append(CultureInfo.InvariantCulture, $"M{x + quiet} {y + quiet}h{run}v1h-{run}z");
                x += run;
            }
        }

        return builder.ToString();
    }

    private void AppendShapedModules(StringBuilder builder, QrMatrix code, int quiet)
    {
        for (var y = 0; y < code.Size; y++)
        {
            for (var x = 0; x < code.Size; x++)
            {
                if (!code[x, y] || IsFinderPattern(code.Size, x, y))
                {
                    continue;
                }

                if (ModuleShape == QrCodeModuleShape.Dot)
                {
                    builder.Append(CultureInfo.InvariantCulture, $"<circle cx=\"{x + quiet + 0.5:0.##}\" cy=\"{y + quiet + 0.5:0.##}\" r=\"0.45\"/>");
                }
                else
                {
                    builder.Append(CultureInfo.InvariantCulture, $"<rect x=\"{x + quiet}\" y=\"{y + quiet}\" width=\"1\" height=\"1\" rx=\"0.3\"/>");
                }
            }
        }
    }

    private void AppendCentreImage(StringBuilder builder, int span)
    {
        if (string.IsNullOrEmpty(Image))
        {
            return;
        }

        // 以码而非整个 SVG 为基准定尺寸，静区宽窄不影响它。
        var width = (span - (EffectiveQuietZone * 2)) * EffectiveImageSize;
        var offset = (span - width) / 2;
        var padding = width * 0.08;
        var patch = width + (padding * 2);
        var patchOffset = offset - padding;
        var patchFill = WebUtility.HtmlEncode(ImageBackground ?? Background);

        builder.Append(CultureInfo.InvariantCulture, $"<rect x=\"{patchOffset:0.###}\" y=\"{patchOffset:0.###}\" width=\"{patch:0.###}\" height=\"{patch:0.###}\" rx=\"{patch * 0.1:0.###}\" fill=\"{patchFill}\"/>");
        builder.Append(CultureInfo.InvariantCulture, $"<image x=\"{offset:0.###}\" y=\"{offset:0.###}\" width=\"{width:0.###}\" height=\"{width:0.###}\" href=\"{WebUtility.HtmlEncode(Image)}\" preserveAspectRatio=\"xMidYMid meet\"/>");
    }

    /// <summary>
    /// 获取某模块是否属于三个七乘七定位图案之一。
    /// </summary>
    private static bool IsFinderPattern(int size, int x, int y) =>
        (x < 7 && y < 7)
        || (x >= size - 7 && y < 7)
        || (x < 7 && y >= size - 7);
}
