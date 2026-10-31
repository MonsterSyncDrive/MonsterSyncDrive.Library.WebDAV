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

namespace MonsterSyncDrive.Library.WebDAV.Request.Collections;

/// <summary>配置 RFC 5689 扩展 MKCOL 的资源类型和创建时原子设置的属性。</summary>
public sealed class ExtendedMkColParameters : WebDavRequestOptions
{
    /// <summary>获取或设置集合的完整资源类型名称，必须包含 DAV:collection。</summary>
    public IReadOnlyCollection<XName> ResourceTypes { get; set; } = [WebDavXml.Dav + "collection"];

    /// <summary>获取或设置创建时写入的普通文本属性，值由 XML 序列化器转义。</summary>
    public IDictionary<XName, string> PropertiesToSet { get; set; } = new Dictionary<XName, string>();

    /// <summary>获取或设置完整 XML 属性元素，用于保留属性和混合内容。</summary>
    public IReadOnlyCollection<XElement> XmlPropertiesToSet { get; set; } = [];

    /// <summary>获取或设置创建资源时提交的锁令牌；显式 IfConditions 或 Headers 可覆盖其简化条件。</summary>
    public string? LockToken
    {
        get; set;
    }

    /// <summary>构造不可与普通属性混淆的扩展 MKCOL XML 正文。</summary>
    /// <returns>包含 DAV:set/DAV:prop 的 DAV:mkcol 元素。</returns>
    internal XElement CreateBody()
    {
        ArgumentNullException.ThrowIfNull(ResourceTypes);
        ArgumentNullException.ThrowIfNull(PropertiesToSet);
        ArgumentNullException.ThrowIfNull(XmlPropertiesToSet);
        XNamespace dav = WebDavXml.Dav;
        if (!ResourceTypes.Contains(dav + "collection"))
            throw new ArgumentException("Extended MKCOL resourcetype must include DAV:collection.", nameof(ResourceTypes));
        if (ResourceTypes.Any(name => name is null || name.NamespaceName.Length == 0))
            throw new ArgumentException("Resource types must use qualified XML names.", nameof(ResourceTypes));
        if (XmlPropertiesToSet.Any(element => element is null))
            throw new ArgumentException("XML property elements cannot be null.", nameof(XmlPropertiesToSet));
        XName[] names = PropertiesToSet.Keys.Concat(XmlPropertiesToSet.Select(element => element.Name)).ToArray();
        // resourcetype 只能由 ResourceTypes 生成一次，否则不同设置分组可能对同一属性提出冲突要求。
        return names.Contains(dav + "resourcetype") || names.Distinct().Count() != names.Length
            ? throw new ArgumentException("A property may occur only once; set resourcetype using ResourceTypes.")
            : new XElement(dav + "mkcol", new XElement(dav + "set", new XElement(dav + "prop",
            new XElement(dav + "resourcetype", ResourceTypes.Distinct().Select(name => new XElement(name))),
            PropertiesToSet.Select(property => new XElement(property.Key, property.Value)),
            XmlPropertiesToSet.Select(element => new XElement(element)))));
    }
}
