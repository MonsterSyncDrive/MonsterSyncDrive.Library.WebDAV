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
using System.Globalization;
using MonsterSyncDrive.WebDAV.Helpers;

namespace MonsterSyncDrive.WebDAV.Request;

/// <summary>指定服务器应返回最小响应还是当前资源表示。</summary>
public enum WebDavReturnPreference
{
    /// <summary>不提出返回正文偏好，遵循具体方法的默认响应。</summary>
    Unspecified,
    /// <summary>请求最小响应；对属性查询包含省略 404 propstat 的语义。</summary>
    Minimal,
    /// <summary>请求返回资源的当前表示，可用于成功修改及条件失败后的资源重新读取。</summary>
    Representation
}

/// <summary>指定服务器处理未知或不支持参数时的严格程度。</summary>
public enum WebDavHandlingPreference
{
    /// <summary>不提出处理严格程度偏好。</summary>
    Unspecified,
    /// <summary>请求严格处理未知或不支持的参数。</summary>
    Strict,
    /// <summary>允许服务器宽松处理无法识别或应用的参数。</summary>
    Lenient
}

/// <summary>构造和解析 RFC 7240/8144 的 Prefer 或 Preference-Applied 字段。</summary>
/// <remarks>偏好是请求而非保证；服务器实际采用的行为以 Preference-Applied 及实际状态、正文为准。</remarks>
public sealed class WebDavPreferences
{
    /// <summary>解析时保留的已知偏好附加参数，避免再次序列化丢失扩展。</summary>
    private readonly Dictionary<string, string> _parameters = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>获取或设置正文返回偏好。</summary>
    public WebDavReturnPreference Return { get; set; }

    /// <summary>获取或设置是否省略属性查询中的 404 属性状态。</summary>
    /// <remarks>该便利属性对应 return=minimal，协议不存在独立的标准 omit-404 偏好。</remarks>
    public bool OmitNotFound
    {
        get => Return == WebDavReturnPreference.Minimal;
        set
        {
            if (value) Return = WebDavReturnPreference.Minimal;
            else if (Return == WebDavReturnPreference.Minimal) Return = WebDavReturnPreference.Unspecified;
        }
    }

    /// <summary>获取或设置 depth-noroot，仅处理 Depth 1 或 infinity 的后代而排除目标根资源。</summary>
    public bool DepthNoRoot { get; set; }

    /// <summary>获取或设置严格或宽松处理偏好。</summary>
    public WebDavHandlingPreference Handling { get; set; }

    /// <summary>获取或设置是否允许服务器异步接受操作，使用 respond-async。</summary>
    public bool RespondAsync { get; set; }

    /// <summary>获取或设置服务器可等待的非负秒数；null 表示不发送 wait。</summary>
    public int? WaitSeconds { get; set; }

    /// <summary>获取或设置尚未建模的合法偏好项，包含可选值及参数。</summary>
    /// <remarks>每个字符串必须是一个完整项，不能包含项外的逗号、控制字符或与已设置项重复的名称。</remarks>
    public IReadOnlyCollection<string> Extensions { get; set; } = [];

    /// <summary>获取当前配置是否没有任何需要发送的偏好。</summary>
    public bool IsEmpty => Return == WebDavReturnPreference.Unspecified && !DepthNoRoot
        && Handling == WebDavHandlingPreference.Unspecified && !RespondAsync && WaitSeconds is null && Extensions is { Count: 0 };

    /// <summary>生成可安全写入 Prefer 字段的规范文本。</summary>
    /// <returns>逗号分隔的偏好文本；没有偏好时返回空字符串。</returns>
    public override string ToString()
    {
        if (!Enum.IsDefined(Return)) throw new ArgumentOutOfRangeException(nameof(Return));
        if (!Enum.IsDefined(Handling)) throw new ArgumentOutOfRangeException(nameof(Handling));
        if (WaitSeconds < 0) throw new ArgumentOutOfRangeException(nameof(WaitSeconds));
        ArgumentNullException.ThrowIfNull(Extensions);
        List<string> result = [];
        HashSet<string> names = new(StringComparer.OrdinalIgnoreCase);
        if (Return != WebDavReturnPreference.Unspecified)
            Add("return", Return == WebDavReturnPreference.Minimal ? "minimal" : "representation");
        if (DepthNoRoot) Add("depth-noroot", null);
        if (Handling != WebDavHandlingPreference.Unspecified)
            Add("handling", Handling == WebDavHandlingPreference.Strict ? "strict" : "lenient");
        if (RespondAsync) Add("respond-async", null);
        if (WaitSeconds is { } seconds) Add("wait", seconds.ToString(CultureInfo.InvariantCulture));
        foreach (var extension in Extensions)
        {
            var parsed = PreferenceHeaderParser.ParseItem(extension);
            if (!names.Add(parsed.Name)) throw new ArgumentException("A preference name occurs more than once.", nameof(Extensions));
            result.Add(extension.Trim());
        }
        return string.Join(", ", result);

        // 已知偏好可以保留解析所得参数，但参数不能改变当前显式选择的偏好值。
        void Add(string name, string? value)
        {
            names.Add(name);
            result.Add(name + (value is null ? "" : "=" + value) + _parameters.GetValueOrDefault(name, ""));
        }
    }

    /// <summary>解析 Prefer 或 Preference-Applied，保留未知偏好与附加参数。</summary>
    /// <param name="header">完整字段值；null 或空白表示没有偏好。</param>
    /// <returns>包含已知偏好及未知扩展的配置；同名项按规范采用首次出现的项。</returns>
    public static WebDavPreferences Parse(string? header)
    {
        var result = new WebDavPreferences();
        if (header is null) return result;
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        List<string> extensions = [];
        foreach (var item in PreferenceHeaderParser.Split(header, ',', allowEmptyItems: true))
        {
            var parsed = PreferenceHeaderParser.ParseItem(item);
            if (!seen.Add(parsed.Name)) continue; // RFC 7240 规定重复偏好名称只取第一项。
            var name = parsed.Name.ToLowerInvariant();
            var value = parsed.Value?.ToLowerInvariant();
            var known = true;
            switch (name)
            {
                case "return" when value == "minimal": result.Return = WebDavReturnPreference.Minimal; break;
                case "return" when value == "representation": result.Return = WebDavReturnPreference.Representation; break;
                case "depth-noroot" when value is null: result.DepthNoRoot = true; break;
                case "handling" when value == "strict": result.Handling = WebDavHandlingPreference.Strict; break;
                case "handling" when value == "lenient": result.Handling = WebDavHandlingPreference.Lenient; break;
                case "respond-async" when value is null: result.RespondAsync = true; break;
                case "wait" when value is not null:
                    if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds))
                        throw new FormatException("Invalid wait preference.");
                    result.WaitSeconds = seconds;
                    break;
                default: known = false; extensions.Add(item.Trim()); break;
            }
            if (known && parsed.Parameters.Length > 0) result._parameters[name] = parsed.Parameters;
        }
        result.Extensions = extensions.AsReadOnly();
        return result;
    }
}
