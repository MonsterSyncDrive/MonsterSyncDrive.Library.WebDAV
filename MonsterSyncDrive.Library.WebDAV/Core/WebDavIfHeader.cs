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
using System.Net.Http.Headers;

namespace MonsterSyncDrive.Library.WebDAV.Core;

/// <summary>标识 DAV If 条件中状态令牌或实体标签两种原子条件。</summary>
public enum WebDavIfConditionKind
{
    /// <summary>尖括号包围的绝对状态令牌 URI，例如写锁或集合同步令牌。</summary>
    StateToken,
    /// <summary>方括号包围的强或弱实体标签。</summary>
    EntityTag
}

/// <summary>表示可选 Not 修饰的一个状态令牌或 ETag 条件。</summary>
/// <remarks>条件自身不访问服务器；服务器依据 RFC 4918 评价其真值。</remarks>
public sealed class WebDavIfCondition
{
    /// <summary>建立已经通过语法校验的不可变原子条件。</summary>
    /// <param name="kind">条件类别。</param>
    /// <param name="value">不带外围尖括号或方括号的条件值。</param>
    /// <param name="isNegated">是否对该原子条件取反。</param>
    private WebDavIfCondition(WebDavIfConditionKind kind, string value, bool isNegated)
    {
        Kind = kind;
        Value = value;
        IsNegated = isNegated;
    }

    /// <summary>获取原子条件的类别。</summary>
    public WebDavIfConditionKind Kind
    {
        get;
    }

    /// <summary>获取已校验的令牌 URI 或带引号的强/弱 ETag。</summary>
    public string Value
    {
        get;
    }

    /// <summary>获取是否使用 Not 对当前条件取反。</summary>
    public bool IsNegated
    {
        get;
    }

    /// <summary>创建一个绝对状态令牌条件。</summary>
    /// <param name="token">绝对 URI，可以带一层外部尖括号。</param>
    /// <param name="isNegated">是否取反，默认 false。</param>
    /// <returns>已校验且不可变的状态令牌条件。</returns>
    public static WebDavIfCondition StateToken(string token, bool isNegated = false)
    {
        return new(WebDavIfConditionKind.StateToken, WebDavIfSyntax.StateToken(token), isNegated);
    }

    /// <summary>创建一个强或弱 ETag 条件，不允许 HTTP 通配符 *。</summary>
    /// <param name="etag">包含双引号的标签，例如 "v1" 或 W/"v1"。</param>
    /// <param name="isNegated">是否取反，默认 false。</param>
    /// <returns>已校验且不可变的标签条件。</returns>
    public static WebDavIfCondition EntityTag(string etag, bool isNegated = false)
    {
        ArgumentNullException.ThrowIfNull(etag);
        return etag.Any(character => character is '\r' or '\n') || !EntityTagHeaderValue.TryParse(etag, out EntityTagHeaderValue? value) || value.Tag == "*"
            ? throw new ArgumentException("Expected one strong or weak entity tag, without a wildcard.", nameof(etag))
            : new(WebDavIfConditionKind.EntityTag, value.ToString(), isNegated);
    }

    /// <summary>生成带标准定界符的原子条件文本。</summary>
    /// <returns>可选 Not 加尖括号令牌或方括号实体标签。</returns>
    public override string ToString()
    {
        return (IsNegated ? "Not " : "") +
        (Kind == WebDavIfConditionKind.StateToken ? "<" + Value + ">" : "[" + Value + "]");
    }
}

/// <summary>表示 DAV If 中由一个或多个条件组成的逻辑 AND 列表。</summary>
public sealed class WebDavIfList
{
    /// <summary>复制并校验列表中的原子条件。</summary>
    /// <param name="conditions">至少一个非 null 条件，列表内条件按 AND 组合。</param>
    public WebDavIfList(params WebDavIfCondition[] conditions)
    {
        ArgumentNullException.ThrowIfNull(conditions);
        if (conditions.Length == 0 || conditions.Any(condition => condition is null))
            throw new ArgumentException("An If list must contain at least one condition.", nameof(conditions));
        Conditions = Array.AsReadOnly(conditions.ToArray());
    }

    /// <summary>获取按 AND 组合的原子条件集合。</summary>
    public IReadOnlyList<WebDavIfCondition> Conditions
    {
        get;
    }

