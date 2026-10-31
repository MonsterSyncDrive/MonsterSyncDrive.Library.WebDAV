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

namespace MonsterSyncDrive.Library.WebDAV.Request;

/// <summary>
/// 生成 PROPPATCH 设置标量属性及删除属性的 XML 正文。
/// </summary>
/// <remarks>结构化 XML 属性由客户端追加；字符串值始终作为文本处理，防止把普通内容误解释为 XML。</remarks>
internal static class ProppatchRequestBuilder
{
    /// <summary>
    /// 构造属性更新文档，以 set 保存文本属性、以 remove 删除属性。
    /// </summary>
    /// <remarks>使用 XElement.Value 写入字符串，自动转义尖括号和 &amp;；不把字符串值当作可执行的 XML 片段。</remarks>
    /// <param name="propertiesToSet">以完整属性名为键、普通文本内容为值的设置集合。</param>
    /// <param name="propertiesToRemove">需要删除的属性完整名称集合。</param>
    /// <param name="namespaces">propertyupdate 根元素的显式命名空间声明。</param>
    /// <returns>DAV:propertyupdate 根元素，供客户端追加结构化属性后统一序列化。</returns>
    public static XElement BuildRequest(IDictionary<XName, string> propertiesToSet,
        IReadOnlyCollection<XName> propertiesToRemove,
        IReadOnlyCollection<NamespaceAttr> namespaces)
    {
        XElement propertyupdate = new("{DAV:}propertyupdate", new XAttribute(XNamespace.Xmlns + "D", "DAV:"));
        foreach (NamespaceAttr ns in namespaces)
        {
            XName nsAttr = string.IsNullOrEmpty(ns.Prefix) ? "xmlns" : XNamespace.Xmlns + ns.Prefix;
            propertyupdate.SetAttributeValue(nsAttr, ns.Namespace);
        }
        if (propertiesToSet.Count != 0)
        {
            // 字符串值按文本转义，完整 XML 属性由客户端另行追加。
            propertyupdate.Add(new XElement(WebDavXml.Dav + "set",
                new XElement(WebDavXml.Dav + "prop", propertiesToSet.Select(prop => new XElement(prop.Key) { Value = prop.Value }))));
        }

        if (propertiesToRemove.Count != 0)
        {
            propertyupdate.Add(new XElement(WebDavXml.Dav + "remove",
                new XElement(WebDavXml.Dav + "prop", propertiesToRemove.Select(prop => new XElement(prop)))));
        }

        return propertyupdate;
    }
}
