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
using MonsterSyncDrive.WebDAV.Core;
using MonsterSyncDrive.WebDAV.Core.AccessControl;
using MonsterSyncDrive.WebDAV.Helpers;
using MonsterSyncDrive.WebDAV.Request;
using MonsterSyncDrive.WebDAV.Response;

namespace MonsterSyncDrive.WebDAV;

/// <summary>实现 RFC 3744 访问控制、主体报告以及 RFC 5397 当前认证主体发现。</summary>
public sealed partial class WebDavClient
{
    /// <summary>ACL 快照同时读取的属性；可选属性失败保留状态，不当作空权限。</summary>
    private static readonly XName[] AclPropertyNames =
    [
        WebDavXml.Dav + "acl", WebDavXml.Dav + "acl-restrictions", WebDavXml.Dav + "supported-privilege-set",
        WebDavXml.Dav + "current-user-privilege-set", WebDavXml.Dav + "owner", WebDavXml.Dav + "group",
        WebDavXml.Dav + "inherited-acl-set", WebDavXml.Dav + "principal-collection-set"
    ];

    /// <summary>用户及组主体查询的标准属性；组成员与组隶属只表示直接关系。</summary>
    private static readonly XName[] PrincipalPropertyNames =
    [
        WebDavXml.Dav + "displayname", WebDavXml.Dav + "resourcetype", WebDavXml.Dav + "principal-URL",
        WebDavXml.Dav + "alternate-URI-set", WebDavXml.Dav + "group-member-set", WebDavXml.Dav + "group-membership",
        WebDavXml.Dav + "principal-collection-set"
    ];

    /// <summary>读取当前认证主体；属性缺失或失败不视为未认证。</summary>
    /// <remarks>使用 Depth:0 查询 current-user-principal；只有明确返回 unauthenticated 才能认定未认证，缺失或 403 应结合 IsSuccessful 及逐属性状态处理。</remarks>
    /// <param name="requestUri">请求资源的绝对或相对 HTTP(S) 地址字符串；相对地址按客户端 BaseAddress 解析。</param>
    /// <param name="options">通用条件与取消设置。</param>
    /// <returns>认证主体 URI、明确未认证标志或查询失败状态。</returns>
    public Task<WebDavCurrentPrincipalResponse> GetCurrentPrincipalAsync(string requestUri, WebDavRequestOptions? options = null) =>
        GetCurrentPrincipalAsync(CreateUri(requestUri), options);

    /// <summary>读取当前认证主体；属性缺失或失败不视为未认证。</summary>
    /// <remarks>使用 Depth:0 查询 current-user-principal；只有明确返回 unauthenticated 才能认定未认证，缺失或 403 应结合 IsSuccessful 及逐属性状态处理。</remarks>
    /// <param name="requestUri">请求资源的绝对或相对 HTTP(S) URI；相对 URI 按客户端 BaseAddress 解析。</param>
    /// <param name="options">通用条件与取消设置。</param>
    /// <returns>认证主体 URI、明确未认证标志或查询失败状态。</returns>
    public async Task<WebDavCurrentPrincipalResponse> GetCurrentPrincipalAsync(Uri requestUri, WebDavRequestOptions? options = null)
    {
        var uri = ResolveUri(requestUri);
        var response = await ReadAclPropertiesAsync(uri, [WebDavXml.Dav + "current-user-principal"], options).ConfigureAwait(false);
        return new WebDavCurrentPrincipalResponse(response, uri);
    }

    /// <summary>查询 ACL、当前有效权限、权限树、限制、owner、group 及主体集合。</summary>
    /// <remarks>使用 Depth:0 读取资源属性；可选属性失败单独保留。权限树区分抽象和聚合权限，当前用户有效权限由服务器计算，不能仅由 ACE 推断。</remarks>
    /// <param name="requestUri">请求资源的绝对或相对 HTTP(S) 地址字符串；相对地址按客户端 BaseAddress 解析。</param>
    /// <param name="options">通用条件与取消设置。</param>
    /// <returns>完整 ACL 快照与逐属性状态；HasAcl=false 不表示 ACL 为空。</returns>
    public Task<WebDavAccessControlResponse> GetAccessControlAsync(string requestUri, WebDavRequestOptions? options = null) =>
        GetAccessControlAsync(CreateUri(requestUri), options);

    /// <summary>查询 ACL、当前有效权限、权限树、限制、owner、group 及主体集合。</summary>
    /// <remarks>使用 Depth:0 读取资源属性；可选属性失败单独保留。权限树区分抽象和聚合权限，当前用户有效权限由服务器计算，不能仅由 ACE 推断。</remarks>
    /// <param name="requestUri">请求资源的绝对或相对 HTTP(S) URI；相对 URI 按客户端 BaseAddress 解析。</param>
    /// <param name="options">通用条件与取消设置。</param>
    /// <returns>完整 ACL 快照与逐属性状态；HasAcl=false 不表示 ACL 为空。</returns>
    public async Task<WebDavAccessControlResponse> GetAccessControlAsync(Uri requestUri, WebDavRequestOptions? options = null) =>
        new(await ReadAclPropertiesAsync(ResolveUri(requestUri), AclPropertyNames, options).ConfigureAwait(false));

    /// <summary>明确替换全部可编辑 ACL 条目；保护和继承条目自动排除，由服务器保留。</summary>
    /// <remarks>这是完整替换，不做隐式读取或加锁；可通过 WebDavAclUpdateParameters.LockToken 提交已有锁。</remarks>
    /// <param name="requestUri">请求资源的绝对或相对 HTTP(S) 地址字符串；相对地址按客户端 BaseAddress 解析。</param>
    /// <param name="entries">完整可编辑替换集合；空集合表示移除可编辑 ACL。</param>
    /// <param name="options">通用条件，可使用 If 或已有 LockToken 保护替换。</param>
    /// <returns>服务器 ACL 响应及结构化 DAV 错误。</returns>
    public Task<WebDavDocumentResponse> SetAclAsync(string requestUri, IEnumerable<WebDavAce> entries, WebDavRequestOptions? options = null) =>
        SetAclAsync(CreateUri(requestUri), entries, options);

