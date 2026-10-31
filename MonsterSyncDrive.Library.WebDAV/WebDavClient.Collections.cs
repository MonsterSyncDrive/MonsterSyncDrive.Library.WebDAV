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
using System.Net;
using System.Xml.Linq;
using MonsterSyncDrive.WebDAV.Core;
using MonsterSyncDrive.WebDAV.Helpers;
using MonsterSyncDrive.WebDAV.Request;
using MonsterSyncDrive.WebDAV.Response;

namespace MonsterSyncDrive.WebDAV;

/// <summary>提供指定资源类型的集合创建及由服务器分配成员 URI 的操作。</summary>
public sealed partial class WebDavClient
{
    /// <summary>原子创建指定资源类型的集合，同时设置普通文本或完整 XML 属性。</summary>
    /// <param name="requestUri">集合的标准 URI 引用。</param>
    /// <param name="parameters">集合类型、初始属性和通用条件。</param>
    /// <returns>保留 mkcol-response、属性状态和资源表示的任务。</returns>
    public Task<ExtendedMkColResponse> ExtendedMkcolAsync(string requestUri, ExtendedMkColParameters parameters) =>
        ExtendedMkcolAsync(CreateUri(requestUri), parameters);

    /// <summary>原子创建指定资源类型的集合，同时设置初始属性。</summary>
    /// <param name="requestUri">集合的绝对或相对地址。</param>
    /// <param name="parameters">扩展集合创建设置，不应为 null。</param>
    /// <returns>包含完整创建结果和逐属性错误的任务。</returns>
    public async Task<ExtendedMkColResponse> ExtendedMkcolAsync(Uri requestUri, ExtendedMkColParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        var headers = parameters.LockToken is null ? null : new[] { new KeyValuePair<string, string>("If", IfHeaderHelper.GetHeaderValue(parameters.LockToken)) };
        var response = await SendDocumentRequestAsync(requestUri, "MKCOL", parameters.CreateBody(), parameters, headers).ConfigureAwait(false);
        return new ExtendedMkColResponse(response);
    }

    /// <summary>查询集合的受保护 DAV:add-member 属性，并验证服务器分配上传地址与集合保持同源。</summary>
    /// <param name="collectionUri">目标集合 URI 引用。</param>
    /// <param name="options">查询的通用条件和取消令牌；null 使用默认。</param>
    /// <returns>集合及其 Add-Member 端点的发现结果；未返回成功属性时 IsSupported 为 false。</returns>
    public Task<AddMemberDiscoveryResponse> DiscoverAddMemberAsync(string collectionUri, WebDavRequestOptions? options = null) =>
        DiscoverAddMemberAsync(CreateUri(collectionUri), options);

    /// <summary>查询 Add-Member URI 并阻止跨服务器的自动 POST 地址。</summary>
    /// <param name="collectionUri">要发现的集合地址。</param>
    /// <param name="options">本次属性查询的设置。</param>
    /// <returns>包含已校验端点的能力响应。</returns>
    public async Task<AddMemberDiscoveryResponse> DiscoverAddMemberAsync(Uri collectionUri, WebDavRequestOptions? options = null)
    {
        var target = ResolveUri(collectionUri);
        options ??= new WebDavRequestOptions();
        var parameters = new PropfindParameters
        {
            ApplyTo = ApplyTo.Propfind.ResourceOnly, RequestType = PropfindRequestType.NamedProperties,
            CustomProperties = [WebDavXml.Dav + "add-member"]
        };
        options.CopyTo(parameters);
        var response = await Propfind(target, parameters).ConfigureAwait(false);
        Uri? endpoint = null;
        if (response.IsSuccessful)
        {
            if (response.Resources.Count != 1) throw new InvalidDataException("Add-Member discovery must return exactly the requested collection.");
            var resource = response.Resources.Single();
            if (!Uri.TryCreate(target, resource.Uri, out var resourceUri)
                || resourceUri.AbsoluteUri.TrimEnd('/') != target.AbsoluteUri.TrimEnd('/'))
                throw new InvalidDataException("Add-Member discovery returned a different resource.");
            var property = resource.Properties.SingleOrDefault(value => value.Name == WebDavXml.Dav + "add-member");
            if (property is not null)
            {
                var href = property.ToXElement().Element(WebDavXml.Dav + "href")?.Value;
                if (string.IsNullOrWhiteSpace(href) || !Uri.TryCreate(target, href, out endpoint))
                    throw new InvalidDataException("DAV:add-member has no valid href.");
                endpoint = ValidateAddMemberEndpoint(target, endpoint);
            }
        }
        return new AddMemberDiscoveryResponse(response.StatusCode, response.Description, target, endpoint, response.Resources)
        { Headers = response.Headers, Error = response.Error };
    }

