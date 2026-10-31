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
using System.Xml;
using System.Xml.Linq;
using MonsterSyncDrive.WebDAV.Request;

namespace MonsterSyncDrive.WebDAV.Response;

/// <summary>保存 POST、PATCH 或 PUT 返回的任意资源表示流及服务器分配的位置。</summary>
/// <remarks>结果本身拥有 HTTP 响应；读取成功或失败状态正文后都需要释放。不会为了响应表示而把整个文件读入内存。</remarks>
public sealed class WebDavWriteResponse : WebDavStreamResponse
{
    /// <summary>通过显式 XML 解析得到的文档快照；未请求解析时为 null。</summary>
    private XElement? _document;

    /// <summary>创建拥有 HTTP 响应和正文流的写操作结果。</summary>
    /// <param name="response">要接管的原始响应。</param>
    /// <param name="stream">可逐步读取的响应正文。</param>
    /// <param name="requestUri">实际发送写操作的绝对地址，用于解析相对 Location。</param>
    public WebDavWriteResponse(HttpResponseMessage response, Stream stream, Uri requestUri) : base(response, stream)
    {
        ArgumentNullException.ThrowIfNull(requestUri);
        if (!requestUri.IsAbsoluteUri) throw new ArgumentException("An absolute request URI is required.", nameof(requestUri));
        RequestUri = requestUri;
        Location = ReadLocation("Location");
        ContentLocation = ReadLocation("Content-Location");
        AppliedPreferences = WebDavPreferences.Parse(GetHeader("Preference-Applied"));
    }

    /// <summary>获取产生该结果的实际请求地址。</summary>
    public Uri RequestUri { get; }

    /// <summary>获取服务器在 Location 中分配的资源绝对地址；没有返回时为 null。</summary>
    public Uri? Location { get; }

    /// <summary>获取响应表示对应的 Content-Location 绝对地址；没有返回时为 null。</summary>
    public Uri? ContentLocation { get; }

    /// <summary>获取服务器通过 Preference-Applied 明确声明已经采用的偏好。</summary>
    public WebDavPreferences AppliedPreferences { get; }

    /// <summary>获取显式 XML 解析后保留的文档副本；没有解析时为 null。</summary>
    public XElement? Document => _document is null ? null : new XElement(_document);

    /// <summary>以安全、有字符总数上限的方式读取当前 XML 表示，并提取 DAV:error。</summary>
    /// <param name="maxCharacters">最多允许的 XML 字符数，必须为正数，默认 64 × 1024 × 1024。</param>
    /// <param name="cancellationToken">取消正文解析的令牌，取消时关闭该结果的正文流。</param>
    /// <returns>完整根元素的独立副本；重复调用返回已经解析的快照。</returns>
    /// <remarks>首次调用会消费当前正文流，因此不能在已经读取一部分正文后再解析完整 XML。</remarks>
    public async Task<XElement> ReadXmlRepresentationAsync(long maxCharacters = 64 * 1024 * 1024,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxCharacters);
        cancellationToken.ThrowIfCancellationRequested();
        if (_document is not null) return new XElement(_document);
        using var registration = cancellationToken.UnsafeRegister(static state => ((Stream)state!).Dispose(), Stream);
        using var reader = XmlReader.Create(Stream, new XmlReaderSettings
        {
            Async = true, DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null,
            MaxCharactersInDocument = maxCharacters, CloseInput = false
        });
        try
        {
            // 等待原 XML 操作真正结束后才释放读取器；取消依靠关闭正文打断，避免遗弃在途 ReadAsync。
            var document = await XDocument.LoadAsync(reader, LoadOptions.PreserveWhitespace, cancellationToken).ConfigureAwait(false);
            _document = document.Root ?? throw new InvalidDataException("The XML representation has no root element.");
            Error = WebDavXml.ParseError(_document.Name == WebDavXml.Dav + "error" ? _document : _document.Element(WebDavXml.Dav + "error"));
            return new XElement(_document);
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested) { throw new OperationCanceledException(cancellationToken); }
    }

    /// <summary>把位置头中的 URI 引用按实际请求地址解析为绝对 HTTP(S) 地址。</summary>
    /// <param name="name">位置头名称。</param>
    /// <returns>已解析地址，头缺失时为 null。</returns>
    private Uri? ReadLocation(string name)
    {
        var raw = GetHeader(name);
        if (raw is null) return null;
        if (!Uri.TryCreate(RequestUri, raw, out var location) || location.Scheme is not ("http" or "https")
            || location.UserInfo.Length != 0 || location.Fragment.Length != 0)
            throw new InvalidDataException("Invalid " + name + " URI in the write response.");
        return location;
    }
}
