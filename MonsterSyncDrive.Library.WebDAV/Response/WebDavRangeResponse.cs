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
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using MonsterSyncDrive.WebDAV.Core;

namespace MonsterSyncDrive.WebDAV.Response;

/// <summary>管理单次 GET 返回的多范围流、单段合并响应或忽略 Range 的完整正文。</summary>
/// <remarks>必须使用 using 或 await using。分段只允许顺序消费，完整枚举后才完成覆盖范围计算、终止边界和外层长度校验。</remarks>
public sealed class WebDavRangeResponse : WebDavResponse, IDisposable, IAsyncDisposable
{
    /// <summary>此对象拥有的原始 HTTP 响应。</summary>
    private readonly HttpResponseMessage _response;
    /// <summary>发送请求时的取消令牌，继续覆盖正文读取。</summary>
    private readonly CancellationToken _requestToken;
    /// <summary>释放范围结果时取消其全部枚举和分段读取。</summary>
    private readonly CancellationTokenSource _lifetime;
    /// <summary>提前保存的生命周期令牌，避免令牌源释放后再次访问 Token。</summary>
    private readonly CancellationToken _lifetimeToken;
    /// <summary>多范围请求的区间与完整性验证器。</summary>
    private readonly ByteRangeRequestValidator _validator;
    /// <summary>multipart 响应的边界；非 multipart 时为 null。</summary>
    private readonly string? _boundary;
    /// <summary>非 multipart 的 206 字节区间；完整 200 正文时为 null。</summary>
    private readonly ContentRangeHeaderValue? _singleRange;
    /// <summary>非 multipart 分段的已知长度；完整未知长度正文时为 null。</summary>
    private readonly long? _singleLength;
    /// <summary>外层 HTTP 正文声明的总长度，包含 multipart 元数据。</summary>
    private readonly long? _outerLength;
    /// <summary>确保正文只能枚举一次，避免并发消费者混用同一个网络流。</summary>
    private int _started;
    /// <summary>幂等释放标志。</summary>
    private int _disposed;
    /// <summary>已正常消费并校验完整 HTTP 正文；提前结束枚举时保持 false。</summary>
    private bool _completed;

    /// <summary>接管原始响应，并校验响应头中的范围格式和 MIME 边界。</summary>
    /// <param name="response">原始 GET 响应，构造成功后所有权转交给当前对象。</param>
    /// <param name="requested">发送到服务器的 bytes Range 快照。</param>
    /// <param name="requestToken">本次请求的取消令牌。</param>
    internal WebDavRangeResponse(HttpResponseMessage response, RangeHeaderValue requested, CancellationToken requestToken)
        : base((int)response.StatusCode, response.ReasonPhrase)
    {
        _response = response;
        _requestToken = requestToken;
        _validator = new ByteRangeRequestValidator(requested);
        RequestedRange = requested.ToString();
        CaptureHeaders<WebDavRangeResponse>(response);
        _outerLength = response.Content.Headers.ContentLength;
        if (response.StatusCode == HttpStatusCode.PartialContent)
        {
            var type = response.Content.Headers.ContentType;
            if (type?.MediaType?.Equals("multipart/byteranges", StringComparison.OrdinalIgnoreCase) == true)
            {
                if (requested.Ranges.Count < 2)
                    throw new InvalidDataException("A server must not send multipart for a single requested byte range.");
                if (response.Content.Headers.Contains("Content-Range"))
                    throw new InvalidDataException("Multipart byte ranges must not carry an outer Content-Range.");
                _boundary = MultipartByteRangeReader.GetBoundary(type);
            }
            else
            {
                _singleRange = response.Content.Headers.ContentRange;
                _singleLength = _validator.Add(_singleRange, _outerLength);
            }
        }
        else _singleLength = _outerLength;
        _lifetime = new CancellationTokenSource();
        _lifetimeToken = _lifetime.Token;
    }

    /// <summary>获取发送时的 Range 请求头快照。</summary>
    public string RequestedRange { get; }
    /// <summary>获取服务器是否返回 multipart/byteranges 格式。</summary>
    public bool IsMultipart => _boundary is not null;
    /// <summary>获取服务器是否忽略 Range 并返回完整 200 正文。</summary>
    public bool IsRangeIgnored => StatusCode == 200;
    /// <summary>获取服务器已经声明的完整文件长度，multipart 时通常在读取第一段后才可取得。</summary>
    public long? TotalLength => _validator.TotalLength ?? (IsRangeIgnored ? _outerLength : null);
    /// <summary>获取完整枚举后仍需续取的可满足字节范围；已完整返回时为空集合，枚举未完成或总长度未知时为 null。</summary>
    /// <remarks>RFC 9110 允许合法 206 仅返回请求的一部分。200 完整正文回退在消费完毕后返回空集合。</remarks>
    public IReadOnlyList<RangeItemHeaderValue>? MissingRanges => !_completed ? null
        : IsRangeIgnored ? Array.Empty<RangeItemHeaderValue>() : _validator.MissingRanges;
    /// <summary>获取请求是否已完整满足；false 表示 MissingRanges 中有待续取区间，null 表示尚无法确定。</summary>
    public bool? IsRangeRequestSatisfied => MissingRanges is { } missing ? missing.Count == 0 : null;

