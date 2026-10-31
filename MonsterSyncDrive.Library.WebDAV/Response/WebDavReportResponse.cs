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
using System.Xml.Linq;
using MonsterSyncDrive.WebDAV.Core;

namespace MonsterSyncDrive.WebDAV.Response;

/// <summary>
/// 保存通用 XML REPORT 的完整根元素及可选多状态资源，兼容原有 PropfindResponse 使用方式。
/// </summary>
/// <remarks>根级属性、扩展元素、sync-token、非 multistatus 报告内容都保留在 Document 中；没有正文时为 null。</remarks>
public class WebDavReportResponse : PropfindResponse
{
    /// <summary>独立保存的完整 XML 根快照，防止外部 XElement 修改影响后续令牌读取。</summary>
    private readonly XElement? _document;

    /// <summary>
    /// 从 HTTP 状态、完整报告根和已解析资源创建报告快照。
    /// </summary>
    /// <param name="statusCode">HTTP 状态码。</param>
    /// <param name="description">可选原因说明。</param>
    /// <param name="document">完整 XML 根元素，允许非 DAV:multistatus 报告；无正文时为 null。</param>
    /// <param name="resources">已经解析的多状态资源；null 时从 multistatus 的直接 response 元素解析。</param>
    public WebDavReportResponse(int statusCode, string? description, XElement? document,
        IEnumerable<WebDavResource>? resources = null)
        : base(statusCode, description, resources ?? ParseResources(document))
    {
        _document = document is null ? null : new XElement(document);
    }

    /// <summary>获取完整 XML 根的独立副本，保留根属性、命名空间及全部扩展子元素。</summary>
    public XElement? Document => _document is null ? null : new XElement(_document);

    /// <summary>获取根级 DAV:sync-token 的原始文本；普通报告没有该字段时为 null。</summary>
    public string? SyncToken => _document?.Element(WebDavXml.Dav + "sync-token")?.Value;

    /// <summary>获取需要保留的原始非 XML 错误正文或 Prefer 返回表示字节；普通 XML 报告通常为 null。</summary>
    public byte[]? Representation { get; internal set; }

    /// <summary>获取根级除 DAV:response 之外的全部元素副本，包括同步令牌和未知扩展内容。</summary>
    public IReadOnlyCollection<XElement> RootExtensions => _document is null ? []
        : Array.AsReadOnly(_document.Elements().Where(element => element.Name != WebDavXml.Dav + "response")
            .Select(element => new XElement(element)).ToArray());

    /// <summary>
    /// 只从标准多状态根中解析资源，其他报告结构完整保留但不强行解释为目录。
    /// </summary>
    /// <param name="document">可选完整根元素。</param>
    /// <returns>多状态资源或空集合。</returns>
    private static IEnumerable<WebDavResource> ParseResources(XElement? document) =>
        document?.Name == WebDavXml.Dav + "multistatus"
            ? document.Elements(WebDavXml.Dav + "response").Select(WebDavXml.ParseResource).ToArray()
            : [];
}

