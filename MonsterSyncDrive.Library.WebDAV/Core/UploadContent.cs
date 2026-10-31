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
using System.Buffers;
using System.Net;
using MonsterSyncDrive.WebDAV.Request;

namespace MonsterSyncDrive.WebDAV.Core;

/// <summary>
/// 将调用方的可读流转换为 PUT 正文，管理长度、进度及源流所有权。
/// </summary>
/// <remarks>可定位流记录初始位置和剩余长度，重新序列化时回到同一起点；不可定位流无法预先报告长度，HTTP/1.1 通常采用分块传输，且禁止重复发送。</remarks>
internal sealed class UploadContent : HttpContent
{
    /// <summary>
    /// 调用方传入的可读源流；默认借用，是否关闭由 LeaveOpen 决定。
    /// </summary>
    private readonly Stream _source;
    /// <summary>
    /// 本次上传的所有权、进度和取消设置；传输期间调用方不应修改。
    /// </summary>
    private readonly PutFileParameters _options;
    /// <summary>
    /// 可定位源流的初始位置；再次序列化时回到此处，null 表示源流不可定位。
    /// </summary>
    private readonly long? _start;
    /// <summary>
    /// 创建正文时记录的剩余字节数；null 表示长度未知，由 HTTP 层选择适当的传输方式。
    /// </summary>
    private readonly long? _length;

    /// <summary>不可定位源流是否已开始序列化，防止重发时上传空正文或剩余片段。</summary>
    private int _serialized;

    /// <summary>保证请求与调用方的清理路径至多关闭一次源流。</summary>
    private int _disposed;

    /// <summary>
    /// 借用可读源流并记录当前上传起点，以及可定位时的剩余长度。
    /// </summary>
    /// <param name="source">从当前位置开始读取的可读源流；是否关闭由 options.LeaveOpen 决定。</param>
    /// <param name="options">本次上传的媒体类型、取消、进度与源流所有权设置。</param>
    /// <exception cref="ArgumentNullException">source 为 null。</exception>
    /// <exception cref="ArgumentException">源流不可读。</exception>
    public UploadContent(Stream source, PutFileParameters options)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(options);
        if (!source.CanRead)
            throw new ArgumentException("Upload stream must be readable.", nameof(source));
        _source = source;
        _options = options;
        if (source.CanSeek)
        {
            // 在源流被 HTTP 层消费前保存位置与剩余长度。认证挑战等重发场景需要从同一起点重新序列化。
            _start = source.Position;
            _length = Math.Max(0, source.Length - _start.Value);
        }
    }

    /// <summary>
    /// 向 HTTP 层报告可定位源流在创建正文时记录的剩余长度。
    /// </summary>
    /// <param name="length">输出缓存的剩余字节数；长度未知时输出 0，但返回值为 false。</param>
    /// <returns>已知长度时为 true，否则为 false，由 HTTP 层选择分块等未知长度传输方式。</returns>
    protected override bool TryComputeLength(out long length) { length = _length ?? 0; return _length.HasValue; }
    /// <summary>
    /// 从源流当前位置或记录的起点流式写入 HTTP 请求正文，并报告累计发送进度。
    /// </summary>
    /// <remarks>可定位源流在每次序列化前恢复初始位置；不可定位源流重复序列化时抛出异常。缓冲区来自共享池，异常和取消也通过 finally 归还。</remarks>
    /// <param name="stream">HTTP 处理器提供的目标网络流，由调用方管理，当前方法不关闭。</param>
    /// <param name="context">框架传入的传输上下文；本实现不使用其额外信息。</param>
    /// <returns>表示本次正文复制完成的任务；进度重新从零累计。</returns>
    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
        SerializeToStreamAsync(stream, context, _options.CancellationToken);

    /// <summary>
    /// 从源流当前位置或记录的起点流式写入 HTTP 请求正文，并报告累计发送进度。
    /// </summary>
    /// <remarks>可定位源流在每次序列化前恢复初始位置；不可定位源流重复序列化时抛出异常。缓冲区来自共享池，异常和取消也通过 finally 归还。</remarks>
    /// <param name="stream">HTTP 处理器提供的目标网络流，由调用方管理，当前方法不关闭。</param>
    /// <param name="context">框架传入的传输上下文；本实现不使用其额外信息。</param>
    /// <param name="cancellationToken">取消本次正文序列化的令牌，传递给源流读取和网络写入。</param>
    /// <returns>表示本次正文复制完成的任务；进度重新从零累计。</returns>
    protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_start.HasValue)
            _source.Position = _start.Value;
        else if (Interlocked.Exchange(ref _serialized, 1) != 0)
            throw new InvalidOperationException("A non-seekable upload stream cannot be sent more than once.");
        // 每次序列化借用一个固定缓冲区，不按文件长度分配内存；不可定位流也可逐块传输。
        var buffer = ArrayPool<byte>.Shared.Rent(81920);
        try
        {
            long total = 0;
            int count;
            while ((count = await _source.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false)) != 0)
            {
                await stream.WriteAsync(buffer.AsMemory(0, count), cancellationToken).ConfigureAwait(false);
                total += count;
                // 仅在目标流写入成功后推进进度；它表示本次正文尝试的发送量，而非服务器最终提交状态。
                _options.Progress?.Report(total);
            }
        }
        finally { ArrayPool<byte>.Shared.Return(buffer); } // 取消、源读取错误和目标写入错误都必须归还缓冲区。
    }

    /// <summary>
    /// 按 LeaveOpen 设置释放源流，并释放 HttpContent 自身资源。
    /// </summary>
    /// <param name="disposing">true 表示显式释放托管资源；只有此时且 LeaveOpen 为 false 才关闭源流。</param>
    protected override void Dispose(bool disposing)
    {
        try
        {
            if (disposing && Interlocked.Exchange(ref _disposed, 1) == 0 && !_options.LeaveOpen)
                _source.Dispose();
        }
        finally { base.Dispose(disposing); }
    }
}
