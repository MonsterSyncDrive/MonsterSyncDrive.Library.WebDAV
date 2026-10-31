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
namespace MonsterSyncDrive.WebDAV.Core;

/// <summary>
/// 为只读下载流叠加请求取消和关闭时的资源归还行为。
/// </summary>
/// <remarks>定位能力取决于内部流；异步读取同时关联请求令牌和本次读取令牌，释放时至多执行一次归还回调。</remarks>
/// <param name="inner">实际提供下载字节的内部流。</param>
/// <param name="requestToken">覆盖当前 HTTP 请求完整生命周期的取消令牌。</param>
/// <param name="release">包装流首次关闭时执行的资源归还回调。</param>
internal sealed class WebDavReadStream(Stream inner, CancellationToken requestToken, Action release) : Stream
{
    /// <summary>
    /// 线程安全的释放标志；0 表示尚未开始释放，1 表示已进入释放流程，用于保证资源归还至多执行一次。
    /// </summary>
    private int _disposed;
    /// <summary>
    /// 获取包装流尚未释放且内部流支持读取时的可读状态。
    /// </summary>
    public override bool CanRead => _disposed == 0 && inner.CanRead;
    /// <summary>
    /// 获取包装流尚未释放且内部流支持定位时的状态。
    /// </summary>
    /// <value>通常网络正文流不可定位；该包装器不会为 Seek 自动重新发起 GET。</value>
    public override bool CanSeek => _disposed == 0 && inner.CanSeek;
    /// <summary>
    /// 获取该下载流是否可写，始终返回 false。
    /// </summary>
    public override bool CanWrite => false;
    /// <summary>
    /// 获取内部流提供的长度。
    /// </summary>
    /// <value>内部网络流不支持长度查询时，其异常直接向调用方传播。</value>
    public override long Length => inner.Length;
    /// <summary>
    /// 获取或设置内部流的位置。
    /// </summary>
    /// <value>仅适用于可定位的内部流，不会通过网络请求模拟定位。</value>
    public override long Position { get => inner.Position; set => inner.Position = value; }
    /// <summary>
    /// 对只读下载流执行空的刷新操作，不向网络写入任何内容。
    /// </summary>
    public override void Flush() { }
    /// <summary>
    /// 把定位请求直接交给内部流，不重新发起 HTTP 请求。
    /// </summary>
    /// <param name="offset">相对于指定基准位置的字节偏移。</param>
    /// <param name="origin">偏移的基准位置，可为开始、当前位置或末尾。</param>
    /// <returns>内部流定位后的新位置。</returns>
    /// <exception cref="NotSupportedException">内部正文流不支持定位。</exception>
    public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
    /// <summary>
    /// 拒绝修改只读下载流的长度。
    /// </summary>
    /// <param name="value">期望的新长度；由于流只读，该参数不会被应用。</param>
    /// <exception cref="NotSupportedException">只读下载流始终不支持 SetLength。</exception>
    public override void SetLength(long value) => throw new NotSupportedException();
    /// <summary>
    /// 拒绝向只读下载流写入数据。
    /// </summary>
    /// <param name="buffer">拟写入的源缓冲区，不会读取。</param>
    /// <param name="offset">拟写入数据在源缓冲区中的起始偏移。</param>
    /// <param name="count">拟写入的字节数。</param>
    /// <exception cref="NotSupportedException">只读下载流始终不支持写入。</exception>
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <summary>
    /// 同步读取内部流，读取前检查请求取消，并统一取消引发的异常。
    /// </summary>
    /// <param name="buffer">接收数据的目标缓冲区。</param>
    /// <param name="offset">目标缓冲区中开始写入数据的偏移。</param>
    /// <param name="count">本次最多读取的字节数。</param>
    /// <returns>实际读取的字节数；流结束时为 0。</returns>
    /// <exception cref="OperationCanceledException">请求已取消，或内部读取因取消关闭正文而失败。</exception>
    public override int Read(byte[] buffer, int offset, int count)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        return Read(buffer.AsSpan(offset, count));
    }

    /// <summary>直接读取到目标 Span，避免 Stream 默认适配产生的临时缓冲与复制。</summary>
    /// <param name="buffer">接收正文数据的目标区域。</param>
    /// <returns>实际读取的字节数；到达正文末尾时为 0。</returns>
    public override int Read(Span<byte> buffer)
    {
        requestToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        try { return inner.Read(buffer); }
        catch (Exception) when (requestToken.IsCancellationRequested) { throw new OperationCanceledException(requestToken); }
    }

    /// <summary>
    /// 异步读取内部流，将本次读取取消令牌与请求生命周期令牌合并。
    /// </summary>
    /// <remarks>请求取消时正文关闭可能抛出 IOException 或 ObjectDisposedException；关联令牌已取消时统一恢复为 OperationCanceledException。</remarks>
    /// <param name="buffer">接收正文数据的目标缓冲区或内存区域。</param>
    /// <param name="offset">数组重载中目标缓冲区的起始偏移。</param>
    /// <param name="count">数组重载中本次最多读取的字节数。</param>
    /// <param name="token">数组重载本次读取的附加取消令牌。</param>
    /// <returns>异步读取结果，包含实际字节数；达到正文末尾时为 0。</returns>
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken token) =>
        ReadAsync(buffer.AsMemory(offset, count), token).AsTask();

    /// <summary>
    /// 异步读取内部流，将本次读取取消令牌与请求生命周期令牌合并。
    /// </summary>
    /// <remarks>请求取消时正文关闭可能抛出 IOException 或 ObjectDisposedException；关联令牌已取消时统一恢复为 OperationCanceledException。</remarks>
    /// <param name="buffer">接收正文数据的目标缓冲区或内存区域。</param>
    /// <param name="cancellationToken">内存重载本次读取的附加取消令牌。</param>
    /// <returns>异步读取结果，包含实际字节数；达到正文末尾时为 0。</returns>
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        // 调用方对单次读取的取消不能替代请求级超时、连接关闭或客户端释放，需要同时保留两者。
        // 单个有效令牌或同一个令牌无需建立新的关联源，避免逐块读取时反复分配。
        using var linked = requestToken.CanBeCanceled && cancellationToken.CanBeCanceled && requestToken != cancellationToken
            ? CancellationTokenSource.CreateLinkedTokenSource(requestToken, cancellationToken) : null;
        var token = linked?.Token ?? (cancellationToken.CanBeCanceled ? cancellationToken : requestToken);
        token.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        try { return await inner.ReadAsync(buffer, token).ConfigureAwait(false); }
        catch (Exception) when (token.IsCancellationRequested) { throw new OperationCanceledException(token); }
    }

    /// <summary>
    /// 至多关闭一次内部流，并在 finally 中执行响应资源归还回调。
    /// </summary>
    /// <remarks>即使内部流的 Dispose 抛出异常，也尝试执行 release，避免丢失响应及关联令牌的清理。</remarks>
    /// <param name="disposing">true 表示释放内部流和托管响应资源；false 不执行这些操作。</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing && Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            // 即使原流关闭失败，finally 也继续归还 HTTP 响应；原子标志避免响应与流互相关闭造成重复回调。
            try { inner.Dispose(); }
            finally { release(); }
        }
        base.Dispose(disposing);
    }

    /// <summary>
    /// 通过幂等的同步释放路径关闭包装流与关联响应。
    /// </summary>
    /// <returns>已经完成的 ValueTask；清理操作在返回前完成。</returns>
    public override ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
}
