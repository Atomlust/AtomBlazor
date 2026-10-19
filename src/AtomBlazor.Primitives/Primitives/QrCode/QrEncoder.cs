using System.Globalization;
using System.Text;

namespace AtomBlazor.Primitives.QrCode;

/// <summary>
/// 按 ISO/IEC 18004 将文本编码为 <see cref="QrMatrix"/>。
/// </summary>
/// <remarks>
/// <para>
/// 编码器先选出整个字符串能容纳的最紧凑模式（数字、字母数字或 UTF-8 字节）
/// 再选出在指定纠错等级下能容纳它的最小版本
/// 最后选出按规范罚分规则得分最优的掩码图案。整个过程是
/// 确定性的：相同文本与等级总是产生相同网格。
/// </para>
/// <para>
/// 整个字符串只选一种模式，而不是拆分为混合模式分段。
/// 因此以数字为主、夹一个小写 ASCII 字母的字符串会按字节编码，与最优拆分相比
/// 可能多占一个版本。更简单的规则更易推断，而且
/// 差异只在冗长混合的字符串上才会显现。
/// </para>
/// </remarks>
public static class QrEncoder
{
    private const string alphanumericCharset = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ $%*+-./:";

    private const int minVersionNumber = 1;
    private const int maxVersionNumber = 40;

    private const int penaltyRun = 3;
    private const int penaltyBlock = 3;
    private const int penaltyFinderLike = 40;
    private const int penaltyImbalance = 10;

    // 按 [纠错等级][版本] 索引。版本轴的下标 0 不使用。
    private static readonly int[][] eccCodewordsPerBlockTable =
    [
        [0, 7, 10, 15, 20, 26, 18, 20, 24, 30, 18, 20, 24, 26, 30, 22, 24, 28, 30, 28, 28, 28, 28, 30, 30, 26, 28, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30],
        [0, 10, 16, 26, 18, 24, 16, 18, 22, 22, 26, 30, 22, 22, 24, 24, 28, 28, 26, 26, 26, 26, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28],
        [0, 13, 22, 18, 26, 18, 24, 18, 22, 20, 24, 28, 26, 24, 20, 30, 24, 28, 28, 26, 30, 28, 30, 30, 30, 30, 28, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30],
        [0, 17, 28, 22, 16, 22, 28, 26, 26, 24, 28, 24, 28, 22, 24, 24, 30, 28, 28, 26, 28, 30, 24, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30],
    ];

    // 按 [纠错等级][版本] 索引。版本轴的下标 0 不使用。
    private static readonly int[][] errorCorrectionBlockCountTable =
    [
        [0, 1, 1, 1, 1, 1, 2, 2, 2, 2, 4, 4, 4, 4, 4, 6, 6, 6, 6, 7, 8, 8, 9, 9, 10, 12, 12, 12, 13, 14, 15, 16, 17, 18, 19, 19, 20, 21, 22, 24, 25],
        [0, 1, 1, 1, 2, 2, 4, 4, 4, 5, 5, 5, 8, 9, 9, 10, 10, 11, 13, 14, 16, 17, 17, 18, 20, 21, 23, 25, 26, 28, 29, 31, 33, 35, 37, 38, 40, 43, 45, 47, 49],
        [0, 1, 1, 2, 2, 4, 4, 6, 6, 8, 8, 8, 10, 12, 16, 12, 17, 16, 18, 21, 20, 23, 23, 25, 27, 29, 34, 34, 35, 38, 40, 43, 45, 48, 51, 53, 56, 59, 62, 65, 68],
        [0, 1, 1, 2, 4, 4, 4, 5, 6, 8, 8, 11, 11, 16, 16, 18, 16, 19, 21, 25, 25, 25, 34, 30, 32, 35, 37, 40, 42, 45, 48, 51, 54, 57, 60, 63, 66, 70, 74, 77, 81],
    ];

