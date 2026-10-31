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
/// 保存 PROPPATCH 的逐属性状态与资源级结果。
/// </summary>
/// <remarks>HTTP 207 本身不表示修改成功；IsSuccessful 还检查资源状态和每项属性状态。</remarks>
public class ProppatchResponse : WebDavResponse
{
    /// <summary>
    /// 使用给定 HTTP 状态及可选结果集合创建 ProppatchResponse 响应快照。
    /// </summary>
    /// <remarks>未提供的结果集合初始化为空，未提供的原因说明为 null。</remarks>
    /// <param name="statusCode">HTTP 响应的整数状态码，例如 200、207、404 或 423。</param>
    public ProppatchResponse(int statusCode)
        : this(statusCode, null, [])
    {
    }

    /// <summary>
    /// 使用给定 HTTP 状态及可选结果集合创建 ProppatchResponse 响应快照。
    /// </summary>
    /// <remarks>构造时物化传入的结果序列；HTTP 状态是否成功由 IsSuccessful 及派生类型的逐项检查决定。</remarks>
    /// <param name="statusCode">HTTP 响应的整数状态码，例如 200、207、404 或 423。</param>
    /// <param name="propertyStatuses">要保存的逐属性状态集合，包含成功与失败条目，不应为 null。</param>
    public ProppatchResponse(int statusCode, IEnumerable<WebDavPropertyStatus> propertyStatuses)
        : this(statusCode, null, propertyStatuses)
    {
    }

    /// <summary>
    /// 使用给定 HTTP 状态及可选结果集合创建 ProppatchResponse 响应快照。
    /// </summary>
    /// <remarks>未提供的结果集合初始化为空，未提供的原因说明为 null。</remarks>
    /// <param name="statusCode">HTTP 响应的整数状态码，例如 200、207、404 或 423。</param>
    /// <param name="description">HTTP 原因短语或服务器状态说明；没有提供时可为 null。</param>
    public ProppatchResponse(int statusCode, string? description)
        : this(statusCode, description, [])
    {
    }

    /// <summary>
    /// 使用给定 HTTP 状态及可选结果集合创建 ProppatchResponse 响应快照。
    /// </summary>
    /// <remarks>构造时物化传入的结果序列；HTTP 状态是否成功由 IsSuccessful 及派生类型的逐项检查决定。</remarks>
    /// <param name="statusCode">HTTP 响应的整数状态码，例如 200、207、404 或 423。</param>
    /// <param name="description">HTTP 原因短语或服务器状态说明；没有提供时可为 null。</param>
    /// <param name="propertyStatuses">要保存的逐属性状态集合，包含成功与失败条目，不应为 null。</param>
    public ProppatchResponse(int statusCode, string? description, IEnumerable<WebDavPropertyStatus> propertyStatuses)
        : base(statusCode, description)
    {
        ArgumentNullException.ThrowIfNull(propertyStatuses);
        PropertyStatuses = [.. propertyStatuses];
    }

    /// <summary>
    /// 获取各资源属性修改的完整状态集合。
    /// </summary>
    /// <value>包含成功与失败状态，不能只依据外层 HTTP 207 判断修改成功。</value>
    public IReadOnlyCollection<WebDavPropertyStatus> PropertyStatuses { get; }

    /// <summary>
    /// 获取 PROPPATCH 保留的资源级和逐属性结果。
    /// </summary>
    /// <value>客户端使用该集合检查请求的属性是否都得到确认。</value>
    public IReadOnlyCollection<WebDavResource> Resources { get; internal set; } = [];

    /// <summary>获取完整属性修改响应 XML；合法的最小响应没有正文时为 null。</summary>
    public System.Xml.Linq.XElement? Document { get; internal set; }

    /// <summary>获取服务器提供的非 XML 返回正文；通常的多状态 XML 使用 Document 保存。</summary>
    public byte[]? Representation { get; internal set; }
    /// <summary>
    /// 获取 HTTP 状态、资源级状态及每个属性修改状态均成功的结果。
    /// </summary>
    public override bool IsSuccessful => base.IsSuccessful
        && PropertyStatuses.All(status => status.IsSuccessful)
        && Resources.All(resource => resource.StatusCode is null or >= 200 and < 300);

    /// <summary>
    /// 返回当前ProppatchResponse的状态或属性概要，便于日志记录和调试。
    /// </summary>
    /// <returns>包含当前对象关键字段的描述字符串，不触发网络请求。</returns>
    public override string ToString() => $"PROPPATCH WebDAV response - StatusCode: {StatusCode}, Description: {Description}";
}