    /// <summary>输出圆括号包围的 AND 条件列表。</summary>
    /// <returns>形如 (&lt;token&gt; ["etag"]) 的规范表达式。</returns>
    public override string ToString()
    {
        return "(" + string.Join(" ", Conditions) + ")";
    }
}

/// <summary>表示一个资源标记及其按 OR 组合的候选条件列表。</summary>
public sealed class WebDavIfResourceConditions
{
    /// <summary>复制资源标记及至少一个候选列表。</summary>
    /// <param name="resourceUri">绝对资源 URI 或服务器根绝对路径，不允许片段及换行。</param>
    /// <param name="alternatives">按 OR 组合的一个或多个 AND 列表。</param>
    public WebDavIfResourceConditions(string resourceUri, params WebDavIfList[] alternatives)
    {
        ResourceUri = WebDavIfSyntax.ResourceTag(resourceUri);
        ArgumentNullException.ThrowIfNull(alternatives);
        if (alternatives.Length == 0 || alternatives.Any(list => list is null))
            throw new ArgumentException("A resource tag must have at least one condition list.", nameof(alternatives));
        Alternatives = Array.AsReadOnly(alternatives.ToArray());
    }

    /// <summary>通过 Uri 对象建立资源标记和候选列表。</summary>
    /// <param name="resourceUri">绝对 URI 或绝对路径形式的 URI 引用。</param>
    /// <param name="alternatives">按 OR 组合的候选列表。</param>
    public WebDavIfResourceConditions(Uri resourceUri, params WebDavIfList[] alternatives)
        : this(resourceUri?.OriginalString ?? throw new ArgumentNullException(nameof(resourceUri)), alternatives) { }

    /// <summary>获取不带外部尖括号的资源 URI 引用。</summary>
    public string ResourceUri
    {
        get;
    }

    /// <summary>获取当前资源按 OR 组合的候选列表。</summary>
    public IReadOnlyList<WebDavIfList> Alternatives
    {
        get;
    }

    /// <summary>生成绑定当前资源的完整带标记条件。</summary>
    /// <returns>资源标记后跟一个或多个圆括号条件列表。</returns>
    public override string ToString()
    {
        return "<" + ResourceUri + "> " + string.Join(" ", Alternatives);
    }
}

/// <summary>表示 RFC 4918 的完整 DAV If 请求头表达式。</summary>
/// <remarks>支持无标记或带资源标记两种语法、列表内 AND、列表间 OR、Not、强弱 ETag 和任意绝对状态令牌。</remarks>
public sealed class WebDavIfHeader
{
    /// <summary>从已经校验的两种互斥表达形式创建请求头。</summary>
    /// <param name="lists">无标记列表，带标记模式时为空。</param>
    /// <param name="resources">带标记列表，无标记模式时为空。</param>
    private WebDavIfHeader(IReadOnlyList<WebDavIfList> lists, IReadOnlyList<WebDavIfResourceConditions> resources)
    {
        Lists = lists;
        Resources = resources;
    }

    /// <summary>获取无标记模式的候选列表，列表之间按 OR 组合。</summary>
    public IReadOnlyList<WebDavIfList> Lists
    {
        get;
    }

    /// <summary>获取带资源标记的候选列表，不能与 Lists 同时使用。</summary>
    public IReadOnlyList<WebDavIfResourceConditions> Resources
    {
        get;
    }

    /// <summary>获取当前表达式是否采用带资源标记的语法。</summary>
    public bool IsTagged => Resources.Count > 0;

    /// <summary>为当前请求 URI 建立一个或多个无标记候选条件。</summary>
    /// <param name="alternatives">至少一个非空 AND 列表，各列表之间按 OR 组合。</param>
    /// <returns>不可变的无标记 If 表达式。</returns>
    public static WebDavIfHeader ForRequest(params WebDavIfList[] alternatives)
    {
        ArgumentNullException.ThrowIfNull(alternatives);
        return alternatives.Length == 0 || alternatives.Any(list => list is null)
            ? throw new ArgumentException("Expected at least one untagged condition list.", nameof(alternatives))
            : new(Array.AsReadOnly(alternatives.ToArray()), Array.Empty<WebDavIfResourceConditions>());
    }

    /// <summary>为一个或多个明确的资源 URI 建立带标记条件。</summary>
    /// <param name="resources">至少一项资源及其候选列表。</param>
    /// <returns>不可变的带标记 If 表达式。</returns>
    public static WebDavIfHeader ForResources(params WebDavIfResourceConditions[] resources)
    {
        ArgumentNullException.ThrowIfNull(resources);
        return resources.Length == 0 || resources.Any(resource => resource is null)
            ? throw new ArgumentException("Expected at least one tagged condition group.", nameof(resources))
            : new(Array.Empty<WebDavIfList>(), Array.AsReadOnly(resources.ToArray()));
    }