    // 五位格式信息与枚举的表序不同：Low 01、Medium 00、Quartile 11、High 10。
    private static readonly int[] formatBitsTable = [1, 0, 3, 2];

    /// <summary>
    /// 将文本编码为二维码。
    /// </summary>
    /// <param name="value">待编码文本，不得为空。</param>
    /// <param name="errorCorrection">二维码应能承受的损坏程度。</param>
    /// <param name="minVersion">
    /// 考虑的最小版本，1 到 40。调高它可以强制二维码
    /// 不小于某一尺寸；数据需要更多空间时编码器仍会继续升版。
    /// </param>
    /// <param name="mask">
    /// 要应用的掩码图案，0 到 7；取 -1 表示自动选择得分最优的掩码。
    /// 除非需要精确复现特定二维码，否则保持 -1。
    /// </param>
    /// <returns>成品模块网格。</returns>
    /// <exception cref="ArgumentException">
    /// 当 <paramref name="value"/> 为空，或在该纠错等级下
    /// 无法放入版本 40 的二维码时抛出。
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="minVersion"/> 超出 1 到 40，或 <paramref name="mask"/> 超出 -1 到 7。
    /// </exception>
    public static QrMatrix Encode(
        string value,
        QrErrorCorrection errorCorrection = QrErrorCorrection.Medium,
        int minVersion = minVersionNumber,
        int mask = -1)
    {
        ArgumentException.ThrowIfNullOrEmpty(value);
        ArgumentOutOfRangeException.ThrowIfLessThan(minVersion, minVersionNumber);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(minVersion, maxVersionNumber);
        ArgumentOutOfRangeException.ThrowIfLessThan(mask, -1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(mask, 7);

        var mode = ChooseMode(value);
        var payload = mode == QrEncodingMode.Byte ? Encoding.UTF8.GetBytes(value) : [];
        var characterCount = mode == QrEncodingMode.Byte ? payload.Length : value.Length;
        var payloadBits = CountPayloadBits(value, mode, payload);

        var version = ChooseVersion(mode, payloadBits, errorCorrection, minVersion);
        if (version == 0)
        {
            var largest = GetCapacity(maxVersionNumber, errorCorrection, mode);
            throw new ArgumentException(
                $"The value needs {characterCount} characters of {mode} capacity, which is more than the {largest} a version 40 code holds at {errorCorrection} error correction. Shorten the value or lower the error correction level.",
                nameof(value));
        }

        BitWriter writer = new();
        writer.Append(ModeIndicator(mode), 4);
        writer.Append(characterCount, CharacterCountBits(mode, version));
        AppendPayload(writer, value, mode, payload);

        var capacityBits = DataCodewordCount(version, errorCorrection) * 8;
        writer.Append(0, Math.Min(4, capacityBits - writer.Length));
        writer.Append(0, (8 - (writer.Length % 8)) % 8);
        for (var padByte = 0xEC; writer.Length < capacityBits; padByte ^= 0xEC ^ 0x11)
        {
            writer.Append(padByte, 8);
        }

        var codewords = AddErrorCorrectionAndInterleave(writer.ToBytes(), version, errorCorrection);
        return Draw(codewords, version, errorCorrection, mask, mode);
    }

    /// <summary>
    /// 获取给定模式与等级下某版本可容纳的最大字符数。
    /// </summary>
    /// <param name="version">二维码版本，1 到 40。</param>
    /// <param name="errorCorrection">纠错等级。</param>
    /// <param name="mode">编码模式。<see cref="QrEncodingMode.Byte"/> 按 UTF-8 字节数而非字符数计。</param>
    /// <returns>The character capacity.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="version"/> 超出 1 到 40 时抛出。</exception>
    public static int GetCapacity(int version, QrErrorCorrection errorCorrection, QrEncodingMode mode)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(version, minVersionNumber);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(version, maxVersionNumber);

        var bits = (DataCodewordCount(version, errorCorrection) * 8) - 4 - CharacterCountBits(mode, version);
        if (bits <= 0)
        {
            return 0;
        }

        return mode switch
        {
            QrEncodingMode.Numeric => (bits / 10 * 3) + ((bits % 10) >= 7 ? 2 : (bits % 10) >= 4 ? 1 : 0),
            QrEncodingMode.Alphanumeric => (bits / 11 * 2) + ((bits % 11) >= 6 ? 1 : 0),
            _ => bits / 8,
        };
    }

