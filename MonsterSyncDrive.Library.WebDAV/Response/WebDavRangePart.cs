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

namespace MonsterSyncDrive.WebDAV.Response;

/// <summary>描述多范围响应中的一个实际分段，或服务器忽略 Range 后返回的完整正文。</summary>
/// <remarks>分段流只在当前枚举位置有效；推进至下一段时自动排空未读部分并关闭旧段，不能并发消费不同分段。</remarks>
public sealed class WebDavRangePart
{
    /// <summary>保存分段元数据及当前段的只读流，不接管整个响应的所有权。</summary>
    /// <param name="range">本段 Content-Range；完整 200 正文时为 null。</param>
    /// <param name="headers">本段的头快照，键比较不区分大小写。</param>
    /// <param name="stream">只能读取本段内容的流。</param>
    internal WebDavRangePart(ContentRangeHeaderValue? range, IReadOnlyDictionary<string, string[]> headers, Stream stream)
    {
        ContentRange = range;
        Headers = headers;
        Stream = stream;
    }

    /// <summary>获取实际返回的字节区间，终点包含在区间内；完整正文回退时为 null。</summary>
    public ContentRangeHeaderValue? ContentRange { get; }

    /// <summary>获取分段头快照；非 multipart 结果使用外层正文头。</summary>
    public IReadOnlyDictionary<string, string[]> Headers { get; }

    /// <summary>获取当前分段的只读流，不能越过本段读取下一段的 MIME 头。</summary>
    public Stream Stream { get; }

    /// <summary>获取本段声明的正文媒体类型，未声明时为 null。</summary>
    public string? ContentType => Headers.TryGetValue("Content-Type", out var values) ? string.Join(", ", values) : null;

    /// <summary>获取由 Content-Range 推导的本段长度，完整正文则读取其 Content-Length。</summary>
    public long? ContentLength => ContentRange?.HasRange == true ? ContentRange.To!.Value - ContentRange.From!.Value + 1
        : Headers.TryGetValue("Content-Length", out var values) && long.TryParse(values.SingleOrDefault(),
            System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var length) ? length : null;
}

