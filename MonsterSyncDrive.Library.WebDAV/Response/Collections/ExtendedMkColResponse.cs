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
using MonsterSyncDrive.Library.WebDAV.Core;
using System.Xml.Linq;

namespace MonsterSyncDrive.Library.WebDAV.Response.Collections;

/// <summary>保留扩展 MKCOL 的完整根元素、逐属性结果及返回资源表示。</summary>
public sealed class ExtendedMkColResponse : WebDavResponse
{
    /// <summary>创建扩展集合响应并解析 mkcol-response 的直接 propstat。</summary>
    /// <param name="response">通用请求响应，包含完整 XML 或非 XML 表示。</param>
    public ExtendedMkColResponse(WebDavDocumentResponse response)
        : base((response ?? throw new ArgumentNullException(nameof(response))).StatusCode, response.Description)
    {
        ArgumentNullException.ThrowIfNull(response);
        Document = response.Document is null ? null : new XElement(response.Document);
        Representation = response.Representation?.ToArray();
        Headers = response.Headers;
        Error = response.Error;
        Resources = response.Resources;
        List<WebDavPropertyStatus> statuses = [];
        if (Document?.Name == WebDavXml.Dav + "mkcol-response")
        {
            foreach (XElement propstat in Document.Elements(WebDavXml.Dav + "propstat"))
            {
                int status = WebDavXml.ReadStatus(propstat.Element(WebDavXml.Dav + "status"));
                XElement properties = propstat.Element(WebDavXml.Dav + "prop")
                    ?? throw new InvalidDataException("MKCOL property status has no DAV:prop.");
                foreach (XElement property in properties.Elements())
                    statuses.Add(new WebDavPropertyStatus(property.Name, status,
                        propstat.Element(WebDavXml.Dav + "responsedescription")?.Value)
                    {
                        Error = WebDavXml.ParseError(propstat.Element(WebDavXml.Dav + "error"))
                    });
            }
            if (statuses.Count == 0)
                throw new InvalidDataException("MKCOL response contains no property acknowledgements.");
        }
        PropertyStatuses = statuses.AsReadOnly();
    }

    /// <summary>获取完整 mkcol-response 或服务器返回的其他 XML 表示。</summary>
    public XElement? Document
    {
        get;
    }

    /// <summary>获取服务器返回的非 XML 原始表示；流式大文件应使用 POST/PATCH 的流式结果。</summary>
    public byte[]? Representation
    {
        get;
    }

    /// <summary>获取 mkcol-response 中每个属性的状态与错误。</summary>
    public IReadOnlyCollection<WebDavPropertyStatus> PropertyStatuses
    {
        get;
    }

    /// <summary>获取服务器返回 multistatus 表示时的资源条目。</summary>
    public IReadOnlyCollection<WebDavResource> Resources
    {
        get;
    }

    /// <summary>获取 HTTP 成功且所有已返回属性及资源均成功的结果。</summary>
    public override bool IsSuccessful => base.IsSuccessful && PropertyStatuses.All(status => status.IsSuccessful)
        && Resources.All(resource => (resource.StatusCode is null or (>= 200 and < 300))
            && resource.PropertyStatuses.All(status => status.IsSuccessful));
}
