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
using MonsterSyncDrive.Library.WebDAV.Helpers;

namespace MonsterSyncDrive.Library.WebDAV.Request;

/// <summary>配置 ACL 读改写操作的并发保护及通用 HTTP 条件。</summary>
/// <remarks>RFC 3744 建议读取及更新 ACL 前先加锁。服务器不支持锁时，可显式关闭自动加锁，并由调用方安排外部同步。</remarks>
public sealed class WebDavAclUpdateParameters : WebDavRequestOptions
{
    /// <summary>获取或设置调用方已经持有的资源锁令牌；指定后不获取也不释放该锁。</summary>
    public string? LockToken
    {
        get; set;
    }

    /// <summary>获取或设置没有现有令牌时是否自动获取独占写锁，默认 true。</summary>
    public bool AcquireLock { get; set; } = true;

    /// <summary>获取或设置自动获取的锁租约，默认五分钟；服务器可以调整实际租约。</summary>
    public TimeSpan LockTimeout { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>校验显式令牌和锁租约，避免在读取 ACL 后才发现本地参数错误。</summary>
    internal void Validate()
    {
        if (LockToken is not null)
            _ = IfHeaderHelper.Normalize(LockToken);
        if (AcquireLock && LockToken is null && LockTimeout != Timeout.InfiniteTimeSpan && LockTimeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(LockTimeout));
    }
}

