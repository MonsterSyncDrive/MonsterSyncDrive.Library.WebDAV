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
using MonsterSyncDrive.WebDAV.Core;
using MonsterSyncDrive.WebDAV.Helpers;
using System.Xml.Linq;

namespace MonsterSyncDrive.WebDAV.Request;

/// <summary>
/// 生成 PROPFIND 各种查询形式的 DAV XML 正文。
/// </summary>
/// <remarks>属性名称通过 XName 序列化；无正文的隐式 allprop 由客户端直接处理。</remarks>
internal static class PropfindRequestBuilder
{
    /// <summary>
    /// 根据属性查询模式生成 PROPFIND 的 XML 正文。
    /// </summary>
    /// <param name="requestType">需要显式正文的查询模式；AllPropertiesImplied 由客户端省略正文处理。</param>
    /// <param name="customProperties">显式属性名称，用于 prop 或 allprop/include。</param>
    /// <param name="namespaces">需要在查询元素中显式声明的命名空间。</param>
    /// <returns>allprop、prop 或 propname 形式的 XML 字符串。</returns>
    /// <exception cref="ArgumentOutOfRangeException">查询类型不能由此构建器生成显式正文。</exception>
    public static string BuildRequest(
        PropfindRequestType requestType,
        IReadOnlyCollection<XName> customProperties,
        IReadOnlyCollection<NamespaceAttr> namespaces) => requestType switch
        {
            PropfindRequestType.NamedProperties => BuildNamedPropRequest(customProperties, namespaces),
            PropfindRequestType.AllProperties => BuildAllPropRequest(customProperties, namespaces),
            PropfindRequestType.PropertyNames => new XElement("{DAV:}propfind", new XElement("{DAV:}propname")).ToString(),
            _ => throw new ArgumentOutOfRangeException(nameof(requestType))
        };

    /// <summary>
    /// 构建 allprop 请求，并在存在自定义属性时附加 include 元素。
    /// </summary>
    /// <param name="customProperties">额外请求的属性完整名称；空集合时只发送 allprop。</param>
    /// <param name="namespaces">附加到 include 上的命名空间声明。</param>
    /// <returns>带 XML 声明的 DAV:propfind 文档。</returns>
    private static string BuildAllPropRequest(IReadOnlyCollection<XName> customProperties, IReadOnlyCollection<NamespaceAttr> namespaces)
    {
        var doc = new XDocument(new XDeclaration("1.0", "utf-8", null));
        var propfind = new XElement(
            "{DAV:}propfind",
            new XAttribute(XNamespace.Xmlns + "D", "DAV:"),
            new XElement("{DAV:}allprop")
        );
        if (customProperties.Count != 0)
        {
            // allprop 不保证返回服务器的每一种扩展属性，include 用于显式要求这些额外属性。
            var include = new XElement("{DAV:}include");
            foreach (var ns in namespaces)
            {
                var nsAttr = string.IsNullOrEmpty(ns.Prefix) ? "xmlns" : XNamespace.Xmlns + ns.Prefix;
                include.SetAttributeValue(nsAttr, ns.Namespace);
            }
            foreach (var prop in customProperties)
            {
                include.Add(new XElement(prop));
            }
            propfind.Add(include);
        }
        doc.Add(propfind);
        return doc.ToStringWithDeclaration();
    }

    /// <summary>
    /// 构建只查询指定属性的 DAV:propfind/DAV:prop 正文。
    /// </summary>
    /// <param name="customProperties">要查询的完整属性名称集合。</param>
    /// <param name="namespaces">声明在 prop 元素上的命名空间前缀。</param>
    /// <returns>带 XML 声明的指定属性查询文档；属性列表为空时仍包含空 prop。</returns>
    private static string BuildNamedPropRequest(IReadOnlyCollection<XName> customProperties, IReadOnlyCollection<NamespaceAttr> namespaces)
    {
        var doc = new XDocument(new XDeclaration("1.0", "utf-8", null));
        var propfind = new XElement("{DAV:}propfind", new XAttribute(XNamespace.Xmlns + "D", "DAV:"));
        // 即使没有指定属性，也保留完整的 prop 元素。
        var propEl = new XElement("{DAV:}prop", customProperties.Select(prop => new XElement(prop)));
        foreach (var ns in namespaces)
        {
            var nsAttr = string.IsNullOrEmpty(ns.Prefix) ? "xmlns" : XNamespace.Xmlns + ns.Prefix;
            propEl.SetAttributeValue(nsAttr, ns.Namespace);
        }
        propfind.Add(propEl);
        doc.Add(propfind);
        return doc.ToStringWithDeclaration();
    }
}
