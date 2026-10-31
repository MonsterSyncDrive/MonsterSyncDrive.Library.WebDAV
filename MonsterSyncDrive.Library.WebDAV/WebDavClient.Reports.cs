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
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using MonsterSyncDrive.WebDAV.Core;
using MonsterSyncDrive.WebDAV.Request;
using MonsterSyncDrive.WebDAV.Response;

namespace MonsterSyncDrive.WebDAV;

/// <summary>
/// 提供通用完整 XML 报告和基于 RFC 6578 同步令牌的集合变更操作。
/// </summary>
public sealed partial class WebDavClient
{
    /// <summary>
    /// 发送通用 XML REPORT，保留整个 XML 根而不丢弃根级扩展与非多状态结果。
    /// </summary>
    /// <param name="requestUri">报告资源的标准绝对或相对 URI 引用。</param>
    /// <param name="report">完整 XML 报告请求根，不应为 null。</param>
    /// <param name="options">请求条件、取消和自定义头；null 使用默认设置。</param>
    /// <returns>完整 XML 报告、可选资源投影及响应头快照。</returns>
    public Task<WebDavReportResponse> ReportDocumentAsync(string requestUri, XElement report,
        WebDavRequestOptions? options = null) => ReportDocumentAsync(CreateUri(requestUri), report, options);

    /// <summary>
    /// 发送通用 XML REPORT，支持任意 XML 结果根并明确保留错误正文状态。
    /// </summary>
    /// <param name="requestUri">绝对或相对报告资源 URI。</param>
    /// <param name="report">请求报告的完整 XML 元素。</param>
    /// <param name="options">通用请求设置；null 表示默认配置。</param>
    /// <returns>完整报告响应，网络正文已读取并释放。</returns>
    /// <exception cref="InvalidDataException">成功 XML 报告没有 XML 正文，或 HTTP 207 不对应 DAV:multistatus。</exception>
    public async Task<WebDavReportResponse> ReportDocumentAsync(Uri requestUri, XElement report,
        WebDavRequestOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(report);
        var response = await SendDocumentRequestAsync(requestUri, "REPORT", report, options).ConfigureAwait(false);
        if (response.StatusCode is >= 200 and < 300 and not 204 && response.Document is null)
            throw new InvalidDataException("A successful XML REPORT requires an XML response body.");
        return new WebDavReportResponse(response.StatusCode, response.Description, response.Document, response.Resources)
        {
            Headers = response.Headers,
            Error = response.Error,
            Representation = response.Representation
        };
    }

    /// <summary>
    /// 使用指定令牌请求一次集合增量同步页面，正常删除不会被解释为 HTTP 失败。
    /// </summary>
    /// <param name="requestUri">要同步的集合 URI 引用。</param>
    /// <param name="parameters">令牌、层级、属性和结果限制；null 进行默认首次列举。</param>
    /// <returns>单页同步响应；令牌失效以 IsTokenInvalid 表达，不自动全量回退。</returns>
    public Task<WebDavSyncResponse> SyncCollectionAsync(string requestUri, SyncCollectionParameters? parameters = null) =>
        SyncCollectionAsync(CreateUri(requestUri), parameters);

    /// <summary>
    /// 使用绝对或相对 URI 执行一次 RFC 6578 集合报告。
    /// </summary>
    /// <param name="requestUri">要同步的集合 URI。</param>
    /// <param name="parameters">同步设置；null 表示空令牌、直接成员和默认属性。</param>
    /// <returns>已经校验文档、令牌、成员唯一性及分页标志的响应。</returns>
    public async Task<WebDavSyncResponse> SyncCollectionAsync(Uri requestUri, SyncCollectionParameters? parameters = null)
    {
        parameters ??= new SyncCollectionParameters();
        var body = parameters.ToReportElement();
        var response = await SendDocumentRequestAsync(requestUri, "REPORT", body, parameters,
            [new("Depth", "0")]).ConfigureAwait(false);
        var report = new WebDavReportResponse(response.StatusCode, response.Description, response.Document, response.Resources)
        {
            Headers = response.Headers,
            Error = response.Error,
            Representation = response.Representation
        };
        var page = new WebDavSyncResponse(report, ResolveUri(requestUri), parameters.Level, parameters.SyncToken,
            response.WasMinimalResponseRequested);
        if (page.StatusCode == 207 && parameters.Limit is { } limit && page.Changes.Count > limit)
            throw new InvalidDataException("The server returned more synchronization changes than the requested limit.");
        return page;
    }

