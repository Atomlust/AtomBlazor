using AtomBlazor.Primitives;
using AtomBlazor.Primitives.Barcode;
using Xunit;

namespace AtomBlazor.Tests.Barcode;

/// <summary>
/// 自 Barcode/BarcodeMessageTests.cs（七规则机械改名 →AtomBlazor、注释汉化；金样与断言字符串字面量逐字节保原样，方法名保英）。
/// <para>
/// 编码器的错误信息会展示给输入该值的人，因此它必须是编码器自己的措辞
/// 而且在任何宿主上都措辞一致。
/// </para>
/// <remarks>
/// <see cref="ArgumentException.Message"/> 会把参数名拼接到消息里，而后缀的措辞
/// 来自框架资源。在 WebAssembly 上这些资源常被裁剪，返回的取而代之是资源键——
/// 这就是含非法值的条码曾在页面上打印 <c>Arg_ParamName_Name</c> 的由来。
/// </remarks>
public class BarcodeMessageTests
{
    [Theory]
    [InlineData("", BarcodeType.Code128)]
    [InlineData("12345", BarcodeType.Ean13)]
    [InlineData("5901234123459", BarcodeType.Ean13)]
    [InlineData("hello", BarcodeType.Itf)]
    [InlineData("1", BarcodeType.Pharmacode)]
    [InlineData("not an isbn", BarcodeType.Isbn)]
    public void ARefusedValueSaysWhyWithoutAnyFrameworkWording(string value, BarcodeType type)
    {
        var error = Assert.Throws<BarcodeFormatException>(() => BarcodeEncoder.Encode(value, type));

        // 框架会追加的两样东西。任何一样出现在页面上都是必须有人处理的 bug。
        Assert.DoesNotContain("Arg_ParamName_Name", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("(Parameter", error.Message, StringComparison.Ordinal);

        // 仍然是一句话，而不是充当句子的空字符串。
        Assert.False(string.IsNullOrWhiteSpace(error.Message));
        Assert.EndsWith(".", error.Message.TrimEnd(), StringComparison.Ordinal);
    }

    [Fact]
    public void TheParameterNameIsStillThereForAnythingThatWantsIt()
    {
        // 保留在异常对象上，只是不留在文本里。
        var error = Assert.Throws<BarcodeFormatException>(() => BarcodeEncoder.Encode("12345", BarcodeType.Ean13));

        Assert.False(string.IsNullOrEmpty(error.ParamName));
    }

    [Fact]
    public void ItIsStillAnArgumentExceptionSoExistingCatchBlocksHold()
    {
        Assert.Throws<BarcodeFormatException>(() => BarcodeEncoder.Encode("", BarcodeType.Code128));

        var caught = Record.Exception(() => BarcodeEncoder.Encode("", BarcodeType.Code128));
        Assert.IsAssignableFrom<ArgumentException>(caught);
    }

    [Fact]
    public void AWrongCheckDigitSaysWhichDigitItShouldHaveBeen()
    {
        // 具体的消息正是不能用改显 DescribeInput 来打发这件事的原因：
        // "校验位不对"告诉人该改什么；"13 位"不告诉。
        var error = Assert.Throws<BarcodeFormatException>(
            () => BarcodeEncoder.Encode("5901234123459", BarcodeType.Ean13));

        Assert.Contains("check digit", error.Message, StringComparison.OrdinalIgnoreCase);
    }
}
