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
using System.Xml;
using System.Xml.Linq;
using MonsterSyncDrive.WebDAV.Request;
using MonsterSyncDrive.WebDAV.Response;

namespace MonsterSyncDrive.WebDAV;

/// <summary>组合标准扩展的请求构造、完整 XML 解析及响应正文所有权处理。</summary>
public sealed partial class WebDavClient
{
    /// <summary>发送 XML DAV 扩展请求并保存完整响应文档。</summary>
    /// <param name="uri">待操作资源的绝对或相对 URI。</param>
    /// <param name="method">标准扩展方法名称。</param>
    /// <param name="body">可选 XML 请求根元素；null 表示无正文。</param>
    /// <param name="options">通用条件、偏好、自定义请求头及取消设置。</param>
    /// <param name="protocolHeaders">扩展协议所需的请求头，随后允许 options.Headers 显式覆盖。</param>
    /// <returns>保留根级扩展、逐资源结果、错误详情和头部快照的响应。</returns>
    private async Task<WebDavDocumentResponse> SendDocumentRequestAsync(Uri uri, string method, XElement? body,
        WebDavRequestOptions? options = null,
        IReadOnlyCollection<KeyValuePair<string, string>>? protocolHeaders = null)
    {
        options ??= new WebDavRequestOptions();
        using var request = CreateRequest(uri, method, null,
            body is null ? null : XmlContent(body));
        if (protocolHeaders is not null)
            foreach (var header in protocolHeaders)
                request.Headers.Add(header.Key, header.Value);
        options.Apply(request);
        var returnPreference = GetRequestPreferences(request).Return;
        var preserveRepresentation = returnPreference == WebDavReturnPreference.Representation;
        using var response = await SendAsync(request, options.CancellationToken).ConfigureAwait(false);
        var result = await ReadDocumentResponseAsync(response, options.CancellationToken, preserveRepresentation).ConfigureAwait(false);
        result.WasMinimalResponseRequested = returnPreference == WebDavReturnPreference.Minimal;
        result.RequestedDepth = request.Headers.TryGetValues("Depth", out var depthValues) ? string.Join(", ", depthValues) : null;
        return result;
    }

    /// <summary>读取最终请求实际发送的偏好，包含原始 Headers 对强类型偏好的覆盖。</summary>
    /// <param name="request">已应用全部请求选项的消息。</param>
    /// <returns>解析后的偏好配置；未提供头时为空配置。</returns>
    private WebDavPreferences GetRequestPreferences(HttpRequestMessage request) =>
        WebDavPreferences.Parse(_dispatcher.GetEffectiveHeaderValue(request, "Prefer"));

