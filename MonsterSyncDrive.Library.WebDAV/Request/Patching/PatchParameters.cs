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

namespace MonsterSyncDrive.WebDAV.Request;

/// <summary>配置 RFC 5789 PATCH 的补丁媒体类型、HTTP 条件、锁和源流所有权。</summary>
/// <remarks>PATCH 不规定统一的补丁格式，媒体类型必须与服务器 Accept-Patch 声明及实际补丁格式一致。</remarks>
public class PatchParameters : PutFileParameters
{
    /// <summary>创建具有明确补丁格式的 PATCH 参数。</summary>
    /// <param name="contentType">合法补丁媒体类型，例如 application/json-patch+json。</param>
    public PatchParameters(string contentType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        ContentType = MediaTypeHeaderValue.Parse(contentType);
    }
}

/// <summary>配置 SabreDAV 私有格式的文件区间覆盖或追加。</summary>
/// <remarks>该格式使用 RFC 5789 PATCH 作为传输方法，但 X-Update-Range 及补丁媒体类型不是通用 WebDAV 标准。</remarks>
public sealed class SabreDavPartialUpdateParameters : PatchParameters
{
    /// <summary>创建采用 SabreDAV partialupdate 媒体类型的参数。</summary>
    public SabreDavPartialUpdateParameters() : base("application/x-sabredav-partialupdate") { }

    /// <summary>获取或设置是否将内容追加到文件末尾；true 时必须省略 Offset 和 End。</summary>
    public bool Append { get; set; }

    /// <summary>获取或设置起始偏移；负数表示相对文件末尾，闭合区间不得使用负起点。</summary>
    public long? Offset { get; set; }

    /// <summary>获取或设置包含式结束偏移；null 表示从起点写入正文长度对应的字节。</summary>
    public long? End { get; set; }

    /// <summary>获取或设置不可定位源流的声明补丁长度；可计算正文长度时必须与其一致。</summary>
    public long? ContentLength { get; set; }

    /// <summary>验证区间和正文长度，并生成 X-Update-Range。</summary>
    /// <param name="length">已知且非负的补丁正文字节数，SabreDAV 要求 Content-Length。</param>
    /// <returns>append、bytes=start- 或 bytes=start-end。</returns>
    internal string CreateRange(long length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        if (Append)
        {
            if (Offset.HasValue || End.HasValue) throw new ArgumentException("Append must not specify a byte interval.");
            return "append";
        }
        if (!Offset.HasValue) throw new ArgumentException("A byte offset is required for partial overwrite.", nameof(Offset));
        if (End.HasValue && (Offset < 0 || End < Offset)) throw new ArgumentOutOfRangeException(nameof(End));
        if (End.HasValue && (End.Value - Offset.Value == long.MaxValue || End.Value - Offset.Value + 1 != length))
            throw new ArgumentException("Patch length must equal the inclusive interval size.");
        return "bytes=" + Offset.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) + "-"
            + (End?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "");
    }
}