    /// <summary>按服务器返回顺序逐段读取正文，自动排空上一段中未消费的字节。</summary>
    /// <param name="cancellationToken">本次枚举的附加取消令牌，取消时会关闭原响应以中断网络读取。</param>
    /// <returns>各实际分段；服务器合并区间后可能比请求项少，完整 200 回退返回一个无 ContentRange 的段。</returns>
    /// <remarks>每个分段 Stream 只在当前枚举位置有效。完整枚举后计算 MissingRanges 并验证文档边界，提前结束枚举只做释放，不宣称下载完整。</remarks>
    /// <exception cref="InvalidOperationException">同一响应被再次或并发枚举。</exception>
    /// <exception cref="InvalidDataException">MIME、分段范围或正文长度不合法。</exception>
    public async IAsyncEnumerable<WebDavRangePart> ReadPartsAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        if (Interlocked.Exchange(ref _started, 1) != 0)
            throw new InvalidOperationException("A byte-range response can only be enumerated once.");
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(_requestToken, _lifetimeToken, cancellationToken);
        try
        {
            using var abort = linked.Token.UnsafeRegister(static value => ((HttpResponseMessage)value!).Dispose(), _response);
            linked.Token.ThrowIfCancellationRequested();
            EnsureSuccessStatusCode();
            if (StatusCode is not (200 or 206))
                throw new InvalidDataException("A range GET must return 200 or 206 to provide readable representation bytes.");
            await using var source = await _response.Content.ReadAsStreamAsync(linked.Token).ConfigureAwait(false);
            var reader = new MultipartByteRangeReader(source);
            if (_boundary is null)
            {
                var headers = _response.Content.Headers.ToDictionary(header => header.Key, header => header.Value.ToArray(), StringComparer.OrdinalIgnoreCase);
                using var segment = new ByteRangePartStream(reader, _singleLength, linked.Token, Dispose);
                yield return new WebDavRangePart(_singleRange, headers, segment);
                await segment.DrainAsync(linked.Token).ConfigureAwait(false);
                // 长度已知的段停止在边界处，额外探测确保测试流或非标准处理器没有偷塞更多正文。
                if (await reader.ReadByteAsync(linked.Token).ConfigureAwait(false) >= 0)
                    throw new InvalidDataException("A single range body exceeds its declared length.");
            }
            else
            {
                if (await reader.ReadBoundaryAsync(_boundary, true, linked.Token).ConfigureAwait(false))
                    throw new InvalidDataException("A multipart partial response cannot be empty.");
                while (true)
                {
                    var headers = await reader.ReadHeadersAsync(linked.Token).ConfigureAwait(false);
                    if (!headers.TryGetValue("Content-Range", out var values) || values.Length != 1
                        || !ContentRangeHeaderValue.TryParse(values[0], out var range))
                        throw new InvalidDataException("A MIME byte-range part has no valid Content-Range.");
                    var length = _validator.Add(range, MultipartByteRangeReader.GetLength(headers));
                    using var segment = new ByteRangePartStream(reader, length, linked.Token, Dispose);
                    // 文件数据按已验证的精确区间长度读取，绝不在二进制负载中搜索边界。
                    yield return new WebDavRangePart(range, headers, segment);
                    await segment.DrainAsync(linked.Token).ConfigureAwait(false);
                    if (await reader.ReadBoundaryAsync(_boundary, false, linked.Token).ConfigureAwait(false)) break;
                }
                await reader.DrainEpilogueAsync(linked.Token).ConfigureAwait(false);
            }
            if (_outerLength.HasValue && reader.BytesRead != _outerLength)
                throw new InvalidDataException("The complete response length differs from Content-Length.");
            if (StatusCode == 206) _validator.Complete();
            linked.Token.ThrowIfCancellationRequested();
            _completed = true;
        }
        finally { Dispose(); }
    }

    /// <summary>取消在途分段读取，并至多释放一次原始响应及内部令牌源。</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        try { _lifetime.Cancel(); }
        finally
        {
            try { _response.Dispose(); }
            finally { _lifetime.Dispose(); }
        }
    }

    /// <summary>异步关闭范围结果，当前实现的资源释放在返回前同步完成。</summary>
    /// <returns>已完成的释放任务。</returns>
    public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
}

