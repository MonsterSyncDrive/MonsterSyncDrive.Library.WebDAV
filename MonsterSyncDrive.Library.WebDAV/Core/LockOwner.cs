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

/// <summary>
/// 表示写锁所有者信息的抽象基类。
/// </summary>
/// <remarks>服务器把该值作为所有者说明保存；它不替代 HTTP 身份认证，也不证明当前用户持有该锁。</remarks>
public abstract class LockOwner
{
    /// <summary>
    /// 获取写入锁所有者元素的文本或绝对 URI。
    /// </summary>
    public abstract string Value
    {
        get;
    }

    /// <summary>生成可写入 LOCK 请求的完整所有者 XML。</summary>
    /// <returns>普通所有者使用文本，URI 所有者使用 DAV:href，结构化派生类型可重写此方法。</returns>
    public virtual XElement ToXElement()
    {
        return this is UriLockOwner
        ? new XElement(WebDavXml.Dav + "owner", new XElement(WebDavXml.Dav + "href", Value))
        : new XElement(WebDavXml.Dav + "owner", Value);
    }
}
