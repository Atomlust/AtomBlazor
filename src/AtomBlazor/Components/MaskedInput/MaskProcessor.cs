using System.Text;

namespace AtomBlazor;

/// <summary>
/// 处理掩码模式并将其应用到输入值上。
/// </summary>
public class MaskProcessor
{
    private readonly string _mask;
    private readonly char _placeholderChar;

    public MaskProcessor(string mask, char placeholderChar = '_')
    {
        _mask = mask;
        _placeholderChar = placeholderChar;
    }

    /// <summary>
    /// 获取掩码模式。
    /// </summary>
    public string Mask => _mask;

    /// <summary>
    /// 获取占位字符。
    /// </summary>
    public char PlaceholderChar => _placeholderChar;

    /// <summary>
    /// 检查掩码某个位置上的字符是否为字面量（不可编辑）。
    /// </summary>
    public bool IsLiteral(int position)
    {
        if (position < 0 || position >= _mask.Length)
        {
            return false;
        }

        var maskChar = _mask[position];
        return !IsMaskChar(maskChar);
    }

    /// <summary>
    /// 检查某个字符是否为掩码占位字符。
    /// </summary>
    public static bool IsMaskChar(char c) => c == '9' || c == 'A' || c == '*';

    /// <summary>
    /// 检查输入字符对某位置上的掩码字符是否有效。
    /// </summary>
    public bool IsValidChar(char input, int position)
    {
        if (position < 0 || position >= _mask.Length)
        {
            return false;
        }

        var maskChar = _mask[position];

        return maskChar switch
        {
            '9' => char.IsDigit(input),
            'A' => char.IsLetter(input),
            '*' => char.IsLetterOrDigit(input),
            _ => input == maskChar // 字面量精确匹配
        };
    }

    /// <summary>
    /// 从给定位置起获取下一个可编辑位置。
    /// </summary>
    public int GetNextEditablePosition(int position)
    {
        for (var i = position; i < _mask.Length; i++)
        {
            if (IsMaskChar(_mask[i]))
            {
                return i;
            }
        }
        return -1;
    }

    /// <summary>
    /// 从给定位置起获取上一个可编辑位置。
    /// </summary>
    public int GetPrevEditablePosition(int position)
    {
        for (var i = position - 1; i >= 0; i--)
        {
            if (IsMaskChar(_mask[i]))
            {
                return i;
            }
        }
        return -1;
    }

    /// <summary>
    /// 对原始输入应用掩码并返回格式化后的值。
    /// </summary>
    public string ApplyMask(string? rawInput)
    {
        if (string.IsNullOrEmpty(rawInput))
        {
            return GetEmptyMask();
        }

        var result = new StringBuilder(_mask.Length);
        var inputIndex = 0;

        for (var maskIndex = 0; maskIndex < _mask.Length; maskIndex++)
        {
            var maskChar = _mask[maskIndex];

            if (IsMaskChar(maskChar))
            {
                // 寻找下一个有效的输入字符
                while (inputIndex < rawInput.Length)
                {
                    var inputChar = rawInput[inputIndex++];

                    // 跳过输入中的字面量字符
                    if (IsValidForMaskChar(inputChar, maskChar))
                    {
                        result.Append(inputChar);
                        break;
                    }
                }

                // 若输入已耗尽，补上占位字符
                if (result.Length <= maskIndex)
                {
                    result.Append(_placeholderChar);
                }
            }
            else
            {
                // 字面量字符
                result.Append(maskChar);

                // 跳过输入中与之匹配的字面量
                if (inputIndex < rawInput.Length && rawInput[inputIndex] == maskChar)
                {
                    inputIndex++;
                }
            }
        }

        return result.ToString();
    }

    /// <summary>
    /// 提取原始的去掩码值。
    /// </summary>
    public string GetUnmaskedValue(string? maskedValue)
    {
        if (string.IsNullOrEmpty(maskedValue))
        {
            return string.Empty;
        }

        var result = new StringBuilder();

        var length = Math.Min(maskedValue.Length, _mask.Length);
        for (var i = 0; i < length; i++)
        {
            var maskChar = _mask[i];
            var valueChar = maskedValue[i];

            if (IsMaskChar(maskChar) && valueChar != _placeholderChar)
            {
                result.Append(valueChar);
            }
        }

        return result.ToString();
    }

    /// <summary>
    /// 获取带占位字符的掩码。
    /// </summary>
    public string GetEmptyMask()
    {
        var result = new StringBuilder(_mask.Length);

        foreach (var c in _mask)
        {
            result.Append(IsMaskChar(c) ? _placeholderChar : c);
        }

        return result.ToString();
    }

    /// <summary>
    /// 检查输入字符对指定掩码字符是否有效。
    /// </summary>
    private static bool IsValidForMaskChar(char input, char maskChar)
    {
        return maskChar switch
        {
            '9' => char.IsDigit(input),
            'A' => char.IsLetter(input),
            '*' => char.IsLetterOrDigit(input),
            _ => false
        };
    }

    /// <summary>
    /// 处理输入并返回新的掩码值与光标位置。
    /// </summary>
    public (string Value, int CursorPosition) ProcessInput(string currentValue, string newInput, int cursorPosition)
    {
        // 从新输入中提取原始字符
        var rawChars = new StringBuilder();
        foreach (var c in newInput)
        {
            if (char.IsLetterOrDigit(c))
            {
                rawChars.Append(c);
            }
        }

        var masked = ApplyMask(rawChars.ToString());

        // 计算新的光标位置
        var newCursorPos = GetNextEditablePosition(0);
        var inputCount = 0;

        for (var i = 0; i < masked.Length && inputCount < rawChars.Length; i++)
        {
            if (IsMaskChar(_mask[i]) && masked[i] != _placeholderChar)
            {
                inputCount++;
                newCursorPos = i + 1;
            }
        }

        // 若停在字面量上，前进到下一个可编辑位置
        if (newCursorPos < _mask.Length && IsLiteral(newCursorPos))
        {
            var nextEdit = GetNextEditablePosition(newCursorPos);
            if (nextEdit >= 0)
            {
                newCursorPos = nextEdit;
            }
        }

        return (masked, newCursorPos);
    }
}
