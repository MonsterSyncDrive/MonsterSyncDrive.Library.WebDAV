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
using MonsterSyncDrive.Library.WebDAV.Request;
using System.Xml.Linq;

namespace MonsterSyncDrive.Library.WebDAV.Response;

/// <summary>
/// 表示一次 RFC 6578 集合报告的完整页面及已经分类的成员变更。
/// </summary>
/// <remarks>资源级 404 是正常删除；request-URI 上的 507 是续取标志，不作为文件失败。RFC 8144 最小响应允许无属性的直接 200 变更，其他失败和不可遍历集合明确保留。</remarks>
public sealed class WebDavSyncResponse : WebDavReportResponse
{

    /// <summary>
    /// 解析已经完整读取的报告，并检查同步令牌、成员唯一性及分页语义。
    /// </summary>
    /// <param name="report">通用报告快照，包括同一次响应的完整 XML 和头部。</param>
    /// <param name="collectionUri">已经解析的绝对请求集合 URI。</param>
    /// <param name="level">本次请求的同步后代层级。</param>
    /// <param name="requestedSyncToken">发送给服务器的令牌；空值表示首次列举。</param>
    /// <param name="wasMinimalResponseRequested">实际请求的最终 Prefer 是否为 return=minimal，包含默认头和自定义头覆盖后的结果。</param>
    internal WebDavSyncResponse(WebDavReportResponse report, Uri collectionUri, WebDavSyncLevel level,
        string? requestedSyncToken, bool wasMinimalResponseRequested)
        : base(report.StatusCode, report.Description, report.Document, report.Resources)
    {
        Headers = report.Headers;
        Error = report.Error;
        Representation = report.Representation;
        RequestedSyncToken = requestedSyncToken;
        XElement? root = report.Document;
        XElement? errorElement = root?.Name == WebDavXml.Dav + "error" ? root : root?.Element(WebDavXml.Dav + "error");
        IsTokenInvalid = errorElement?.Elements(WebDavXml.Dav + "valid-sync-token").Any() == true;

        // 对失败响应保留状态和错误详情，不要求服务器还返回成功页面中的同步令牌。
        // 特别是失效令牌的 403 不会自动触发空令牌重试，避免调用方意外替换已有快照。
        if (StatusCode is not (>= 200 and < 300))
        {
            Changes = [];
            return;
        }
        if (StatusCode != 207 || root?.Name != WebDavXml.Dav + "multistatus")
            throw new InvalidDataException("A successful sync-collection report must return DAV:multistatus with HTTP 207.");
        XElement[] tokenElements = root.Elements(WebDavXml.Dav + "sync-token").ToArray();
        if (tokenElements.Length != 1 || tokenElements[0].HasElements)
            throw new InvalidDataException("A sync-collection response must contain exactly one scalar sync-token.");
        try
        {
            SyncCollectionParameters.ValidateToken(tokenElements[0].Value, allowEmpty: false);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException("The server returned an invalid synchronization token.", exception);
        }

        XElement[] elements = root.Elements(WebDavXml.Dav + "response").ToArray();
        if (elements.Length != Resources.Count)
            throw new InvalidDataException("The sync response resource projection is incomplete.");
        WebDavResource[] resources = Resources.ToArray();
        HashSet<Uri> seen = [];
        List<WebDavSyncChange> changes = [];
        for (int index = 0; index < elements.Length; index++)
        {
            XElement element = elements[index];
            if (element.Elements(WebDavXml.Dav + "href").Count() != 1)
                throw new InvalidDataException("A sync member must contain exactly one href.");
            WebDavResource resource = resources[index];
            Uri resolved = ResolveMemberUri(collectionUri, resource.Uri);
            if (!seen.Add(resolved))
                throw new InvalidDataException("A member URI appeared more than once in one sync response.");
            XElement[] directStatuses = element.Elements(WebDavXml.Dav + "status").ToArray();
            XElement[] propstats = element.Elements(WebDavXml.Dav + "propstat").ToArray();
            if (directStatuses.Length > 1 || (directStatuses.Length > 0 && propstats.Length > 0))
                throw new InvalidDataException("A sync member cannot combine resource status and property status.");

            if (SameCollection(collectionUri, resolved))
            {
                // RFC 6578 把分页标志放在请求集合自身，而不是任意成员的 507 上。
                // number-of-matches-within-limits 是 SHOULD，缺少该子错误仍可按 507 正确续取。
                if (resource.StatusCode != 507 || propstats.Length != 0)
                    throw new InvalidDataException("The sync report unexpectedly included the request collection.");
                IsTruncated = true;
                TruncationResource = resource;
                continue;
            }
            ValidateScope(collectionUri, resolved, level);
            WebDavSyncChangeKind kind;
            if (resource.StatusCode == 404)
            {
                if (string.IsNullOrEmpty(requestedSyncToken))
                    throw new InvalidDataException("An initial sync report must not contain removed members.");
                kind = WebDavSyncChangeKind.Deleted;
            }
            else if (resource.StatusCode == 403
                && element.Element(WebDavXml.Dav + "error") is { } error
                && (error.Element(WebDavXml.Dav + "supported-report") is not null
                    || error.Element(WebDavXml.Dav + "sync-traversal-supported") is not null))
            {
                kind = WebDavSyncChangeKind.UnsupportedCollection;
            }
            else if (resource.StatusCode == 200 && wasMinimalResponseRequested)
            {
                // RFC 8144 §2.1 允许在省略全部 404 属性后，用资源级 200 保留该成员的存在和变更事实。
                // 只按最终实际请求偏好启用此例外；其他直接 2xx 或未经请求的 200 仍违反普通同步报告结构。
                kind = WebDavSyncChangeKind.AddedOrModified;
            }
            else if (resource.StatusCode.HasValue)
            {
                if (resource.StatusCode is >= 200 and < 300)
                    throw new InvalidDataException("A changed sync member must use propstat rather than a direct success status.");
                kind = WebDavSyncChangeKind.Failed;
            }
            else
            {
                if (propstats.Length == 0)
                    throw new InvalidDataException("A changed sync member has no property status.");
                kind = WebDavSyncChangeKind.AddedOrModified;
            }
            changes.Add(new WebDavSyncChange(resource, resolved, kind));
        }
        Changes = changes.AsReadOnly();
    }

