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

namespace MonsterSyncDrive.Library.WebDAV.Request;

/// <summary>
/// 配置创建写锁或刷新已有写锁时的范围、所有者和租约。
/// </summary>
/// <remarks>LockToken 非 null 时发送无正文的续期 LOCK；否则生成包含锁模式和所有者信息的新锁正文。</remarks>
public class LockParameters : WebDavRequestOptions
{
    /// <summary>
    /// 获取或设置要续期的已有锁令牌。
    /// </summary>
    /// <value>非 null 时发送空正文 LOCK 并提交 If 条件；null 表示创建新锁。</value>
    public string? LockToken
    {
        get; set;
    }
    /// <summary>
    /// 初始化 LockParameters 的默认请求设置。
    /// </summary>
    /// <remarks>使用 XML UTF-8 正文、Shared 锁模式、空请求头及默认取消令牌；深度和租约未指定。</remarks>
    public LockParameters()
    {
        ContentType = MediaTypes.XmlMediaType;
    }

    /// <summary>
    /// 获取或设置新建锁的共享或独占模式。
    /// </summary>
    /// <value>默认枚举值 Shared；续期已有锁时不重新发送 lockinfo，因此此值不参与续期正文。</value>
    public LockScope LockScope
    {
        get; set;
    }

    /// <summary>
    /// 获取或设置请求的服务器锁租约。
    /// </summary>
    /// <value>null 不发送 Timeout；Timeout.InfiniteTimeSpan 请求 Infinite，否则取正秒数并向上取整，服务器可能返回不同租约。</value>
    public TimeSpan? Timeout
    {
        get; set;
    }

    /// <summary>
    /// 获取或设置新建锁的覆盖范围。
    /// </summary>
    /// <value>null 默认 infinity；续期时不发送 Depth，服务器保留原锁范围。</value>
    public ApplyTo.Lock? ApplyTo
    {
        get; set;
    }

    /// <summary>
    /// 获取或设置新建锁的所有者说明。
    /// </summary>
    /// <value>默认 null，省略 owner；该值不参与身份认证，续期时也不重写所有者。</value>
    public LockOwner? Owner
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
