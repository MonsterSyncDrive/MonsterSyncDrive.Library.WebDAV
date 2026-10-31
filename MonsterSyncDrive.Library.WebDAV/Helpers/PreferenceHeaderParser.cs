/*
 * MIT License
 *
 * Copyright (c) 2025 - 2026 MonsterSyncDrive.Com
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */
namespace MonsterSyncDrive.Library.WebDAV.Helpers;

/// <summary>解析带引号、转义和参数的 HTTP 偏好及媒体类型字段，避免按裸逗号错误切分。</summary>
internal static class PreferenceHeaderParser
{
    /// <summary>按不在引号中的分隔符切分字段，同时拒绝控制字符与未闭合字符串。</summary>
    /// <param name="text">要切分的字段文本。</param>
    /// <param name="separator">分隔符，通常为逗号或分号。</param>
    /// <param name="allowEmptyItems">接收 HTTP 列表时忽略空项；验证单个发送项时保持 false。</param>
    /// <returns>去除项外空白后的非空片段。</returns>
    internal static IReadOnlyList<string> Split(string text, char separator, bool allowEmptyItems = false)
    {
        ArgumentNullException.ThrowIfNull(text);
        List<string> result = [];
        bool quoted = false;
        bool escaped = false;
        int start = 0;
        for (int index = 0; index < text.Length; index++)
        {
            char value = text[index];
            if (value is '\r' or '\n' or '\0' || (char.IsControl(value) && value != '\t'))
                throw new FormatException("Control characters are not allowed in an HTTP field.");
            if (escaped)
            {
                escaped = false;
                continue;
            }
            if (quoted && value == '\\')
            {
                escaped = true;
                continue;
            }
            if (value == '"')
            {
                quoted = !quoted;
                continue;
            }
            if (!quoted && value == separator)
            {
                Add(text[start..index]);
                start = index + 1;
            }
        }
        if (quoted || escaped)
            throw new FormatException("Unterminated quoted HTTP field value.");
        Add(text[start..]);
        return result.AsReadOnly();

        // HTTP 列表接收方需要容忍合理的空项；调用方构造单个扩展时则拒绝空项，防止多个偏好被伪装成一个。
        void Add(string item)
        {
            if (string.IsNullOrWhiteSpace(item))
            {
                if (allowEmptyItems)
                    return;
                throw new FormatException("An HTTP list item is empty.");
            }
            result.Add(item.Trim());
        }
    }

    /// <summary>解析单个偏好名称、可选值和可选参数，验证完整语法。</summary>
    /// <param name="item">一个不含列表分隔符的偏好项。</param>
    /// <returns>偏好名称、解码值及可供往返保存的参数后缀。</returns>
    internal static (string Name, string? Value, string Parameters) ParseItem(string item)
    {
        if (Split(item, ',').Count != 1)
            throw new FormatException("Expected exactly one preference.");
        IReadOnlyList<string> parts = Split(item, ';');
        (string? Name, string? Value) = ParsePair(parts[0]);
        foreach (string? part in parts.Skip(1))
            _ = ParsePair(part);
        return (Name, Value, parts.Count == 1 ? "" : "; " + string.Join("; ", parts.Skip(1)));
    }

    /// <summary>解析 token 或 token=value；引号值支持 quoted-pair。</summary>
    /// <param name="pair">待解析的偏好或参数。</param>
    /// <returns>名称与去引号后的值。</returns>
    private static (string Name, string? Value) ParsePair(string pair)
    {
        int equal = pair.IndexOf('=');
        string name = (equal < 0 ? pair : pair[..equal]).Trim();
        if (!IsToken(name))
            throw new FormatException("Invalid preference token.");
        if (equal < 0)
            return (name, null);
        string value = pair[(equal + 1)..].Trim();
        if (value.StartsWith('"'))
        {
            if (value.Length < 2 || !value.EndsWith('"'))
                throw new FormatException("Invalid quoted preference value.");
            var decoded = new System.Text.StringBuilder();
            for (int index = 1; index < value.Length - 1; index++)
            {
                char character = value[index];
                if (character == '\\')
                {
                    if (++index >= value.Length - 1)
                        throw new FormatException("Invalid quoted-pair.");
                    character = value[index];
                }
                else if (character == '"')
                    throw new FormatException("Unescaped quote in preference value.");
                _ = decoded.Append(character);
            }
            return (name, decoded.ToString());
        }
        return !IsToken(value) ? throw new FormatException("Invalid preference value.") : ((string Name, string? Value))(name, value);
    }

    /// <summary>判断字符串是否全部由 HTTP token 的 ASCII 字符组成。</summary>
    /// <param name="text">待检查名称或值。</param>
    /// <returns>非空且每个字符均合法时为 true。</returns>
    private static bool IsToken(string text)
    {
        return text.Length > 0 && text.All(character =>
        char.IsAsciiLetterOrDigit(character) || "!#$%&'*+-.^_`|~".Contains(character));
    }
}
