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
using System.Xml.Linq;

namespace MonsterSyncDrive.WebDAV.Response;

/// <summary>查询模式发现中的一组属性能力说明；缺少能力标记表示未声明，而不是明确不支持。</summary>
public sealed class WebDavSearchPropertyDescription
{
    /// <summary>保存完整 propdesc，保留服务器未来扩展。</summary>
    private readonly XElement _element;

    /// <summary>解析一个 propdesc，支持多个属性共用同一能力描述。</summary>
    /// <param name="element">标准 DAV:propdesc 元素。</param>
    internal WebDavSearchPropertyDescription(XElement element)
    {
        _element = new XElement(element);
        Properties = Array.AsReadOnly((element.Element(WebDavXml.Dav + "prop")?.Elements() ?? []).Select(value => value.Name).ToArray());
        AnyOtherProperty = element.Element(WebDavXml.Dav + "any-other-property") is not null;
        if ((Properties.Count == 0) == !AnyOtherProperty)
            throw new InvalidDataException("A property description must contain prop or any-other-property, exclusively.");
        var dataType = element.Element(WebDavXml.Dav + "datatype");
        if (dataType is not null && dataType.Elements().Count() != 1)
            throw new InvalidDataException("A search datatype must contain exactly one type element.");
        DeclaredDataType = dataType?.Elements().Single().Name;
        Searchable = Capability("searchable");
        Selectable = Capability("selectable");
        Sortable = Capability("sortable");
        Caseless = Capability("caseless");

        bool? Capability(string name) => element.Element(WebDavXml.Dav + name) is not null ? true : null;
    }

    /// <summary>获取同一 propdesc 中描述的完整属性名称。</summary>
    public IReadOnlyCollection<XName> Properties { get; }

    /// <summary>获取该描述是否适用于其他条目中未明确列出的属性。</summary>
    public bool AnyOtherProperty { get; }

    /// <summary>获取服务器显式声明的数据类型；缺失时为 null。</summary>
    public XName? DeclaredDataType { get; }

    /// <summary>获取有效数据类型，未声明时按规范使用 xs:string。</summary>
    public XName DataType => DeclaredDataType ?? XName.Get("string", "http://www.w3.org/2001/XMLSchema");

    /// <summary>获取服务器是否声明属性可用于 where；null 表示未声明。</summary>
    public bool? Searchable { get; }

    /// <summary>获取服务器是否声明属性可在 select 中返回。</summary>
    public bool? Selectable { get; }

    /// <summary>获取服务器是否声明属性可用于排序。</summary>
    public bool? Sortable { get; }

    /// <summary>获取服务器是否声明字符串比较默认不区分大小写。</summary>
    public bool? Caseless { get; }

    /// <summary>返回完整属性描述的副本，包含未知扩展信息。</summary>
    /// <returns>独立的 propdesc 元素。</returns>
    public XElement ToXElement() => new(_element);
}

/// <summary>QSD 公布的可选操作符签名；多个同名条目可表达不同操作数组合。</summary>
public sealed class WebDavSearchOperatorDescription
{
    /// <summary>原始操作符描述的独立快照。</summary>
    private readonly XElement _element;

    /// <summary>解析操作符名称、操作数类型及是否允许字符数据。</summary>
    /// <param name="element">标准 DAV:opdesc 元素。</param>
    internal WebDavSearchOperatorDescription(XElement element)
    {
        _element = new XElement(element);
        var children = element.Elements().ToArray();
        if (children.Length == 0)
            throw new InvalidDataException("A search operator description has no operator.");
        Name = children[0].Name;
        Operands = Array.AsReadOnly(children.Skip(1).Select(value => value.Name).ToArray());
        AllowCharacterData = element.Attribute("allow-pcdata")?.Value switch
        {
            null or "no" => false,
            "yes" => true,
            _ => throw new InvalidDataException("Invalid allow-pcdata attribute.")
        };
    }

    /// <summary>获取完整操作符名称，允许服务器扩展命名空间。</summary>
    public XName Name { get; }

    /// <summary>获取按位置排列的操作数种类，如 DAV:operand-property 和 DAV:operand-typed-literal。</summary>
    public IReadOnlyCollection<XName> Operands { get; }

    /// <summary>获取操作符是否允许直接字符内容，如 contains。</summary>
    public bool AllowCharacterData { get; }

    /// <summary>返回完整操作符签名副本。</summary>
    /// <returns>独立的 opdesc 元素。</returns>
    public XElement ToXElement() => new(_element);
}

/// <summary>一个资源返回的搜索模式，包括成功或失败状态，以及 basicsearch 属性和可选运算能力。</summary>
public sealed class WebDavSearchSchema
{
    /// <summary>保存完整查询模式，其他查询语法也不丢失。</summary>
    private readonly XElement? _schema;

