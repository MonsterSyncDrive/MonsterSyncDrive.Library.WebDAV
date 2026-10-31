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
using MonsterSyncDrive.WebDAV.Request;
using MonsterSyncDrive.WebDAV.Response;

namespace MonsterSyncDrive.WebDAV;

/// <summary>提供活动锁和锁类型发现的客户端实现。</summary>
public sealed partial class WebDavClient
{
    /// <summary>一次查询指定资源的活动锁及服务器支持的锁模式和类型。</summary>
    /// <param name="requestUri">需要查询的绝对或相对资源 URI。</param>
    /// <param name="options">可选版本条件、DAV If、Prefer、请求头及取消令牌。</param>
    /// <returns>包含活动锁、支持锁类型及原始属性状态的查询结果。</returns>
    /// <exception cref="InvalidDataException">成功的 Depth:0 查询没有返回恰好一个目标资源。</exception>
    public async Task<LockDiscoveryResponse> GetLockDiscoveryAsync(string requestUri, WebDavRequestOptions? options = null)
    {
        options ??= new WebDavRequestOptions();
        var parameters = new PropfindParameters
        {
            ApplyTo = ApplyTo.Propfind.ResourceOnly,
            RequestType = PropfindRequestType.NamedProperties,
            CustomProperties = [WebDavXml.Dav + "lockdiscovery", WebDavXml.Dav + "supportedlock"]
        };
        options.CopyTo(parameters);
        var response = await Propfind(requestUri, parameters).ConfigureAwait(false);
        // 多个资源或成功空集合意味着服务器没有遵守单资源查询语义，不能据此误判资源“无锁”。
        if (response.IsSuccessful && response.Resources.Count != 1 || response.Resources.Count > 1)
            throw new InvalidDataException("A Depth:0 lock discovery must identify one resource.");
        return new LockDiscoveryResponse(response);
    }
}

/// <summary>定义活动锁及支持锁类型发现接口。</summary>
public partial interface IWebDavClient
{
    /// <summary>一次查询资源的活动锁及服务器支持的锁类型。</summary>
    /// <param name="requestUri">目标资源 URI。</param>
    /// <param name="options">通用 HTTP 条件、请求头及取消设置。</param>
    /// <returns>完整的锁发现结果。</returns>
    Task<LockDiscoveryResponse> GetLockDiscoveryAsync(string requestUri, WebDavRequestOptions? options = null);
}

