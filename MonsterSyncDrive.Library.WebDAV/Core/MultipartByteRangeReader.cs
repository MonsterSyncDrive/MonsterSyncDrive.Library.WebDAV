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
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;

namespace MonsterSyncDrive.Library.WebDAV.Core;

/// <summary>以固定缓冲区读取 multipart/byteranges，正文长度由 Content-Range 限制而不扫描二进制数据中的边界近似串。</summary>
internal sealed class MultipartByteRangeReader
{
    /// <summary>单个 MIME 头块或前导区允许的最大字节数，防止未终止头部无限占用内存。</summary>
    private const int MaxMetadataBytes = 64 * 1024;
    /// <summary>实际 HTTP 正文流，所有权由外层范围响应管理。</summary>
    private readonly Stream _source;
    /// <summary>网络预读缓冲区；头和正文共享此缓冲，避免读头时丢弃已经预读的文件字节。</summary>
    private readonly byte[] _buffer = new byte[8192];
    /// <summary>下一次可消费的缓冲起点。</summary>
    private int _start;
    /// <summary>当前缓冲有效数据的结束位置。</summary>
    private int _end;
    /// <summary>从底层实际读入的总字节数，用于最终核对外层 Content-Length。</summary>
    public long BytesRead
    {
        get; private set;
    }

    /// <summary>建立不拥有源流的缓冲协议读取器。</summary>
    /// <param name="source">已经关联请求取消与超时的响应正文流。</param>
    public MultipartByteRangeReader(Stream source)
    {
        _source = source;
    }

