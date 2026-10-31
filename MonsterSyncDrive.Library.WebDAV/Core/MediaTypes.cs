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
using System.Net.Http.Headers;
using System.Text;

namespace MonsterSyncDrive.Library.WebDAV.Core;

/// <summary>
/// 为请求参数创建独立的标准 HTTP 正文媒体类型实例。
/// </summary>
/// <remarks>每次访问返回新对象，避免一个请求修改字符集或媒体类型后影响其他请求。</remarks>
internal static class MediaTypes
{
    /// <summary>
    /// 获取新创建的 application/xml; charset=utf-8 媒体类型。
    /// </summary>
    /// <value>每次调用均为独立对象，可安全用于单个请求参数。</value>
    internal static MediaTypeHeaderValue XmlMediaType => new("application/xml")
    {
        CharSet = Encoding.UTF8.WebName
    };

    /// <summary>
    /// 获取新创建的 application/octet-stream 媒体类型。
    /// </summary>
    /// <value>每次调用均为独立对象，避免请求间共享可变状态。</value>
    internal static MediaTypeHeaderValue BinaryDataMediaType => new("application/octet-stream");
}
