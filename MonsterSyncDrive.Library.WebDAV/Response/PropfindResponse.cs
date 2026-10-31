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

namespace MonsterSyncDrive.WebDAV.Response;

/// <summary>
/// 保存 PROPFIND、SEARCH 或多状态 REPORT 返回的资源集合。
/// </summary>
/// <remarks>合法空 multistatus 可以表示成功查询；资源级错误影响 IsSuccessful，可选属性错误保留在 PropertyStatuses 中。</remarks>
public class PropfindResponse : WebDavResponse
{
    /// <summary>
    /// 使用给定 HTTP 状态及可选结果集合创建 PropfindResponse 响应快照。
    /// </summary>
    /// <remarks>未提供的结果集合初始化为空，未提供的原因说明为 null。</remarks>
    /// <param name="statusCode">HTTP 响应的整数状态码，例如 200、207、404 或 423。</param>
    public PropfindResponse(int statusCode)
        : this(statusCode, null, [])
    {
    }

    /// <summary>
    /// 使用给定 HTTP 状态及可选结果集合创建 PropfindResponse 响应快照。
    /// </summary>
    /// <remarks>构造时物化传入的结果序列；HTTP 状态是否成功由 IsSuccessful 及派生类型的逐项检查决定。</remarks>
    /// <param name="statusCode">HTTP 响应的整数状态码，例如 200、207、404 或 423。</param>
    /// <param name="resources">要保存的逐资源结果集合，不应为 null；构造后按响应类型的约定读取。</param>
    public PropfindResponse(int statusCode, IEnumerable<WebDavResource> resources)
        : this(statusCode, null, resources)
    {
    }

    /// <summary>
    /// 使用给定 HTTP 状态及可选结果集合创建 PropfindResponse 响应快照。
    /// </summary>
    /// <remarks>未提供的结果集合初始化为空，未提供的原因说明为 null。</remarks>
    /// <param name="statusCode">HTTP 响应的整数状态码，例如 200、207、404 或 423。</param>
    /// <param name="description">HTTP 原因短语或服务器状态说明；没有提供时可为 null。</param>
    public PropfindResponse(int statusCode, string? description)
        : this(statusCode, description, [])
    {
    }

    /// <summary>
    /// 使用给定 HTTP 状态及可选结果集合创建 PropfindResponse 响应快照。
    /// </summary>
    /// <remarks>构造时物化传入的结果序列；HTTP 状态是否成功由 IsSuccessful 及派生类型的逐项检查决定。</remarks>
    /// <param name="statusCode">HTTP 响应的整数状态码，例如 200、207、404 或 423。</param>
    /// <param name="description">HTTP 原因短语或服务器状态说明；没有提供时可为 null。</param>
    /// <param name="resources">要保存的逐资源结果集合，不应为 null；构造后按响应类型的约定读取。</param>
    public PropfindResponse(int statusCode, string? description, IEnumerable<WebDavResource> resources)
        : base(statusCode, description)
    {
        ArgumentNullException.ThrowIfNull(resources);
        Resources = [.. resources];
    }

    /// <summary>
    /// 获取物化后的资源快照集合。
    /// </summary>
    /// <value>构造时枚举并保存一份集合；合法空 multistatus 对应空集合。</value>
    public IReadOnlyCollection<WebDavResource> Resources { get; }

    /// <summary>
    /// 获取 HTTP 为 2xx 且资源级状态均成功的结果。
    /// </summary>
    /// <value>可选属性的 404 不直接导致整个查询失败，调用方可检查各资源的 PropertyStatuses。</value>
    public override bool IsSuccessful => base.IsSuccessful && Resources.All(resource => resource.StatusCode is null or >= 200 and < 300);

    /// <summary>
    /// 返回当前PropfindResponse的状态或属性概要，便于日志记录和调试。
    /// </summary>
    /// <returns>包含当前对象关键字段的描述字符串，不触发网络请求。</returns>
    public override string ToString() => $"PROPFIND WebDAV response - StatusCode: {StatusCode}, Description: {Description}";
}
