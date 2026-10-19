namespace AtomBlazor.Primitives.QrCode;

/// <summary>
/// 在 GF(256) 上按二维码域多项式 x^8 + x^4 + x^3 + x^2 + 1 进行 Reed-Solomon 运算。
/// </summary>
internal static class QrGaloisField
{
    private const int fieldPolynomial = 0x11D;

    /// <summary>
    /// 为一个具有给定纠错码字数量的分块构建生成多项式。
    /// </summary>
    /// <param name="degree">每个分块的纠错码字数量，1 到 255。</param>
    /// <returns>自最高次幂向下排列的系数，省略隐含的首项 1。</returns>
    internal static byte[] ComputeDivisor(int degree)
    {
        var result = new byte[degree];
        result[degree - 1] = 1;

        // 逐个根相乘，展开 (x - r^0)(x - r^1)...(x - r^(degree-1))。
        var root = 1;
        for (var i = 0; i < degree; i++)
        {
            for (var j = 0; j < degree; j++)
            {
                result[j] = Multiply(result[j], (byte)root);
                if (j + 1 < degree)
                {
                    result[j] ^= result[j + 1];
                }
            }

            root = Multiply((byte)root, 0x02);
        }

        return result;
    }

    /// <summary>
    /// 将数据除以生成多项式并返回余式，即该分块的
    /// 纠错码字。
    /// </summary>
    /// <param name="data">该分块的数据码字。</param>
    /// <param name="divisor">由 <see cref="ComputeDivisor"/> 得到的生成多项式。</param>
    /// <returns>纠错码字，除式每阶对应一个。</returns>
    internal static byte[] ComputeRemainder(ReadOnlySpan<byte> data, byte[] divisor)
    {
        var result = new byte[divisor.Length];
        foreach (var b in data)
        {
            var factor = (byte)(b ^ result[0]);
            Array.Copy(result, 1, result, 0, result.Length - 1);
            result[^1] = 0;
            for (var i = 0; i < result.Length; i++)
            {
                result[i] ^= Multiply(divisor[i], factor);
            }
        }

        return result;
    }

    /// <summary>
    /// 将两个域元素相乘。
    /// </summary>
    /// <param name="x">第一个元素。</param>
    /// <param name="y">第二个元素。</param>
    /// <returns>域中的乘积。</returns>
    internal static byte Multiply(byte x, byte y)
    {
        var result = 0;
        for (var i = 7; i >= 0; i--)
        {
            result = (result << 1) ^ ((result >>> 7) * fieldPolynomial);
            result ^= ((y >>> i) & 1) * x;
        }

        return (byte)result;
    }
}