    /// <summary>获取本次请求提交的原始同步令牌；首次列举时为 null 或空。</summary>
    public string? RequestedSyncToken
    {
        get;
    }

    /// <summary>获取本次请求是否使用空令牌开始首次完整列举。</summary>
    public bool IsInitialSync => string.IsNullOrEmpty(RequestedSyncToken);

    /// <summary>获取本页成员变更，不包括集合自身的 507 续取标记。</summary>
    public IReadOnlyCollection<WebDavSyncChange> Changes
    {
        get;
    }

    /// <summary>获取新增或修改的成员；报告本身不区分新增和修改。</summary>
    public IReadOnlyCollection<WebDavResource> UpdatedResources => Array.AsReadOnly(Changes
        .Where(change => change.Kind == WebDavSyncChangeKind.AddedOrModified).Select(change => change.Resource).ToArray());

    /// <summary>获取已移除的成员，资源级 404 在这里表示正常删除而非请求失败。</summary>
    public IReadOnlyCollection<WebDavResource> DeletedResources => Array.AsReadOnly(Changes.Where(change => change.IsDeleted)
        .Select(change => change.Resource).ToArray());

    /// <summary>获取需要另行同步或发现的不可遍历子集合。</summary>
    public IReadOnlyCollection<WebDavResource> TraversalFailures => Array.AsReadOnly(Changes
        .Where(change => change.Kind == WebDavSyncChangeKind.UnsupportedCollection).Select(change => change.Resource).ToArray());

    /// <summary>获取全部需要上层处理的资源级失败，包括不可遍历集合。</summary>
    public IReadOnlyCollection<WebDavResource> FailedResources => Array.AsReadOnly(Changes.Where(change => change.IsFailure)
        .Select(change => change.Resource).ToArray());

    /// <summary>获取服务器是否截断了本页结果，需要用本页 SyncToken 继续请求。</summary>
    public bool IsTruncated
    {
        get;
    }

    /// <summary>获取请求集合自身的 507 续取条目；未截断时为 null。</summary>
    public WebDavResource? TruncationResource
    {
        get;
    }

    /// <summary>获取服务器是否通过 DAV:valid-sync-token 明确拒绝输入令牌。</summary>
    public bool IsTokenInvalid
    {
        get;
    }

    /// <summary>获取本页是否在协议上成功且没有未处理成员失败；正常删除与截断标志不计作失败。</summary>
    public override bool IsSuccessful => StatusCode == 207 && !IsTokenInvalid && Changes.All(change => !change.IsFailure);

    /// <summary>
    /// 将成员 href 按原请求集合 URI 解析，拒绝带片段或不合法的 URI。
    /// </summary>
    /// <param name="collectionUri">报告目标集合的绝对 URI。</param>
    /// <param name="href">服务器返回的成员 URI 引用。</param>
    /// <returns>标准 URI 规则解析后的绝对成员地址。</returns>
    private static Uri ResolveMemberUri(Uri collectionUri, string? href)
    {
        return string.IsNullOrWhiteSpace(href) || !Uri.TryCreate(collectionUri, href, out Uri? resolved)
            || resolved.Fragment.Length != 0 || resolved.Scheme is not ("http" or "https")
            ? throw new InvalidDataException("The sync response contains an invalid member href.")
            : resolved;
    }

    /// <summary>
    /// 判断某个结果是否指向请求集合自身，允许集合末尾斜杠的规范化差异。
    /// </summary>
    /// <param name="collectionUri">请求的集合地址。</param>
    /// <param name="candidate">已解析的结果地址。</param>
    /// <returns>地址与查询一致且路径仅有末尾斜杠差异时为 true。</returns>
    private static bool SameCollection(Uri collectionUri, Uri candidate)
    {
        return collectionUri.GetLeftPart(UriPartial.Authority).Equals(candidate.GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase)
        && collectionUri.Query == candidate.Query
        && collectionUri.AbsolutePath.TrimEnd('/') == candidate.AbsolutePath.TrimEnd('/');
    }

    /// <summary>
    /// 校验成员 URI 没有越出报告的来源、集合路径及请求层级。
    /// </summary>
    /// <param name="collectionUri">报告目标集合。</param>
    /// <param name="candidate">待验证的成员地址。</param>
    /// <param name="level">直接成员或全部后代范围。</param>
    private static void ValidateScope(Uri collectionUri, Uri candidate, WebDavSyncLevel level)
    {
        string rootPath = collectionUri.AbsolutePath.TrimEnd('/') + "/";
        if (!collectionUri.GetLeftPart(UriPartial.Authority).Equals(candidate.GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase)
            || !candidate.AbsolutePath.StartsWith(rootPath, StringComparison.Ordinal))
            throw new InvalidDataException("The sync response contains a member outside the requested collection.");
        string remainder = candidate.AbsolutePath[rootPath.Length..].TrimEnd('/');
        if (remainder.Length == 0 || (level == WebDavSyncLevel.Children && remainder.Contains('/')))
            throw new InvalidDataException("The sync member is outside the requested synchronization level.");
    }
}

