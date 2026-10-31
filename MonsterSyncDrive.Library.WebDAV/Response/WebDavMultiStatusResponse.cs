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

/// <summary>
/// 保存 DELETE、COPY、MOVE 等修改操作的多资源响应。
/// </summary>
/// <remarks>普通成功响应的 Resources 为空；HTTP 207 保留内部资源或属性错误，避免仅凭 HTTP 2xx 误判成功。</remarks>
/// <param name="statusCode">修改操作的 HTTP 状态码，可能为 207。</param>
/// <param name="description">可选的 HTTP 状态说明。</param>
/// <param name="resources">已经收集的逐资源结果；此构造函数保存集合引用，不进行深复制。</param>
public class WebDavMultiStatusResponse(int statusCode, string? description, IReadOnlyCollection<WebDavResource> resources)
    : WebDavResponse(statusCode, description)
{
    /// <summary>
    /// 获取修改操作返回的逐资源结果。
    /// </summary>
    /// <value>普通非 207 响应时为空集合；207 中保存服务器返回的全部资源条目。</value>
    public IReadOnlyCollection<WebDavResource> Resources { get; } = resources;
    /// <summary>
    /// 获取 HTTP 为 2xx 且所有内部资源及属性状态均成功的结果。
    /// </summary>
    public override bool IsSuccessful => base.IsSuccessful && Resources.All(resource =>
        (resource.StatusCode is null or (>= 200 and < 300)) && resource.PropertyStatuses.All(status => status.IsSuccessful));
}