    private static QrEncodingMode ChooseMode(string value)
    {
        var numeric = true;
        var alphanumeric = true;

        foreach (var c in value)
        {
            if (c is < '0' or > '9')
            {
                numeric = false;
            }

            if (alphanumericCharset.IndexOf(c, StringComparison.Ordinal) < 0)
            {
                alphanumeric = false;
            }

            if (!numeric && !alphanumeric)
            {
                break;
            }
        }

        return numeric ? QrEncodingMode.Numeric
            : alphanumeric ? QrEncodingMode.Alphanumeric
            : QrEncodingMode.Byte;
    }

    private static int ModeIndicator(QrEncodingMode mode) => mode switch
    {
        QrEncodingMode.Numeric => 0b0001,
        QrEncodingMode.Alphanumeric => 0b0010,
        _ => 0b0100,
    };

    private static int CharacterCountBits(QrEncodingMode mode, int version) => mode switch
    {
        QrEncodingMode.Numeric => version <= 9 ? 10 : version <= 26 ? 12 : 14,
        QrEncodingMode.Alphanumeric => version <= 9 ? 9 : version <= 26 ? 11 : 13,
        _ => version <= 9 ? 8 : 16,
    };

    private static int CountPayloadBits(string value, QrEncodingMode mode, byte[] payload) => mode switch
    {
        QrEncodingMode.Numeric => (value.Length / 3 * 10) + ((value.Length % 3) switch { 2 => 7, 1 => 4, _ => 0 }),
        QrEncodingMode.Alphanumeric => (value.Length / 2 * 11) + (value.Length % 2 * 6),
        _ => payload.Length * 8,
    };

    private static void AppendPayload(BitWriter writer, string value, QrEncodingMode mode, byte[] payload)
    {
        switch (mode)
        {
            case QrEncodingMode.Numeric:
                for (var i = 0; i < value.Length;)
                {
                    var take = Math.Min(value.Length - i, 3);
                    var digits = int.Parse(value.AsSpan(i, take), CultureInfo.InvariantCulture);
                    writer.Append(digits, (take * 3) + 1);
                    i += take;
                }

                break;

            case QrEncodingMode.Alphanumeric:
                var index = 0;
                for (; index + 2 <= value.Length; index += 2)
                {
                    var pair = (alphanumericCharset.IndexOf(value[index], StringComparison.Ordinal) * 45)
                        + alphanumericCharset.IndexOf(value[index + 1], StringComparison.Ordinal);
                    writer.Append(pair, 11);
                }

                if (index < value.Length)
                {
                    writer.Append(alphanumericCharset.IndexOf(value[index], StringComparison.Ordinal), 6);
                }

                break;

            default:
                foreach (var b in payload)
                {
                    writer.Append(b, 8);
                }

                break;
        }
    }

    /// <summary>
    /// 找出能容纳载荷的最小版本，均不能容纳时返回 0。
    /// </summary>
    private static int ChooseVersion(
        QrEncodingMode mode,
        int payloadBits,
        QrErrorCorrection errorCorrection,
        int minVersion)
    {
        for (var version = minVersion; version <= maxVersionNumber; version++)
        {
            var used = 4 + CharacterCountBits(mode, version) + payloadBits;
            if (used <= DataCodewordCount(version, errorCorrection) * 8)
            {
                return version;
            }
        }

        return 0;
    }

