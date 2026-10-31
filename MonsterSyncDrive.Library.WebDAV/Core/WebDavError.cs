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

/// <summary>保留 DAV:error 的全部前置/后置条件及扩展 XML 详情。</summary>
/// <remarks>条件以完整名称区分，未知命名空间和嵌套内容不会被丢弃；公开元素方法返回独立副本。</remarks>
public sealed class WebDavError
{
    /// <summary>不可由外部直接修改的完整错误元素快照。</summary>
    private readonly XElement _element;

    /// <summary>从完整 DAV:error 元素建立错误快照。</summary>
    /// <param name="element">完整错误根元素，必须位于 DAV: 命名空间。</param>
    /// <exception cref="ArgumentException">元素不是 DAV:error。</exception>
    /// <exception cref="ArgumentNullException">element 为 null。</exception>
    public WebDavError(XElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        if (element.Name != WebDavXml.Dav + "error")
            throw new ArgumentException("Expected DAV:error.", nameof(element));
        _element = new XElement(element);
        Conditions = Array.AsReadOnly(_element.Elements().Select(value => new WebDavErrorCondition(value)).ToArray());
    }

    /// <summary>获取服务器声明的所有错误条件，保留名称、属性和完整嵌套内容。</summary>
    public IReadOnlyCollection<WebDavErrorCondition> Conditions { get; }

    /// <summary>判断服务器错误是否包含指定的完整条件名称。</summary>
    /// <param name="name">例如 DAV:valid-sync-token 或 DAV:lock-token-submitted。</param>
    /// <returns>存在直接错误条件时为 true，否则为 false。</returns>
    public bool Contains(XName name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return Conditions.Any(condition => condition.Name == name);
    }

    /// <summary>取得完整错误 XML 的可独立修改副本。</summary>
    /// <returns>保留所有未知条件和扩展细节的 DAV:error 元素。</returns>
    public XElement ToXElement() => new(_element);
}

/// <summary>描述 DAV:error 中的单个具名错误条件及其完整扩展数据。</summary>
public sealed class WebDavErrorCondition
{
    /// <summary>当前条件的完整 XML 快照。</summary>
    private readonly XElement _element;

    /// <summary>复制服务器提供的一个前置或后置条件。</summary>
    /// <param name="element">错误条件元素，可以属于 DAV: 或扩展命名空间。</param>
    public WebDavErrorCondition(XElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        _element = new XElement(element);
        Name = element.Name;
        Text = element.Value;
        Hrefs = Array.AsReadOnly(element.Descendants(WebDavXml.Dav + "href").Select(value => value.Value).ToArray());
    }

    /// <summary>获取包含命名空间的完整条件名称。</summary>
    public XName Name { get; }

    /// <summary>获取条件后代的解码文本，原 XML 仍由 ToXElement 保留。</summary>
    public string Text { get; }

    /// <summary>获取条件中列出的 DAV:href，例如未提交锁令牌的相关资源。</summary>
    public IReadOnlyCollection<string> Hrefs { get; }

    /// <summary>取得完整条件 XML 的独立副本。</summary>
    /// <returns>保留元素属性和混合内容的条件元素。</returns>
    public XElement ToXElement() => new(_element);
}

