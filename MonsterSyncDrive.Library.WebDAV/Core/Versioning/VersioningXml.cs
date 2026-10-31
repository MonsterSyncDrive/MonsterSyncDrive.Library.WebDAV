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

namespace MonsterSyncDrive.WebDAV.Core;

/// <summary>提供 DeltaV、绑定和重定向引用共用的安全 XML 构造与属性读取。</summary>
internal static class VersioningXml
{
    /// <summary>创建经过校验的 DAV:href 元素，转义非法 URI 字符并保留相对引用的形式。</summary>
    /// <param name="uri">资源 URI，不应为 null。</param>
    /// <returns>包含 URI 文本的 DAV:href。</returns>
    public static XElement Href(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        if (uri.OriginalString.Length == 0 || uri.OriginalString.Any(char.IsControl))
            throw new ArgumentException("A non-empty URI reference is required.", nameof(uri));
        // XML 转义与 URI 转义是两层不同规则。先保留 URI 的绝对/相对形式并转义空格、Unicode，
        // 再交给 XElement 处理 &amp; 等 XML 语法；绝不使用本地文件系统的路径拼接或编码规则。
        return new XElement(WebDavXml.Dav + "href", uri.GetComponents(UriComponents.SerializationInfoString, UriFormat.UriEscaped));
    }

    /// <summary>创建指定 DAV 容器并填入 href 列表。</summary>
    /// <param name="name">DAV 容器本地名称。</param>
    /// <param name="uris">要列出的 URI 集合。</param>
    /// <returns>包含各个 href 的容器元素。</returns>
    public static XElement Hrefs(string name, IEnumerable<Uri> uris) =>
        new(WebDavXml.Dav + name, uris.Select(Href));

    /// <summary>生成指定属性名称列表；空列表仍保留空 prop，避免变成隐式 allprop。</summary>
    /// <param name="properties">需要查询的完整属性名称。</param>
    /// <returns>DAV:prop 元素。</returns>
    public static XElement Prop(IEnumerable<XName> properties) =>
        new(WebDavXml.Dav + "prop", properties.Select(name => new XElement(name)));

    /// <summary>从资源的成功属性中取出完整属性元素副本。</summary>
    /// <param name="resource">资源快照。</param>
    /// <param name="localName">DAV 属性本地名称。</param>
    /// <returns>完整属性元素，未成功返回时为 null。</returns>
    public static XElement? Property(WebDavResource resource, string localName) =>
        resource.Properties.FirstOrDefault(property => property.Name == WebDavXml.Dav + localName)?.ToXElement();

    /// <summary>读取成功属性下直接 href 子项，不遍历到嵌套报告的其他资源。</summary>
    /// <param name="resource">资源快照。</param>
    /// <param name="localName">href 容器名称。</param>
    /// <returns>保持服务器顺序的 URI 引用文本集合。</returns>
    public static IReadOnlyCollection<string> ReadHrefs(WebDavResource resource, string localName) =>
        Property(resource, localName)?.Elements(WebDavXml.Dav + "href").Select(element => element.Value).ToArray() ?? [];

    /// <summary>为请求根元素复制扩展节点，使调用者的 XML 树不被重新挂载或修改。</summary>
    /// <param name="root">接收扩展的正文根。</param>
    /// <param name="extensions">扩展节点集合。</param>
    /// <returns>添加扩展后的同一根元素。</returns>
    public static XElement AddExtensions(XElement root, IEnumerable<XElement> extensions)
    {
        foreach (var extension in extensions)
        {
            ArgumentNullException.ThrowIfNull(extension);
            root.Add(new XElement(extension));
        }
        return root;
    }
}

