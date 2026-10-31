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
/// 保存操作的 HTTP 状态及脱离网络生命周期的响应头快照。
/// </summary>
/// <remarks>基础 IsSuccessful 仅判断 2xx；派生多状态类型还检查内部资源或属性状态。</remarks>
public class WebDavResponse
{
    /// <summary>
    /// 使用给定 HTTP 状态及可选结果集合创建 WebDavResponse 响应快照。
    /// </summary>
    /// <remarks>只保存状态及可选原因说明；响应头初始为空，网络客户端随后通过 CaptureHeaders 保存头部快照。</remarks>
    /// <param name="statusCode">HTTP 响应的整数状态码，例如 200、207、404 或 423。</param>
    public WebDavResponse(int statusCode)
    {
        StatusCode = statusCode;
    }

    /// <summary>
    /// 使用给定 HTTP 状态及可选结果集合创建 WebDavResponse 响应快照。
    /// </summary>
    /// <remarks>只保存状态及可选原因说明；响应头初始为空，网络客户端随后通过 CaptureHeaders 保存头部快照。</remarks>
    /// <param name="statusCode">HTTP 响应的整数状态码，例如 200、207、404 或 423。</param>
    /// <param name="description">HTTP 原因短语或服务器状态说明；没有提供时可为 null。</param>
    public WebDavResponse(int statusCode, string? description)
    {
        StatusCode = statusCode;
        Description = description;
    }

    /// <summary>
    /// 获取服务器返回的整数 HTTP 状态码。
    /// </summary>
    public int StatusCode { get; }

    /// <summary>
    /// 获取 HTTP 响应原因短语或调用方提供的状态说明。
    /// </summary>
    /// <value>HTTP/2 等响应可能不提供原因短语，此时为 null 或空文本。</value>
    public string? Description { get; }

    /// <summary>获取 HTTP 错误正文中的 DAV 前置或后置条件及扩展详情，未提供时为 null。</summary>
    public MonsterSyncDrive.WebDAV.Core.WebDavError? Error { get; internal set; }

    /// <summary>
    /// 获取基础 HTTP 状态码是否在 200 至 299 范围内。
    /// </summary>
    /// <value>派生多状态类型会进一步检查内部结果，304 不属于成功下载。</value>
    public virtual bool IsSuccessful => StatusCode is >= 200 and <= 299;

    /// <summary>
    /// 获取与网络响应生命周期分离的响应头及正文头快照。
    /// </summary>
    /// <value>键比较不区分大小写；多值头保留为字符串数组，原始响应释放后仍可读取。</value>
    public IReadOnlyDictionary<string, string[]> Headers { get; internal set; } = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
    /// <summary>
    /// 获取响应头 ETag 的原始文本，包括引号及可能的弱标记。
    /// </summary>
    /// <value>未提供时为 null；与资源属性中的 DAV:getetag 相对应但来源不同。</value>
    public string? ETag => GetHeader("ETag");
    /// <summary>
    /// 按不区分大小写的名称读取响应头快照，并合并该头的多个值。
    /// </summary>
    /// <param name="name">响应头或正文头名称，例如 ETag、Content-Length 或 DAV。</param>
    /// <returns>以逗号及空格连接的头值，未提供时为 null。</returns>
    public string? GetHeader(string name) => Headers.TryGetValue(name, out var values) ? string.Join(", ", values) : null;

    /// <summary>
    /// 检查实际派生类型的 IsSuccessful，在 HTTP 或多状态内部失败时抛出异常。
    /// </summary>
    /// <remarks>使用虚属性 IsSuccessful，因此 HTTP 207 的属性或资源错误也能被发现，而非只检查外层状态。</remarks>
    /// <returns>当前响应对象，便于在读取状态后继续访问快照。</returns>
    /// <exception cref="HttpRequestException">响应的 IsSuccessful 为 false，异常保留外层 HTTP 状态码。</exception>
    public WebDavResponse EnsureSuccessStatusCode()
    {
        if (!IsSuccessful)
            throw new System.Net.Http.HttpRequestException($"WebDAV returned {StatusCode}: {Description}", null, (System.Net.HttpStatusCode)StatusCode);
        return this;
    }

    /// <summary>
    /// 复制 HTTP 响应头和正文头，按大小写不敏感的键保存多值快照。
    /// </summary>
    /// <param name="response">要捕获头字段的原始 HTTP 响应；复制后无需继续保留原始响应。</param>
    /// <typeparam name="T">当前响应对象的实际类型，用于保持内部调用的返回类型。</typeparam>
    /// <returns>当前响应对象转换为指定具体类型后的实例。</returns>
    internal T CaptureHeaders<T>(HttpResponseMessage response) where T : WebDavResponse
    {
        // 响应头和正文头分属两个集合；合并后按不区分大小写的名称分组，并复制每个值序列。
        // 原始 HTTP 响应随后即使已关闭，调用方仍可读取 ETag、长度、Range 和服务器能力等信息。
        Headers = response.Headers.Concat(response.Content.Headers)
            .GroupBy(header => header.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.SelectMany(header => header.Value).ToArray(), StringComparer.OrdinalIgnoreCase);
        return (T)this;
    }

    /// <summary>
    /// 返回当前WebDavResponse的状态或属性概要，便于日志记录和调试。
    /// </summary>
    /// <returns>包含当前对象关键字段的描述字符串，不触发网络请求。</returns>
    public override string ToString() => $"WebDAV response - StatusCode: {StatusCode}, Description: {Description}";
}
