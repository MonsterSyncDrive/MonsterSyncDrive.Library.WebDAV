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

namespace MonsterSyncDrive.Library.WebDAV.Request;

/// <summary>表示 principal-property-search 的一组属性及其匹配文本。</summary>
/// <remarks>同组属性及不同组条件均按 RFC 3744 进行逻辑 AND；文本匹配规则由服务器定义，不等同 DASL 查询。</remarks>
public sealed class WebDavPrincipalPropertySearch
{
    /// <summary>使用非空属性名称集合和匹配文本创建搜索条件。</summary>
    /// <param name="properties">参与匹配的完整属性名称。</param>
    /// <param name="match">匹配文本；保留原始文本并按 XML 自动转义。</param>
    public WebDavPrincipalPropertySearch(IEnumerable<XName> properties, string match)
    {
        ArgumentNullException.ThrowIfNull(properties);
        ArgumentNullException.ThrowIfNull(match);
        XName[] names = properties.Distinct().ToArray();
        if (names.Length == 0 || names.Any(name => name is null))
            throw new ArgumentException("A principal search requires at least one property.", nameof(properties));
        Properties = Array.AsReadOnly(names);
        Match = match;
    }

    /// <summary>获取需要同时匹配的属性完整名称。</summary>
    public IReadOnlyList<XName> Properties
    {
        get;
    }

    /// <summary>获取服务器应匹配的原始文本。</summary>
    public string Match
    {
        get;
    }

    /// <summary>生成 RFC 3744 的 property-search XML。</summary>
    /// <returns>独立的完整搜索条件元素。</returns>
    public XElement ToXElement()
    {
        return new(WebDavXml.Dav + "property-search",
        new XElement(WebDavXml.Dav + "prop", Properties.Select(name => new XElement(name))),
        new XElement(WebDavXml.Dav + "match", Match));
    }
}

/// <summary>配置主体属性搜索条件、结果属性以及主体集合搜索范围。</summary>
public sealed class WebDavPrincipalSearchParameters : WebDavRequestOptions
{
    /// <summary>获取或设置需要同时满足的搜索条件，至少一项。</summary>
    public IReadOnlyCollection<WebDavPrincipalPropertySearch> Searches { get; set; } = [];

    /// <summary>获取或设置搜索结果中希望读取的属性；空集合表示服务器的默认结果。</summary>
    public IReadOnlyCollection<XName> ReturnProperties { get; set; } = [];

    /// <summary>获取或设置是否改为搜索请求资源 DAV:principal-collection-set 中的所有主体集合。</summary>
    public bool ApplyToPrincipalCollectionSet
    {
        get; set;
    }
}