    /// <summary>发现服务器的 Add-Member 端点并上传新成员，让服务器选择最终资源 URI。</summary>
    /// <param name="collectionUri">集合 URI 引用。</param>
    /// <param name="source">从当前位置开始读取的可读源流。</param>
    /// <param name="parameters">上传、Slug 和端点覆盖配置。</param>
    /// <returns>保留 Location 和任意返回表示流的结果，调用方必须释放。</returns>
    public Task<WebDavWriteResponse> AddMemberAsync(string collectionUri, Stream source, AddMemberParameters? parameters = null) =>
        AddMemberAsync(CreateUri(collectionUri), source, parameters);

    /// <summary>通过同源 Add-Member URI 流式 POST 新成员。</summary>
    /// <param name="collectionUri">目标集合地址。</param>
    /// <param name="source">上传源流，默认保留源流。</param>
    /// <param name="parameters">创建成员配置，null 使用默认。</param>
    /// <returns>服务器分配 URI 及资源表示的流式结果。</returns>
    public async Task<WebDavWriteResponse> AddMemberAsync(Uri collectionUri, Stream source, AddMemberParameters? parameters = null)
    {
        parameters ??= new AddMemberParameters();
        using var content = new UploadContent(source, parameters);
        return await AddMemberAsync(collectionUri, content, parameters).ConfigureAwait(false);
    }

    /// <summary>使用自定义 HTTP 正文向服务器分配地址的成员创建入口提交数据。</summary>
    /// <param name="collectionUri">目标集合 URI 引用。</param>
    /// <param name="content">由本操作接管的上传正文。</param>
    /// <param name="parameters">内容类型、端点与创建条件。</param>
    /// <returns>包含 Location 及流式返回表示的响应。</returns>
    public Task<WebDavWriteResponse> AddMemberAsync(string collectionUri, HttpContent content, AddMemberParameters? parameters = null) =>
        AddMemberAsync(CreateUri(collectionUri), content, parameters);

    /// <summary>发现或验证上传端点，发送 POST，校验成功创建响应包含服务器分配的位置。</summary>
    /// <param name="collectionUri">集合地址。</param>
    /// <param name="content">本操作负责释放的正文。</param>
    /// <param name="parameters">创建成员设置。</param>
    /// <returns>需要调用方关闭的响应表示流。</returns>
    public async Task<WebDavWriteResponse> AddMemberAsync(Uri collectionUri, HttpContent content, AddMemberParameters? parameters = null)
    {
        ArgumentNullException.ThrowIfNull(content);
        parameters ??= new AddMemberParameters();
        var collection = ResolveUri(collectionUri);
        try
        {
            Uri endpoint;
            if (parameters.AddMemberUri is null)
            {
                // 创建条件属于即将写入的新成员，不能误用到发现阶段的集合 GET/PROPFIND 上。
                var discovered = await DiscoverAddMemberAsync(collection, new WebDavRequestOptions
                { Headers = parameters.Headers, CancellationToken = parameters.CancellationToken }).ConfigureAwait(false);
                discovered.EnsureSuccessStatusCode();
                endpoint = discovered.AddMemberUri ?? throw new NotSupportedException("The collection does not advertise DAV:add-member.");
            }
            else endpoint = ValidateAddMemberEndpoint(collection, parameters.AddMemberUri);
            using var request = CreateRequest(endpoint, "POST", null, content);
            content.Headers.ContentType = parameters.ContentType;
            AddLock(request, parameters.LockToken);
            if (parameters.SuggestedName is not null)
                request.Headers.Add("Slug", Uri.EscapeDataString(parameters.SuggestedName));
            parameters.Apply(request);
            var response = await SendAsync(request, parameters.CancellationToken).ConfigureAwait(false);
            try
            {
                var stream = await response.Content.ReadAsStreamAsync(parameters.CancellationToken).ConfigureAwait(false);
                var result = new WebDavWriteResponse(response, stream, endpoint);
                // 成功且同步完成的 RFC 5995 创建必须告诉调用方新资源 URI；202 可继续通过 Location 跟踪异步操作。
                if (response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.Accepted && result.Location is null)
                    throw new InvalidDataException("A successful Add-Member response must include Location.");
                return result;
            }
            catch { response.Dispose(); throw; }
        }
        catch { content.Dispose(); throw; }
    }

