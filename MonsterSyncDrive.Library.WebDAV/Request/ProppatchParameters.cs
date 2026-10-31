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
/// 配置 PROPPATCH 要设置或删除的属性，以及锁和版本条件。
/// </summary>
/// <remarks>字符串值按普通文本转义；具有元素属性或嵌套 XML 的值使用 XmlPropertiesToSet，结果应检查逐属性状态。</remarks>
public class ProppatchParameters : WebDavRequestOptions
{
    /// <summary>
    /// 获取或设置完整的结构化属性元素，支持元素属性、嵌套 XML 与混合内容。
    /// </summary>
    /// <value>默认空集合；发送时复制各元素，不把字符串值当成 XML 片段。</value>
    public IReadOnlyCollection<XElement> XmlPropertiesToSet { get; set; } = [];
    /// <summary>
    /// 初始化 ProppatchParameters 的默认请求设置。
    /// </summary>
    /// <remarks>设置、删除及结构化属性集合均为空，使用 XML UTF-8 正文和默认取消令牌。</remarks>
    public ProppatchParameters()
    {
        PropertiesToSet = new Dictionary<XName, string>();
        PropertiesToRemove = [];
        Namespaces = [];
        ContentType = MediaTypes.XmlMediaType;
    }

    /// <summary>
    /// 获取或设置以完整属性名为键的普通文本属性值。
    /// </summary>
    /// <value>默认空字典；内容按 XML 文本转义，需要嵌套 XML 时使用 XmlPropertiesToSet。</value>
    public IDictionary<XName, string> PropertiesToSet
    {
        get; set;
    }

    /// <summary>
    /// 获取或设置要删除的完整属性名称集合。
    /// </summary>
    /// <value>默认空集合；发送对应的 DAV:remove/DAV:prop 元素。</value>
    public IReadOnlyCollection<XName> PropertiesToRemove
    {
        get; set;
    }

    /// <summary>
    /// 获取或设置 propertyupdate 根元素上的显式命名空间声明。
    /// </summary>
    /// <value>默认空集合，不改变属性 XName 中的完整命名空间。</value>
    public IReadOnlyCollection<NamespaceAttr> Namespaces
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

    /// <summary>
    /// 获取或设置修改资源属性时提交的写锁令牌。
    /// </summary>
    /// <value>默认 null；与 HTTP If-Match 等版本条件可以同时使用。</value>
    public string? LockToken
    {
        get; set;
    }
}
