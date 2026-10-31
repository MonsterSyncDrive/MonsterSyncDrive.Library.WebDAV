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
using System.Xml.Linq;
using MonsterSyncDrive.WebDAV.Core;
using MonsterSyncDrive.WebDAV.Request;
using MonsterSyncDrive.WebDAV.Response;

namespace MonsterSyncDrive.WebDAV;

/// <summary>搜索、查询模式发现及配额扩展的客户端接口。</summary>
public partial interface IWebDavClient
{
    /// <summary>执行完整搜索并保留评分、截断提示和根级 XML。</summary>
    /// <param name="requestUri">搜索仲裁资源的 URI 引用。</param>
    /// <param name="parameters">类型化搜索参数。</param>
    /// <returns>包含完整查询结果的任务。</returns>
    Task<WebDavSearchResponse> SearchDocumentAsync(string requestUri, SearchParameters parameters);

    /// <summary>请求指定搜索范围的 DAV:basicsearch 查询模式。</summary>
    /// <param name="requestUri">接受 SEARCH 的仲裁资源地址。</param>
    /// <param name="parameters">范围和通用请求设置；Filter 和结果字段不参与 QSD。</param>
    /// <returns>属性能力及操作符描述。</returns>
    Task<WebDavSearchSchemaResponse> DiscoverSearchSchemaAsync(string requestUri, SearchParameters? parameters = null);

    /// <summary>发现目标资源支持的 XML 搜索语法。</summary>
    /// <param name="requestUri">目标资源的 URI 引用。</param>
    /// <param name="options">可选通用请求设置。</param>
    /// <returns>完整语法名称和查询状态。</returns>
    Task<WebDavSearchGrammarResponse> GetSearchGrammarsAsync(string requestUri, WebDavRequestOptions? options = null);

    /// <summary>查询标准已用字节与可用额度，不将未知值解释为零或无限。</summary>
    /// <param name="requestUri">通常为集合的 URI 引用。</param>
    /// <param name="options">可选通用条件、请求头和取消设置。</param>
    /// <returns>包含配额值与属性状态的任务。</returns>
    Task<WebDavQuotaResponse> GetQuotaAsync(string requestUri, WebDavRequestOptions? options = null);
}

/// <summary>实现完整搜索结果、查询模式发现与配额查询。</summary>
public sealed partial class WebDavClient
{
    /// <summary>发送 basicsearch 并保留响应评分、截断提示和根级扩展字段。</summary>
    /// <param name="requestUri">接受 SEARCH 的仲裁资源地址。</param>
    /// <param name="parameters">表达式、搜索范围、排序、数量限制及 HTTP 条件。</param>
    /// <returns>包含评分、资源和完整 XML 的搜索结果。</returns>
    public async Task<WebDavSearchResponse> SearchDocumentAsync(string requestUri, SearchParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        var body = SearchRequestBuilder.BuildRequest(parameters);
        var uri = ResolveUri(CreateUri(requestUri));
        var response = await SendDocumentRequestAsync(uri, "SEARCH", body, parameters).ConfigureAwait(false);
        return new WebDavSearchResponse(response, uri);
    }

    /// <summary>通过 QSD 请求发现 basicsearch 在指定范围中允许的属性、数据类型和可选运算。</summary>
    /// <param name="requestUri">查询仲裁资源的 URI 引用。</param>
    /// <param name="parameters">范围及通用设置；null 搜索默认 / 的全部后代。</param>
    /// <returns>包含每个资源发现结果的任务。</returns>
    public async Task<WebDavSearchSchemaResponse> DiscoverSearchSchemaAsync(string requestUri, SearchParameters? parameters = null)
    {
        parameters ??= new SearchParameters();
        var body = new XElement(WebDavXml.Dav + "query-schema-discovery",
            new XElement(WebDavXml.Dav + "basicsearch", SearchRequestBuilder.BuildFrom(parameters)));
        var response = await SendDocumentRequestAsync(CreateUri(requestUri), "SEARCH", body, parameters).ConfigureAwait(false);
        return new WebDavSearchSchemaResponse(response);
    }

    /// <summary>显式查询 supported-query-grammar-set，不能依赖 allprop 自动返回该能力属性。</summary>
    /// <param name="requestUri">目标资源 URI 引用。</param>
    /// <param name="options">条件、请求头和取消选项。</param>
    /// <returns>保留发现状态和完整语法名称的结果。</returns>
    public async Task<WebDavSearchGrammarResponse> GetSearchGrammarsAsync(string requestUri, WebDavRequestOptions? options = null)
    {
        var response = await QuerySearchPropertiesAsync(requestUri, [WebDavXml.Dav + "supported-query-grammar-set"], options).ConfigureAwait(false);
        return new WebDavSearchGrammarResponse(response);
    }

    /// <summary>以 Depth: 0 显式请求标准配额属性。</summary>
    /// <param name="requestUri">待查询的资源 URI，通常为集合。</param>
    /// <param name="options">可选通用 HTTP 设置。</param>
    /// <returns>配额快照；缺失值为 null，对应失败属性状态仍保留。</returns>
    public async Task<WebDavQuotaResponse> GetQuotaAsync(string requestUri, WebDavRequestOptions? options = null)
    {
        var response = await QuerySearchPropertiesAsync(requestUri,
            [WebDavXml.Dav + "quota-used-bytes", WebDavXml.Dav + "quota-available-bytes"], options).ConfigureAwait(false);
        return new WebDavQuotaResponse(response);
    }

    /// <summary>按单资源查询扩展属性，并复制通用请求条件，避免修改调用方选项。</summary>
    /// <param name="requestUri">查询地址。</param>
    /// <param name="properties">必须显式查询的属性列表。</param>
    /// <param name="options">可选通用请求设置。</param>
    /// <returns>完整属性查询结果。</returns>
    private Task<PropfindResponse> QuerySearchPropertiesAsync(string requestUri, IReadOnlyCollection<XName> properties, WebDavRequestOptions? options)
    {
        var parameters = new PropfindParameters
        {
            ApplyTo = ApplyTo.Propfind.ResourceOnly,
            RequestType = PropfindRequestType.NamedProperties,
            CustomProperties = properties
        };
        if (options is not null)
            options.CopyTo(parameters);
        return Propfind(requestUri, parameters);
    }
}