    /// <summary>明确替换全部可编辑 ACL 条目；保护和继承条目自动排除，由服务器保留。</summary>
    /// <remarks>这是完整替换，不做隐式读取或加锁；可通过 WebDavAclUpdateParameters.LockToken 提交已有锁。</remarks>
    /// <param name="requestUri">请求资源的绝对或相对 HTTP(S) URI；相对 URI 按客户端 BaseAddress 解析。</param>
    /// <param name="entries">完整可编辑替换集合；空集合表示移除可编辑 ACL。</param>
    /// <param name="options">通用条件，可使用 If 或已有 LockToken 保护替换。</param>
    /// <returns>服务器 ACL 响应及结构化 DAV 错误。</returns>
    public async Task<WebDavDocumentResponse> SetAclAsync(Uri requestUri, IEnumerable<WebDavAce> entries, WebDavRequestOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var materialized = entries.ToArray();
        if (materialized.Any(entry => entry is null)) throw new ArgumentException("ACL entries cannot contain null.", nameof(entries));
        // RFC 3744 §8.1 的替换集合仅包含可编辑项。保护或继承项由服务器保留，重发它们反而可能触发前置条件失败。
        var body = new XElement(WebDavXml.Dav + "acl", materialized.Where(entry => entry.IsEditable).Select(entry => entry.ToXElement()));
        var token = (options as WebDavAclUpdateParameters)?.LockToken;
        var headers = AclLockHeaders(token);
        var result = await SendDocumentRequestAsync(ResolveUri(requestUri), "ACL", body, options, headers).ConfigureAwait(false);
        result.TreatPropertyErrorsAsFailure = true;
        return result;
    }

    /// <summary>读改写授予主体精确名称权限，并移除同主体可编辑 deny 中相同权限。</summary>
    /// <remarks>默认获取独占锁；保留其他权限和服务器固定项，不自动展开聚合权限或修改用户账户。</remarks>
    /// <param name="requestUri">请求资源的绝对或相对 HTTP(S) 地址字符串；相对地址按客户端 BaseAddress 解析。</param>
    /// <param name="principal">完整主体选择器。</param>
    /// <param name="privileges">需要授予的权限名称。</param>
    /// <param name="options">锁、HTTP 条件及取消设置。</param>
    /// <returns>ACL 写入或获取锁失败的响应；无法安全读取 ACL 时抛出异常。</returns>
    public Task<WebDavDocumentResponse> GrantPrivilegesAsync(string requestUri, WebDavPrincipal principal, IEnumerable<XName> privileges,
        WebDavAclUpdateParameters? options = null) => GrantPrivilegesAsync(CreateUri(requestUri), principal, privileges, options);

    /// <summary>读改写授予主体精确名称权限，并移除同主体可编辑 deny 中相同权限。</summary>
    /// <remarks>默认获取独占锁；保留其他权限和服务器固定项，不自动展开聚合权限或修改用户账户。</remarks>
    /// <param name="requestUri">请求资源的绝对或相对 HTTP(S) URI；相对 URI 按客户端 BaseAddress 解析。</param>
    /// <param name="principal">完整主体选择器。</param>
    /// <param name="privileges">需要授予的权限名称。</param>
    /// <param name="options">锁、HTTP 条件及取消设置。</param>
    /// <returns>ACL 写入或获取锁失败的响应；无法安全读取 ACL 时抛出异常。</returns>
    public Task<WebDavDocumentResponse> GrantPrivilegesAsync(Uri requestUri, WebDavPrincipal principal, IEnumerable<XName> privileges,
        WebDavAclUpdateParameters? options = null) => MutateAclAsync(requestUri, principal, privileges, AclMutation.Grant, options);

    /// <summary>读改写拒绝主体精确名称权限，并移除同主体可编辑 grant 中相同权限。</summary>
    /// <remarks>默认获取独占锁；服务器 grant-only 等限制在读取后检查，服务器仍决定完整授权语义。</remarks>
    /// <param name="requestUri">请求资源的绝对或相对 HTTP(S) 地址字符串；相对地址按客户端 BaseAddress 解析。</param>
    /// <param name="principal">完整主体选择器。</param>
    /// <param name="privileges">需要拒绝的权限名称。</param>
    /// <param name="options">锁、HTTP 条件及取消设置。</param>
    /// <returns>ACL 写入响应。</returns>
    public Task<WebDavDocumentResponse> DenyPrivilegesAsync(string requestUri, WebDavPrincipal principal, IEnumerable<XName> privileges,
        WebDavAclUpdateParameters? options = null) => DenyPrivilegesAsync(CreateUri(requestUri), principal, privileges, options);

    /// <summary>读改写拒绝主体精确名称权限，并移除同主体可编辑 grant 中相同权限。</summary>
    /// <remarks>默认获取独占锁；服务器 grant-only 等限制在读取后检查，服务器仍决定完整授权语义。</remarks>
    /// <param name="requestUri">请求资源的绝对或相对 HTTP(S) URI；相对 URI 按客户端 BaseAddress 解析。</param>
    /// <param name="principal">完整主体选择器。</param>
    /// <param name="privileges">需要拒绝的权限名称。</param>
    /// <param name="options">锁、HTTP 条件及取消设置。</param>
    /// <returns>ACL 写入响应。</returns>
    public Task<WebDavDocumentResponse> DenyPrivilegesAsync(Uri requestUri, WebDavPrincipal principal, IEnumerable<XName> privileges,
        WebDavAclUpdateParameters? options = null) => MutateAclAsync(requestUri, principal, privileges, AclMutation.Deny, options);

    /// <summary>只从同主体可编辑 grant 中撤销精确权限名称，始终保留 deny、保护及继承项。</summary>
    /// <remarks>不保证用户最终失去经组或继承获得的权限；需再次查询 current-user-privilege-set 确认有效权限。</remarks>
    /// <param name="requestUri">请求资源的绝对或相对 HTTP(S) 地址字符串；相对地址按客户端 BaseAddress 解析。</param>
    /// <param name="principal">完整主体选择器。</param>
    /// <param name="privileges">应撤销的显式授权名称。</param>
    /// <param name="options">锁、HTTP 条件及取消设置。</param>
    /// <returns>ACL 写入响应。</returns>
    public Task<WebDavDocumentResponse> RevokePrivilegesAsync(string requestUri, WebDavPrincipal principal, IEnumerable<XName> privileges,
        WebDavAclUpdateParameters? options = null) => RevokePrivilegesAsync(CreateUri(requestUri), principal, privileges, options);

    /// <summary>只从同主体可编辑 grant 中撤销精确权限名称，始终保留 deny、保护及继承项。</summary>
    /// <remarks>不保证用户最终失去经组或继承获得的权限；需再次查询 current-user-privilege-set 确认有效权限。</remarks>
    /// <param name="requestUri">请求资源的绝对或相对 HTTP(S) URI；相对 URI 按客户端 BaseAddress 解析。</param>
    /// <param name="principal">完整主体选择器。</param>
    /// <param name="privileges">应撤销的显式授权名称。</param>
    /// <param name="options">锁、HTTP 条件及取消设置。</param>
    /// <returns>ACL 写入响应。</returns>
    public Task<WebDavDocumentResponse> RevokePrivilegesAsync(Uri requestUri, WebDavPrincipal principal, IEnumerable<XName> privileges,
        WebDavAclUpdateParameters? options = null) => MutateAclAsync(requestUri, principal, privileges, AclMutation.Revoke, options);

