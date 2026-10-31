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

namespace MonsterSyncDrive.WebDAV.Core;

/// <summary>只允许读取当前 MIME 分段的字节，不关闭共享的下一分段网络流。</summary>
/// <remarks>用户关闭本段后，枚举器仍可排空其未读部分并进入下一段；枚举器前进后旧段不可再读取。</remarks>
internal sealed class ByteRangePartStream : Stream
{
    /// <summary>同时供头部和正文消费的缓冲读取器。</summary>
    private readonly MultipartByteRangeReader _reader;
    /// <summary>本段固定长度；完整 200 正文没有声明长度时为 null。</summary>
    private readonly long? _length;
    /// <summary>当前段尚未读取的字节数，未知长度时为 null。</summary>
    private long? _remaining;
    /// <summary>当前分段已经消费的字节数。</summary>
    private long _position;
    /// <summary>该响应枚举及请求生命周期的取消令牌。</summary>
    private readonly CancellationToken _token;
    /// <summary>单次读取被取消时关闭整个响应的回调，以中断不接受令牌的底层读取。</summary>
    private readonly Action _abort;
    /// <summary>公开流是否已关闭，关闭不会释放共享读取器。</summary>
    private bool _disposed;
    /// <summary>未知长度正文是否已经读到末尾。</summary>
    private bool _ended;

    /// <summary>构造固定区间流或完整正文回退流。</summary>
    /// <param name="reader">共享正文读取器。</param>
    /// <param name="length">本段精确长度；null 代表未知长度的完整正文。</param>
    /// <param name="token">请求及枚举生命周期令牌。</param>
    /// <param name="abort">取消时释放外层响应的回调。</param>
    public ByteRangePartStream(MultipartByteRangeReader reader, long? length, CancellationToken token, Action abort)
    {
        _reader = reader;
        _length = _remaining = length;
        _token = token;
        _abort = abort;
    }

    /// <summary>获取公开流是否尚可读取。</summary>
    public override bool CanRead => !_disposed;
    /// <summary>获取定位能力；分段共享网络流，始终不支持定位。</summary>
    public override bool CanSeek => false;
    /// <summary>获取写入能力，始终为 false。</summary>
    public override bool CanWrite => false;
    /// <summary>获取本段已知长度；完整正文没有声明长度时不支持查询。</summary>
    public override long Length => _length ?? throw new NotSupportedException("The full response length is unknown.");
    /// <summary>获取本段已消费的字节数；不支持修改位置。</summary>
    public override long Position { get => _position; set => throw new NotSupportedException(); }

    /// <summary>同步读取本段剩余内容，严格禁止跨过 Content-Range 边界。</summary>
    /// <param name="buffer">接收正文的目标数组。</param>
    /// <param name="offset">目标数组中的写入偏移。</param>
    /// <param name="count">本次最多读取的字节数。</param>
    /// <returns>实际读取字节数，本段结束时为 0。</returns>
    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

    /// <summary>同步读取本段剩余字节。</summary>
    /// <param name="buffer">接收字节的内存区域。</param>
    /// <returns>实际读取字节数，本段结束时为 0。</returns>
    public override int Read(Span<byte> buffer)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _token.ThrowIfCancellationRequested();
        var count = Limit(buffer.Length);
        if (count == 0) return 0;
        int actual;
        try { actual = _reader.Read(buffer[..count]); }
        catch (Exception) when (_token.IsCancellationRequested) { throw new OperationCanceledException(_token); }
        Advance(actual);
        return actual;
    }

    /// <summary>异步读取数组中的分段内容。</summary>
    /// <param name="buffer">接收字节的数组。</param>
    /// <param name="offset">目标数组偏移。</param>
    /// <param name="count">最多读取字节数。</param>
    /// <param name="cancellationToken">单次读取取消令牌。</param>
    /// <returns>读取完成任务，结果为实际字节数。</returns>
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    /// <summary>异步读取本段内容，并同时遵守请求与单次调用的取消令牌。</summary>
    /// <param name="buffer">接收字节的目标区域。</param>
    /// <param name="cancellationToken">单次调用的附加取消令牌。</param>
    /// <returns>实际字节数；本段完成时为 0。</returns>
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(_token, cancellationToken);
        linked.Token.ThrowIfCancellationRequested();
        var count = Limit(buffer.Length);
        if (count == 0) return 0;
        using var abort = cancellationToken.UnsafeRegister(static value => ((Action)value!).Invoke(), _abort);
        try
        {
            var actual = await _reader.ReadAsync(buffer[..count], linked.Token).ConfigureAwait(false);
            Advance(actual);
            return actual;
        }
        catch (Exception) when (linked.IsCancellationRequested) { throw new OperationCanceledException(linked.Token); }
    }

    /// <summary>排空未读取的本段内容，以便枚举器安全进入下一段。</summary>
    /// <remarks>即使用户已经关闭公开流，也继续按剩余长度排空，不从已关闭的公开 Read 方法重新进入。</remarks>
    /// <param name="token">枚举操作的取消令牌。</param>
    /// <returns>当前分段剩余字节被消费完毕后完成的任务。</returns>
    public async Task DrainAsync(CancellationToken token)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(8192);
        try
        {
            while (Limit(buffer.Length) is > 0 and var count)
            {
                var actual = await _reader.ReadAsync(buffer.AsMemory(0, count), token).ConfigureAwait(false);
                Advance(actual);
            }
        }
        finally { ArrayPool<byte>.Shared.Return(buffer); }
    }

    /// <summary>按本段剩余长度限制单次读取量。</summary>
    /// <param name="count">调用方请求的读取量。</param>
    /// <returns>不会越过本段末尾的读取量。</returns>
    private int Limit(int count) => _ended ? 0 : (int)Math.Min(count, _remaining ?? count);

    /// <summary>记录读取进度，并在已知长度提前结束时拒绝不完整正文。</summary>
    /// <param name="count">底层实际返回的字节数。</param>
    private void Advance(int count)
    {
        if (count == 0)
        {
            if (_remaining is > 0) throw new InvalidDataException("The byte-range part ended before Content-Range was satisfied.");
            _ended = true;
            return;
        }
        _position += count;
        if (_remaining.HasValue) _remaining -= count;
    }

    /// <summary>关闭当前分段的公开读取入口，不关闭共享的外层响应。</summary>
    /// <param name="disposing">是否执行托管清理。</param>
    protected override void Dispose(bool disposing) { _disposed = true; base.Dispose(disposing); }
    /// <summary>异步关闭本段的公开读取入口。</summary>
    /// <returns>已经完成的释放任务。</returns>
    public override ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
    /// <summary>只读网络分段的 Flush 不执行任何写入。</summary>
    public override void Flush() { }
    /// <summary>拒绝对共享的网络分段定位。</summary>
    /// <param name="offset">不被应用的字节偏移。</param>
    /// <param name="origin">不被应用的定位基准。</param>
    /// <returns>该操作始终抛出 NotSupportedException。</returns>
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    /// <summary>拒绝改变只读分段的长度。</summary>
    /// <param name="value">不被应用的新长度。</param>
    public override void SetLength(long value) => throw new NotSupportedException();
    /// <summary>拒绝向只读分段写入内容。</summary>
    /// <param name="buffer">不被读取的输入数组。</param>
    /// <param name="offset">不被应用的数组偏移。</param>
    /// <param name="count">不被应用的字节数。</param>
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}

