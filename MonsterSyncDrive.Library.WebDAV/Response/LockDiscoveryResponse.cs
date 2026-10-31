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

namespace MonsterSyncDrive.Library.WebDAV.Response;

/// <summary>表示一次同时查询活动锁和支持锁类型的单资源结果。</summary>
/// <remarks>属性的 404/403 状态保留在 Resource.PropertyStatuses，不能把缺少锁能力属性当成服务器支持所有锁。</remarks>
public sealed class LockDiscoveryResponse : PropfindResponse
{
    /// <summary>从已完整读取的 Depth:0 属性响应建立锁发现结果。</summary>
    /// <param name="response">原始已物化的属性响应。</param>
    internal LockDiscoveryResponse(PropfindResponse response)
        : base(response.StatusCode, response.Description, response.Resources)
    {
        Headers = response.Headers;
        Error = response.Error;
    }

    /// <summary>获取服务器返回的目标资源，HTTP 失败且没有多状态结果时为 null。</summary>
    public WebDavResource? Resource => Resources.SingleOrDefault();

    /// <summary>获取目标资源已报告的活动锁，没有成功活动锁属性时为空集合。</summary>
    public IReadOnlyCollection<ActiveLock> ActiveLocks => Resource?.ActiveLocks ?? Array.Empty<ActiveLock>();

    /// <summary>获取服务器声明支持的锁模式与锁类型。</summary>
    public IReadOnlyCollection<SupportedLock> SupportedLocks => Resource?.SupportedLocks ?? Array.Empty<SupportedLock>();

    /// <summary>获取活动锁及能力属性各自的状态，便于区分无锁与未授权读取。</summary>
    public IReadOnlyCollection<WebDavPropertyStatus> PropertyStatuses => Resource?.PropertyStatuses ?? Array.Empty<WebDavPropertyStatus>();
}

