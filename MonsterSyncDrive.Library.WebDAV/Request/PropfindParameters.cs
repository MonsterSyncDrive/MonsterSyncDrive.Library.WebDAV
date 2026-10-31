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
using System.Net.Http.Headers;
using System.Xml.Linq;

namespace MonsterSyncDrive.Library.WebDAV.Request;

/// <summary>
/// 配置 PROPFIND 查询模式、属性名称、命名空间及深度。
/// </summary>
/// <remarks>默认 allprop 和 Depth: 1；CustomProperties 在指定属性模式中组成 prop，在 allprop 模式中组成 include。</remarks>
public class PropfindParameters : WebDavRequestOptions
{
    /// <summary>
    /// 初始化 PropfindParameters 的默认请求设置。
    /// </summary>
    /// <remarks>使用 AllProperties、空属性和命名空间集合，以及 XML UTF-8 正文；深度为 null，由客户端默认采用 Depth: 1。</remarks>
    public PropfindParameters()
    {
        RequestType = PropfindRequestType.AllProperties;
        CustomProperties = [];
        Namespaces = [];
        ContentType = MediaTypes.XmlMediaType;
    }

    /// <summary>
    /// 获取或设置属性查询模式。
    /// </summary>
    /// <value>默认 AllProperties，发送 allprop；其他模式可指定名称、仅名称或省略正文。</value>
    public PropfindRequestType RequestType
    {
        get; set;
    }

    /// <summary>
    /// 获取或设置要显式查询的完整属性名称集合。
    /// </summary>
    /// <value>默认空集合；NamedProperties 使用 prop，AllProperties 使用 include，其余查询模式不使用此集合。</value>
    public IReadOnlyCollection<XName> CustomProperties
    {
        get; set;
    }

    /// <summary>
    /// 获取或设置请求属性上显式声明的命名空间集合。
    /// </summary>
    /// <value>默认空集合；声明只控制 XML 前缀，不改变 CustomProperties 中 XName 的身份。</value>
    public IReadOnlyCollection<NamespaceAttr> Namespaces
    {
        get; set;
    }

    /// <summary>
    /// 获取或设置资源查询深度。
    /// </summary>
    /// <value>null 默认 Depth: 1；可显式选择当前资源、直接子项或全部后代。</value>
    public ApplyTo.Propfind? ApplyTo
    {
        get; set;
    }

    /// <summary>
    /// 获取或设置 XML 请求正文的媒体类型。
    /// </summary>
    /// <value>默认 application/xml; charset=utf-8；正文固定用 UTF-8 编码，调用方指定的字符集应与实际编码保持一致。</value>
    public MediaTypeHeaderValue ContentType
    {
        get; set;
    }
}
