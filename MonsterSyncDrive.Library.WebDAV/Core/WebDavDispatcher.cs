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

namespace MonsterSyncDrive.Library.WebDAV.Core;

/// <summary>
/// 管理 HTTP 发送、客户端生命周期及覆盖响应正文的超时和取消作用域。
/// </summary>
/// <remarks>以 ResponseHeadersRead 发送请求；收到响应头后，把令牌源所有权交给正文包装器，直至正文释放才结束本次请求的计时。</remarks>
/// <param name="client">已经创建的 HTTP 客户端，用于实际网络请求。</param>
/// <param name="ownsClient">是否在调度器释放时同时释放 HTTP 客户端。</param>
/// <param name="timeout">覆盖请求头与正文读取的默认超时，可为 Timeout.InfiniteTimeSpan。</param>
internal sealed class WebDavDispatcher(HttpClient client, bool ownsClient, TimeSpan timeout) : IDisposable
{
    /// <summary>
    /// 客户端生命周期令牌源；释放客户端时取消全部仍关联的请求及其正文。
    /// </summary>
    private readonly CancellationTokenSource _lifetime = new();
    /// <summary>
    /// 线程安全的释放标志；0 表示尚未开始释放，1 表示已进入释放流程，用于保证资源归还至多执行一次。
    /// </summary>
    private int _disposed;
    /// <summary>
    /// 获取解析相对请求 URI 使用的 HTTP 基地址。
    /// </summary>
    /// <value>未配置时为 null，只能发送绝对请求 URI；相对地址按标准 URI 规则解析。</value>
    public Uri? BaseAddress => client.BaseAddress;

    /// <summary>读取单次请求最终使用的头字段，缺省时读取 HttpClient 的默认字段。</summary>
    /// <remarks>HttpClient 在真正发送时才合并默认头；协议参数必须在请求产生副作用前完成校验，因此预先按相同的整字段覆盖规则查询。</remarks>
    /// <param name="request">尚未发送或已经发送的请求。</param>
    /// <param name="name">需要查询的 HTTP 字段名。</param>
    /// <returns>逗号连接的字段值；单次请求与默认字段均未提供时返回 null。</returns>
    internal string? GetEffectiveHeaderValue(HttpRequestMessage request, string name)
    {
        return request.Headers.TryGetValues(name, out IEnumerable<string>? values) ? string.Join(", ", values)
        : client.DefaultRequestHeaders.TryGetValues(name, out values) ? string.Join(", ", values) : null;
    }

