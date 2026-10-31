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
namespace MonsterSyncDrive.Library.WebDAV.Core;

/// <summary>
/// 集中保存 WebDAV 使用的标准及兼容性 HTTP 头名称。
/// </summary>
internal static class WebDavHeaders
{
    /// <summary>
    /// Depth 请求头名称，用于指定当前资源及其后代的操作范围。
    /// </summary>
    public static readonly string Depth = "Depth";

    /// <summary>
    /// Destination 请求头名称，COPY 和 MOVE 用它指定绝对目标 URI。
    /// </summary>
    public static readonly string Destination = "Destination";

    /// <summary>
    /// If 请求头名称，用于提交 WebDAV 锁令牌和绑定资源的状态条件。
    /// </summary>
    public static readonly string If = "If";

    /// <summary>
    /// Lock-Token 请求头名称，UNLOCK 用它提交尖括号包围的锁令牌。
    /// </summary>
    public static readonly string LockToken = "Lock-Token";

    /// <summary>
    /// Overwrite 请求头名称；T 允许覆盖目标，F 在目标已存在时拒绝覆盖。
    /// </summary>
    public static readonly string Overwrite = "Overwrite";

    /// <summary>
    /// Timeout 请求头名称，表示服务器锁租约时长，不等同于 HTTP 请求超时。
    /// </summary>
    public static readonly string Timeout = "Timeout";

    /// <summary>
    /// Translate 兼容性请求头名称，提示服务器是否处理下载资源；f 请求原始内容。
    /// </summary>
    public static readonly string Translate = "Translate";
}