    /// <summary>查询用户或组主体属性，包括标准主体 URL、直接组成员及直接所属组。</summary>
    /// <remarks>使用 Depth:0 读取标准主体属性，保留无法读取属性的状态；组成员及组隶属只表示直接关系，不自动展开嵌套组，空组仍可识别。</remarks>
    /// <param name="requestUri">请求资源的绝对或相对 HTTP(S) 地址字符串；相对地址按客户端 BaseAddress 解析。</param>
    /// <param name="options">通用条件与取消设置。</param>
    /// <returns>主体属性与逐属性失败状态。</returns>
    public Task<WebDavPrincipalResponse> GetPrincipalAsync(string requestUri, WebDavRequestOptions? options = null) =>
        GetPrincipalAsync(CreateUri(requestUri), options);

    /// <summary>查询用户或组主体属性，包括标准主体 URL、直接组成员及直接所属组。</summary>
    /// <remarks>使用 Depth:0 读取标准主体属性，保留无法读取属性的状态；组成员及组隶属只表示直接关系，不自动展开嵌套组，空组仍可识别。</remarks>
    /// <param name="requestUri">请求资源的绝对或相对 HTTP(S) URI；相对 URI 按客户端 BaseAddress 解析。</param>
    /// <param name="options">通用条件与取消设置。</param>
    /// <returns>主体属性与逐属性失败状态。</returns>
    public async Task<WebDavPrincipalResponse> GetPrincipalAsync(Uri requestUri, WebDavRequestOptions? options = null) =>
        new(await ReadAclPropertiesAsync(ResolveUri(requestUri), PrincipalPropertyNames, options).ConfigureAwait(false));

    /// <summary>通过 RFC 3744 属性匹配报告发现用户或组主体，多个条件按逻辑 AND 组合。</summary>
    /// <remarks>报告固定 Depth:0；同组多个属性及不同搜索条件均为逻辑 AND。文本匹配、大小写和子串规则由服务器定义。</remarks>
    /// <param name="requestUri">请求资源的绝对或相对 HTTP(S) 地址字符串；相对地址按客户端 BaseAddress 解析。</param>
    /// <param name="parameters">搜索及返回属性设置，报告固定 Depth:0。</param>
    /// <returns>匹配的主体资源与逐项状态。</returns>
    public Task<WebDavPrincipalResponse> PrincipalPropertySearchAsync(string requestUri, WebDavPrincipalSearchParameters parameters) =>
        PrincipalPropertySearchAsync(CreateUri(requestUri), parameters);

    /// <summary>通过 RFC 3744 属性匹配报告发现用户或组主体，多个条件按逻辑 AND 组合。</summary>
    /// <remarks>报告固定 Depth:0；同组多个属性及不同搜索条件均为逻辑 AND。文本匹配、大小写和子串规则由服务器定义。</remarks>
    /// <param name="requestUri">请求资源的绝对或相对 HTTP(S) URI；相对 URI 按客户端 BaseAddress 解析。</param>
    /// <param name="parameters">搜索及返回属性设置，报告固定 Depth:0。</param>
    /// <returns>匹配的主体资源与逐项状态。</returns>
    public async Task<WebDavPrincipalResponse> PrincipalPropertySearchAsync(Uri requestUri, WebDavPrincipalSearchParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(parameters.Searches);
        if (parameters.Searches.Count == 0 || parameters.Searches.Any(search => search is null))
            throw new ArgumentException("At least one principal property search is required.", nameof(parameters));
        var body = new XElement(WebDavXml.Dav + "principal-property-search", parameters.Searches.Select(search => search.ToXElement()));
        AddPrincipalReturnProperties(body, parameters.ReturnProperties);
        if (parameters.ApplyToPrincipalCollectionSet) body.Add(new XElement(WebDavXml.Dav + "apply-to-principal-collection-set"));
        return new WebDavPrincipalResponse(await SendPrincipalReportAsync(ResolveUri(requestUri), body, parameters).ConfigureAwait(false));
    }

    /// <summary>发现服务器允许用于主体属性搜索的属性集合及语言说明。</summary>
    /// <remarks>报告固定 Depth:0；成功结果解析标准 200 XML 根文档，保留说明的 xml:lang，而非将其当作多状态资源列表。</remarks>
    /// <param name="requestUri">请求资源的绝对或相对 HTTP(S) 地址字符串；相对地址按客户端 BaseAddress 解析。</param>
    /// <param name="options">通用条件及取消设置，报告固定 Depth:0。</param>
    /// <returns>来自标准 200 XML 报告的搜索属性声明。</returns>
    public Task<WebDavPrincipalSearchPropertySetResponse> PrincipalSearchPropertySetAsync(string requestUri, WebDavRequestOptions? options = null) =>
        PrincipalSearchPropertySetAsync(CreateUri(requestUri), options);

    /// <summary>发现服务器允许用于主体属性搜索的属性集合及语言说明。</summary>
    /// <remarks>报告固定 Depth:0；成功结果解析标准 200 XML 根文档，保留说明的 xml:lang，而非将其当作多状态资源列表。</remarks>
    /// <param name="requestUri">请求资源的绝对或相对 HTTP(S) URI；相对 URI 按客户端 BaseAddress 解析。</param>
    /// <param name="options">通用条件及取消设置，报告固定 Depth:0。</param>
    /// <returns>来自标准 200 XML 报告的搜索属性声明。</returns>
    public async Task<WebDavPrincipalSearchPropertySetResponse> PrincipalSearchPropertySetAsync(Uri requestUri, WebDavRequestOptions? options = null)
    {
        ValidatePrincipalReportDepth(options);
        var response = await SendDocumentRequestAsync(ResolveUri(requestUri), "REPORT",
            new XElement(WebDavXml.Dav + "principal-search-property-set"), options, [KeyValuePair.Create("Depth", "0")]).ConfigureAwait(false);
        return new WebDavPrincipalSearchPropertySetResponse(response);
    }

