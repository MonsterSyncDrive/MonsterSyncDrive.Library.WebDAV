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
/// 生成新建写锁所需的 DAV:lockinfo XML 正文。
/// </summary>
/// <remarks>锁模式转换为 shared 或 exclusive；所有者按具体类型写成文本或 href，避免把普通文本误当成 XML。</remarks>
internal static class LockRequestBuilder
{
    /// <summary>
    /// 构建新建写锁的 DAV:lockinfo 正文，包含锁范围、write 类型和可选所有者。
    /// </summary>
    /// <param name="lockParams">新锁的模式和所有者配置；续期请求不使用此正文构建方法。</param>
    /// <returns>带 UTF-8 XML 声明的请求正文字符串。</returns>
    public static string BuildRequestBody(LockParameters lockParams)
    {
        var doc = new XDocument(new XDeclaration("1.0", "utf-8", null));
        var lockinfo = new XElement(
            "{DAV:}lockinfo",
            new XAttribute(XNamespace.Xmlns + "D", "DAV:"),
            GetLockScope(lockParams.LockScope),
            new XElement(
                "{DAV:}locktype",
                new XElement("{DAV:}write")
            )
        );
        if (lockParams.Owner != null)
            lockinfo.Add(GetLockOwner(lockParams.Owner));


        doc.Add(lockinfo);
        return doc.ToStringWithDeclaration();
    }

    /// <summary>
    /// 将共享或独占模式转换为 DAV:lockscope 子树。
    /// </summary>
    /// <param name="lockScope">要表达的写锁模式。</param>
    /// <returns>包含 DAV:shared 或 DAV:exclusive 的 lockscope 元素。</returns>
    /// <exception cref="ArgumentOutOfRangeException">提供的枚举值不是 Shared 或 Exclusive。</exception>
    private static XElement GetLockScope(LockScope lockScope)
    {
        var lockscope = new XElement("{DAV:}lockscope");
        switch (lockScope)
        {
            case LockScope.Shared:
                lockscope.Add(new XElement("{DAV:}shared"));
                break;
            case LockScope.Exclusive:
                lockscope.Add(new XElement("{DAV:}exclusive"));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(lockScope));
        }
        return lockscope;
    }

    /// <summary>
    /// 委托所有者模型生成普通文本、URI 或任意结构化 owner 元素。
    /// </summary>
    /// <param name="lockOwner">提供有效 DAV:owner 的所有者模型。</param>
    /// <returns>独立的 DAV:owner 元素及完整内容。</returns>
    private static XElement GetLockOwner(LockOwner lockOwner) => lockOwner.ToXElement();
}