    private static int RawDataModuleCount(int version)
    {
        var result = (((16 * version) + 128) * version) + 64;
        if (version >= 2)
        {
            var alignmentCount = (version / 7) + 2;
            result -= (((25 * alignmentCount) - 10) * alignmentCount) - 55;
            if (version >= 7)
            {
                result -= 36;
            }
        }

        return result;
    }

    private static int DataCodewordCount(int version, QrErrorCorrection errorCorrection) =>
        (RawDataModuleCount(version) / 8)
        - (eccCodewordsPerBlockTable[(int)errorCorrection][version]
            * errorCorrectionBlockCountTable[(int)errorCorrection][version]);

    private static byte[] AddErrorCorrectionAndInterleave(byte[] data, int version, QrErrorCorrection errorCorrection)
    {
        var blockCount = errorCorrectionBlockCountTable[(int)errorCorrection][version];
        var blockEccLength = eccCodewordsPerBlockTable[(int)errorCorrection][version];
        var rawCodewords = RawDataModuleCount(version) / 8;
        var shortBlockCount = blockCount - (rawCodewords % blockCount);
        var shortBlockLength = rawCodewords / blockCount;

        var divisor = QrGaloisField.ComputeDivisor(blockEccLength);
        var blocks = new byte[blockCount][];

        for (int i = 0, offset = 0; i < blockCount; i++)
        {
            // 前段分块比末尾分块少一个码字，这样交织时
            // 可按等长处理并跳过缺失的位置。
            var dataLength = shortBlockLength - blockEccLength + (i < shortBlockCount ? 0 : 1);
            var block = new byte[shortBlockLength + 1];
            Array.Copy(data, offset, block, 0, dataLength);

            var ecc = QrGaloisField.ComputeRemainder(data.AsSpan(offset, dataLength), divisor);
            Array.Copy(ecc, 0, block, block.Length - blockEccLength, blockEccLength);
            blocks[i] = block;
            offset += dataLength;
        }

        var result = new byte[rawCodewords];
        for (int i = 0, k = 0; i < blocks[0].Length; i++)
        {
            for (var j = 0; j < blocks.Length; j++)
            {
                // 短分块在最后一个数据位置没有码字，此处需跳过它们。
                if (i != shortBlockLength - blockEccLength || j >= shortBlockCount)
                {
                    result[k] = blocks[j][i];
                    k++;
                }
            }
        }

        return result;
    }

    private static QrMatrix Draw(
        byte[] codewords,
        int version,
        QrErrorCorrection errorCorrection,
        int mask,
        QrEncodingMode mode)
    {
        var size = (version * 4) + 17;
        var modules = new bool[size * size];
        var isFunction = new bool[size * size];

        DrawFunctionPatterns(modules, isFunction, size, version, errorCorrection);
        DrawCodewords(modules, isFunction, size, codewords);

        var chosen = mask;
        if (chosen < 0)
        {
            var bestPenalty = int.MaxValue;
            chosen = 0;
            for (var candidate = 0; candidate < 8; candidate++)
            {
                ApplyMask(modules, isFunction, size, candidate);
                DrawFormatBits(modules, isFunction, size, errorCorrection, candidate);
                var penalty = PenaltyScore(modules, size);
                if (penalty < bestPenalty)
                {
                    bestPenalty = penalty;
                    chosen = candidate;
                }

                ApplyMask(modules, isFunction, size, candidate);
            }
        }

        ApplyMask(modules, isFunction, size, chosen);
        DrawFormatBits(modules, isFunction, size, errorCorrection, chosen);

        return new QrMatrix(size, modules, version, errorCorrection, chosen, mode);
    }

