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
using System.Net.Http.Headers;

using MonsterSyncDrive.WebDAV.Core;

namespace MonsterSyncDrive.WebDAV.Request;

/// <summary>
/// 保存 DAV 操作共用的请求头、HTTP 条件和取消令牌。
/// </summary>
/// <remarks>并发请求应分别创建选项对象；Headers 最后应用，可以覆盖本次请求已经构造的同名请求头。</remarks>
public class WebDavRequestOptions
{
    /// <summary>获取或设置完整 DAV If 表达式；存在时替换操作自动生成的简化锁条件。</summary>
    /// <remarks>表达式支持带资源标记或无标记模式、Not 和多列表；最后应用的原始 Headers 仍可显式覆盖它。</remarks>
    public WebDavIfHeader? IfConditions { get; set; }

    /// <summary>获取或设置标准 Prefer 首选项，例如最小响应或返回资源表示。</summary>
    public WebDavPreferences? Preferences { get; set; }

    /// <summary>获取或设置操作是否直接作用于重定向引用资源自身，而不是其目标资源。</summary>
    /// <value>null 不发送 Apply-To-Redirect-Ref；true 发送 T，false 发送 F。</value>
    /// <remarks>遵循 RFC 4437；最后应用的原始 Headers 可以覆盖该强类型设置。</remarks>
    public bool? ApplyToRedirectReference { get; set; }
    /// <summary>
    /// 获取或设置本次请求的自定义 HTTP 请求头。
    /// </summary>
    /// <value>默认空集合；最后覆盖已构造的同名头，不修改客户端默认头。正文媒体类型使用具体操作的 ContentType 配置。</value>
    public IReadOnlyCollection<KeyValuePair<string, string>> Headers { get; set; } = [];
    /// <summary>
    /// 获取或设置取消本次请求及后续响应正文读取的令牌。
    /// </summary>
    /// <value>默认 CancellationToken.None；释放客户端仍会取消该客户端的在途操作。</value>
    public CancellationToken CancellationToken { get; set; }
    /// <summary>
    /// 获取或设置修改或读取前要求匹配的服务器 ETag。
    /// </summary>
    /// <value>null 不发送；使用带引号的标签，或使用 * 表示资源必须存在。</value>
    public string? IfMatch { get; set; }
    /// <summary>
    /// 获取或设置要求不匹配的服务器 ETag。
    /// </summary>
    /// <value>null 不发送；* 可用于仅创建不存在的资源，GET 条件命中时可能返回 304。</value>
    public string? IfNoneMatch { get; set; }
    /// <summary>
    /// 获取或设置仅在资源自指定时间后发生修改时执行的 HTTP 时间条件。
    /// </summary>
    /// <value>默认 null；按 HTTP 日期发送，精度通常为秒。</value>
    public DateTimeOffset? IfModifiedSince { get; set; }
    /// <summary>
    /// 获取或设置资源自指定时间后未被修改的 HTTP 前置条件。
    /// </summary>
    /// <value>默认 null，可在缺少强 ETag 时进行时间级并发检查。</value>
    public DateTimeOffset? IfUnmodifiedSince { get; set; }

    /// <summary>把全部通用请求选项复制到一个具体操作的参数对象。</summary>
    /// <param name="target">接收通用 HTTP 条件、DAV If、Prefer、重定向引用和取消设置的参数对象。</param>
    /// <remarks>仅复制引用和标量，不修改原对象；调用方在请求期间不应改变共享的头集合或首选项。</remarks>
    internal void CopyTo(WebDavRequestOptions target)
    {
        ArgumentNullException.ThrowIfNull(target);
        target.Headers = Headers;
        target.CancellationToken = CancellationToken;
        target.IfMatch = IfMatch;
        target.IfNoneMatch = IfNoneMatch;
        target.IfModifiedSince = IfModifiedSince;
        target.IfUnmodifiedSince = IfUnmodifiedSince;
        target.IfConditions = IfConditions;
        target.Preferences = Preferences;
        target.ApplyToRedirectReference = ApplyToRedirectReference;
    }

    /// <summary>
    /// 把 HTTP 条件和自定义请求头应用到当前请求。
    /// </summary>
    /// <remarks>实体标签 * 使用 EntityTagHeaderValue.Any；自定义 Headers 最后覆盖请求级同名头，保证不会污染 HttpClient 的默认请求头集合。</remarks>
    /// <param name="request">当前操作创建的独立请求对象。</param>
    internal void Apply(HttpRequestMessage request)
    {
        if (IfConditions is not null)
        {
            // 简化 LockToken 是操作的便利默认值；完整表达式明确指定全部逻辑后应成为唯一 If 字段。
            request.Headers.Remove("If");
            request.Headers.Add("If", IfConditions.ToString());
        }
        if (Preferences is { IsEmpty: false })
        {
            request.Headers.Remove("Prefer");
            request.Headers.Add("Prefer", Preferences.ToString());
        }
        if (ApplyToRedirectReference is { } applyToReference)
        {
            request.Headers.Remove("Apply-To-Redirect-Ref");
            request.Headers.Add("Apply-To-Redirect-Ref", applyToReference ? "T" : "F");
        }
        // 星号是整个条件集合的特殊语法，不是普通带引号的 ETag；直接使用 Any，避免 Parse 拒绝合法的 *。
        if (IfMatch is not null)
            request.Headers.IfMatch.Add(IfMatch == "*" ? EntityTagHeaderValue.Any : EntityTagHeaderValue.Parse(IfMatch));
        if (IfNoneMatch is not null)
            request.Headers.IfNoneMatch.Add(IfNoneMatch == "*" ? EntityTagHeaderValue.Any : EntityTagHeaderValue.Parse(IfNoneMatch));
        request.Headers.IfModifiedSince = IfModifiedSince;
        request.Headers.IfUnmodifiedSince = IfUnmodifiedSince;
        // RFC 4918 要求 If 请求禁止不认识 DAV 条件的中间缓存参与条件判断。
        // 简化 LockToken、完整模型和原始 If 头都应用同一默认值；用户显式 Cache-Control 仍按最后覆盖原则处理。
        if (request.Headers.Contains("If") || Headers.Any(header => header.Key.Equals("If", StringComparison.OrdinalIgnoreCase)))
        {
            request.Headers.CacheControl ??= new CacheControlHeaderValue();
            request.Headers.CacheControl.NoCache = true;
        }
        // 覆盖范围只限于当前 HttpRequestMessage，不修改 HttpClient 默认头，保证并发请求之间互不污染。
        foreach (var header in Headers)
        {
            request.Headers.Remove(header.Key);
            request.Headers.Add(header.Key, header.Value);
        }
    }
}