    /// <summary>查找匹配当前用户的主体，或指定主体属性引用当前用户的资源。</summary>
    /// <remarks>self 结果可能包含所属组，不能用本报告推断唯一的认证用户身份。</remarks>
    /// <param name="requestUri">请求资源的绝对或相对 HTTP(S) 地址字符串；相对地址按客户端 BaseAddress 解析。</param>
    /// <param name="properties">希望返回的属性，空集合省略 prop。</param>
    /// <param name="principalProperty">主体引用属性，例如 DAV:owner；null 使用 self。</param>
    /// <param name="options">通用选项，报告固定 Depth:0。</param>
    /// <returns>匹配资源及主体属性。</returns>
    public Task<WebDavPrincipalResponse> PrincipalMatchAsync(string requestUri, IReadOnlyCollection<XName>? properties = null,
        XName? principalProperty = null, WebDavRequestOptions? options = null) =>
        PrincipalMatchAsync(CreateUri(requestUri), properties, principalProperty, options);

    /// <summary>查找匹配当前用户的主体，或指定主体属性引用当前用户的资源。</summary>
    /// <remarks>self 结果可能包含所属组，不能用本报告推断唯一的认证用户身份。</remarks>
    /// <param name="requestUri">请求资源的绝对或相对 HTTP(S) URI；相对 URI 按客户端 BaseAddress 解析。</param>
    /// <param name="properties">希望返回的属性，空集合省略 prop。</param>
    /// <param name="principalProperty">主体引用属性，例如 DAV:owner；null 使用 self。</param>
    /// <param name="options">通用选项，报告固定 Depth:0。</param>
    /// <returns>匹配资源及主体属性。</returns>
    public async Task<WebDavPrincipalResponse> PrincipalMatchAsync(Uri requestUri, IReadOnlyCollection<XName>? properties = null,
        XName? principalProperty = null, WebDavRequestOptions? options = null)
    {
        var body = new XElement(WebDavXml.Dav + "principal-match",
            principalProperty is null ? new XElement(WebDavXml.Dav + "self")
                : new XElement(WebDavXml.Dav + "principal-property", new XElement(principalProperty)));
        AddPrincipalReturnProperties(body, properties);
        return new WebDavPrincipalResponse(await SendPrincipalReportAsync(ResolveUri(requestUri), body, options).ConfigureAwait(false));
    }

    /// <summary>读取 ACL 引用的各个用户或组主体的属性。</summary>
    /// <remarks>报告固定 Depth:0；空属性集合显式发送空 prop。只读取 ACL 引用主体的属性，不修改用户、组或资源 ACL。</remarks>
    /// <param name="requestUri">请求资源的绝对或相对 HTTP(S) 地址字符串；相对地址按客户端 BaseAddress 解析。</param>
    /// <param name="properties">返回属性；null 使用标准主体属性集合。</param>
    /// <param name="options">通用选项，报告固定 Depth:0。</param>
    /// <returns>主体报告的逐资源结果及完整 XML。</returns>
    public Task<WebDavPrincipalResponse> AclPrincipalPropSetAsync(string requestUri, IReadOnlyCollection<XName>? properties = null,
        WebDavRequestOptions? options = null) => AclPrincipalPropSetAsync(CreateUri(requestUri), properties, options);

    /// <summary>读取 ACL 引用的各个用户或组主体的属性。</summary>
    /// <remarks>报告固定 Depth:0；空属性集合显式发送空 prop。只读取 ACL 引用主体的属性，不修改用户、组或资源 ACL。</remarks>
    /// <param name="requestUri">请求资源的绝对或相对 HTTP(S) URI；相对 URI 按客户端 BaseAddress 解析。</param>
    /// <param name="properties">返回属性；null 使用标准主体属性集合。</param>
    /// <param name="options">通用选项，报告固定 Depth:0。</param>
    /// <returns>主体报告的逐资源结果及完整 XML。</returns>
    public async Task<WebDavPrincipalResponse> AclPrincipalPropSetAsync(Uri requestUri, IReadOnlyCollection<XName>? properties = null,
        WebDavRequestOptions? options = null)
    {
        var body = new XElement(WebDavXml.Dav + "acl-principal-prop-set");
        // 此报告的 prop 是必需元素，允许调用方请求空 prop，含义仍由服务器标准响应决定。
        body.Add(new XElement(WebDavXml.Dav + "prop", ValidatePrincipalPropertyNames(properties ?? PrincipalPropertyNames).Select(name => new XElement(name))));
        return new WebDavPrincipalResponse(await SendPrincipalReportAsync(ResolveUri(requestUri), body, options).ConfigureAwait(false));
    }

    /// <summary>对资源执行 Depth:0 属性查询并校验唯一响应资源，防止更新时误用其他资源的 ACL。</summary>
    /// <param name="uri">已经验证的请求 URI。</param>
    /// <param name="properties">需要读取的属性完整名称。</param>
    /// <param name="options">通用请求条件。</param>
    /// <returns>保留完整 XML 与状态的响应。</returns>
    private async Task<WebDavDocumentResponse> ReadAclPropertiesAsync(Uri uri, IEnumerable<XName> properties, WebDavRequestOptions? options)
    {
        ValidatePrincipalReportDepth(options);
        var response = await SendDocumentRequestAsync(uri, "PROPFIND",
            new XElement(WebDavXml.Dav + "propfind", new XElement(WebDavXml.Dav + "prop", properties.Select(name => new XElement(name)))),
            options, [KeyValuePair.Create("Depth", "0")]).ConfigureAwait(false);
        if (response.StatusCode is >= 200 and < 300)
        {
            ValidatePrincipalMultiStatus(response);
            if (response.Resources.Count != 1)
                throw new InvalidDataException("A depth-zero ACL property query must return exactly one resource.");
            var href = response.Resources.Single().Uri;
            if (string.IsNullOrWhiteSpace(href) || !Uri.TryCreate(uri, href, out var resourceUri)
                || (resourceUri != uri && resourceUri.AbsoluteUri.TrimEnd('/') != uri.AbsoluteUri.TrimEnd('/')))
                throw new InvalidDataException("The ACL property response does not identify the requested resource.");
        }
        return response;
    }

    /// <summary>发送只允许 Depth:0 的主体报告并验证多状态响应根。</summary>
    /// <param name="uri">请求资源 URI。</param>
    /// <param name="body">报告 XML。</param>
    /// <param name="options">通用选项。</param>
    /// <returns>完整报告响应。</returns>
    private async Task<WebDavDocumentResponse> SendPrincipalReportAsync(Uri uri, XElement body, WebDavRequestOptions? options)
    {
        ValidatePrincipalReportDepth(options);
        var response = await SendDocumentRequestAsync(uri, "REPORT", body, options, [KeyValuePair.Create("Depth", "0")]).ConfigureAwait(false);
        if (response.StatusCode is >= 200 and < 300) ValidatePrincipalMultiStatus(response);
        return response;
    }

