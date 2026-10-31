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
using System.Net;
using MonsterSyncDrive.WebDAV.Response;

namespace MonsterSyncDrive.WebDAV.Core;

/// <summary>
/// 表示服务器明确拒绝集合旧同步令牌，调用方必须自行决定何时重新建立完整快照。
/// </summary>
/// <remarks>分页接口不会自动清空旧令牌或重试全量同步，以避免把不完整的替代快照误提交给同步引擎。</remarks>
public sealed class WebDavSyncTokenInvalidException : HttpRequestException
{
    /// <summary>
    /// 使用服务器的失效令牌响应创建异常，并保留全部协议错误信息。
    /// </summary>
    /// <param name="response">包含 DAV:valid-sync-token 错误的同步响应。</param>
    public WebDavSyncTokenInvalidException(WebDavSyncResponse response)
        : base("The server rejected the synchronization token; a new complete synchronization must be explicitly requested.",
            null, (HttpStatusCode?)response?.StatusCode)
    {
        ArgumentNullException.ThrowIfNull(response);
        Response = response;
    }

    /// <summary>获取服务器的原始同步失败快照，包括被拒绝令牌、XML、状态和 DAV 错误详情。</summary>
    public WebDavSyncResponse Response { get; }
}

