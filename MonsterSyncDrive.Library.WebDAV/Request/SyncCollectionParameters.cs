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
using System.Globalization;
using System.Xml.Linq;
using MonsterSyncDrive.WebDAV.Core;

namespace MonsterSyncDrive.WebDAV.Request;

/// <summary>
/// 配置 RFC 6578 集合同步的起始令牌、层级、所需属性与每页结果上限。
/// </summary>
/// <remarks>空令牌执行首次完整列举；非空令牌为不透明状态标识，客户端不比较其版本大小，也不自动回退至全量同步。</remarks>
public sealed class SyncCollectionParameters : WebDavRequestOptions
{
    /// <summary>获取或设置上次完整同步得到的服务器令牌；null 或空字符串请求首次列举。</summary>
    public string? SyncToken { get; set; }

    /// <summary>获取或设置直接成员或全部后代的同步层级，默认直接成员。</summary>
    public WebDavSyncLevel Level { get; set; }

    /// <summary>获取或设置显式请求的属性；默认查询 getetag 与 resourcetype。</summary>
    public IReadOnlyCollection<XName> Properties { get; set; } = [WebDavXml.Dav + "getetag", WebDavXml.Dav + "resourcetype"];

    /// <summary>获取或设置每次报告期望的最大成员结果数；null 不请求结果数限制。</summary>
    /// <remarks>服务器可以用 request-URI 的 507 条目截断结果，客户端以本页返回的令牌继续，而非发送 HTTP 页码。</remarks>
    public int? Limit { get; set; }

    /// <summary>获取或设置自动分页最多允许的请求数，默认 10,000，防止失控服务器无限返回新令牌。</summary>
    public int MaxPages { get; set; } = 10_000;

    /// <summary>
    /// 构造协议要求的 sync-collection 正文，并在发送前检查参数。
    /// </summary>
    /// <returns>包含 sync-token、sync-level、prop 及可选 limit 的 XML 元素。</returns>
    /// <exception cref="ArgumentException">令牌、属性集合或 Depth 请求头不符合协议要求。</exception>
    /// <exception cref="ArgumentOutOfRangeException">层级、结果数上限或分页上限无效。</exception>
    internal XElement ToReportElement()
    {
        ValidateToken(SyncToken, allowEmpty: true);
        ArgumentNullException.ThrowIfNull(Properties);
        ArgumentNullException.ThrowIfNull(Headers);
        if (Properties.Any(property => property is null))
            throw new ArgumentException("Sync properties cannot contain null.", nameof(Properties));
        if (Level is not WebDavSyncLevel.Children and not WebDavSyncLevel.Descendants)
            throw new ArgumentOutOfRangeException(nameof(Level));
        if (Limit is <= 0)
            throw new ArgumentOutOfRangeException(nameof(Limit));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaxPages);

        // sync-level 控制后代范围，HTTP Depth 本身必须为 0。
        // 禁止通用自定义头把协议头覆盖为 1/infinity，否则请求与调用方期待的同步范围不一致。
        if (Headers.Any(header => header.Key.Equals("Depth", StringComparison.OrdinalIgnoreCase) && header.Value.Trim() != "0"))
            throw new ArgumentException("RFC 6578 sync reports require Depth: 0.", nameof(Headers));
        return new XElement(WebDavXml.Dav + "sync-collection",
            new XElement(WebDavXml.Dav + "sync-token", SyncToken ?? ""),
            new XElement(WebDavXml.Dav + "sync-level", Level == WebDavSyncLevel.Children ? "1" : "infinite"),
            Limit.HasValue ? new XElement(WebDavXml.Dav + "limit",
                new XElement(WebDavXml.Dav + "nresults", Limit.Value.ToString(CultureInfo.InvariantCulture))) : null,
            new XElement(WebDavXml.Dav + "prop", Properties.Select(property => new XElement(property))));
    }

    /// <summary>
    /// 复制当前选项并替换续取令牌与关联取消令牌，不修改调用方的参数对象。
    /// </summary>
    /// <param name="syncToken">本次页面请求应使用的不透明令牌。</param>
    /// <param name="cancellationToken">同时覆盖枚举器和原始请求取消的令牌。</param>
    /// <returns>共享只读配置集合、但具有独立令牌状态的参数副本。</returns>
    internal SyncCollectionParameters ForPage(string? syncToken, CancellationToken cancellationToken)
    {
        // MemberwiseClone 保留未来由基类新增的 If、Prefer 等设置，避免手工复制遗漏条件。
        // AOT 下该操作是普通对象浅复制，不依赖反射；传输期间调用方仍不应修改集合内容。
        var copy = (SyncCollectionParameters)MemberwiseClone();
        copy.SyncToken = syncToken;
        copy.CancellationToken = cancellationToken;
        return copy;
    }

    /// <summary>
    /// 验证同步令牌是空初始令牌或合法的绝对 URI，同时保留其原始文本。
    /// </summary>
    /// <param name="token">待验证的不透明令牌。</param>
    /// <param name="allowEmpty">是否允许 null 或空字符串代表首次同步。</param>
    /// <exception cref="ArgumentException">非初始令牌不是合法绝对 URI，或包含不可接受的空白。</exception>
    internal static void ValidateToken(string? token, bool allowEmpty)
    {
        if (string.IsNullOrEmpty(token) && allowEmpty)
            return;
        if (string.IsNullOrWhiteSpace(token) || token.Any(char.IsWhiteSpace)
            || !Uri.IsWellFormedUriString(token, UriKind.Absolute))
            throw new ArgumentException("A synchronization token must be an opaque absolute URI.", nameof(token));
    }
}

