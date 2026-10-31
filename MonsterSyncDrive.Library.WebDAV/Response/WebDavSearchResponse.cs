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
using System.Numerics;
using System.Xml.Linq;
using MonsterSyncDrive.WebDAV.Core;

namespace MonsterSyncDrive.WebDAV.Response;

/// <summary>搜索命中及本次查询上下文中的全文相关性分数。</summary>
public sealed class WebDavSearchHit
{
    /// <summary>保存资源和已经过范围校验的可选相关性分数。</summary>
    /// <param name="resource">资源及逐属性状态。</param>
    /// <param name="score">可选的整数相关性分数。</param>
    internal WebDavSearchHit(WebDavResource resource, int? score)
    {
        Resource = resource;
        Score = score;
    }

    /// <summary>获取命中的资源快照，错误范围条目也保留在此。</summary>
    public WebDavResource Resource { get; }

    /// <summary>获取服务器返回的相关性分数；未提供时为 null。</summary>
    /// <remarks>不同服务器或不同查询的分数不能直接比较。</remarks>
    public int? Score { get; }
}

/// <summary>完整 basicsearch 响应，保留评分、截断提示与根级扩展。</summary>
public sealed class WebDavSearchResponse : WebDavResponse
{
    /// <summary>解析包含资源、评分及截断错误的完整搜索文档。</summary>
    /// <param name="response">已安全读取的 XML 搜索响应。</param>
    /// <param name="searchArbiter">接收本次 SEARCH 的绝对仲裁资源 URI。</param>
    internal WebDavSearchResponse(WebDavDocumentResponse response, Uri searchArbiter) : base(response.StatusCode, response.Description)
    {
        Headers = response.Headers;
        Error = response.Error;
        Document = response.Document is null ? null : new XElement(response.Document);
        if (response.StatusCode is >= 200 and < 300 && response.StatusCode != 207)
            throw new InvalidDataException("SEARCH requires HTTP 207.");
        if (response.StatusCode == 207)
        {
            if (Document?.Name != WebDavXml.Dav + "multistatus")
                throw new InvalidDataException("SEARCH response is not DAV:multistatus.");
            Hits = Array.AsReadOnly(Document.Elements(WebDavXml.Dav + "response").Zip(response.Resources, (element, resource) =>
            {
                var scoreElement = element.Element(WebDavXml.Dav + "score");
                int? score = null;
                if (scoreElement is not null)
                {
                    if (!int.TryParse(scoreElement.Value.AsSpan().Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) || parsed is < 0 or > 10000)
                        throw new InvalidDataException("Search score must be an integer from 0 through 10000.");
                    score = parsed;
                }
                return new WebDavSearchHit(resource, score);
            }).ToArray());
            // RFC 5323 用仲裁资源自己的 507 状态表示截断，并没有要求额外的 error 子元素。
            // 只把仲裁资源的 507 解释成截断；其他资源的普通失败仍原样保留，避免误报查询完整性。
            IsTruncated = Hits.Any(hit => hit.Resource.StatusCode == 507
                && Uri.TryCreate(searchArbiter, hit.Resource.Uri, out var resourceUri) && resourceUri == searchArbiter);
        }
    }

    /// <summary>获取完整响应 XML，可读取查询扩展及未知元数据。</summary>
    public XElement? Document { get; }

    /// <summary>获取包含资源和可选评分的全部结果。</summary>
    public IReadOnlyCollection<WebDavSearchHit> Hits { get; } = [];

    /// <summary>获取服务器是否明确声明结果因数量限制被截断。</summary>
    public bool IsTruncated { get; }

    /// <summary>获取 HTTP 与资源级状态是否均成功；截断范围的 507 会保留为失败结果。</summary>
    public override bool IsSuccessful => base.IsSuccessful && Hits.All(hit => hit.Resource.StatusCode is null or >= 200 and < 300);
}

/// <summary>RFC 5323 公布的 XML 查询语法名称及属性查询状态。</summary>
public sealed class WebDavSearchGrammarResponse : WebDavResponse
{
    /// <summary>提取 supported-query-grammar-set，保留不支持该属性的状态。</summary>
    /// <param name="response">Depth: 0 的属性查询结果。</param>
    internal WebDavSearchGrammarResponse(PropfindResponse response) : base(response.StatusCode, response.Description)
    {
        Headers = response.Headers;
        Error = response.Error;
        if (response.StatusCode != 207)
            return;
        if (response.Resources.Count != 1)
            throw new InvalidDataException("Grammar discovery must return exactly one resource.");
        var resource = response.Resources.Single();
        ResourceStatusCode = resource.StatusCode;
        var name = WebDavXml.Dav + "supported-query-grammar-set";
        PropertyStatusCode = resource.PropertyStatuses.FirstOrDefault(status => status.Name == name)?.StatusCode;
        var property = resource.Properties.SingleOrDefault(value => value.Name == name);
        if (property is null)
            return;
        Grammars = Array.AsReadOnly(property.ToXElement().Elements(WebDavXml.Dav + "supported-query-grammar")
            .Select(element => element.Element(WebDavXml.Dav + "grammar")
                ?? throw new InvalidDataException("A supported grammar has no DAV:grammar."))
            .Select(ParseGrammar)
            .Distinct().ToArray());
    }