    private static void DrawFunctionPatterns(
        bool[] modules,
        bool[] isFunction,
        int size,
        int version,
        QrErrorCorrection errorCorrection)
    {
        for (var i = 0; i < size; i++)
        {
            SetFunctionModule(modules, isFunction, size, 6, i, i % 2 == 0);
            SetFunctionModule(modules, isFunction, size, i, 6, i % 2 == 0);
        }

        DrawFinderPattern(modules, isFunction, size, 3, 3);
        DrawFinderPattern(modules, isFunction, size, size - 4, 3);
        DrawFinderPattern(modules, isFunction, size, 3, size - 4);

        var positions = AlignmentPatternPositions(version, size);
        for (var i = 0; i < positions.Length; i++)
        {
            for (var j = 0; j < positions.Length; j++)
            {
                var overlapsFinder = (i == 0 && j == 0)
                    || (i == 0 && j == positions.Length - 1)
                    || (i == positions.Length - 1 && j == 0);
                if (!overlapsFinder)
                {
                    DrawAlignmentPattern(modules, isFunction, size, positions[i], positions[j]);
                }
            }
        }

        // 先用占位掩码预留这些模块，使数据放置时跳过它们
        // 掩码选定后再写入真正的格式位。
        DrawFormatBits(modules, isFunction, size, errorCorrection, 0);
        DrawVersionBits(modules, isFunction, size, version);
    }

    private static int[] AlignmentPatternPositions(int version, int size)
    {
        if (version == 1)
        {
            return [];
        }

        var count = (version / 7) + 2;
        var step = version == 32 ? 26 : ((version * 4) + (count * 2) + 1) / ((count * 2) - 2) * 2;

        var result = new int[count];
        result[0] = 6;
        for (int i = count - 1, position = size - 7; i >= 1; i--, position -= step)
        {
            result[i] = position;
        }

        return result;
    }

    private static void DrawFinderPattern(bool[] modules, bool[] isFunction, int size, int x, int y)
    {
        for (var dy = -4; dy <= 4; dy++)
        {
            for (var dx = -4; dx <= 4; dx++)
            {
                var distance = Math.Max(Math.Abs(dx), Math.Abs(dy));
                var xx = x + dx;
                var yy = y + dy;
                if (xx >= 0 && xx < size && yy >= 0 && yy < size)
                {
                    SetFunctionModule(modules, isFunction, size, xx, yy, distance != 2 && distance != 4);
                }
            }
        }
    }

    private static void DrawAlignmentPattern(bool[] modules, bool[] isFunction, int size, int x, int y)
    {
        for (var dy = -2; dy <= 2; dy++)
        {
            for (var dx = -2; dx <= 2; dx++)
            {
                SetFunctionModule(modules, isFunction, size, x + dx, y + dy, Math.Max(Math.Abs(dx), Math.Abs(dy)) != 1);
            }
        }
    }

    private static void DrawFormatBits(
        bool[] modules,
        bool[] isFunction,
        int size,
        QrErrorCorrection errorCorrection,
        int mask)
    {
        var data = (formatBitsTable[(int)errorCorrection] << 3) | mask;
        var remainder = data;
        for (var i = 0; i < 10; i++)
        {
            remainder = (remainder << 1) ^ ((remainder >>> 9) * 0x537);
        }

        var bits = ((data << 10) | remainder) ^ 0x5412;

        for (var i = 0; i <= 5; i++)
        {
            SetFunctionModule(modules, isFunction, size, 8, i, GetBit(bits, i));
        }

        SetFunctionModule(modules, isFunction, size, 8, 7, GetBit(bits, 6));
        SetFunctionModule(modules, isFunction, size, 8, 8, GetBit(bits, 7));
        SetFunctionModule(modules, isFunction, size, 7, 8, GetBit(bits, 8));
        for (var i = 9; i < 15; i++)
        {
            SetFunctionModule(modules, isFunction, size, 14 - i, 8, GetBit(bits, i));
        }

        for (var i = 0; i < 8; i++)
        {
            SetFunctionModule(modules, isFunction, size, size - 1 - i, 8, GetBit(bits, i));
        }

        for (var i = 8; i < 15; i++)
        {
            SetFunctionModule(modules, isFunction, size, 8, size - 15 + i, GetBit(bits, i));
        }

        // 左下定位图案上方的模块恒为深色。
        SetFunctionModule(modules, isFunction, size, 8, size - 8, true);
    }