    /// <summary>校验主体报告及属性查询的成功响应必须使用 DAV 多状态根。</summary>
    /// <param name="response">待验证的文档响应。</param>
    private static void ValidatePrincipalMultiStatus(WebDavDocumentResponse response)
    {
        if (response.StatusCode != 207 || response.Document?.Name != WebDavXml.Dav + "multistatus")
            throw new InvalidDataException("The ACL operation requires a 207 DAV:multistatus response.");
    }

    /// <summary>拒绝通过最终覆盖头改变 RFC 3744 主体报告的固定深度。</summary>
    /// <param name="options">通用请求选项。</param>
    private static void ValidatePrincipalReportDepth(WebDavRequestOptions? options)
    {
        if (options?.Headers.Any(header => header.Key.Equals("Depth", StringComparison.OrdinalIgnoreCase) && header.Value != "0") == true)
            throw new ArgumentException("RFC 3744 principal operations require Depth: 0.", nameof(options));
    }

    /// <summary>向报告添加可选返回属性列表。</summary>
    /// <param name="body">报告根节点。</param>
    /// <param name="properties">返回属性完整名称；null 或空时省略。</param>
    private static void AddPrincipalReturnProperties(XElement body, IReadOnlyCollection<XName>? properties)
    {
        if (properties is { Count: > 0 })
            body.Add(new XElement(WebDavXml.Dav + "prop", ValidatePrincipalPropertyNames(properties).Select(name => new XElement(name))));
    }

    /// <summary>校验属性完整名称并保持第一次出现的顺序。</summary>
    /// <param name="properties">属性名称集合。</param>
    /// <returns>物化并去重后的名称。</returns>
    private static XName[] ValidatePrincipalPropertyNames(IEnumerable<XName> properties)
    {
        var names = properties.Distinct().ToArray();
        if (names.Any(name => name is null)) throw new ArgumentException("Property names cannot be null.", nameof(properties));
        return names;
    }

    /// <summary>区分权限便利方法的精确集合操作。</summary>
    private enum AclMutation
    {
        /// <summary>向可编辑 grant 增加权限，并移除同一主体相同名称的可编辑 deny。</summary>
        Grant,
        /// <summary>向可编辑 deny 增加权限，并移除同一主体相同名称的可编辑 grant。</summary>
        Deny,
        /// <summary>只移除可编辑 grant；不能删除 deny 以免意外提升权限。</summary>
        Revoke
    }

