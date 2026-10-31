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

/// <summary>保存任意 DAV:owner XML，支持元素属性、扩展节点和混合内容。</summary>
/// <remarks>所有者只是说明数据，不代表当前认证主体。构造和输出都复制 XML，避免修改响应快照。</remarks>
public sealed class XmlLockOwner : LockOwner
{
    /// <summary>完整的 DAV:owner 元素快照。</summary>
    private readonly XElement _owner;

    /// <summary>通过完整 owner 元素构造所有者信息。</summary>
    /// <param name="owner">DAV:owner 元素，可以包含任意合法子节点与属性。</param>
    /// <exception cref="ArgumentException">owner 不是 DAV:owner 元素。</exception>
    public XmlLockOwner(XElement owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        if (owner.Name != WebDavXml.Dav + "owner")
            throw new ArgumentException("Expected DAV:owner.", nameof(owner));
        _owner = new XElement(owner);
    }

    /// <summary>获取所有者节点的合并文本；结构化内容通过 ToXElement 读取。</summary>
    public override string Value => _owner.Value;

    /// <summary>输出保留属性、命名空间和混合内容的所有者副本。</summary>
    /// <returns>可以直接写入 lockinfo 的 DAV:owner 元素。</returns>
    public override XElement ToXElement()
    {
        return new(_owner);
    }
}

