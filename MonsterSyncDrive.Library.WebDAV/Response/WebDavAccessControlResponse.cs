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

/// <summary>保存资源 ACL、当前用户权限、支持权限树及服务器修改限制。</summary>
/// <remarks>可选属性的 403/404 可通过 Resource.PropertyStatuses 查看；HasAcl=false 不能当作空 ACL 用于替换操作。</remarks>
public sealed class WebDavAccessControlResponse : WebDavResponse
{
    /// <summary>从保留完整文档的 PROPFIND 响应构造 ACL 快照。</summary>
    /// <param name="response">HTTP 与 DAV 文档响应。</param>
    internal WebDavAccessControlResponse(WebDavDocumentResponse response)
        : base(response.StatusCode, response.Description)
    {
        Headers = response.Headers;
        Error = response.Error;
        Document = response.Document is null ? null : new XElement(response.Document);
        Resource = response.Resources.Count == 1 ? response.Resources.Single() : null;
        XElement? acl = Property("acl");
        HasAcl = acl is not null;
        Acl = Array.AsReadOnly(acl?.Elements(WebDavXml.Dav + "ace").Select(WebDavAce.Parse).ToArray() ?? []);
        CurrentUserPrivileges = Array.AsReadOnly(Property("current-user-privilege-set")?.Elements(WebDavXml.Dav + "privilege")
            .Select(ReadPrivilege).ToArray() ?? []);
        SupportedPrivileges = Array.AsReadOnly(Property("supported-privilege-set")?.Elements(WebDavXml.Dav + "supported-privilege")
            .Select(WebDavSupportedPrivilege.Parse).ToArray() ?? []);
        Restrictions = new WebDavAclRestrictions(Property("acl-restrictions"));
        Owner = WebDavPrincipalInfo.Hrefs(Property("owner")).SingleOrDefault();
        Group = WebDavPrincipalInfo.Hrefs(Property("group")).SingleOrDefault();
        InheritedAclSet = WebDavPrincipalInfo.Hrefs(Property("inherited-acl-set"));
        PrincipalCollections = WebDavPrincipalInfo.Hrefs(Property("principal-collection-set"));
    }

    /// <summary>获取服务器返回的完整 XML，包含未知属性和逐属性错误。</summary>
    public XElement? Document
    {
        get;
    }
    /// <summary>获取唯一请求资源，响应缺失或包含多余资源时为 null。</summary>
    public WebDavResource? Resource
    {
        get;
    }
    /// <summary>获取 ACL 属性是否实际成功返回；失败或省略不等于空 ACL。</summary>
    public bool HasAcl
    {
        get;
    }
    /// <summary>获取有序的 ACL 条目，包含服务器保护及继承项。</summary>
    public IReadOnlyList<WebDavAce> Acl
    {
        get;
    }
    /// <summary>获取服务器计算的当前用户有效权限；不能仅由 ACL 推断此集合。</summary>
    public IReadOnlyList<XName> CurrentUserPrivileges
    {
        get;
    }
    /// <summary>获取服务器支持的聚合权限森林。</summary>
    public IReadOnlyList<WebDavSupportedPrivilege> SupportedPrivileges
    {
        get;
    }
    /// <summary>获取 ACL 写入限制以及限制属性是否可用。</summary>
    public WebDavAclRestrictions Restrictions
    {
        get;
    }
    /// <summary>获取 owner 属性的主体 href，缺失时为 null。</summary>
    public string? Owner
    {
        get;
    }
    /// <summary>获取 group 属性的主体 href，缺失时为 null。</summary>
    public string? Group
    {
        get;
    }
    /// <summary>获取影响此资源的继承 ACL 源资源。</summary>
    public IReadOnlyList<string> InheritedAclSet
    {
        get;
    }
    /// <summary>获取可用于用户或组发现的主体集合 URI。</summary>
    public IReadOnlyList<string> PrincipalCollections
    {
        get;
    }
    /// <summary>获取 HTTP 和资源状态是否成功；可选属性失败单独保留。</summary>
    public override bool IsSuccessful => base.IsSuccessful && Resource is not null
        && Resource.StatusCode is null or (>= 200 and < 300);

    /// <summary>读取成功返回的 DAV 属性 XML。</summary>
    /// <param name="name">DAV 属性本地名称。</param>
    /// <returns>属性完整副本或 null。</returns>
    private XElement? Property(string name)
    {
        return Resource?.Properties.FirstOrDefault(property => property.Name == WebDavXml.Dav + name)?.ToXElement();
    }

    /// <summary>校验 privilege 恰好包含一个完整权限名称。</summary>
    /// <param name="element">DAV:privilege 元素。</param>
    /// <returns>权限完整名称。</returns>
    private static XName ReadPrivilege(XElement element)
    {
        XElement[] children = element.Elements().ToArray();
        return children.Length == 1 ? children[0].Name : throw new InvalidDataException("Invalid DAV:privilege.");
    }
}

