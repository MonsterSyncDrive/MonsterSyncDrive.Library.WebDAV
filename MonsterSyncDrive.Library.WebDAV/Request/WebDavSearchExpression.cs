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
using System.Xml;
using System.Xml.Linq;

namespace MonsterSyncDrive.Library.WebDAV.Request;

/// <summary>RFC 5323 标准比较运算；不等于使用 Not(Equal(...)) 表达，避免生成未定义的 ne 元素。</summary>
public enum WebDavSearchComparison
{
    /// <summary>相等，生成 DAV:eq。</summary>
    Equal,
    /// <summary>严格小于，生成 DAV:lt。</summary>
    LessThan,
    /// <summary>小于或等于，生成 DAV:lte。</summary>
    LessThanOrEqual,
    /// <summary>严格大于，生成 DAV:gt。</summary>
    GreaterThan,
    /// <summary>大于或等于，生成 DAV:gte。</summary>
    GreaterThanOrEqual
}

/// <summary>不可变的搜索字面值，支持普通文本以及具有完整 XML Schema 类型名称的 typed-literal。</summary>
public sealed class WebDavSearchLiteral
{
    /// <summary>初始化搜索字面值；类型名称与值分开保存，不把用户输入解释为 XML。</summary>
    /// <param name="value">字面值的词法文本，允许空字符串但不允许 null。</param>
    /// <param name="dataType">完整类型名称；null 使用 DAV:literal，否则生成带 xsi:type 的 typed-literal。</param>
    /// <param name="language">可选 xml:lang 语言标记。</param>
    public WebDavSearchLiteral(string value, XName? dataType = null, string? language = null)
    {
        ArgumentNullException.ThrowIfNull(value);
        Value = value;
        DataType = dataType;
        Language = language;
    }

    /// <summary>获取未经 XML 转义的字面值文本，序列化时自动转义。</summary>
    public string Value
    {
        get;
    }

    /// <summary>获取完整数据类型名称，null 表示普通字面值。</summary>
    public XName? DataType
    {
        get;
    }

    /// <summary>获取可选语言标记，不自动进行区域相关转换。</summary>
    public string? Language
    {
        get;
    }

    /// <summary>创建使用固定 XML 词法格式的整数搜索值。</summary>
    /// <param name="value">要比较的整数。</param>
    /// <returns>类型为 xs:integer 的字面值。</returns>
    public static WebDavSearchLiteral Integer(long value)
    {
        return new(XmlConvert.ToString(value), XName.Get("integer", "http://www.w3.org/2001/XMLSchema"));
    }

    /// <summary>创建不受系统小数分隔符影响的十进制搜索值。</summary>
    /// <param name="value">要比较的十进制值。</param>
    /// <returns>类型为 xs:decimal 的字面值。</returns>
    public static WebDavSearchLiteral Decimal(decimal value)
    {
        return new(XmlConvert.ToString(value), XName.Get("decimal", "http://www.w3.org/2001/XMLSchema"));
    }

    /// <summary>创建 XML 布尔搜索值。</summary>
    /// <param name="value">要比较的布尔值。</param>
    /// <returns>类型为 xs:boolean 的字面值。</returns>
    public static WebDavSearchLiteral Boolean(bool value)
    {
        return new(XmlConvert.ToString(value), XName.Get("boolean", "http://www.w3.org/2001/XMLSchema"));
    }

    /// <summary>创建保留时区偏移的 ISO 日期时间搜索值。</summary>
    /// <param name="value">要比较的带偏移日期时间。</param>
    /// <returns>类型为 xs:dateTime 的字面值。</returns>
    public static WebDavSearchLiteral DateTime(DateTimeOffset value)
    {
        return new(XmlConvert.ToString(value), XName.Get("dateTime", "http://www.w3.org/2001/XMLSchema"));
    }

