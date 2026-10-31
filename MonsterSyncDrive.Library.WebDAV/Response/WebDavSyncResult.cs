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
using MonsterSyncDrive.WebDAV.Core;

namespace MonsterSyncDrive.WebDAV.Response;

/// <summary>
/// 保存自动续取结束后的集合变更集合和最终同步令牌。
/// </summary>
/// <remarks>同一 URL 若在多次请求之间再次变化，聚合保留最后一次结果；不会据此自动修改任何本地文件或旧快照。</remarks>
public sealed class WebDavSyncResult : WebDavResponse
{
    /// <summary>
    /// 构造已经取完全部分页的同步结果。
    /// </summary>
    /// <param name="lastPage">最后一页，必须具有可提交的非空同步令牌。</param>
    /// <param name="changes">按成员 URL 合并后的最终变更集合。</param>
    /// <param name="pageCount">实际执行的报告请求数。</param>
    /// <param name="requestedSyncToken">最初提交的令牌；空值表示从完整列举开始。</param>
    internal WebDavSyncResult(WebDavSyncResponse lastPage, IEnumerable<WebDavSyncChange> changes,
        int pageCount, string? requestedSyncToken) : base(lastPage.StatusCode, lastPage.Description)
    {
        SyncToken = lastPage.SyncToken ?? throw new InvalidDataException("The completed synchronization has no token.");
        Changes = Array.AsReadOnly(changes.ToArray());
        PageCount = pageCount;
        RequestedSyncToken = requestedSyncToken;
        Headers = lastPage.Headers;
        Error = lastPage.Error;
    }

    /// <summary>获取全部分页完成后服务器返回的最终不透明同步令牌。</summary>
    public string SyncToken { get; }

    /// <summary>获取最初请求的令牌；null 或空值表示首次完整列举。</summary>
    public string? RequestedSyncToken { get; }

    /// <summary>获取本次聚合是否从空令牌开始。</summary>
    public bool IsInitialSync => string.IsNullOrEmpty(RequestedSyncToken);

    /// <summary>获取实际完成的报告页面数。</summary>
    public int PageCount { get; }

    /// <summary>获取按成员 URL 合并后保留的最后一次变更。</summary>
    public IReadOnlyCollection<WebDavSyncChange> Changes { get; }

    /// <summary>获取新增或发生修改的资源结果。</summary>
    public IReadOnlyCollection<WebDavResource> UpdatedResources => Array.AsReadOnly(Changes
        .Where(change => change.Kind == WebDavSyncChangeKind.AddedOrModified).Select(change => change.Resource).ToArray());

    /// <summary>获取正常删除的成员记录；已知删除对象为集合时，宿主还应移除其本地后代。</summary>
    public IReadOnlyCollection<WebDavResource> DeletedResources => Array.AsReadOnly(Changes.Where(change => change.IsDeleted)
        .Select(change => change.Resource).ToArray());

    /// <summary>获取不能随父集合遍历、需要宿主单独处理的子集合。</summary>
    public IReadOnlyCollection<WebDavResource> TraversalFailures => Array.AsReadOnly(Changes
        .Where(change => change.Kind == WebDavSyncChangeKind.UnsupportedCollection).Select(change => change.Resource).ToArray());

    /// <summary>获取失败的成员结果，包括不可遍历子集合。</summary>
    public IReadOnlyCollection<WebDavResource> FailedResources => Array.AsReadOnly(Changes.Where(change => change.IsFailure)
        .Select(change => change.Resource).ToArray());

    /// <summary>获取全部分页是否完成且所有成员均为正常变更或删除；未处理失败使结果为 false。</summary>
    public override bool IsSuccessful => StatusCode is >= 200 and < 300 && Changes.All(change => !change.IsFailure);
}

