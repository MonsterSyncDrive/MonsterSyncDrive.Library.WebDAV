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
using MonsterSyncDrive.WebDAV.Helpers;

namespace MonsterSyncDrive.WebDAV.Request;

/// <summary>一个独立的搜索范围，支持多集合检索、深度及包含历史版本的标准选项。</summary>
public sealed class WebDavSearchScope
{
    /// <summary>创建搜索范围。</summary>
    /// <param name="href">范围 URI 引用，不应为空。</param>
    /// <param name="depth">当前资源、直接子项或全部后代，默认 infinity。</param>
    /// <param name="includeVersions">是否请求把该范围的历史版本纳入搜索。</param>
    public WebDavSearchScope(string href, ApplyTo.Propfind depth = ApplyTo.Propfind.ResourceAndDescendants, bool includeVersions = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(href);
        if (!Uri.TryCreate(href, UriKind.RelativeOrAbsolute, out _))
            throw new ArgumentException("Search scope is not a valid URI reference.", nameof(href));
        _ = DepthHeaderHelper.GetValueForPropfind(depth);
        Href = href;
        Depth = depth;
        IncludeVersions = includeVersions;
    }

    /// <summary>获取由服务器解释的搜索范围 URI。</summary>
    public string Href { get; }

    /// <summary>获取该范围的搜索深度。</summary>
    public ApplyTo.Propfind Depth { get; }

    /// <summary>获取是否包含历史版本；服务器不支持时会返回协议错误。</summary>
    public bool IncludeVersions { get; }

    /// <summary>构建按 RFC 5323 顺序排列的 href、depth 和可选 include-versions。</summary>
    /// <returns>独立的 DAV:scope 元素。</returns>
    public XElement ToXElement() => new(WebDavXml.Dav + "scope",
        new XElement(WebDavXml.Dav + "href", Href),
        new XElement(WebDavXml.Dav + "depth", DepthHeaderHelper.GetValueForPropfind(Depth)),
        IncludeVersions ? new XElement(WebDavXml.Dav + "include-versions") : null);
}

/// <summary>一个搜索排序键，多实例按集合顺序组成稳定的多字段优先级。</summary>
public sealed class WebDavSearchOrder
{
    /// <summary>创建属性或全文相关性分数的排序键。</summary>
    /// <param name="property">完整属性名；null 表示 DAV:score。</param>
    /// <param name="descending">true 降序，false 升序。</param>
    /// <param name="caseless">可选不区分大小写设置，null 保留服务器默认。</param>
    public WebDavSearchOrder(XName? property = null, bool descending = false, bool? caseless = null)
    {
        Property = property;
        Descending = descending;
        Caseless = caseless;
    }

    /// <summary>获取排序属性名称；null 表示按全文相关性分数排序。</summary>
    public XName? Property { get; }

    /// <summary>获取是否按降序排列。</summary>
    public bool Descending { get; }

    /// <summary>获取可选大小写比较设置。</summary>
    public bool? Caseless { get; }

    /// <summary>构造单个 DAV:order，分数字段使用 score 而不是 prop/score。</summary>
    /// <returns>独立的排序元素。</returns>
    public XElement ToXElement()
    {
        var element = new XElement(WebDavXml.Dav + "order",
            Property is null ? new XElement(WebDavXml.Dav + "score")
                : new XElement(WebDavXml.Dav + "prop", new XElement(Property)),
            new XElement(WebDavXml.Dav + (Descending ? "descending" : "ascending")));
        WebDavSearchExpression.AddCaseless(element, Caseless);
        return element;
    }
}