    /// <summary>
    /// 按 RFC 6578 的返回令牌自动续取集合页面，支持取消、重复令牌与最大页数保护。
    /// </summary>
    /// <param name="requestUri">同步集合的 URI 引用。</param>
    /// <param name="parameters">起始令牌、属性、层级和分页保护设置。</param>
    /// <param name="cancellationToken">枚举器级取消令牌，与参数中的请求令牌同时生效。</param>
    /// <returns>完整解析的一页页同步结果；必须完整枚举后才能确认全部续取完成。</returns>
    /// <exception cref="WebDavSyncTokenInvalidException">服务器拒绝旧同步令牌。</exception>
    /// <exception cref="InvalidDataException">服务器重复续取令牌、页面不合法或超过页数限制。</exception>
    public async IAsyncEnumerable<WebDavSyncResponse> EnumerateSyncPagesAsync(string requestUri,
        SyncCollectionParameters? parameters = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        parameters ??= new SyncCollectionParameters();
        var uri = CreateUri(requestUri);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(parameters.CancellationToken, cancellationToken);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(parameters.MaxPages);
        var maxPages = parameters.MaxPages;
        var token = parameters.SyncToken;
        HashSet<string> seen = new(StringComparer.Ordinal);
        if (!string.IsNullOrEmpty(token))
            seen.Add(token);

        for (var index = 0; index < maxPages; index++)
        {
            linked.Token.ThrowIfCancellationRequested();
            var page = await SyncCollectionAsync(uri, parameters.ForPage(token, linked.Token)).ConfigureAwait(false);
            if (page.IsTokenInvalid)
                throw new WebDavSyncTokenInvalidException(page);
            if (page.StatusCode != 207)
            {
                page.EnsureSuccessStatusCode();
                throw new InvalidDataException("A sync report did not return HTTP 207.");
            }

            // 只有真正截断时才要求令牌前进；没有变更的最终页可以合法返回与输入相同的令牌。
            // 在产出页面前检查循环，避免调用方把无进展页误当作可持续提交的新检查点。
            if (page.IsTruncated && !seen.Add(page.SyncToken!))
                throw new InvalidDataException("The server repeated a synchronization continuation token.");
            yield return page;
            if (!page.IsTruncated)
                yield break;
            token = page.SyncToken;
        }
        throw new InvalidDataException("The synchronization exceeded the configured maximum page count.");
    }

    /// <summary>
    /// 自动收集全部增量同步页面，按成员 URI 保留最后结果并返回最终令牌。
    /// </summary>
    /// <param name="requestUri">同步集合 URI 引用。</param>
    /// <param name="parameters">同步请求与分页保护配置。</param>
    /// <param name="cancellationToken">覆盖整个分页聚合的附加取消令牌。</param>
    /// <returns>全部续取完成后的变更集合；成员级失败仍保留并使 IsSuccessful 为 false。</returns>
    public async Task<WebDavSyncResult> SynchronizeCollectionAsync(string requestUri,
        SyncCollectionParameters? parameters = null, CancellationToken cancellationToken = default)
    {
        parameters ??= new SyncCollectionParameters();
        Dictionary<Uri, WebDavSyncChange> latest = [];
        WebDavSyncResponse? last = null;
        var pageCount = 0;
        await foreach (var page in EnumerateSyncPagesAsync(requestUri, parameters, cancellationToken).ConfigureAwait(false))
        {
            pageCount++;
            last = page;
            // 各页请求之间服务器仍可继续变化，同一成员可再次出现；最后页面中的状态覆盖先前状态。
            // 删除集合意味着其本地后代应由宿主根据既有目录快照一起移除，不能只依赖本批是否列出后代。
            foreach (var change in page.Changes)
                latest[change.ResolvedUri] = change;
        }
        return new WebDavSyncResult(last ?? throw new InvalidDataException("No synchronization page was returned."),
            latest.Values, pageCount, parameters.SyncToken);
    }
}

/// <summary>
/// 定义完整 XML 报告及集合增量同步的客户端操作。
/// </summary>
public partial interface IWebDavClient
{
    /// <summary>发送 XML 报告并保留完整根元素与根级扩展。</summary>
    /// <param name="requestUri">标准 URI 引用。</param>
    /// <param name="report">完整报告请求根。</param>
    /// <param name="options">可选通用请求设置。</param>
    /// <returns>完整 XML 报告响应。</returns>
    Task<WebDavReportResponse> ReportDocumentAsync(string requestUri, XElement report, WebDavRequestOptions? options = null);

    /// <summary>使用 Uri 发送 XML 报告并保留完整根元素。</summary>
    /// <param name="requestUri">报告资源 URI。</param>
    /// <param name="report">完整报告请求根。</param>
    /// <param name="options">可选通用请求设置。</param>
    /// <returns>完整 XML 报告响应。</returns>
    Task<WebDavReportResponse> ReportDocumentAsync(Uri requestUri, XElement report, WebDavRequestOptions? options = null);

    /// <summary>请求一次集合初始或增量同步页面。</summary>
    /// <param name="requestUri">集合 URI 引用。</param>
    /// <param name="parameters">可选同步请求参数。</param>
    /// <returns>包含正常删除与续取标志的同步页面。</returns>
    Task<WebDavSyncResponse> SyncCollectionAsync(string requestUri, SyncCollectionParameters? parameters = null);

    /// <summary>使用 Uri 请求一次集合同步页面。</summary>
    /// <param name="requestUri">集合地址。</param>
    /// <param name="parameters">可选同步参数。</param>
    /// <returns>单页同步响应。</returns>
    Task<WebDavSyncResponse> SyncCollectionAsync(Uri requestUri, SyncCollectionParameters? parameters = null);

    /// <summary>按服务器返回的令牌自动枚举全部同步页面。</summary>
    /// <param name="requestUri">集合 URI 引用。</param>
    /// <param name="parameters">同步和分页保护设置。</param>
    /// <param name="cancellationToken">整个枚举过程的取消令牌。</param>
    /// <returns>完整同步页面的异步序列。</returns>
    IAsyncEnumerable<WebDavSyncResponse> EnumerateSyncPagesAsync(string requestUri, SyncCollectionParameters? parameters = null,
        CancellationToken cancellationToken = default);

    /// <summary>自动续取并合并整个同步批次，返回最终不透明令牌。</summary>
    /// <param name="requestUri">集合 URI 引用。</param>
    /// <param name="parameters">同步与分页设置。</param>
    /// <param name="cancellationToken">整个操作的取消令牌。</param>
    /// <returns>保留最终每个成员状态的同步结果。</returns>
    Task<WebDavSyncResult> SynchronizeCollectionAsync(string requestUri, SyncCollectionParameters? parameters = null,
        CancellationToken cancellationToken = default);
}

