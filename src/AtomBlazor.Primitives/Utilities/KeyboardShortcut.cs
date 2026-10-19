namespace AtomBlazor.Primitives.Utilities;

/// <summary>
/// 表示一个由修饰键与主键构成的键盘快捷键定义。
/// 提供解析、规范化与显示格式化能力。
/// </summary>
public class KeyboardShortcut
{
    /// <summary>
    /// 主键（例如 "T"、"N"、"Delete"、"F1"）。
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// 是否要求 Ctrl（Mac 上为 Cmd）。
    /// </summary>
    public bool Ctrl { get; set; }

    /// <summary>
    /// 是否要求 Shift。
    /// </summary>
    public bool Shift { get; set; }

    /// <summary>
    /// 是否要求 Alt（Mac 上为 Option）。
    /// </summary>
    public bool Alt { get; set; }

    /// <summary>
    /// 是否要求 Meta（Windows 键或 Mac 上的 Cmd）。
    /// 注意：为实现跨平台快捷键，Ctrl 与 Meta 通常被同等对待。
    /// </summary>
    public bool Meta { get; set; }

    /// <summary>
    /// 把 "Ctrl+Shift+T" 这样的快捷键字符串解析为 <see cref="KeyboardShortcut"/>。
    /// </summary>
    /// <param name="shortcutString">快捷键字符串（例如 "Ctrl+N"、"Shift+Delete"、"Ctrl+Shift+S"）。</param>
    /// <returns>一个 <see cref="KeyboardShortcut"/> 实例。</returns>
    /// <exception cref="ArgumentException">快捷键字符串非法时抛出。</exception>
    public static KeyboardShortcut Parse(string shortcutString)
    {
        if (string.IsNullOrWhiteSpace(shortcutString))
        {
            throw new ArgumentException("快捷键字符串不能为 null 或空。", nameof(shortcutString));
        }

        var shortcut = new KeyboardShortcut();
        var parts = shortcutString.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length == 0)
        {
            throw new ArgumentException("快捷键字符串必须至少包含一个键。", nameof(shortcutString));
        }

        foreach (var part in parts)
        {
            var lowerPart = part.ToLowerInvariant();

            switch (lowerPart)
            {
                case "ctrl":
                case "control":
                case "cmd":
                case "command":
                    shortcut.Ctrl = true;
                    break;
                case "shift":
                    shortcut.Shift = true;
                    break;
                case "alt":
                case "option":
                case "opt":
                    shortcut.Alt = true;
                    break;
                case "meta":
                case "win":
                case "windows":
                    shortcut.Meta = true;
                    break;
                default:
                    // 其余的都是主键
                    if (!string.IsNullOrEmpty(shortcut.Key))
                    {
                        throw new ArgumentException(
                            $"快捷键字符串只能有一个主键。同时发现了 '{shortcut.Key}' 与 '{part}'。",
                            nameof(shortcutString));
                    }
                    shortcut.Key = NormalizeKey(part);
                    break;
            }
        }

        if (string.IsNullOrEmpty(shortcut.Key))
        {
            throw new ArgumentException("快捷键字符串必须包含主键（不能只有修饰键）。", nameof(shortcutString));
        }