    /// <summary>解析 QSD 扩展 response 中的 status 与 query-schema。</summary>
    /// <param name="response">包含 href、status 和可选 query-schema 的 DAV:response。</param>
    internal WebDavSearchSchema(XElement response)
    {
        Href = response.Element(WebDavXml.Dav + "href")?.Value;
        if (string.IsNullOrWhiteSpace(Href))
            throw new InvalidDataException("A query schema response has no href.");
        StatusCode = WebDavXml.ReadStatus(response.Element(WebDavXml.Dav + "status"));
        Description = response.Element(WebDavXml.Dav + "responsedescription")?.Value;
        var container = response.Element(WebDavXml.Dav + "query-schema");
        if (container is not null && container.Elements().Count() != 1)
            throw new InvalidDataException("query-schema must contain one grammar schema.");
        _schema = container?.Elements().FirstOrDefault() is { } schema ? new XElement(schema) : null;
        if (StatusCode is >= 200 and < 300 && _schema is null)
            throw new InvalidDataException("A successful QSD response omitted its query schema.");
        if (_schema?.Name == WebDavXml.Dav + "basicsearchschema")
        {
            var properties = _schema.Element(WebDavXml.Dav + "properties")
                ?? throw new InvalidDataException("basicsearchschema has no properties container.");
            var operators = _schema.Element(WebDavXml.Dav + "operators")
                ?? throw new InvalidDataException("basicsearchschema has no operators container.");
            Properties = Array.AsReadOnly(properties.Elements(WebDavXml.Dav + "propdesc")
                .Select(value => new WebDavSearchPropertyDescription(value)).ToArray());
            Operators = Array.AsReadOnly(operators.Elements(WebDavXml.Dav + "opdesc")
                .Select(value => new WebDavSearchOperatorDescription(value)).ToArray());
        }
    }

    /// <summary>获取该模式描述对应的资源 URI 引用。</summary>
    public string? Href { get; }

    /// <summary>获取该资源的模式发现 HTTP 状态码。</summary>
    public int StatusCode { get; }

    /// <summary>获取服务器提供的可选状态说明。</summary>
    public string? Description { get; }

    /// <summary>获取模式名称，通常为 DAV:basicsearchschema。</summary>
    public XName? SchemaName => _schema?.Name;

    /// <summary>获取全部属性或其他属性的能力描述。</summary>
    public IReadOnlyCollection<WebDavSearchPropertyDescription> Properties { get; } = [];

    /// <summary>获取服务器公布的可选操作符签名。</summary>
    public IReadOnlyCollection<WebDavSearchOperatorDescription> Operators { get; } = [];

    /// <summary>取得模式 XML 的独立副本；发现失败时可为 null。</summary>
    /// <returns>完整模式子树或 null。</returns>
    public XElement? ToXElement() => _schema is null ? null : new XElement(_schema);
}

/// <summary>完整搜索模式发现响应，保留每个资源的能力与错误。</summary>
public sealed class WebDavSearchSchemaResponse : WebDavResponse
{
    /// <summary>从通用文档响应解析 QSD 的扩展字段。</summary>
    /// <param name="response">SEARCH 返回的完整文档响应。</param>
    internal WebDavSearchSchemaResponse(WebDavDocumentResponse response) : base(response.StatusCode, response.Description)
    {
        Headers = response.Headers;
        Error = response.Error;
        Document = response.Document is null ? null : new XElement(response.Document);
        if (response.IsSuccessful && response.StatusCode != 207)
            throw new InvalidDataException("QSD requires a multistatus response.");
        if (response.StatusCode == 207)
        {
            if (Document?.Name != WebDavXml.Dav + "multistatus")
                throw new InvalidDataException("QSD response is not DAV:multistatus.");
            Schemas = Array.AsReadOnly(Document.Elements(WebDavXml.Dav + "response").Select(value => new WebDavSearchSchema(value)).ToArray());
            if (Schemas.Count == 0)
                throw new InvalidDataException("QSD did not describe any scope.");
        }
    }

    /// <summary>获取完整发现文档，保留所有模式的扩展字段。</summary>
    public XElement? Document { get; }

    /// <summary>获取每个已返回资源的模式与状态。</summary>
    public IReadOnlyCollection<WebDavSearchSchema> Schemas { get; } = [];

    /// <summary>获取 HTTP 和所有资源级 QSD 状态是否均成功。</summary>
    public override bool IsSuccessful => base.IsSuccessful && Schemas.All(schema => schema.StatusCode is >= 200 and < 300);
}