    /// <summary>解析完整 DAV If 字段值，拒绝空列表、混用语法和头部注入字符。</summary>
    /// <param name="value">不含字段名称 If: 的字段值，不允许 CR/LF 折叠。</param>
    /// <returns>保留逻辑结构的不可变 If 表达式。</returns>
    /// <exception cref="FormatException">字段值不是合法 DAV If 表达式。</exception>
    public static WebDavIfHeader Parse(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new Parser(value).Parse();
    }

    /// <summary>尝试解析完整 If 字段，不以格式异常打断调用方。</summary>
    /// <param name="value">待解析字段值。</param>
    /// <param name="result">成功时输出模型，失败时为 null。</param>
    /// <returns>解析成功时为 true。</returns>
    public static bool TryParse(string? value, out WebDavIfHeader? result)
    {
        result = null;
        if (value is null)
            return false;
        try
        {
            result = Parse(value);
            return true;
        }
        catch (Exception error) when (error is ArgumentException or FormatException)
        {
            return false;
        }
    }

    /// <summary>输出单个规范 If 字段值，不混合有标记和无标记语法。</summary>
    /// <returns>可以直接写入 HttpRequestHeaders 的表达式。</returns>
    public override string ToString()
    {
        return IsTagged ? string.Join(" ", Resources) : string.Join(" ", Lists);
    }

    /// <summary>采用非递归扫描解析 If，避免嵌套表达式或非法分隔符被宽松接受。</summary>
    private sealed class Parser
    {
        /// <summary>调用方提交的原始字段文本。</summary>
        private readonly string _text;

        /// <summary>尚未消费的字符位置。</summary>
        private int _position;

        /// <summary>校验字段不存在换行后记录待解析文本。</summary>
        /// <param name="text">单行 If 字段值。</param>
        public Parser(string text)
        {
            if (text.Any(character => character is '\r' or '\n' || (char.IsControl(character) && character != '\t')))
                throw new FormatException("An If field cannot contain line breaks or control characters.");
            _text = text;
        }

        /// <summary>按第一个非空白分隔符选择且固定一种语法形式。</summary>
        /// <returns>带标记或无标记的完整表达式。</returns>
        public WebDavIfHeader Parse()
        {
            SkipWhitespace();
            if (Peek() == '(')
            {
                List<WebDavIfList> lists = [];
                while (Peek() == '(')
                {
                    lists.Add(ReadList());
                    SkipWhitespace();
                }
                End();
                return ForRequest(lists.ToArray());
            }
            List<WebDavIfResourceConditions> resources = [];
            while (Peek() == '<')
            {
                string resource = ReadCoded();
                SkipWhitespace();
                List<WebDavIfList> alternatives = [];
                while (Peek() == '(')
                {
                    alternatives.Add(ReadList());
                    SkipWhitespace();
                }
                if (alternatives.Count == 0)
                    throw Invalid();
                try
                {
                    resources.Add(new(resource, alternatives.ToArray()));
                }
                catch (ArgumentException error)
                {
                    throw new FormatException("Invalid If resource tag.", error);
                }
            }
            End();
            return resources.Count == 0 ? throw Invalid() : ForResources(resources.ToArray());
        }

        /// <summary>解析一个圆括号列表，并要求至少存在一个条件。</summary>
        /// <returns>列表内条件按 AND 组合。</returns>
        private WebDavIfList ReadList()
        {
            Require('(');
            SkipWhitespace();
            List<WebDavIfCondition> conditions = [];
            while (Peek() != ')')
            {
                if (_position >= _text.Length)
                    throw Invalid();
                bool negate = false;
                if (_position + 3 <= _text.Length && _text.AsSpan(_position, 3).Equals("Not", StringComparison.OrdinalIgnoreCase))
                {
                    negate = true;
                    _position += 3;
                    SkipWhitespace();
                }
                try
                {
                    conditions.Add(Peek() switch
                    {
                        '<' => WebDavIfCondition.StateToken(ReadCoded(), negate),
                        '[' => WebDavIfCondition.EntityTag(ReadETag(), negate),
                        _ => throw Invalid()
                    });
                }
                catch (ArgumentException error)
                {
                    throw new FormatException("Invalid If condition.", error);
                }
                SkipWhitespace();
            }
            Require(')');
            return conditions.Count == 0 ? throw Invalid() : new(conditions.ToArray());
        }