    /// <summary>严格检查语法身份只有一个完整 XML 元素，避免静默选择错误的查询语言。</summary>
    /// <param name="element">DAV:grammar 容器。</param>
    /// <returns>该查询语法的完整名称。</returns>
    private static XName ParseGrammar(XElement element)
    {
        var names = element.Elements().ToArray();
        if (names.Length != 1)
            throw new InvalidDataException("A DAV:grammar must contain exactly one grammar name.");
        return names[0].Name;
    }

    /// <summary>获取服务器公布的完整语法元素名称。</summary>
    public IReadOnlyCollection<XName> Grammars { get; } = [];

    /// <summary>获取资源级状态，未返回时为 null。</summary>
    public int? ResourceStatusCode { get; }

    /// <summary>获取语法属性的状态，如 404 表示服务器未提供。</summary>
    public int? PropertyStatusCode { get; }

    /// <summary>获取 HTTP 和资源级状态是否成功；属性未提供不被误当成已支持任何语法。</summary>
    public override bool IsSuccessful => base.IsSuccessful && ResourceStatusCode is null or >= 200 and < 300;
}

/// <summary>RFC 4331 配额快照，保留未知额度与合法零值的区别。</summary>
/// <remarks>使用 BigInteger 表达协议中的非负十进制字节数，不人为截断到 Int64。可用额度缺失不能据此断定无限额度。</remarks>
public sealed class WebDavQuotaResponse : WebDavResponse
{
    /// <summary>从已明确查询两项配额属性的响应中建立配额快照。</summary>
    /// <param name="response">Depth: 0 的 PROPFIND 结果。</param>
    internal WebDavQuotaResponse(PropfindResponse response) : base(response.StatusCode, response.Description)
    {
        Headers = response.Headers;
        Error = response.Error;
        if (response.StatusCode != 207)
            return;
        if (response.Resources.Count != 1)
            throw new InvalidDataException("Quota query must return exactly one resource.");
        Resource = response.Resources.Single();
        UsedBytes = Read("quota-used-bytes");
        AvailableBytes = Read("quota-available-bytes");
        UsedStatusCode = Status("quota-used-bytes");
        AvailableStatusCode = Status("quota-available-bytes");

        // 只接受成功属性中的非负整数。404/403 留在状态中，不能把未知额度或禁止访问误报成零。
        BigInteger? Read(string localName)
        {
            var property = Resource.Properties.SingleOrDefault(value => value.Name == WebDavXml.Dav + localName);
            if (property is null)
                return null;
            if (!BigInteger.TryParse(property.TextValue.AsSpan().Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var value) || value.Sign < 0)
                throw new InvalidDataException("A successful DAV quota property must be a non-negative integer.");
            return value;
        }
        int? Status(string localName) => Resource.PropertyStatuses.FirstOrDefault(value => value.Name == WebDavXml.Dav + localName)?.StatusCode;
    }

    /// <summary>获取服务器报告已计入配额的字节数，未知时为 null。</summary>
    public BigInteger? UsedBytes { get; }

    /// <summary>获取服务器报告还可分配的字节数，0 表示已满，null 表示未提供或无权读取。</summary>
    public BigInteger? AvailableBytes { get; }

    /// <summary>获取已用配额属性的状态码，缺少属性状态时为 null。</summary>
    public int? UsedStatusCode { get; }

    /// <summary>获取可用额度属性的状态码，404 不被推断为明确无限额度。</summary>
    public int? AvailableStatusCode { get; }

    /// <summary>获取查询资源和全部属性状态，便于区分不支持、权限拒绝及资源错误。</summary>
    public WebDavResource? Resource { get; }

    /// <summary>获取 HTTP 和资源状态是否成功；属性是否可用由对应状态及可空数值判断。</summary>
    public override bool IsSuccessful => base.IsSuccessful && Resource?.StatusCode is null or >= 200 and < 300;
}