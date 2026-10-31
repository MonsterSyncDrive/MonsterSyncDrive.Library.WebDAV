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

namespace MonsterSyncDrive.Library.WebDAV.Core;

/// <summary>表示 DAV:supportedlock 中的一项锁模式与锁类型能力。</summary>
/// <remarks>未知的锁模式或类型仍保留原 XML，不把未来扩展错误地解释成标准共享写锁。</remarks>
public sealed class SupportedLock
{
    /// <summary>服务器返回的完整 DAV:lockentry 元素快照。</summary>
    private readonly XElement _element;

    /// <summary>复制并解析一个锁能力条目。</summary>
    /// <param name="element">DAV:supportedlock 中的 DAV:lockentry 元素。</param>
    /// <exception cref="ArgumentException">元素不是 DAV:lockentry。</exception>
    public SupportedLock(XElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        if (element.Name != WebDavXml.Dav + "lockentry")
            throw new ArgumentException("Expected DAV:lockentry.", nameof(element));
        _element = new XElement(element);
        XElement? scope = element.Element(WebDavXml.Dav + "lockscope");
        Scope = scope?.Element(WebDavXml.Dav + "exclusive") is not null ? LockScope.Exclusive
            : scope?.Element(WebDavXml.Dav + "shared") is not null ? LockScope.Shared : null;
        LockType = element.Element(WebDavXml.Dav + "locktype")?.Elements().FirstOrDefault()?.Name;
    }

    /// <summary>获取共享或独占锁模式，未识别扩展模式时为 null。</summary>
    public LockScope? Scope
    {
        get;
    }

    /// <summary>获取完整锁类型名称，例如 DAV:write；缺失时为 null。</summary>
    public XName? LockType
    {
        get;
    }

    /// <summary>获取当前条目是否声明支持标准 DAV 写锁。</summary>
    public bool IsWriteLock => LockType == WebDavXml.Dav + "write";

    /// <summary>取得服务器完整能力条目的独立 XML 副本。</summary>
    /// <returns>DAV:lockentry 元素及其扩展内容。</returns>
    public XElement ToXElement()
    {
        return new(_element);
    }
}

