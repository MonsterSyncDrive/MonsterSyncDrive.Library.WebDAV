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
namespace MonsterSyncDrive.WebDAV.Core;

/// <summary>
/// 使用绝对 URI 描述 LOCK 请求的锁所有者。
/// </summary>
/// <remarks>序列化为 DAV:owner/DAV:href；支持 mailto 等合法绝对 URI，不局限于 HTTP 地址。</remarks>
public class UriLockOwner : LockOwner
{
    /// <summary>
    /// 验证并保存绝对 URI 形式的锁所有者说明。
    /// </summary>
    /// <param name="absoluteUri">合法绝对 URI，例如 mailto:owner@example.com。</param>
    /// <exception cref="ArgumentException">absoluteUri 不是格式正确的绝对 URI。</exception>
    public UriLockOwner(string absoluteUri)
    {
        if (!Uri.IsWellFormedUriString(absoluteUri, UriKind.Absolute))
            throw new ArgumentException("The parameter uri should be a valid absolute uri.", nameof(absoluteUri));
        Value = absoluteUri;
    }

    /// <summary>
    /// 获取构造时校验通过的绝对所有者 URI。
    /// </summary>
    public override string Value { get; }
}
