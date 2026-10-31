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
namespace MonsterSyncDrive.WebDAV.Response;

/// <summary>
/// 管理 GET 下载流、响应头快照及原始 HTTP 响应的所有权。
/// </summary>
/// <remarks>关闭结果或公开的 Stream 都会归还网络资源；读取完成后也应释放，结束计时和取消注册。</remarks>
public class WebDavStreamResponse : WebDavResponse, IDisposable, IAsyncDisposable
{
    /// <summary>
    /// 接管原始下载响应与正文流，并保存响应头快照。
    /// </summary>
    /// <remarks>公开的 Stream 再包装一层释放回调，使调用方只关闭流也能释放整个 HTTP 响应。</remarks>
    /// <param name="response">下载结果接管所有权的原始 HTTP 响应。</param>
    /// <param name="stream">HTTP 正文的下载流；关闭包装流时也会释放原始响应。</param>
    public WebDavStreamResponse(HttpResponseMessage response, Stream stream)
        : base((int)response.StatusCode, response.ReasonPhrase)
    {
        ArgumentNullException.ThrowIfNull(stream);
        CaptureHeaders<WebDavStreamResponse>(response);
        Stream = new MonsterSyncDrive.WebDAV.Core.WebDavReadStream(stream, CancellationToken.None, response.Dispose);
    }

    /// <summary>
    /// 获取只读下载流，关闭此流也会释放其 HTTP 响应。
    /// </summary>
    /// <value>响应支持 using 或 await using；网络流通常不可定位，不应假定支持 Length 或 Seek。</value>
    public Stream Stream { get; }

    /// <summary>
    /// 获取响应头声明的正文长度，单位为字节。
    /// </summary>
    /// <value>未提供或不能解析时为 null；206 时通常表示本段长度，不是整个文件长度。</value>
    public long? ContentLength => long.TryParse(GetHeader("Content-Length"), System.Globalization.NumberStyles.None,
        System.Globalization.CultureInfo.InvariantCulture, out var value) ? value : null;
    /// <summary>
    /// 获取响应正文的 Content-Type 头文本。
    /// </summary>
    /// <value>未提供时为 null，保留服务器的媒体类型参数。</value>
    public string? ContentType => GetHeader("Content-Type");
    /// <summary>
    /// 获取解析后的 Content-Range 正文范围信息。
    /// </summary>
    /// <value>未提供或无法解析时为 null；支持读取本段起止位置和服务器声明的总长度。</value>
    public System.Net.Http.Headers.ContentRangeHeaderValue? ContentRange =>
        System.Net.Http.Headers.ContentRangeHeaderValue.TryParse(GetHeader("Content-Range"), out var value) ? value : null;

    /// <summary>
    /// 返回当前WebDavStreamResponse的状态或属性概要，便于日志记录和调试。
    /// </summary>
    /// <returns>包含当前对象关键字段的描述字符串，不触发网络请求。</returns>
    public override string ToString() => $"WebDAV stream response - StatusCode: {StatusCode}, Description: {Description}";

    /// <summary>
    /// 关闭下载流及原始 HTTP 响应，归还连接和关联的请求生命周期资源。
    /// </summary>
    /// <remarks>公开的流自身也有响应释放回调，因此关闭流和关闭结果可组合使用；底层生命周期包装器负责幂等清理。</remarks>
    public void Dispose()
    {
        Stream.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 通过现有同步清理路径释放下载结果和网络资源。
    /// </summary>
    /// <returns>已经完成的 ValueTask，返回前资源已执行释放。</returns>
    public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
}