    /// <summary>解析并校验 MIME boundary 参数，包括引号和 RFC 长度限制。</summary>
    /// <param name="contentType">multipart/byteranges 类型头。</param>
    /// <returns>去掉外部引号的边界字符串。</returns>
    public static string GetBoundary(MediaTypeHeaderValue contentType)
    {
        NameValueHeaderValue[] values = contentType.Parameters.Where(parameter => parameter.Name.Equals("boundary", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (values.Length != 1 || string.IsNullOrEmpty(values[0].Value))
            throw new InvalidDataException("A multipart byte-range response requires one boundary parameter.");
        string value = values[0].Value!;
        if (value.StartsWith('"') && value.EndsWith('"') && value.Length >= 2)
        {
            // quoted-string 的 quoted-pair 可以转义原本合法的边界字符，例如 "a\\?b" 表示 a?b。
            // 先解开这一层语法，再按 MIME 的 bchars 约束检查，不能把反斜杠当作实际边界内容。
            var decoded = new StringBuilder();
            for (int index = 1; index < value.Length - 1; index++)
            {
                if (value[index] == '\\')
                {
                    if (++index >= value.Length - 1)
                        throw new InvalidDataException("Invalid escaped MIME boundary.");
                }
                _ = decoded.Append(value[index]);
            }
            value = decoded.ToString();
        }
        return value.Length is < 1 or > 70 || value.EndsWith(' ')
            || value.Any(character => !char.IsAsciiLetterOrDigit(character) && !"'()+_,-./:=? ".Contains(character))
            ? throw new InvalidDataException("The MIME byte-range boundary is invalid.")
            : value;
    }

    /// <summary>读取第一条或正文之后的 MIME 分隔行。</summary>
    /// <param name="boundary">已验证的 MIME 边界，不包含开头两个连字符。</param>
    /// <param name="initial">true 时忽略有界前导区；false 时要求当前正文后立即出现 CRLF 和边界。</param>
    /// <param name="token">控制网络读取的取消令牌。</param>
    /// <returns>true 表示终止边界，false 表示还有一个分段头块。</returns>
    public async Task<bool> ReadBoundaryAsync(string boundary, bool initial, CancellationToken token)
    {
        if (!initial)
        {
            if (await ReadByteAsync(token).ConfigureAwait(false) != '\r' || await ReadByteAsync(token).ConfigureAwait(false) != '\n')
                throw new InvalidDataException("A byte-range part is not followed by a MIME CRLF boundary.");
        }
        int consumed = 0;
        while (true)
        {
            string line = await ReadLineAsync(MaxMetadataBytes - consumed, true, token).ConfigureAwait(false);
            consumed += line.Length + 2;
            string marker = "--" + boundary;
            if (line.StartsWith(marker, StringComparison.Ordinal))
            {
                string tail = line[marker.Length..].TrimEnd(' ', '\t');
                if (tail.Length == 0)
                    return false;
                if (tail == "--")
                    return true;
            }
            if (!initial)
                throw new InvalidDataException("The byte-range MIME boundary does not match.");
            // 前导区中的近似边界不是实际分隔符，继续查找完整的边界行；总量仍受上限约束。
            if (consumed >= MaxMetadataBytes)
                throw new InvalidDataException("The multipart preamble exceeds its limit.");
        }
    }

    /// <summary>读取当前分段的独立头块，拒绝重复范围头、控制字符和过大的头部。</summary>
    /// <param name="token">控制读取的取消令牌。</param>
    /// <returns>不区分大小写的多值头快照。</returns>
    public async Task<IReadOnlyDictionary<string, string[]>> ReadHeadersAsync(CancellationToken token)
    {
        Dictionary<string, List<string>> headers = new(StringComparer.OrdinalIgnoreCase);
        int consumed = 0;
        while (true)
        {
            string line = await ReadLineAsync(MaxMetadataBytes - consumed, false, token).ConfigureAwait(false);
            consumed += line.Length + 2;
            if (line.Length == 0)
                break;
            int colon = line.IndexOf(':');
            if (colon <= 0 || line[0] is ' ' or '\t')
                throw new InvalidDataException("Invalid MIME part header.");
            string name = line[..colon];
            if (!name.All(character => char.IsAsciiLetterOrDigit(character) || "!#$%&'*+-.^_`|~".Contains(character)))
                throw new InvalidDataException("Invalid MIME header field name.");
            string value = line[(colon + 1)..].Trim(' ', '\t');
            if (value.Any(character => character is (< ' ' and not '\t') or '\u007f'))
                throw new InvalidDataException("Invalid MIME header field value.");
            if (!headers.TryGetValue(name, out List<string>? values))
                headers[name] = values = [];
            if (values.Count > 0 && (name.Equals("Content-Range", StringComparison.OrdinalIgnoreCase)
                || name.Equals("Content-Length", StringComparison.OrdinalIgnoreCase) || name.Equals("Content-Type", StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException("A byte-range part contains duplicate framing headers.");
            values.Add(value);
        }
        return headers.TryGetValue("Content-Encoding", out List<string>? encoding) && encoding.Any(value => !value.Equals("identity", StringComparison.OrdinalIgnoreCase))
            ? throw new InvalidDataException("A byte-range part must use identity content encoding.")
            : headers.TryGetValue("Content-Transfer-Encoding", out List<string>? transfer)
            && transfer.Any(value => !value.Equals("binary", StringComparison.OrdinalIgnoreCase)
                && !value.Equals("8bit", StringComparison.OrdinalIgnoreCase) && !value.Equals("7bit", StringComparison.OrdinalIgnoreCase)
                && !value.Equals("identity", StringComparison.OrdinalIgnoreCase))
            ? throw new InvalidDataException("Encoded MIME byte-range parts are not raw representation bytes.")
            : (IReadOnlyDictionary<string, string[]>)new System.Collections.ObjectModel.ReadOnlyDictionary<string, string[]>(headers.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray(), StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>从分段头中取得并校验可选的长度值。</summary>
    /// <param name="headers">已校验的分段头。</param>
    /// <returns>声明的非负字节数；未声明时为 null。</returns>
    public static long? GetLength(IReadOnlyDictionary<string, string[]> headers)
    {
        return !headers.TryGetValue("Content-Length", out string[]? values)
            ? null
            : values.Length != 1 || !long.TryParse(values[0], NumberStyles.None, CultureInfo.InvariantCulture, out long length) || length < 0
            ? throw new InvalidDataException("Invalid MIME Content-Length.")
            : length;
    }

    /// <summary>在分段之间保留预读缓冲的情况下，同步读取文件字节。</summary>
    /// <param name="destination">接收正文内容的目标区域。</param>
    /// <returns>实际读取字节数，末尾为 0。</returns>
    public int Read(Span<byte> destination)
    {
        if (destination.Length == 0)
            return 0;
        if (_start < _end)
        {
            int count = Math.Min(destination.Length, _end - _start);
            _buffer.AsSpan(_start, count).CopyTo(destination);
            _start += count;
            return count;
        }
        int read = _source.Read(destination);
        BytesRead += read;
        return read;
    }

    /// <summary>异步读取正文内容，优先消费头部解析留下的预读字节。</summary>
    /// <param name="destination">接收正文的目标区域。</param>
    /// <param name="token">控制底层读取的取消令牌。</param>
    /// <returns>实际字节数，HTTP 正文结束时为 0。</returns>
    public async ValueTask<int> ReadAsync(Memory<byte> destination, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (destination.Length == 0)
            return 0;
        if (_start < _end)
        {
            int count = Math.Min(destination.Length, _end - _start);
            _buffer.AsMemory(_start, count).CopyTo(destination);
            _start += count;
            return count;
        }
        int read = await _source.ReadAsync(destination, token).ConfigureAwait(false);
        BytesRead += read;
        return read;
    }

    /// <summary>读取协议元数据的单个字节，正常情况下从固定预读缓冲返回。</summary>
    /// <param name="token">控制补充缓冲区的取消令牌。</param>
    /// <returns>字节值；HTTP 正文结束时为 -1。</returns>
    public async ValueTask<int> ReadByteAsync(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (_start == _end)
        {
            _start = 0;
            _end = await _source.ReadAsync(_buffer, token).ConfigureAwait(false);
            BytesRead += _end;
            if (_end == 0)
                return -1;
        }
        return _buffer[_start++];
    }

    /// <summary>完整消费终止边界后的 MIME 尾声，确保外层正文长度也能得到校验。</summary>
    /// <param name="token">控制读取的取消令牌。</param>
    /// <returns>读取到 HTTP 正文末尾后完成的任务。</returns>
    public async Task DrainEpilogueAsync(CancellationToken token)
    {
        byte[] scratch = new byte[8192];
        while (await ReadAsync(scratch, token).ConfigureAwait(false) != 0)
        {
        }
    }

    /// <summary>读取严格 CRLF 终止的 MIME 行，终止边界允许恰好位于正文末尾。</summary>
    /// <param name="limit">当前头块剩余的允许字节数。</param>
    /// <param name="allowEof">是否允许一个非空行直接终止于 HTTP 正文末尾。</param>
    /// <param name="token">取消读取的令牌。</param>
    /// <returns>移除 CRLF 的 Latin-1 头部行文本。</returns>
    private async Task<string> ReadLineAsync(int limit, bool allowEof, CancellationToken token)
    {
        if (limit <= 0)
            throw new InvalidDataException("MIME metadata exceeds its limit.");
        using var line = new MemoryStream();
        while (true)
        {
            int value = await ReadByteAsync(token).ConfigureAwait(false);
            if (value < 0)
            {
                return allowEof && line.Length > 0
                    ? Encoding.Latin1.GetString(line.GetBuffer(), 0, (int)line.Length)
                    : throw new InvalidDataException("Unexpected end of multipart metadata.");
            }
            if (value == '\r')
            {
                return await ReadByteAsync(token).ConfigureAwait(false) != '\n'
                    ? throw new InvalidDataException("MIME lines must end with CRLF.")
                    : line.Length + 2 > limit
                    ? throw new InvalidDataException("MIME metadata exceeds its limit.")
                    : Encoding.Latin1.GetString(line.GetBuffer(), 0, (int)line.Length);
            }
            if (value == '\n')
                throw new InvalidDataException("MIME lines must end with CRLF.");
            line.WriteByte((byte)value);
            if (line.Length > limit)
                throw new InvalidDataException("MIME metadata exceeds its limit.");
        }
    }
}