    /// <summary>在保持 HTTP 状态的同时读取有界的完整 XML 文档或原始返回表示。</summary>
    /// <param name="response">需要接收正文的原始 HTTP 响应。</param>
    /// <param name="token">同时控制网络读取和文档解析的令牌。</param>
    /// <param name="preserveRepresentation">是否保留原始表示字节；非 XML 正文总会保留。</param>
    /// <returns>可在网络响应释放后继续使用的完整协议响应。</returns>
    private async Task<WebDavDocumentResponse> ReadDocumentResponseAsync(HttpResponseMessage response,
        CancellationToken token, bool preserveRepresentation = false)
    {
        // 完整 REPORT 必须保留根级字段，不能只依赖旧的逐 response 枚举器。
        // 先有界地读完正文，既可正确接受 204，也不会把非 XML 错误页误当作协议 XML。
        var bytes = await ReadBoundedProtocolBodyAsync(response.Content, token).ConfigureAwait(false);
        XElement? document = null;
        var mediaType = response.Content.Headers.ContentType?.MediaType;
        var xmlMediaType = mediaType?.EndsWith("/xml", StringComparison.OrdinalIgnoreCase) == true
            || mediaType?.EndsWith("+xml", StringComparison.OrdinalIgnoreCase) == true;
        // 表示正文属于资源本身：二进制数据恰好以 '<' 开头也不一定是 XML。
        // 仅在服务器未明确给出媒体类型时探测，207 则始终必须是完整 DAV 文档。
        var representationOnly = preserveRepresentation && response.StatusCode != HttpStatusCode.MultiStatus;
        if (bytes.Length > 0 && (xmlMediaType || (!representationOnly || mediaType is null) && LooksLikeXml(bytes)))
        {
            try
            {
                using var buffered = new HttpResponseMessage(response.StatusCode) { Content = new ByteArrayContent(bytes) };
                document = await WebDavXml.ReadDocumentAsync(buffered, _maxXmlCharacters, token).ConfigureAwait(false);
            }
            catch (XmlException) when (!response.IsSuccessStatusCode || representationOnly)
            {
                // 错误正文可能是服务器 HTML 页面；不能让它掩盖 401、403 等原始状态。
                // 原始表示即使标为 XML 也必须保留字节；期望 XML 的 REPORT 会自行要求根元素。
                // 207 不走此宽容分支，避免把损坏文档投影成空的成功资源集合。
            }
        }
        if (response.StatusCode == HttpStatusCode.MultiStatus && document?.Name != WebDavXml.Dav + "multistatus")
            throw new InvalidDataException("A DAV 207 response must contain a DAV:multistatus document.");
        var result = new WebDavDocumentResponse((int)response.StatusCode, response.ReasonPhrase, document)
        {
            Representation = document is null || preserveRepresentation ? bytes : null,
            Error = WebDavXml.ParseError(document?.Name == WebDavXml.Dav + "error"
                ? document : document?.Element(WebDavXml.Dav + "error")
                    ?? document?.Element(WebDavXml.Dav + "responsedescription")?.Element(WebDavXml.Dav + "error"))
        };
        return result.CaptureHeaders<WebDavDocumentResponse>(response);
    }

    /// <summary>有界读取协议正文，防止未知长度的错误页或返回表示无限占用内存。</summary>
    /// <param name="content">需要读取的 HTTP 正文。</param>
    /// <param name="token">正文读取令牌。</param>
    /// <returns>完整正文的独立字节数组。</returns>
    /// <exception cref="System.IO.InvalidDataException">正文超出配置允许的协议缓冲范围。</exception>
    private async Task<byte[]> ReadBoundedProtocolBodyAsync(HttpContent content, CancellationToken token)
    {
        // XML 字符可能编码为多个字节，网络缓冲上限按 UTF-8 最大四字节系数预留；
        // XmlReader 随后仍独立检查真正的字符上限。流式文件读写不走这条协议文档路径。
        var byteLimit = Math.Min(int.MaxValue, _maxXmlCharacters > int.MaxValue / 4
            ? int.MaxValue : _maxXmlCharacters * 4);
        if (content.Headers.ContentLength is { } length && length > byteLimit)
            throw new InvalidDataException("DAV response exceeds the configured protocol body limit.");
        await using var source = await content.ReadAsStreamAsync(token).ConfigureAwait(false);
        using var destination = new MemoryStream();
        var buffer = ArrayPool<byte>.Shared.Rent(81920);
        try
        {
            int count;
            while ((count = await source.ReadAsync(buffer.AsMemory(), token).ConfigureAwait(false)) != 0)
            {
                if (destination.Length > byteLimit - count)
                    throw new InvalidDataException("DAV response exceeds the configured protocol body limit.");
                destination.Write(buffer, 0, count);
            }
            return destination.ToArray();
        }
        finally { ArrayPool<byte>.Shared.Return(buffer); }
    }

    /// <summary>识别无 Content-Type 的 XML 正文，包括 UTF-8/UTF-16/UTF-32 字节序标记。</summary>
    /// <param name="bytes">完整网络正文。</param>
    /// <returns>正文以 XML 起始标记或 Unicode 字节序标记开头时为 true。</returns>
    private static bool LooksLikeXml(ReadOnlySpan<byte> bytes)
    {
        var index = 0;
        while (index < bytes.Length && bytes[index] is 9 or 10 or 13 or 32)
            index++;
        return index < bytes.Length && bytes[index] == '<'
            || bytes.StartsWith<byte>([0xEF, 0xBB, 0xBF])
            || bytes.StartsWith<byte>([0xFF, 0xFE])
            || bytes.StartsWith<byte>([0xFE, 0xFF])
            || bytes.StartsWith<byte>([0, 0, 0xFE, 0xFF]);
    }
}

