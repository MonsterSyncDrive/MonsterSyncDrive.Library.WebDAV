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
namespace MonsterSyncDrive.WebDAV.Helpers;

/// <summary>
/// 生成 WebDAV If 条件头，并规范化锁令牌的尖括号格式。
/// </summary>
/// <remarks>源锁和目标锁通过带标记条件绑定各自的资源，不能把目标锁误作为仅针对请求 URI 的无标记条件。</remarks>
internal static class IfHeaderHelper
{
    /// <summary>
    /// 为当前请求资源生成单个锁令牌的无标记 If 条件。
    /// </summary>
    /// <param name="lockToken">待提交的绝对锁令牌 URI，可带一层外部尖括号。</param>
    /// <returns>形如 (&lt;token&gt;) 的条件文本。</returns>
    public static string GetHeaderValue(string lockToken) => $"(<{Normalize(lockToken)}>)";

    /// <summary>
    /// 生成绑定指定绝对资源 URI 的带标记锁条件。
    /// </summary>
    /// <param name="uri">锁令牌应作用的绝对资源 URI，必须是对应的源资源或目标资源。</param>
    /// <param name="lockToken">该资源的绝对锁令牌 URI。</param>
    /// <returns>形如 &lt;resource-uri&gt; (&lt;token&gt;) 的条件文本。</returns>
    public static string Tagged(Uri uri, string lockToken) => $"<{uri.AbsoluteUri}> {GetHeaderValue(lockToken)}";

    /// <summary>
    /// 移除可选的一层外部尖括号，并验证锁令牌可安全写入 DAV 条件头。
    /// </summary>
    /// <remarks>拒绝空白、换行及条件分隔字符，避免无效令牌破坏 If 或 Lock-Token 头结构。</remarks>
    /// <param name="lockToken">服务器返回的原始锁令牌。</param>
    /// <returns>不带外部尖括号的绝对锁令牌 URI 文本。</returns>
    /// <exception cref="ArgumentException">令牌为空白、包含非法字符，或不是绝对 URI。</exception>
    public static string Normalize(string lockToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(lockToken);
        if (lockToken.Any(character => character is '\r' or '\n'))
            throw new ArgumentException("Lock token cannot contain line breaks.", nameof(lockToken));
        var token = lockToken.Trim();
        // API 接受服务器返回的裸 URI 或已有一层尖括号的头值，但最终生成协议头时只保留一层。
        if (token.StartsWith('<') && token.EndsWith('>'))
            token = token[1..^1];
        // 拒绝换行、空白和条件分隔字符，避免令牌破坏 If 列表或注入额外请求头。
        // URI 本身可以合法包含圆括号；只有 URI 外的圆括号才是 If 列表语法，不应错误拒绝合法状态令牌。
        return MonsterSyncDrive.WebDAV.Core.WebDavIfSyntax.StateToken(token);
    }
}