    /// <summary>在锁保护下读取、合并并替换可编辑 ACL，所有路径均释放本方法获取的锁。</summary>
    /// <param name="requestUri">目标资源 URI。</param>
    /// <param name="principal">应修改的完整主体选择器。</param>
    /// <param name="privileges">精确权限名称，聚合权限不自动展开。</param>
    /// <param name="mutation">需要执行的集合操作。</param>
    /// <param name="options">锁、HTTP 条件与取消设置。</param>
    /// <returns>ACL 写入响应，或获取锁失败的 HTTP 响应。</returns>
    private async Task<WebDavDocumentResponse> MutateAclAsync(Uri requestUri, WebDavPrincipal principal, IEnumerable<XName> privileges,
        AclMutation mutation, WebDavAclUpdateParameters? options)
    {
        ArgumentNullException.ThrowIfNull(principal);
        ArgumentNullException.ThrowIfNull(privileges);
        var names = privileges.Distinct().ToArray();
        if (names.Length == 0 || names.Any(name => name is null))
            throw new ArgumentException("At least one privilege is required.", nameof(privileges));
        options ??= new WebDavAclUpdateParameters();
        options.Validate();
        ValidatePrincipalReportDepth(options);
        var uri = ResolveUri(requestUri);
        var token = options.LockToken;
        var ownsLock = false;
        if (token is null && options.AcquireLock)
        {
            // 自动锁的令牌在请求前未知，不能让调用方的完整 If 或原始 If 覆盖它。使用已有令牌时完整条件由调用方负责。
            if (options.IfConditions is not null || options.Headers.Any(header => header.Key.Equals("If", StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException("Automatic ACL locking cannot be combined with an explicit If expression; supply LockToken or disable AcquireLock.", nameof(options));
            var lockOptions = new LockParameters { LockScope = LockScope.Exclusive, ApplyTo = ApplyTo.Lock.ResourceOnly, Timeout = options.LockTimeout };
            options.CopyTo(lockOptions);
            var locked = await Lock(uri, lockOptions).ConfigureAwait(false);
            if (!locked.IsSuccessful)
            {
                // 集合加锁可能以 207 包含失败资源。不能丢掉资源后返回一个表面成功的空 207。
                if (locked.StatusCode is >= 200 and < 300) locked.EnsureSuccessStatusCode();
                return new WebDavDocumentResponse(locked.StatusCode, locked.Description, null) { Headers = locked.Headers, Error = locked.Error };
            }
            token = locked.GetHeader("Lock-Token") ?? locked.ActiveLocks.Select(value => value.LockToken).Where(value => value is not null).Distinct().SingleOrDefault();
            if (token is null) throw new InvalidDataException("Successful ACL locking did not return a usable lock token.");
            token = IfHeaderHelper.Normalize(token);
            ownsLock = true;
        }

        Exception? operationFailure = null;
        try
        {
            var response = await ReadAclPropertiesAsync(uri, AclPropertyNames, options).ConfigureAwait(false);
            var snapshot = new WebDavAccessControlResponse(response);
            snapshot.EnsureSuccessStatusCode();
            if (!snapshot.HasAcl)
                throw new InvalidDataException("The server did not return a readable ACL; replacing an unknown ACL is unsafe.");
            var replacement = BuildAclReplacement(snapshot, principal, names, mutation);
            var body = new XElement(WebDavXml.Dav + "acl", replacement.Select(entry => entry.ToXElement()));
            var headers = AclLockHeaders(token);
            var result = await SendDocumentRequestAsync(uri, "ACL", body, options, headers).ConfigureAwait(false);
            result.TreatPropertyErrorsAsFailure = true;
            return result;
        }
        catch (Exception error)
        {
            operationFailure = error;
            throw;
        }
        finally
        {
            if (ownsLock)
            {
                try
                {
                    // 即便调用方取消，也尝试在独立请求超时内解锁；绝不释放调用方借给本方法的锁。
                    var unlocked = await Unlock(uri, new UnlockParameters(token!) { CancellationToken = CancellationToken.None }).ConfigureAwait(false);
                    unlocked.EnsureSuccessStatusCode();
                }
                catch (Exception cleanupError) when (operationFailure is not null)
                {
                    throw new AggregateException("ACL operation and release of its temporary lock both failed.", operationFailure, cleanupError);
                }
            }
        }
    }

    /// <summary>生成锁条件及防缓存头，避免不理解 DAV If 的代理复用条件响应。</summary>
    /// <param name="token">已有资源锁令牌，null 表示没有锁条件。</param>
    /// <returns>协议请求头；未持有锁时为 null。</returns>
    private static IReadOnlyCollection<KeyValuePair<string, string>>? AclLockHeaders(string? token) => token is null ? null
        : [KeyValuePair.Create("If", IfHeaderHelper.GetHeaderValue(token)), KeyValuePair.Create("Cache-Control", "no-cache")];

    /// <summary>仅修改可编辑 ACE，保留未知扩展、其他主体及原有顺序，并应用明确的服务器限制。</summary>
    /// <param name="snapshot">服务器 ACL 及限制快照。</param>
    /// <param name="principal">需要修改的精确主体。</param>
    /// <param name="privileges">精确权限名称集合。</param>
    /// <param name="mutation">授予、拒绝或撤销。</param>
    /// <returns>不含服务器保护和继承项的替换集合。</returns>
    private static IReadOnlyList<WebDavAce> BuildAclReplacement(WebDavAccessControlResponse snapshot, WebDavPrincipal principal,
        XName[] privileges, AclMutation mutation)
    {
        var targetAction = mutation == AclMutation.Deny ? WebDavAceAction.Deny : WebDavAceAction.Grant;
        if (mutation != AclMutation.Revoke)
        {
            if (snapshot.Restrictions.GrantOnly && targetAction == WebDavAceAction.Deny)
                throw new InvalidOperationException("The server only permits grant ACEs.");
            if (snapshot.Restrictions.NoInvert && principal.IsInverted)
                throw new InvalidOperationException("The server does not permit inverted principals.");
            var abstractNames = snapshot.SupportedPrivileges.SelectMany(value => value.DescendantsAndSelf())
                .Where(value => value.IsAbstract).Select(value => value.Name).ToHashSet();
            if (privileges.Any(abstractNames.Contains))
                throw new InvalidOperationException("Abstract privileges cannot be placed in an ACE.");
        }
        var editable = new List<WebDavAce>();
        var merged = false;
        foreach (var entry in snapshot.Acl)
        {
            // 保护/继承权限绝不能混进可编辑项，否则随后撤销时会留下本来不存在的显式授权。
            if (!entry.IsEditable) continue;
            if (!entry.Principal.Equals(principal)) { editable.Add(entry); continue; }
            if (mutation == AclMutation.Revoke)
            {
                if (entry.Action == WebDavAceAction.Deny) { editable.Add(entry); continue; }
                var remaining = entry.Privileges.Except(privileges).ToArray();
                if (remaining.Length > 0) editable.Add(entry.WithPrivileges(remaining));
            }
            else if (entry.Action != targetAction)
            {
                var remaining = entry.Privileges.Except(privileges).ToArray();
                if (remaining.Length > 0) editable.Add(entry.WithPrivileges(remaining));
            }
            else
            {
                // 多个原有同主体项保留原位置与未知 XML；仅向首个相同动作项增加请求权限。
                editable.Add(!merged ? entry.WithPrivileges(entry.Privileges.Concat(privileges)) : entry);
                merged = true;
            }
        }
        if (mutation != AclMutation.Revoke && !merged) editable.Add(new WebDavAce(principal, targetAction, privileges));
        if (snapshot.Restrictions.DenyBeforeGrant)
            editable = editable.OrderBy(entry => entry.Action == WebDavAceAction.Deny ? 0 : 1).ToList();

        // required-principal 可以由服务器固定项满足。不能因为未重发固定项就错误认定主体被移除。
        var retainedPrincipals = snapshot.Acl.Where(entry => !entry.IsEditable).Concat(editable).Select(entry => entry.Principal).ToArray();
        if (snapshot.Restrictions.RequiredPrincipals.Any(required => !retainedPrincipals.Contains(required)))
            throw new InvalidOperationException("The update would remove a required ACL principal.");
        return editable;
    }
}

/// <summary>定义访问控制及用户、组主体发现接口。</summary>
public partial interface IWebDavClient
{
    /// <summary>读取当前认证主体；属性缺失或失败不视为未认证。</summary>
    /// <param name="requestUri">用于发现当前主体的资源地址。</param><param name="options">通用条件与取消设置。</param>
    /// <returns>认证主体 URI、明确未认证标志或查询失败状态。</returns>
    Task<WebDavCurrentPrincipalResponse> GetCurrentPrincipalAsync(Uri requestUri, WebDavRequestOptions? options = null);
    /// <summary>读取当前认证主体；属性缺失或失败不视为未认证。</summary>
    /// <remarks>使用 Depth:0 查询 current-user-principal；只有明确返回 unauthenticated 才能认定未认证，缺失或 403 应结合 IsSuccessful 及逐属性状态处理。</remarks>
    /// <param name="requestUri">请求资源的绝对或相对 HTTP(S) 地址字符串；相对地址按客户端 BaseAddress 解析。</param>
    /// <param name="options">通用条件与取消设置。</param>
    /// <returns>认证主体 URI、明确未认证标志或查询失败状态。</returns>
    Task<WebDavCurrentPrincipalResponse> GetCurrentPrincipalAsync(string requestUri, WebDavRequestOptions? options = null);

    /// <summary>查询 ACL、当前有效权限、权限树、限制、owner、group 及主体集合。</summary>
    /// <param name="requestUri">资源地址。</param><param name="options">通用条件与取消设置。</param>
    /// <returns>完整 ACL 快照与逐属性状态；HasAcl=false 不表示 ACL 为空。</returns>
    Task<WebDavAccessControlResponse> GetAccessControlAsync(Uri requestUri, WebDavRequestOptions? options = null);
    /// <summary>查询 ACL、当前有效权限、权限树、限制、owner、group 及主体集合。</summary>
    /// <remarks>使用 Depth:0 读取资源属性；可选属性失败单独保留。权限树区分抽象和聚合权限，当前用户有效权限由服务器计算，不能仅由 ACE 推断。</remarks>
    /// <param name="requestUri">请求资源的绝对或相对 HTTP(S) 地址字符串；相对地址按客户端 BaseAddress 解析。</param>
    /// <param name="options">通用条件与取消设置。</param>
    /// <returns>完整 ACL 快照与逐属性状态；HasAcl=false 不表示 ACL 为空。</returns>
    Task<WebDavAccessControlResponse> GetAccessControlAsync(string requestUri, WebDavRequestOptions? options = null);

    /// <summary>明确替换全部可编辑 ACL 条目；保护和继承条目自动排除，由服务器保留。</summary>
    /// <remarks>这是完整替换，不做隐式读取或加锁；可通过 WebDavAclUpdateParameters.LockToken 提交已有锁。</remarks>
    /// <param name="requestUri">资源地址。</param><param name="entries">完整可编辑替换集合；空集合表示移除可编辑 ACL。</param>
    /// <param name="options">通用条件，可使用 If 或已有 LockToken 保护替换。</param><returns>服务器 ACL 响应及结构化 DAV 错误。</returns>
    Task<WebDavDocumentResponse> SetAclAsync(Uri requestUri, IEnumerable<WebDavAce> entries, WebDavRequestOptions? options = null);
    /// <summary>明确替换全部可编辑 ACL 条目；保护和继承条目自动排除，由服务器保留。</summary>
    /// <remarks>这是完整替换，不做隐式读取或加锁；可通过 WebDavAclUpdateParameters.LockToken 提交已有锁。</remarks>
    /// <param name="requestUri">请求资源的绝对或相对 HTTP(S) 地址字符串；相对地址按客户端 BaseAddress 解析。</param>
    /// <param name="entries">完整可编辑替换集合；空集合表示移除可编辑 ACL。</param>
    /// <param name="options">通用条件，可使用 If 或已有 LockToken 保护替换。</param>
    /// <returns>服务器 ACL 响应及结构化 DAV 错误。</returns>
    Task<WebDavDocumentResponse> SetAclAsync(string requestUri, IEnumerable<WebDavAce> entries, WebDavRequestOptions? options = null);

    /// <summary>读改写授予主体精确名称权限，并移除同主体可编辑 deny 中相同权限。</summary>
    /// <remarks>默认获取独占锁；保留其他权限和服务器固定项，不自动展开聚合权限或修改用户账户。</remarks>
    /// <param name="requestUri">资源地址。</param><param name="principal">完整主体选择器。</param>
    /// <param name="privileges">需要授予的权限名称。</param><param name="options">锁、HTTP 条件及取消设置。</param>
    /// <returns>ACL 写入或获取锁失败的响应；无法安全读取 ACL 时抛出异常。</returns>
    Task<WebDavDocumentResponse> GrantPrivilegesAsync(Uri requestUri, WebDavPrincipal principal, IEnumerable<XName> privileges, WebDavAclUpdateParameters? options = null);
    /// <summary>读改写授予主体精确名称权限，并移除同主体可编辑 deny 中相同权限。</summary>
    /// <remarks>默认获取独占锁；保留其他权限和服务器固定项，不自动展开聚合权限或修改用户账户。</remarks>
    /// <param name="requestUri">请求资源的绝对或相对 HTTP(S) 地址字符串；相对地址按客户端 BaseAddress 解析。</param>
    /// <param name="principal">完整主体选择器。</param>
    /// <param name="privileges">需要授予的权限名称。</param>
    /// <param name="options">锁、HTTP 条件及取消设置。</param>
    /// <returns>ACL 写入或获取锁失败的响应；无法安全读取 ACL 时抛出异常。</returns>
    Task<WebDavDocumentResponse> GrantPrivilegesAsync(string requestUri, WebDavPrincipal principal, IEnumerable<XName> privileges, WebDavAclUpdateParameters? options = null);

    /// <summary>读改写拒绝主体精确名称权限，并移除同主体可编辑 grant 中相同权限。</summary>
    /// <remarks>默认获取独占锁；服务器 grant-only 等限制在读取后检查，服务器仍决定完整授权语义。</remarks>
    /// <param name="requestUri">资源地址。</param><param name="principal">完整主体选择器。</param>
    /// <param name="privileges">需要拒绝的权限名称。</param><param name="options">锁、HTTP 条件及取消设置。</param><returns>ACL 写入响应。</returns>
    Task<WebDavDocumentResponse> DenyPrivilegesAsync(Uri requestUri, WebDavPrincipal principal, IEnumerable<XName> privileges, WebDavAclUpdateParameters? options = null);
    /// <summary>读改写拒绝主体精确名称权限，并移除同主体可编辑 grant 中相同权限。</summary>
    /// <remarks>默认获取独占锁；服务器 grant-only 等限制在读取后检查，服务器仍决定完整授权语义。</remarks>
    /// <param name="requestUri">请求资源的绝对或相对 HTTP(S) 地址字符串；相对地址按客户端 BaseAddress 解析。</param>
    /// <param name="principal">完整主体选择器。</param>
    /// <param name="privileges">需要拒绝的权限名称。</param>
    /// <param name="options">锁、HTTP 条件及取消设置。</param>
    /// <returns>ACL 写入响应。</returns>
    Task<WebDavDocumentResponse> DenyPrivilegesAsync(string requestUri, WebDavPrincipal principal, IEnumerable<XName> privileges, WebDavAclUpdateParameters? options = null);

    /// <summary>只从同主体可编辑 grant 中撤销精确权限名称，始终保留 deny、保护及继承项。</summary>
    /// <remarks>不保证用户最终失去经组或继承获得的权限；需再次查询 current-user-privilege-set 确认有效权限。</remarks>
    /// <param name="requestUri">资源地址。</param><param name="principal">完整主体选择器。</param>
    /// <param name="privileges">应撤销的显式授权名称。</param><param name="options">锁、HTTP 条件及取消设置。</param><returns>ACL 写入响应。</returns>
    Task<WebDavDocumentResponse> RevokePrivilegesAsync(Uri requestUri, WebDavPrincipal principal, IEnumerable<XName> privileges, WebDavAclUpdateParameters? options = null);
    /// <summary>只从同主体可编辑 grant 中撤销精确权限名称，始终保留 deny、保护及继承项。</summary>
    /// <remarks>不保证用户最终失去经组或继承获得的权限；需再次查询 current-user-privilege-set 确认有效权限。</remarks>
    /// <param name="requestUri">请求资源的绝对或相对 HTTP(S) 地址字符串；相对地址按客户端 BaseAddress 解析。</param>
    /// <param name="principal">完整主体选择器。</param>
    /// <param name="privileges">应撤销的显式授权名称。</param>
    /// <param name="options">锁、HTTP 条件及取消设置。</param>
    /// <returns>ACL 写入响应。</returns>
    Task<WebDavDocumentResponse> RevokePrivilegesAsync(string requestUri, WebDavPrincipal principal, IEnumerable<XName> privileges, WebDavAclUpdateParameters? options = null);

    /// <summary>查询用户或组主体属性，包括标准主体 URL、直接组成员及直接所属组。</summary>
    /// <param name="requestUri">主体资源地址。</param><param name="options">通用条件与取消设置。</param><returns>主体属性与逐属性失败状态。</returns>
    Task<WebDavPrincipalResponse> GetPrincipalAsync(Uri requestUri, WebDavRequestOptions? options = null);
    /// <summary>查询用户或组主体属性，包括标准主体 URL、直接组成员及直接所属组。</summary>
    /// <remarks>使用 Depth:0 读取标准主体属性，保留无法读取属性的状态；组成员及组隶属只表示直接关系，不自动展开嵌套组，空组仍可识别。</remarks>
    /// <param name="requestUri">请求资源的绝对或相对 HTTP(S) 地址字符串；相对地址按客户端 BaseAddress 解析。</param>
    /// <param name="options">通用条件与取消设置。</param>
    /// <returns>主体属性与逐属性失败状态。</returns>
    Task<WebDavPrincipalResponse> GetPrincipalAsync(string requestUri, WebDavRequestOptions? options = null);

    /// <summary>通过 RFC 3744 属性匹配报告发现用户或组主体，多个条件按逻辑 AND 组合。</summary>
    /// <param name="requestUri">主体集合或发现资源地址。</param><param name="parameters">搜索及返回属性设置，报告固定 Depth:0。</param>
    /// <returns>匹配的主体资源与逐项状态。</returns>
    Task<WebDavPrincipalResponse> PrincipalPropertySearchAsync(Uri requestUri, WebDavPrincipalSearchParameters parameters);
    /// <summary>通过 RFC 3744 属性匹配报告发现用户或组主体，多个条件按逻辑 AND 组合。</summary>
    /// <remarks>报告固定 Depth:0；同组多个属性及不同搜索条件均为逻辑 AND。文本匹配、大小写和子串规则由服务器定义。</remarks>
    /// <param name="requestUri">请求资源的绝对或相对 HTTP(S) 地址字符串；相对地址按客户端 BaseAddress 解析。</param>
    /// <param name="parameters">搜索及返回属性设置，报告固定 Depth:0。</param>
    /// <returns>匹配的主体资源与逐项状态。</returns>
    Task<WebDavPrincipalResponse> PrincipalPropertySearchAsync(string requestUri, WebDavPrincipalSearchParameters parameters);

    /// <summary>发现服务器允许用于主体属性搜索的属性集合及语言说明。</summary>
    /// <param name="requestUri">支持报告的资源地址。</param><param name="options">通用条件及取消设置，报告固定 Depth:0。</param>
    /// <returns>来自标准 200 XML 报告的搜索属性声明。</returns>
    Task<WebDavPrincipalSearchPropertySetResponse> PrincipalSearchPropertySetAsync(Uri requestUri, WebDavRequestOptions? options = null);
    /// <summary>发现服务器允许用于主体属性搜索的属性集合及语言说明。</summary>
    /// <remarks>报告固定 Depth:0；成功结果解析标准 200 XML 根文档，保留说明的 xml:lang，而非将其当作多状态资源列表。</remarks>
    /// <param name="requestUri">请求资源的绝对或相对 HTTP(S) 地址字符串；相对地址按客户端 BaseAddress 解析。</param>
    /// <param name="options">通用条件及取消设置，报告固定 Depth:0。</param>
    /// <returns>来自标准 200 XML 报告的搜索属性声明。</returns>
    Task<WebDavPrincipalSearchPropertySetResponse> PrincipalSearchPropertySetAsync(string requestUri, WebDavRequestOptions? options = null);

    /// <summary>查找匹配当前用户的主体，或指定主体属性引用当前用户的资源。</summary>
    /// <remarks>self 结果可能包含所属组，不能用本报告推断唯一的认证用户身份。</remarks>
    /// <param name="requestUri">报告资源地址。</param><param name="properties">希望返回的属性，空集合省略 prop。</param>
    /// <param name="principalProperty">主体引用属性，例如 DAV:owner；null 使用 self。</param>
    /// <param name="options">通用选项，报告固定 Depth:0。</param><returns>匹配资源及主体属性。</returns>
    Task<WebDavPrincipalResponse> PrincipalMatchAsync(Uri requestUri, IReadOnlyCollection<XName>? properties = null, XName? principalProperty = null, WebDavRequestOptions? options = null);
    /// <summary>查找匹配当前用户的主体，或指定主体属性引用当前用户的资源。</summary>
    /// <remarks>self 结果可能包含所属组，不能用本报告推断唯一的认证用户身份。</remarks>
    /// <param name="requestUri">请求资源的绝对或相对 HTTP(S) 地址字符串；相对地址按客户端 BaseAddress 解析。</param>
    /// <param name="properties">希望返回的属性，空集合省略 prop。</param>
    /// <param name="principalProperty">主体引用属性，例如 DAV:owner；null 使用 self。</param>
    /// <param name="options">通用选项，报告固定 Depth:0。</param>
    /// <returns>匹配资源及主体属性。</returns>
    Task<WebDavPrincipalResponse> PrincipalMatchAsync(string requestUri, IReadOnlyCollection<XName>? properties = null, XName? principalProperty = null, WebDavRequestOptions? options = null);

    /// <summary>读取 ACL 引用的各个用户或组主体的属性。</summary>
    /// <param name="requestUri">含 ACL 的资源地址。</param><param name="properties">返回属性；null 使用标准主体属性集合。</param>
    /// <param name="options">通用选项，报告固定 Depth:0。</param><returns>主体报告的逐资源结果及完整 XML。</returns>
    Task<WebDavPrincipalResponse> AclPrincipalPropSetAsync(Uri requestUri, IReadOnlyCollection<XName>? properties = null, WebDavRequestOptions? options = null);
    /// <summary>读取 ACL 引用的各个用户或组主体的属性。</summary>
    /// <remarks>报告固定 Depth:0；空属性集合显式发送空 prop。只读取 ACL 引用主体的属性，不修改用户、组或资源 ACL。</remarks>
    /// <param name="requestUri">请求资源的绝对或相对 HTTP(S) 地址字符串；相对地址按客户端 BaseAddress 解析。</param>
    /// <param name="properties">返回属性；null 使用标准主体属性集合。</param>
    /// <param name="options">通用选项，报告固定 Depth:0。</param>
    /// <returns>主体报告的逐资源结果及完整 XML。</returns>
    Task<WebDavPrincipalResponse> AclPrincipalPropSetAsync(string requestUri, IReadOnlyCollection<XName>? properties = null, WebDavRequestOptions? options = null);
}

