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
using MonsterSyncDrive.Library.WebDAV.Core;
using MonsterSyncDrive.Library.WebDAV.Core.AccessControl;
using System.Xml.Linq;

namespace MonsterSyncDrive.Library.WebDAV.Response;

/// <summary>保存 RFC 5397 当前认证主体结果，明确区分未认证与属性查询失败。</summary>
public sealed class WebDavCurrentPrincipalResponse : WebDavResponse
{
    /// <summary>从单资源属性响应解析当前认证主体。</summary>
    /// <param name="response">查询 current-user-principal 的响应。</param>
    /// <param name="requestUri">用于解析服务器相对主体 URI 的请求地址。</param>
    internal WebDavCurrentPrincipalResponse(WebDavDocumentResponse response, Uri requestUri)
        : base(response.StatusCode, response.Description)
    {
        Headers = response.Headers;
        Error = response.Error;
        Resource = response.Resources.Count == 1 ? response.Resources.Single() : null;
        XElement? property = Resource?.Properties.FirstOrDefault(value => value.Name == WebDavXml.Dav + "current-user-principal")?.ToXElement();
        if (property is null)
            return;
        XElement[] values = property.Elements().ToArray();
        if (values.Length != 1 || (values[0].Name != WebDavXml.Dav + "href" && values[0].Name != WebDavXml.Dav + "unauthenticated"))
            throw new InvalidDataException("current-user-principal requires exactly one href or unauthenticated selector.");
        Principal = WebDavPrincipal.Parse(values[0]);
        if (Principal.Kind == WebDavPrincipalKind.Href)
        {
            if (!Uri.TryCreate(requestUri, Principal.Href, out Uri? resolved)
                || (resolved.Scheme != Uri.UriSchemeHttp && resolved.Scheme != Uri.UriSchemeHttps)
                || !string.IsNullOrEmpty(resolved.UserInfo) || !string.IsNullOrEmpty(resolved.Fragment))
                throw new InvalidDataException("Current principal URL must resolve to an HTTP(S) URI without credentials or fragment.");
            PrincipalUri = resolved;
        }
    }

    /// <summary>获取原始资源及逐属性状态，便于区分禁止读取和属性不存在。</summary>
    public WebDavResource? Resource
    {
        get;
    }
    /// <summary>获取 href 或 unauthenticated 主体；查询失败或属性缺失时为 null。</summary>
    public WebDavPrincipal? Principal
    {
        get;
    }
    /// <summary>获取已经解析为绝对 HTTP(S) 的主体 URI；未认证或缺失时为 null。</summary>
    public Uri? PrincipalUri
    {
        get;
    }
    /// <summary>获取是否已确认当前用户已经认证；未知结果为 false，应结合 IsSuccessful 判断。</summary>
    public bool IsAuthenticated => PrincipalUri is not null;
    /// <summary>获取是否已确认当前用户尚未认证；缺失或读取失败时为 false。</summary>
    public bool IsUnauthenticated => Principal?.Kind == WebDavPrincipalKind.Unauthenticated;
    /// <summary>获取 HTTP、资源状态及所需属性是否均成功。</summary>
    public override bool IsSuccessful => base.IsSuccessful && Principal is not null && Resource is not null
        && Resource.StatusCode is null or (>= 200 and < 300);
}

/// <summary>保存主体属性查询或主体报告结果，保留组成员与逐资源错误。</summary>
public sealed class WebDavPrincipalResponse : WebDavDocumentResponse
{
    /// <summary>从通用文档响应构造主体结果投影。</summary>
    /// <param name="response">完整 DAV 文档及 HTTP 状态。</param>
    internal WebDavPrincipalResponse(WebDavDocumentResponse response)
        : base(response.StatusCode, response.Description, response.Document)
    {
        Headers = response.Headers;
        Error = response.Error;
        Representation = response.Representation;
        Principals = Array.AsReadOnly(Resources.Select(resource => new WebDavPrincipalInfo(resource)).ToArray());
    }

    /// <summary>获取结果主体快照，包含用户及组的直接成员和直接所属组。</summary>
    public IReadOnlyList<WebDavPrincipalInfo> Principals
    {
        get;
    }
}

/// <summary>保存服务器声明的可搜索主体属性及人类可读说明。</summary>
public sealed class WebDavPrincipalSearchProperty
{
    /// <summary>从 principal-search-property 声明构造一项搜索属性。</summary>
    /// <param name="element">完整声明节点。</param>
    internal WebDavPrincipalSearchProperty(XElement element)
    {
        XElement[]? properties = element.Element(WebDavXml.Dav + "prop")?.Elements().ToArray();
        if (element.Name != WebDavXml.Dav + "principal-search-property" || properties is not { Length: 1 })
            throw new InvalidDataException("A principal search property must name exactly one property.");
        Name = properties[0].Name;
        XElement? description = element.Element(WebDavXml.Dav + "description");
        Description = description?.Value;
        Language = (string?)description?.Attribute(XNamespace.Xml + "lang");
    }

    /// <summary>获取可用于 property-search 的完整属性名称。</summary>
    public XName Name
    {
        get;
    }
    /// <summary>获取属性用途说明。</summary>
    public string? Description
    {
        get;
    }
    /// <summary>获取说明的语言标记。</summary>
    public string? Language
    {
        get;
    }
}

/// <summary>保存 principal-search-property-set 报告的 200 XML 响应。</summary>
public sealed class WebDavPrincipalSearchPropertySetResponse : WebDavDocumentResponse
{
    /// <summary>从完整报告文档解析可搜索属性声明。</summary>
    /// <param name="response">报告 HTTP 和 XML 响应。</param>
    internal WebDavPrincipalSearchPropertySetResponse(WebDavDocumentResponse response)
        : base(response.StatusCode, response.Description, response.Document)
    {
        Headers = response.Headers;
        Error = response.Error;
        Representation = response.Representation;
        if (response.IsSuccessful && response.Document?.Name != WebDavXml.Dav + "principal-search-property-set")
            throw new InvalidDataException("Expected DAV:principal-search-property-set report response.");
        Properties = Array.AsReadOnly(response.Document?.Name == WebDavXml.Dav + "principal-search-property-set"
            ? response.Document.Elements(WebDavXml.Dav + "principal-search-property").Select(value => new WebDavPrincipalSearchProperty(value)).ToArray()
            : []);
    }

    /// <summary>获取服务器支持查询的主体属性与说明。</summary>
    public IReadOnlyList<WebDavPrincipalSearchProperty> Properties
    {
        get;
    }
}

