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
namespace MonsterSyncDrive.WebDAV.Request;

/// <summary>
/// 配置 GET 的字节范围、If-Range 及 HTTP 缓存或版本条件。
/// </summary>
/// <remarks>服务器可忽略 Range 返回完整 200 正文；调用方必须根据 StatusCode 区分完整响应与 206 分段响应。</remarks>
public class GetFileParameters : WebDavRequestOptions
{
    /// <summary>
    /// 获取或设置 GET 请求的字节范围，可表示闭合、开放或后缀范围。
    /// </summary>
    /// <value>默认 null 请求完整内容；主要支持单段字节范围，服务器可合法返回完整 200 响应。</value>
    public System.Net.Http.Headers.RangeHeaderValue? Range { get; set; }
    /// <summary>
    /// 获取或设置分段下载使用的 ETag 或日期校验条件。
    /// </summary>
    /// <value>默认 null；条件不匹配时服务器可以返回完整正文，调用方需检查实际状态。</value>
    public System.Net.Http.Headers.RangeConditionHeaderValue? IfRange { get; set; }
}
