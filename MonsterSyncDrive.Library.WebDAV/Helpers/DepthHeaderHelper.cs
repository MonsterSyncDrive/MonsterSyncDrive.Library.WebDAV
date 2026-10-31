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

namespace MonsterSyncDrive.Library.WebDAV.Helpers;

/// <summary>
/// 将各操作的深度枚举转换成协议允许的 Depth 请求头文本。
/// </summary>
internal static class DepthHeaderHelper
{
    /// <summary>
    /// 把 PROPFIND 范围转换为 0、1 或 infinity 深度文本。
    /// </summary>
    /// <param name="applyTo">待转换的 PROPFIND 深度枚举。</param>
    /// <returns>可直接写入 Depth 头的协议文本。</returns>
    /// <exception cref="ArgumentOutOfRangeException">枚举值不属于支持的深度范围。</exception>
    public static string GetValueForPropfind(ApplyTo.Propfind applyTo)
    {
        return applyTo switch
        {
            ApplyTo.Propfind.ResourceOnly => "0",
            ApplyTo.Propfind.ResourceAndChildren => "1",
            ApplyTo.Propfind.ResourceAndAncestors => "infinity",
            _ => throw new ArgumentOutOfRangeException(nameof(applyTo)),
        };
    }

    /// <summary>
    /// 把 COPY 范围转换为 0 或 infinity，拒绝不合法枚举。
    /// </summary>
    /// <param name="applyTo">待转换的 COPY 深度枚举。</param>
    /// <returns>集合复制使用的 Depth 头文本。</returns>
    /// <exception cref="ArgumentOutOfRangeException">枚举值不属于支持的深度范围。</exception>
    public static string GetValueForCopy(ApplyTo.Copy applyTo)
    {
        return applyTo switch
        {
            ApplyTo.Copy.ResourceOnly => "0",
            ApplyTo.Copy.ResourceAndAncestors => "infinity",
            _ => throw new ArgumentOutOfRangeException(nameof(applyTo)),
        };
    }

    /// <summary>
    /// 把 LOCK 范围转换为 0 或 infinity，拒绝不合法枚举。
    /// </summary>
    /// <param name="applyTo">待转换的 LOCK 深度枚举。</param>
    /// <returns>锁创建使用的 Depth 头文本。</returns>
    /// <exception cref="ArgumentOutOfRangeException">枚举值不属于支持的深度范围。</exception>
    public static string GetValueForLock(ApplyTo.Lock applyTo)
    {
        return applyTo switch
        {
            ApplyTo.Lock.ResourceOnly => "0",
            ApplyTo.Lock.ResourceAndAncestors => "infinity",
            _ => throw new ArgumentOutOfRangeException(nameof(applyTo)),
        };
    }
}
