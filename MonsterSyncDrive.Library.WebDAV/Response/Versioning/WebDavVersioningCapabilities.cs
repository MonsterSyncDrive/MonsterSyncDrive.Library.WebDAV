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

namespace MonsterSyncDrive.WebDAV.Response;

/// <summary>保存 OPTIONS 声明的 DeltaV 功能与可创建历史、工作区和活动的集合。</summary>
public sealed class WebDavVersioningCapabilities : WebDavResponse
{
    /// <summary>获取 DAV 头中声明的功能标识，保留未知扩展。</summary>
    public IReadOnlyCollection<string> Features { get; }
    /// <summary>获取可创建版本历史的集合 URI。</summary>
    public IReadOnlyCollection<string> VersionHistoryCollections { get; }
    /// <summary>获取可创建工作区的集合 URI。</summary>
    public IReadOnlyCollection<string> WorkspaceCollections { get; }
    /// <summary>获取可创建活动的集合 URI。</summary>
    public IReadOnlyCollection<string> ActivityCollections { get; }
    /// <summary>获取完整 OPTIONS XML 文档，保留未知能力字段。</summary>
    public XElement? Document { get; }

    /// <summary>从 OPTIONS 响应解析功能标识与服务器创建位置。</summary>
    /// <param name="source">完整且已物化的响应。</param>
    internal WebDavVersioningCapabilities(WebDavDocumentResponse source) : base(source.StatusCode, source.Description)
    {
        Headers = source.Headers;
        Error = source.Error;
        Document = source.Document is null ? null : new XElement(source.Document);
        if (source.StatusCode is >= 200 and < 300 && Document is not null && Document.Name != WebDavXml.Dav + "options-response")
            throw new InvalidDataException("DeltaV OPTIONS returned an unexpected document.");
        Features = Array.AsReadOnly((GetHeader("DAV") ?? "").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
        VersionHistoryCollections = Locations("version-history-collection-set");
        WorkspaceCollections = Locations("workspace-collection-set");
        ActivityCollections = Locations("activity-collection-set");
    }

    /// <summary>判断服务器是否声明指定 DeltaV 功能。</summary>
    /// <param name="feature">例如 version-control、checkout-in-place、workspace、merge、baseline 或 activity。</param>
    /// <returns>功能标识存在时为 true；只反映服务器声明，不执行探测性写操作。</returns>
    public bool Supports(string feature) => Features.Contains(feature, StringComparer.Ordinal);

    /// <summary>读取某种创建位置的 href 列表，缺失时保留为空集合。</summary>
    /// <param name="name">DAV 位置集合名称。</param>
    /// <returns>服务器声明的创建位置 URI 引用。</returns>
    private IReadOnlyCollection<string> Locations(string name) =>
        Document?.Element(WebDavXml.Dav + name)?.Elements(WebDavXml.Dav + "href").Select(element => element.Value).ToArray() ?? [];
}