    /// <summary>构造独立字面值元素，局部声明 xsi:type 使用的前缀以避免与调用方命名空间冲突。</summary>
    /// <returns>DAV:literal 或 DAV:typed-literal 元素。</returns>
    public XElement ToXElement()
    {
        XElement element = new(WebDavXml.Dav + (DataType is null ? "literal" : "typed-literal"), Value);
        if (DataType is not null)
        {
            XNamespace xsi = "http://www.w3.org/2001/XMLSchema-instance";
            element.SetAttributeValue(XNamespace.Xmlns + "xsi", xsi.NamespaceName);
            if (DataType.NamespaceName.Length > 0)
            {
                element.SetAttributeValue(XNamespace.Xmlns + "type", DataType.NamespaceName);
                element.SetAttributeValue(xsi + "type", "type:" + DataType.LocalName);
            }
            else
            {
                // 无命名空间的 QName 不能意外继承父级的默认 DAV 命名空间。
                element.SetAttributeValue("xmlns", "");
                // 独立导出元素时也需为元素本身声明 DAV 前缀，避免空默认命名空间与元素名冲突。
                element.SetAttributeValue(XNamespace.Xmlns + "D", WebDavXml.Dav.NamespaceName);
                element.SetAttributeValue(xsi + "type", DataType.LocalName);
            }
        }
        if (Language is not null)
            element.SetAttributeValue(XNamespace.Xml + "lang", Language);
        return element;
    }
}

/// <summary>不可变的 DAV:basicsearch 条件表达式；通过类型化工厂组合标准逻辑和比较，不要求调用方拼接 XML。</summary>
/// <remarks>操作数写入 XElement 后自动转义；每次导出返回副本，外部修改不会改变已经创建的表达式。</remarks>
public sealed class WebDavSearchExpression
{
    /// <summary>保存已构造且不会向外暴露引用的表达式树。</summary>
    private readonly XElement _element;

    /// <summary>接管工厂方法刚生成的表达式元素。</summary>
    /// <param name="element">内部独立构建的表达式。</param>
    private WebDavSearchExpression(XElement element)
    {
        _element = element;
    }

    /// <summary>合取一个或多个条件，保留各条件的顺序和嵌套结构。</summary>
    /// <param name="conditions">至少一个非 null 条件。</param>
    /// <returns>DAV:and 表达式。</returns>
    public static WebDavSearchExpression And(params WebDavSearchExpression[] conditions)
    {
        return Logical("and", conditions);
    }

    /// <summary>析取一个或多个条件。</summary>
    /// <param name="conditions">至少一个非 null 条件。</param>
    /// <returns>DAV:or 表达式。</returns>
    public static WebDavSearchExpression Or(params WebDavSearchExpression[] conditions)
    {
        return Logical("or", conditions);
    }

    /// <summary>对单个条件取逻辑反值。</summary>
    /// <param name="condition">要否定的条件。</param>
    /// <returns>DAV:not 表达式。</returns>
    public static WebDavSearchExpression Not(WebDavSearchExpression condition)
    {
        ArgumentNullException.ThrowIfNull(condition);
        return new(new XElement(WebDavXml.Dav + "not", condition.ToXElement()));
    }

    /// <summary>构造属性与普通或带类型字面值之间的标准比较。</summary>
    /// <param name="property">属性完整名称。</param>
    /// <param name="comparison">标准比较运算。</param>
    /// <param name="literal">普通或 typed-literal 字面值。</param>
    /// <param name="caseless">是否请求不区分大小写；null 保留服务器默认。</param>
    /// <returns>eq、lt、lte、gt 或 gte 表达式。</returns>
    public static WebDavSearchExpression Compare(XName property, WebDavSearchComparison comparison, WebDavSearchLiteral literal, bool? caseless = null)
    {
        ArgumentNullException.ThrowIfNull(property);
        ArgumentNullException.ThrowIfNull(literal);
        string name = comparison switch
        {
            WebDavSearchComparison.Equal => "eq",
            WebDavSearchComparison.LessThan => "lt",
            WebDavSearchComparison.LessThanOrEqual => "lte",
            WebDavSearchComparison.GreaterThan => "gt",
            WebDavSearchComparison.GreaterThanOrEqual => "gte",
            _ => throw new ArgumentOutOfRangeException(nameof(comparison))
        };
        XElement element = new(WebDavXml.Dav + name, Property(property), literal.ToXElement());
        AddCaseless(element, caseless);
        return new(element);
    }

    /// <summary>构造属性字符串的 LIKE 模式匹配。</summary>
    /// <param name="property">目标属性完整名称。</param>
    /// <param name="pattern">包含协议 LIKE 通配符的普通文本；XML 特殊字符自动转义。</param>
    /// <param name="caseless">是否不区分大小写；null 采用服务器默认。</param>
    /// <returns>DAV:like 表达式。</returns>
    public static WebDavSearchExpression Like(XName property, string pattern, bool? caseless = null)
    {
        ArgumentNullException.ThrowIfNull(property);
        ArgumentNullException.ThrowIfNull(pattern);
        XElement element = new(WebDavXml.Dav + "like", Property(property), new XElement(WebDavXml.Dav + "literal", pattern));
        AddCaseless(element, caseless);
        return new(element);
    }

