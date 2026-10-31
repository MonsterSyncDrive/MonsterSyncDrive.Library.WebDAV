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
using MonsterSyncDrive.Library.WebDAV.Request;
using MonsterSyncDrive.Library.WebDAV.Response;
using System.Net;
using System.Net.Http.Headers;

namespace MonsterSyncDrive.WebDAV;

/// <summary>提供单次 GET 请求读取多个不连续字节范围的客户端操作。</summary>
public sealed partial class WebDavClient
{
    /// <summary>异步请求多个字节区间，保留流式 MIME 分段、单段合并及完整正文回退结果。</summary>
    /// <param name="requestUri">绝对或相对的资源 URI 引用；普通路径应先按 URI 路径段编码。</param>
    /// <param name="parameters">含非空 bytes Range 的 GET 参数；可同时提供 If-Range、版本条件和取消令牌。</param>
    /// <returns>拥有原始 HTTP 响应的范围结果，调用方必须释放，并按顺序枚举其分段。</returns>
    /// <remarks>服务器可以合并相邻区间或忽略 Range 返回 200。根据返回的实际 ContentRange 处理各段，而不是假设响应与请求一一对应。</remarks>
    public Task<WebDavRangeResponse> GetRangesAsync(string requestUri, GetFileParameters parameters)
    {
        return GetRangesAsync(CreateUri(requestUri), parameters);
    }

    /// <summary>异步发送多范围 GET，校验响应类型后返回无需整文件缓冲的下载结果。</summary>
    /// <param name="requestUri">请求资源的 URI，相对 URI 需要配置 BaseAddress。</param>
    /// <param name="parameters">字节范围、版本条件和取消设置；Range 至少包含一个区间。</param>
    /// <returns>可使用 using 或 await using 管理的范围响应。</returns>
    /// <exception cref="ArgumentException">最终 Range 不是有效的非空 bytes 范围。</exception>
    /// <exception cref="InvalidDataException">206 的媒体类型、边界、范围或编码不合法。</exception>
    public async Task<WebDavRangeResponse> GetRangesAsync(Uri requestUri, GetFileParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        using HttpRequestMessage request = CreateRequest(requestUri, "GET");
        request.Headers.Add("Translate", "f");
        request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("identity"));
        request.Headers.Range = parameters.Range;
        request.Headers.IfRange = parameters.IfRange;
        parameters.Apply(request);
        RangeHeaderValue? requested = request.Headers.Range;
        if (requested is null || !requested.Unit.Equals("bytes", StringComparison.OrdinalIgnoreCase) || requested.Ranges.Count == 0)
            throw new ArgumentException("A non-empty bytes Range is required.", nameof(parameters));
        // 复制最终请求头，避免自定义头覆盖后仍用旧参数校验，以及调用方之后修改集合影响正在消费的结果。
        RangeHeaderValue snapshot = RangeHeaderValue.Parse(requested.ToString());
        HttpResponseMessage response = await SendAsync(request, parameters.CancellationToken).ConfigureAwait(false);
        try
        {
            return response.StatusCode is HttpStatusCode.OK or HttpStatusCode.PartialContent
                && response.Content.Headers.ContentEncoding.Any(value => !value.Equals("identity", StringComparison.OrdinalIgnoreCase))
                ? throw new InvalidDataException("A byte-range response must contain identity-encoded bytes.")
                : new WebDavRangeResponse(response, snapshot, parameters.CancellationToken);
        }
        catch
        {
            response.Dispose();
            throw;
        }
    }
}

/// <summary>定义多范围流式下载接口；标准单范围 GET 接口的行为保持独立。</summary>
public partial interface IWebDavClient
{
    /// <summary>请求资源的一组字节范围，服务器可返回 multipart、合并后的单段或完整正文。</summary>
    /// <param name="requestUri">请求资源的 URI 引用。</param>
    /// <param name="parameters">包含非空 bytes Range 的下载参数。</param>
    /// <returns>由调用方释放并顺序读取分段的范围响应。</returns>
    Task<WebDavRangeResponse> GetRangesAsync(string requestUri, GetFileParameters parameters);

    /// <summary>使用 Uri 请求资源的一组字节范围。</summary>
    /// <param name="requestUri">请求资源的绝对或相对 URI。</param>
    /// <param name="parameters">包含非空 bytes Range 的下载参数。</param>
    /// <returns>由调用方拥有的范围响应。</returns>
    Task<WebDavRangeResponse> GetRangesAsync(Uri requestUri, GetFileParameters parameters);
}