        /// <summary>读取尖括号内完整 URI，不把 URI 自身的圆括号当成列表定界符。</summary>
        /// <returns>不包含尖括号的原始 URI 文本。</returns>
        private string ReadCoded()
        {
            Require('<');
            int start = _position;
            while (Peek() != '>')
            {
                if (_position >= _text.Length)
                    throw Invalid();
                _position++;
            }
            string value = _text[start.._position];
            Require('>');
            return value;
        }

        /// <summary>依据引号边界读取 ETag，允许标签内容本身包含右方括号。</summary>
        /// <returns>保留弱标记和双引号的单个 ETag。</returns>
        private string ReadETag()
        {
            Require('[');
            int start = _position;
            if (_position + 2 <= _text.Length && _text.AsSpan(_position, 2).SequenceEqual("W/"))
                _position += 2;
            Require('"');
            while (Peek() != '"')
            {
                if (_position >= _text.Length)
                    throw Invalid();
                _position++;
            }
            Require('"');
            string value = _text[start.._position];
            Require(']');
            return value;
        }

        /// <summary>读取当前位置字符，文档末尾返回零字符。</summary>
        /// <returns>当前字符或文档末尾的零字符。</returns>
        private char Peek()
        {
            return _position < _text.Length ? _text[_position] : '\0';
        }

        /// <summary>消费协议允许的空格及水平制表符。</summary>
        private void SkipWhitespace()
        {
            while (Peek() is ' ' or '\t')
                _position++;
        }

        /// <summary>匹配并消费一个必须出现的分隔符。</summary>
        /// <param name="character">预期分隔字符。</param>
        private void Require(char character)
        {
            if (Peek() != character)
                throw Invalid();
            _position++;
        }

        /// <summary>确保输入没有未消费的混用语法或多余内容。</summary>
        private void End()
        {
            SkipWhitespace();
            if (_position != _text.Length)
                throw Invalid();
        }

        /// <summary>为当前错误位置创建不泄露请求凭据的格式异常。</summary>
        /// <returns>带字符位置的 FormatException。</returns>
        private FormatException Invalid()
        {
            return new("Invalid DAV If expression at character " + _position + ".");
        }
    }
}

/// <summary>校验 If 中 URI 的严格头部语法，避免自动规范化改变状态令牌的精确身份。</summary>
internal static class WebDavIfSyntax
{
    /// <summary>校验并去除可选尖括号，保留绝对状态令牌原始文本。</summary>
    /// <param name="token">调用方提供的状态令牌。</param>
    /// <returns>可写入 Coded-URL 的绝对 URI。</returns>
    internal static string StateToken(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        string value = token;
        if (value.StartsWith('<') && value.EndsWith('>'))
            value = value[1..^1];
        return !HeaderSafe(value) || !Uri.IsWellFormedUriString(value, UriKind.Absolute)
            ? throw new ArgumentException("A state token must be an absolute URI without whitespace or angle brackets.", nameof(token))
            : value;
    }

    /// <summary>校验绝对 URI 或绝对路径形式的资源标记，不接受相对路径和片段。</summary>
    /// <param name="resource">资源 URI 引用。</param>
    /// <returns>保持原形式的 Simple-ref 文本。</returns>
    internal static string ResourceTag(string resource)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resource);
        return !HeaderSafe(resource) || resource.Contains('#')
            || !(Uri.IsWellFormedUriString(resource, UriKind.Absolute)
                || (resource.StartsWith('/') && !resource.StartsWith("//", StringComparison.Ordinal) && Uri.IsWellFormedUriString(resource, UriKind.Relative)))
            ? throw new ArgumentException("A resource tag must be an absolute URI or absolute path without a fragment.", nameof(resource))
            : resource;
    }

    /// <summary>拒绝头部定界字符、控制字符、裸空白及未编码的非 ASCII 字符。</summary>
    /// <param name="value">待检查的 URI 字段。</param>
    /// <returns>文本适合写入尖括号字段时为 true。</returns>
    private static bool HeaderSafe(string value)
    {
        return value.Length > 0 &&
        value.All(character => character is > ' ' and < '\x7f' and not ('<' or '>'));
    }
}