    private static void DrawVersionBits(bool[] modules, bool[] isFunction, int size, int version)
    {
        if (version < 7)
        {
            return;
        }

        var remainder = version;
        for (var i = 0; i < 12; i++)
        {
            remainder = (remainder << 1) ^ ((remainder >>> 11) * 0x1F25);
        }

        var bits = (version << 12) | remainder;
        for (var i = 0; i < 18; i++)
        {
            var bit = GetBit(bits, i);
            var a = size - 11 + (i % 3);
            var b = i / 3;
            SetFunctionModule(modules, isFunction, size, a, b, bit);
            SetFunctionModule(modules, isFunction, size, b, a, bit);
        }
    }

    private static void DrawCodewords(bool[] modules, bool[] isFunction, int size, byte[] codewords)
    {
        var bit = 0;
        for (var right = size - 1; right >= 1; right -= 2)
        {
            // 第 6 列是纵向定时图案，列对需左移越过它。
            if (right == 6)
            {
                right = 5;
            }

            for (var vertical = 0; vertical < size; vertical++)
            {
                for (var j = 0; j < 2; j++)
                {
                    var x = right - j;
                    var upward = ((right + 1) & 2) == 0;
                    var y = upward ? size - 1 - vertical : vertical;
                    var index = (y * size) + x;
                    if (!isFunction[index] && bit < codewords.Length * 8)
                    {
                        modules[index] = GetBit(codewords[bit >>> 3], 7 - (bit & 7));
                        bit++;
                    }
                }
            }
        }
    }

