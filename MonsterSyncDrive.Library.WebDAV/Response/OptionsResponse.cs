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
namespace MonsterSyncDrive.WebDAV.Response;

/// <summary>
/// 保存 OPTIONS 的 HTTP 状态、DAV 能力标识和允许的方法列表。
/// </summary>
/// <remarks>能力对应当前请求资源，不保证服务器上所有资源具有相同的权限或功能。</remarks>
/// <param name="statusCode">OPTIONS 返回的整数 HTTP 状态码。</param>
/// <param name="description">可选的 HTTP 原因短语或状态说明。</param>
public sealed class OptionsResponse(int statusCode, string? description) : WebDavResponse(statusCode, description)
{
    /// <summary>
    /// 获取从 DAV 响应头按逗号拆分并去除空白后的合规标识。
    /// </summary>
    /// <value>未提供 DAV 头时为空集合；常见值包括 1 和 2。</value>
    public IReadOnlyCollection<string> ComplianceClasses => Split("DAV");
    /// <summary>
    /// 获取从 Allow 响应头拆分得到的服务器允许方法列表。
    /// </summary>
    /// <value>未提供 Allow 时为空集合，不据此推断其他资源的权限。</value>
    public IReadOnlyCollection<string> AllowedMethods => Split("Allow");
    /// <summary>
    /// 获取 DAV 合规标识是否包含类 2，即服务器声明支持写锁。
    /// </summary>
    /// <value>只检查响应声明，不发起实际 LOCK 探测。</value>
    public bool SupportsLocking => ComplianceClasses.Contains("2", StringComparer.Ordinal);

    /// <summary>
    /// 把指定响应头的逗号分隔值规范为去空白的非空字符串数组。
    /// </summary>
    /// <param name="name">待拆分的头名称，通常为 DAV 或 Allow。</param>
    /// <returns>规范化后的各项；响应头缺失时返回空数组。</returns>
    private string[] Split(string name) => (GetHeader(name) ?? "").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
}