        return shortcut;
    }

    /// <summary>
    /// 尝试解析快捷键字符串，失败时返回 false。
    /// </summary>
    /// <param name="shortcutString">要解析的快捷键字符串。</param>
    /// <param name="shortcut">解析结果；失败时为 null。</param>
    /// <returns>解析成功返回 true，否则返回 false。</returns>
    public static bool TryParse(string? shortcutString, out KeyboardShortcut? shortcut)
    {
        shortcut = null;
        if (string.IsNullOrWhiteSpace(shortcutString))
        {
            return false;
        }

        try
        {
            shortcut = Parse(shortcutString);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    /// <summary>
    /// 返回用于比较的规范化组合键字符串。
    /// 格式："ctrl+shift+alt+meta+key"（小写、顺序固定）。
    /// </summary>
    /// <returns>快捷键的规范化字符串表示。</returns>
    public string GetNormalizedKey()
    {
        var parts = new List<string>();

        if (Ctrl)
        {
            parts.Add("ctrl");
        }

        if (Shift)
        {
            parts.Add("shift");
        }

        if (Alt)
        {
            parts.Add("alt");
        }

        if (Meta)
        {
            parts.Add("meta");
        }

        parts.Add(Key.ToLowerInvariant());

        return string.Join("+", parts);
    }

    /// <summary>
    /// 返回快捷键的显示字符串。
    /// </summary>
    /// <param name="useMacSymbols">为 true 时使用 Mac 风格符号（⌘、⇧、⌥、⌃）。默认为 false。</param>
    /// <returns>人类可读的显示字符串。</returns>
    public string ToDisplayString(bool useMacSymbols = false)
    {
        var parts = new List<string>();

        if (useMacSymbols)
        {
            if (Ctrl)
            {
                parts.Add("\u2318");
            }

            if (Shift)
            {
                parts.Add("\u21e7");
            }

            if (Alt)
            {
                parts.Add("\u2325");
            }

            if (Meta)
            {
                parts.Add("\u2303");
            }
        }
        else
        {
            if (Ctrl)
            {
                parts.Add("Ctrl");
            }

            if (Shift)
            {
                parts.Add("Shift");
            }

            if (Alt)
            {
                parts.Add("Alt");
            }

            if (Meta)
            {
                parts.Add("Win");
            }
        }

        // 显示时把主键首字母大写
        var displayKey = Key.Length == 1 ? Key.ToUpperInvariant() : CapitalizeFirst(Key);
        parts.Add(displayKey);

        return useMacSymbols ? string.Concat(parts) : string.Join("+", parts);
    }

    /// <summary>
    /// 判断本快捷键是否与某个键盘事件匹配。
    /// </summary>
    /// <param name="key">来自键盘事件的键。</param>
    /// <param name="ctrlKey">是否按下了 Ctrl。</param>
    /// <param name="shiftKey">是否按下了 Shift。</param>
    /// <param name="altKey">是否按下了 Alt。</param>
    /// <param name="metaKey">是否按下了 Meta。</param>
    /// <returns>事件与本快捷键匹配时为 true。</returns>
    public bool Matches(string key, bool ctrlKey, bool shiftKey, bool altKey, bool metaKey)
    {
        // 规范化 Ctrl/Meta——为实现跨平台而同等对待
        var eventHasCtrlOrMeta = ctrlKey || metaKey;
        var shortcutNeedsCtrlOrMeta = Ctrl || Meta;

        if (shortcutNeedsCtrlOrMeta != eventHasCtrlOrMeta)
        {
            return false;
        }

        if (Shift != shiftKey)
        {
            return false;
        }

        if (Alt != altKey)
        {
            return false;
        }

        // 主键比较不区分大小写
        return string.Equals(Key, NormalizeKey(key), StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public override string ToString() => ToDisplayString();

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        if (obj is KeyboardShortcut other)
        {
            return GetNormalizedKey() == other.GetNormalizedKey();
        }
        return false;
    }

    /// <inheritdoc />
    public override int GetHashCode() => GetNormalizedKey().GetHashCode();

    private static string NormalizeKey(string key)
    {
        // 规范化常见键名
        return key.ToLowerInvariant() switch
        {
            " " or "space" or "spacebar" => "space",
            "esc" or "escape" => "escape",
            "del" or "delete" => "delete",
            "ins" or "insert" => "insert",
            "pgup" or "pageup" => "pageup",
            "pgdn" or "pagedown" => "pagedown",
            "arrowup" or "up" => "arrowup",
            "arrowdown" or "down" => "arrowdown",
            "arrowleft" or "left" => "arrowleft",
            "arrowright" or "right" => "arrowright",
            "return" or "enter" => "enter",
            "backspace" or "back" => "backspace",
            "tab" => "tab",
            _ => key.ToLowerInvariant()
        };
    }

    private static string CapitalizeFirst(string s)
    {
        if (string.IsNullOrEmpty(s))
        {
            return s;
        }
        return char.ToUpperInvariant(s[0]) + s.Substring(1).ToLowerInvariant();
    }
}