    private static void ApplyMask(bool[] modules, bool[] isFunction, int size, int mask)
    {
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var invert = mask switch
                {
                    0 => (x + y) % 2 == 0,
                    1 => y % 2 == 0,
                    2 => x % 3 == 0,
                    3 => (x + y) % 3 == 0,
                    4 => (((x / 3) + (y / 2)) % 2) == 0,
                    5 => (x * y % 2) + (x * y % 3) == 0,
                    6 => (((x * y % 2) + (x * y % 3)) % 2) == 0,
                    _ => ((((x + y) % 2) + (x * y % 3)) % 2) == 0,
                };

                var index = (y * size) + x;
                if (invert && !isFunction[index])
                {
                    modules[index] = !modules[index];
                }
            }
        }
    }

    /// <summary>
    /// 按 ISO/IEC 18004 表 11 的四条罚分规则为成品符号打分，分数越低越好。
    /// </summary>
    private static int PenaltyScore(bool[] modules, int size)
    {
        var result = 0;

        for (var line = 0; line < size; line++)
        {
            result += RunPenalty(modules, size, line, horizontal: true);
            result += RunPenalty(modules, size, line, horizontal: false);
            result += FinderLikePenalty(modules, size, line, horizontal: true);
            result += FinderLikePenalty(modules, size, line, horizontal: false);
        }

        // N2：每一块二乘二的同色模块。
        for (var y = 0; y < size - 1; y++)
        {
            for (var x = 0; x < size - 1; x++)
            {
                var colour = modules[(y * size) + x];
                if (colour == modules[(y * size) + x + 1]
                    && colour == modules[((y + 1) * size) + x]
                    && colour == modules[((y + 1) * size) + x + 1])
                {
                    result += penaltyBlock;
                }
            }
        }

        // N4：深色模块占比偏离一半的程度，以五个百分点为一档。
        var dark = 0;
        foreach (var module in modules)
        {
            if (module)
            {
                dark++;
            }
        }

        var total = size * size;
        var imbalance = ((Math.Abs((dark * 20) - (total * 10)) + total - 1) / total) - 1;
        result += imbalance * penaltyImbalance;

        return result;
    }

    /// <summary>
    /// N1：五个及以上的同色连续模块，超过五个的每个模块再追加一分。
    /// </summary>
    private static int RunPenalty(bool[] modules, int size, int line, bool horizontal)
    {
        var result = 0;
        var runColour = ModuleAt(modules, size, line, 0, horizontal);
        var runLength = 1;

        for (var i = 1; i < size; i++)
        {
            var colour = ModuleAt(modules, size, line, i, horizontal);
            if (colour == runColour)
            {
                runLength++;
                continue;
            }

            if (runLength >= 5)
            {
                result += penaltyRun + runLength - 5;
            }

            runColour = colour;
            runLength = 1;
        }

        if (runLength >= 5)
        {
            result += penaltyRun + runLength - 5;
        }

        return result;
    }

    /// <summary>
    /// N3：会被扫码器误认作定位图案一部分的 :3: 深浅色序列。
    /// </summary>
    /// <remarks>
    /// 规范规定：该图案前方或后方有四模块宽的浅区时只计一次分
    /// 因此两侧皆有浅区的图案也只计一次而不是
    /// 两次。有些编码器会计两次；那只会改变哪个掩码胜出
    /// 不影响二维码能否被读出。
    /// </remarks>
    private static int FinderLikePenalty(bool[] modules, int size, int line, bool horizontal)
    {
        var result = 0;

        for (var i = 0; i + 7 <= size; i++)
        {
            if (!IsFinderLike(modules, size, line, i, horizontal))
            {
                continue;
            }

            // 符号外的静区是浅色，因此紧贴任一边缘的图案
            // 视为自带浅区。真正的定位图案正是由此在此计分。
            if (IsLightRun(modules, size, line, Math.Max(0, i - 4), i, horizontal)
                || IsLightRun(modules, size, line, i + 7, Math.Min(size, i + 11), horizontal))
            {
                result += penaltyFinderLike;
            }
        }

        return result;
    }

    private static bool IsFinderLike(bool[] modules, int size, int line, int start, bool horizontal) =>
        ModuleAt(modules, size, line, start, horizontal)
        && !ModuleAt(modules, size, line, start + 1, horizontal)
        && ModuleAt(modules, size, line, start + 2, horizontal)
        && ModuleAt(modules, size, line, start + 3, horizontal)
        && ModuleAt(modules, size, line, start + 4, horizontal)
        && !ModuleAt(modules, size, line, start + 5, horizontal)
        && ModuleAt(modules, size, line, start + 6, horizontal);

    private static bool IsLightRun(bool[] modules, int size, int line, int from, int to, bool horizontal)
    {
        for (var i = from; i < to; i++)
        {
            if (ModuleAt(modules, size, line, i, horizontal))
            {
                return false;
            }
        }

        return true;
    }

    private static bool ModuleAt(bool[] modules, int size, int line, int index, bool horizontal) =>
        horizontal ? modules[(line * size) + index] : modules[(index * size) + line];

    private static void SetFunctionModule(bool[] modules, bool[] isFunction, int size, int x, int y, bool dark)
    {
        var index = (y * size) + x;
        modules[index] = dark;
        isFunction[index] = true;
    }

    private static bool GetBit(int value, int position) => ((value >>> position) & 1) != 0;

    private sealed class BitWriter
    {
        private readonly List<bool> bits = [];

        public int Length => bits.Count;

        public void Append(int value, int count)
        {
            for (var i = count - 1; i >= 0; i--)
            {
                bits.Add(((value >>> i) & 1) != 0);
            }
        }

        public byte[] ToBytes()
        {
            var result = new byte[(bits.Count + 7) / 8];
            for (var i = 0; i < bits.Count; i++)
            {
                if (bits[i])
                {
                    result[i >>> 3] |= (byte)(1 << (7 - (i & 7)));
                }
            }

            return result;
        }
    }
}