    /// <summary>
    /// 创建关联取消作用域并发送请求，把完整请求的生命周期转交给响应正文。
    /// </summary>
    /// <remarks>未取得响应时立即释放关联令牌源；创建正文包装失败时先关闭原响应，防止连接和计时器泄漏。</remarks>
    /// <param name="request">已经构造且 URI 已完成解析的请求；本方法不接管请求对象本身。</param>
    /// <param name="token">调用方的请求令牌，与客户端生命周期和请求超时共同关联。</param>
    /// <returns>仅等待响应头的任务；成功结果包含接管令牌源的 LifetimeContent。</returns>
    public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        // 这里不能用 using：响应头到达后，调用方还可能长时间读取正文。
        // 成功时令牌源交给 LifetimeContent，只有发送失败时才在本方法直接释放。
        CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token, token);
        try
        {
            cancellation.Token.ThrowIfCancellationRequested();
            if (timeout != Timeout.InfiniteTimeSpan)
                cancellation.CancelAfter(timeout);
            HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellation.Token).ConfigureAwait(false);
            try
            {
                // 包装器同时保留正文头和取消语义，原响应仍可按标准 HttpResponseMessage 方式使用。
                response.Content = new LifetimeContent(response.Content, cancellation);
                return response;
            }
            catch
            {
                response.Dispose();
                throw;
            }
        }
        catch
        {
            cancellation.Dispose();
            throw;
        }
    }

    /// <summary>
    /// 以幂等方式取消本客户端关联的全部操作，并按所有权约定释放 HttpClient。
    /// </summary>
    /// <remarks>Interlocked 保证清理至多执行一次；先取消在途正文，再关闭自有客户端，借用的 HttpClient 保持可用。</remarks>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;
        try
        {
            _lifetime.Cancel();
        }
        finally
        {
            if (ownsClient)
                client.Dispose();
            _lifetime.Dispose();
        }
    }

    /// <summary>
    /// 为 HTTP 正文关联请求取消、超时及确定性的资源释放。
    /// </summary>
    /// <remarks>复制原正文头；取消时关闭原始正文，以中断不接受 CancellationToken 的读取，并为返回的流保留请求令牌。</remarks>
    private sealed class LifetimeContent : HttpContent
    {
        /// <summary>
        /// 负责实际网络读取的原始响应正文，同时提供原始正文头。
        /// </summary>
        private readonly HttpContent _inner;
        /// <summary>
        /// 覆盖请求头与正文阶段的关联令牌源，由正文包装器接管并负责释放。
        /// </summary>
        private readonly CancellationTokenSource _cancellation;
        /// <summary>
        /// 预先保存的请求令牌，供正文与下载流读取使用，避免令牌源释放后再次访问其 Token 属性。
        /// </summary>
        private readonly CancellationToken _token;
        /// <summary>
        /// 请求取消时关闭原始正文的注册句柄；正常释放时先注销以避免关闭正文的并发竞争。
        /// </summary>
        private readonly CancellationTokenRegistration _abort;
        /// <summary>
        /// 线程安全的释放标志；0 表示尚未开始释放，1 表示已进入释放流程，用于保证资源归还至多执行一次。
        /// </summary>
        private int _disposed;

        /// <summary>
        /// 接管原始正文与请求令牌源，复制正文头并注册取消时的关闭动作。
        /// </summary>
        /// <remarks>读取 ContentLength 会触发原内容的延迟长度计算；取消注册只关闭原始正文，包装器自身的清理由正常释放路径完成。</remarks>
        /// <param name="inner">原始 HTTP 响应正文，包装器接管其生命周期。</param>
        /// <param name="cancellation">覆盖请求头和正文阶段的关联令牌源，所有权转交给包装器。</param>
        public LifetimeContent(HttpContent inner, CancellationTokenSource cancellation)
        {
            _inner = inner;
            _cancellation = cancellation;
            _token = cancellation.Token;
            // 部分 HttpContent 直到读取该属性才调用 TryComputeLength。先触发计算，再复制头部，避免丢失长度。
            _ = inner.Headers.ContentLength;
            foreach (KeyValuePair<string, IEnumerable<string>> header in inner.Headers)
                _ = Headers.TryAddWithoutValidation(header.Key, header.Value);
            // 取消不只是设置标志，还要关闭底层正文，才能打断不接收令牌的 XML 或其他网络读取。
            // 回调只关闭原正文，不递归释放包装器，完整清理由拥有包装器的正常释放路径完成。
            _abort = _token.UnsafeRegister(static state => ((HttpContent)state!).Dispose(), inner);
        }

        /// <summary>
        /// 向正文消费者转发原始 Content-Length，保留未知长度状态。
        /// </summary>
        /// <param name="length">输出原正文的声明字节数；未知时输出 0，不表示正文为空。</param>
        /// <returns>原正文提供 Content-Length 时为 true，否则为 false。</returns>
        protected override bool TryComputeLength(out long length)
        {
            length = _inner.Headers.ContentLength ?? 0;
            return _inner.Headers.ContentLength.HasValue;
        }

        /// <summary>
        /// 复制原始响应正文，同时组合请求级和调用级取消令牌。
        /// </summary>
        /// <remarks>取消可能在底层表现为关闭流异常；取消令牌已触发时统一转换为 OperationCanceledException，保持上层取消语义。</remarks>
        /// <param name="stream">接收原始正文的目标流，本方法不关闭该目标流。</param>
        /// <param name="context">由 HTTP 框架提供并转发给原始正文的传输上下文。</param>
        /// <returns>表示本次正文复制结束的任务。</returns>
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            return SerializeToStreamAsync(stream, context, _token);
        }

        /// <summary>
        /// 复制原始响应正文，同时组合请求级和调用级取消令牌。
        /// </summary>
        /// <remarks>取消可能在底层表现为关闭流异常；取消令牌已触发时统一转换为 OperationCanceledException，保持上层取消语义。</remarks>
        /// <param name="stream">接收原始正文的目标流，本方法不关闭该目标流。</param>
        /// <param name="context">由 HTTP 框架提供并转发给原始正文的传输上下文。</param>
        /// <param name="cancellationToken">调用方对本次正文复制附加的取消令牌。</param>
        /// <returns>表示本次正文复制结束的任务。</returns>
        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken)
        {
            using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(_token, cancellationToken);
            try
            {
                await _inner.CopyToAsync(stream, context, linked.Token).ConfigureAwait(false);
            }
            catch (Exception) when (linked.IsCancellationRequested)
            {
                throw new OperationCanceledException(linked.Token);
            }
        }

        /// <summary>
        /// 异步打开原始正文流，并包装请求取消和关闭时的生命周期归还行为。
        /// </summary>
        /// <remarks>显式传入的令牌用于打开正文流；返回流仍保留请求生命周期令牌，后续每次读取可再传入自己的取消令牌。</remarks>
        /// <returns>支持请求级取消的只读包装流；关闭包装流会释放当前正文包装器。</returns>
        protected override Task<Stream> CreateContentReadStreamAsync()
        {
            return CreateContentReadStreamAsync(_token);
        }

        /// <summary>
        /// 异步打开原始正文流，并包装请求取消和关闭时的生命周期归还行为。
        /// </summary>
        /// <remarks>显式传入的令牌用于打开正文流；返回流仍保留请求生命周期令牌，后续每次读取可再传入自己的取消令牌。</remarks>
        /// <param name="cancellationToken">本次打开正文流的附加取消令牌，与请求令牌关联。</param>
        /// <returns>支持请求级取消的只读包装流；关闭包装流会释放当前正文包装器。</returns>
        protected override async Task<Stream> CreateContentReadStreamAsync(CancellationToken cancellationToken)
        {
            using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(_token, cancellationToken);
            try
            {
                Stream stream = await _inner.ReadAsStreamAsync(linked.Token).ConfigureAwait(false);
                return new WebDavReadStream(stream, _token, Dispose);
            }
            catch (Exception) when (linked.IsCancellationRequested)
            {
                throw new OperationCanceledException(linked.Token);
            }
        }

        /// <summary>
        /// 注销正文取消回调，关闭原始正文并释放关联请求令牌源。
        /// </summary>
        /// <remarks>先等待回调注销完成，再释放正文及令牌源，避免取消回调与清理阶段并发关闭同一资源。</remarks>
        /// <param name="disposing">true 执行托管资源清理；通过原子标志确保嵌套流关闭导致的重入不会重复清理。</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                // 关闭正文流可能反过来触发本包装器的 Dispose。先置释放标志，并等待取消回调注销，避免重复清理。
                _abort.Dispose();
                try
                {
                    _inner.Dispose();
                }
                finally
                {
                    _cancellation.Dispose();
                }
                base.Dispose(disposing);
            }
        }
    }
}
