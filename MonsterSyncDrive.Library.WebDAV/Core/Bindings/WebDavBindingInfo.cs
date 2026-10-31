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
using MonsterSyncDrive.Library.WebDAV.Core.Versioning;
using System.Xml.Linq;

namespace MonsterSyncDrive.Library.WebDAV.Core.Bindings;

/// <summary>表示资源的持久身份及服务器允许当前用户看到的全部父绑定。</summary>
public sealed class WebDavBindingInfo
{
    /// <summary>获取原始资源及其完整属性、状态和扩展信息。</summary>
    public WebDavResource Resource
    {
        get;
    }
    /// <summary>获取 DAV:resource-id 的全局唯一 URI；未支持或未成功返回时为 null。</summary>
    public string? ResourceId
    {
        get;
    }
    /// <summary>获取父集合和成员名称配对；不递归访问父集合，因此集合环不会导致无限遍历。</summary>
    public IReadOnlyCollection<WebDavBindingParent> Parents
    {
        get;
    }
    /// <summary>获取本实现显式查询的绑定属性，避免昂贵的 allprop。</summary>
    public static IReadOnlyCollection<XName> PropertyNames
    {
        get;
    } = Array.AsReadOnly(new[]
    {
        WebDavXml.Dav + "resource-id", WebDavXml.Dav + "parent-set", WebDavXml.Dav + "resourcetype"
    });

    /// <summary>解析已成功返回的标准属性；缺失属性保留未知状态，结构损坏则抛出异常。</summary>
    /// <param name="resource">一项多状态资源。</param>
    public WebDavBindingInfo(WebDavResource resource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        Resource = resource;
        XElement? id = VersioningXml.Property(resource, "resource-id");
        if (id is not null)
        {
            ResourceId = RequiredChild(id, "href");
            if (!Uri.TryCreate(ResourceId, UriKind.Absolute, out _))
                throw new InvalidDataException("DAV:resource-id must contain an absolute URI.");
        }
        Parents = Array.AsReadOnly((VersioningXml.Property(resource, "parent-set")?.Elements(WebDavXml.Dav + "parent") ?? [])
            .Select(parent => new WebDavBindingParent(RequiredChild(parent, "href"), RequiredChild(parent, "segment"))).ToArray());
    }

    /// <summary>读取父绑定或资源标识要求的唯一非空子元素。</summary>
    /// <param name="parent">属性或父绑定元素。</param>
    /// <param name="name">必需子元素名。</param>
    /// <returns>服务器原始文本，不对 URI 标识解码。</returns>
    private static string RequiredChild(XElement parent, string name)
    {
        XElement[] values = parent.Elements(WebDavXml.Dav + name).ToArray();
        return values.Length != 1 || string.IsNullOrWhiteSpace(values[0].Value)
            ? throw new InvalidDataException("Malformed DAV binding property: " + name)
            : values[0].Value;
    }
}

/// <summary>描述从一个集合和路径段到同一资源的绑定。</summary>
/// <param name="Collection">父集合的服务器 URI 引用。</param>
/// <param name="Segment">RFC3986 路径段文本，保留服务器转义形式。</param>
public sealed record WebDavBindingParent(string Collection, string Segment)
{
    /// <summary>获取用于显示的解码名称；构造后续协议 URI 时仍应使用 Segment。</summary>
    public string DecodedSegment => Uri.UnescapeDataString(Segment);
}