    /// <summary>解析并校验 Add-Member URI，禁止向另一来源或带嵌入凭据的地址自动上传。</summary>
    /// <param name="collection">原集合绝对地址。</param>
    /// <param name="endpoint">服务器返回或调用方显式设置的端点引用。</param>
    /// <returns>通过校验的绝对端点。</returns>
    private Uri ValidateAddMemberEndpoint(Uri collection, Uri endpoint)
    {
        var absolute = ResolveUri(endpoint.IsAbsoluteUri ? endpoint : new Uri(collection, endpoint));
        if (!string.Equals(collection.Scheme, absolute.Scheme, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(collection.IdnHost, absolute.IdnHost, StringComparison.OrdinalIgnoreCase) || collection.Port != absolute.Port)
            throw new InvalidDataException("The Add-Member endpoint must have the same origin as its collection.");
        return absolute;
    }
}

/// <summary>公开扩展集合创建和服务器分配成员 URI 的客户端契约。</summary>
public partial interface IWebDavClient
{
    /// <summary>以字符串 URI 创建指定类型的集合及属性。</summary>
    /// <param name="requestUri">目标集合 URI 引用。</param>
    /// <param name="parameters">创建类型和属性。</param>
    /// <returns>完整扩展创建结果。</returns>
    Task<ExtendedMkColResponse> ExtendedMkcolAsync(string requestUri, ExtendedMkColParameters parameters);
    /// <summary>以 Uri 创建指定类型的集合及属性。</summary>
    /// <param name="requestUri">目标集合 URI。</param>
    /// <param name="parameters">创建类型和属性。</param>
    /// <returns>完整扩展创建结果。</returns>
    Task<ExtendedMkColResponse> ExtendedMkcolAsync(Uri requestUri, ExtendedMkColParameters parameters);
    /// <summary>发现集合创建成员的服务器端点。</summary>
    /// <param name="collectionUri">集合 URI 引用。</param>
    /// <param name="options">请求条件。</param>
    /// <returns>同源创建端点和状态。</returns>
    Task<AddMemberDiscoveryResponse> DiscoverAddMemberAsync(string collectionUri, WebDavRequestOptions? options = null);
    /// <summary>发现集合创建成员的服务器端点。</summary>
    /// <param name="collectionUri">集合 URI。</param>
    /// <param name="options">请求条件。</param>
    /// <returns>同源创建端点和状态。</returns>
    Task<AddMemberDiscoveryResponse> DiscoverAddMemberAsync(Uri collectionUri, WebDavRequestOptions? options = null);
    /// <summary>流式上传新成员并让服务器分配 URI。</summary>
    /// <param name="collectionUri">集合 URI 引用。</param>
    /// <param name="source">源流。</param>
    /// <param name="parameters">创建设置。</param>
    /// <returns>需要释放的响应表示流。</returns>
    Task<WebDavWriteResponse> AddMemberAsync(string collectionUri, Stream source, AddMemberParameters? parameters = null);
    /// <summary>流式上传新成员并让服务器分配 URI。</summary>
    /// <param name="collectionUri">集合 URI。</param>
    /// <param name="source">源流。</param>
    /// <param name="parameters">创建设置。</param>
    /// <returns>需要释放的响应表示流。</returns>
    Task<WebDavWriteResponse> AddMemberAsync(Uri collectionUri, Stream source, AddMemberParameters? parameters = null);
    /// <summary>使用自定义正文上传新成员。</summary>
    /// <param name="collectionUri">集合 URI 引用。</param>
    /// <param name="content">由操作释放的正文。</param>
    /// <param name="parameters">创建设置。</param>
    /// <returns>需要释放的响应表示流。</returns>
    Task<WebDavWriteResponse> AddMemberAsync(string collectionUri, HttpContent content, AddMemberParameters? parameters = null);
    /// <summary>使用自定义正文上传新成员。</summary>
    /// <param name="collectionUri">集合 URI。</param>
    /// <param name="content">由操作释放的正文。</param>
    /// <param name="parameters">创建设置。</param>
    /// <returns>需要释放的响应表示流。</returns>
    Task<WebDavWriteResponse> AddMemberAsync(Uri collectionUri, HttpContent content, AddMemberParameters? parameters = null);
}
