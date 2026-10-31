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

namespace MonsterSyncDrive.Library.WebDAV.Response;

/// <summary>
/// 保存 LOCK 返回的活动锁，或集合加锁失败时的多资源状态。
/// </summary>
public class LockResponse : WebDavResponse
{
    /// <summary>
    /// 使用给定 HTTP 状态及可选结果集合创建 LockResponse 响应快照。
    /// </summary>
    /// <remarks>未提供的结果集合初始化为空，未提供的原因说明为 null。</remarks>
    /// <param name="statusCode">HTTP 响应的整数状态码，例如 200、207、404 或 423。</param>
    public LockResponse(int statusCode)
        : this(statusCode, null, [])
    {
    }

    /// <summary>
    /// 使用给定 HTTP 状态及可选结果集合创建 LockResponse 响应快照。
    /// </summary>
    /// <remarks>构造时物化传入的结果序列；HTTP 状态是否成功由 IsSuccessful 及派生类型的逐项检查决定。</remarks>
    /// <param name="statusCode">HTTP 响应的整数状态码，例如 200、207、404 或 423。</param>
    /// <param name="activeLocks">服务器返回的活动锁集合，不应为 null。</param>
    public LockResponse(int statusCode, IEnumerable<ActiveLock> activeLocks)
        : this(statusCode, null, activeLocks)
    {
    }

    /// <summary>
    /// 使用给定 HTTP 状态及可选结果集合创建 LockResponse 响应快照。
    /// </summary>
    /// <remarks>未提供的结果集合初始化为空，未提供的原因说明为 null。</remarks>
    /// <param name="statusCode">HTTP 响应的整数状态码，例如 200、207、404 或 423。</param>
    /// <param name="description">HTTP 原因短语或服务器状态说明；没有提供时可为 null。</param>
    public LockResponse(int statusCode, string? description)
        : this(statusCode, description, [])
    {
    }

    /// <summary>
    /// 使用给定 HTTP 状态及可选结果集合创建 LockResponse 响应快照。
    /// </summary>
    /// <remarks>构造时物化传入的结果序列；HTTP 状态是否成功由 IsSuccessful 及派生类型的逐项检查决定。</remarks>
    /// <param name="statusCode">HTTP 响应的整数状态码，例如 200、207、404 或 423。</param>
    /// <param name="description">HTTP 原因短语或服务器状态说明；没有提供时可为 null。</param>
    /// <param name="activeLocks">服务器返回的活动锁集合，不应为 null。</param>
    public LockResponse(int statusCode, string? description, IEnumerable<ActiveLock> activeLocks)
        : base(statusCode, description)
    {
        ArgumentNullException.ThrowIfNull(activeLocks);
        ActiveLocks = [.. activeLocks];
    }

    /// <summary>
    /// 获取成功 LOCK 响应中的活动锁集合。
    /// </summary>
    /// <value>失败状态或集合错误响应中通常为空集合。</value>
    public IReadOnlyCollection<ActiveLock> ActiveLocks
    {
        get;
    }

    /// <summary>
    /// 获取集合 LOCK 的 HTTP 207 中逐资源结果。
    /// </summary>
    /// <value>普通 LOCK 成功响应为空集合；其中的失败资源使 IsSuccessful 返回 false。</value>
    public IReadOnlyCollection<WebDavResource> Resources { get; internal set; } = [];
    /// <summary>
    /// 获取 HTTP 为 2xx 且所有已返回资源和属性状态均成功的结果。
    /// </summary>
    public override bool IsSuccessful => base.IsSuccessful
        && Resources.All(resource => (resource.StatusCode is null or (>= 200 and < 300))
            && resource.PropertyStatuses.All(status => status.IsSuccessful));

    /// <summary>
    /// 返回当前LockResponse的状态或属性概要，便于日志记录和调试。
    /// </summary>
    /// <returns>包含当前对象关键字段的描述字符串，不触发网络请求。</returns>
    public override string ToString()
    {
        return $"LOCK response - StatusCode: {StatusCode}, Description: {Description}";
    }
}