    /// <summary>构造资源正文全文匹配，分词与相关性语义由服务器实现。</summary>
    /// <param name="text">要搜索的普通文本。</param>
    /// <returns>DAV:contains 表达式。</returns>
    public static WebDavSearchExpression Contains(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return new(new XElement(WebDavXml.Dav + "contains", text));
    }

    /// <summary>匹配当前资源是集合的情况。</summary>
    /// <returns>无操作数的 DAV:is-collection 表达式。</returns>
    public static WebDavSearchExpression IsCollection()
    {
        return new(new XElement(WebDavXml.Dav + "is-collection"));
    }

    /// <summary>匹配指定属性在资源上已定义的情况。</summary>
    /// <param name="property">要检查的完整属性名称。</param>
    /// <returns>DAV:is-defined 表达式。</returns>
    public static WebDavSearchExpression IsDefined(XName property)
    {
        return new(new XElement(WebDavXml.Dav + "is-defined", Property(property)));
    }

    /// <summary>匹配属性具有明确语言标记的情况。</summary>
    /// <param name="property">要检查语言信息的完整属性名称。</param>
    /// <returns>DAV:language-defined 表达式。</returns>
    public static WebDavSearchExpression LanguageDefined(XName property)
    {
        return new(new XElement(WebDavXml.Dav + "language-defined", Property(property)));
    }

    /// <summary>按 RFC 5323 的语言范围匹配属性语言。</summary>
    /// <param name="property">要检查的完整属性名称。</param>
    /// <param name="languageRange">语言范围，例如 zh 或 en-US。</param>
    /// <returns>DAV:language-matches 表达式。</returns>
    public static WebDavSearchExpression LanguageMatches(XName property, string languageRange)
    {
        ArgumentNullException.ThrowIfNull(languageRange);
        return new(new XElement(WebDavXml.Dav + "language-matches", Property(property), new XElement(WebDavXml.Dav + "literal", languageRange)));
    }

    /// <summary>为 QSD 发现的服务器自定义操作符提供显式扩展入口。</summary>
    /// <param name="element">已经正确构造的操作符元素；复制整个元素而不是解析字符串。</param>
    /// <returns>不共享输入元素引用的扩展表达式。</returns>
    public static WebDavSearchExpression Extension(XElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return new(new XElement(element));
    }

    /// <summary>返回可独立修改的表达式 XML 副本。</summary>
    /// <returns>完整操作符子树。</returns>
    public XElement ToXElement()
    {
        return new(_element);
    }

    /// <summary>生成至少含一个操作数的逻辑运算，拒绝空列表和 null 子项。</summary>
    /// <param name="name">内部固定的 and 或 or 名称。</param>
    /// <param name="conditions">要组合的条件数组。</param>
    /// <returns>完整逻辑条件。</returns>
    private static WebDavSearchExpression Logical(string name, WebDavSearchExpression[] conditions)
    {
        ArgumentNullException.ThrowIfNull(conditions);
        return conditions.Length == 0 || conditions.Any(condition => condition is null)
            ? throw new ArgumentException("A logical expression requires at least one non-null operand.", nameof(conditions))
            : new(new XElement(WebDavXml.Dav + name, conditions.Select(condition => condition.ToXElement())));
    }

    /// <summary>把完整属性名包装为协议要求的 DAV:prop 操作数。</summary>
    /// <param name="property">完整属性名称。</param>
    /// <returns>含一个属性元素的 prop 元素。</returns>
    private static XElement Property(XName property)
    {
        ArgumentNullException.ThrowIfNull(property);
        return new XElement(WebDavXml.Dav + "prop", new XElement(property));
    }

    /// <summary>仅在明确指定时写入 yes/no 属性，避免改变服务器默认行为。</summary>
    /// <param name="element">接收 caseless 属性的运算元素。</param>
    /// <param name="caseless">可选大小写设置。</param>
    internal static void AddCaseless(XElement element, bool? caseless)
    {
        if (caseless.HasValue)
            element.SetAttributeValue("caseless", caseless.Value ? "yes" : "no");
    }
}