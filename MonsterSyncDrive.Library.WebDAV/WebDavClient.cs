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
using MonsterSyncDrive.Library.WebDAV;
using MonsterSyncDrive.Library.WebDAV.Core;
using MonsterSyncDrive.Library.WebDAV.Helpers;
using MonsterSyncDrive.Library.WebDAV.Request;
using MonsterSyncDrive.Library.WebDAV.Response;
using System.Buffers;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Xml.Linq;

namespace MonsterSyncDrive.WebDAV;

/// <summary>
/// 基于 HttpClient 实现跨平台、支持 NativeAOT 的异步 WebDAV 客户端。
/// </summary>
/// <remarks>实例支持并发独立请求，不使用动态序列化或反射；HTTP 失败通过响应报告，网络、取消和解析失败抛出异常。</remarks>
public sealed partial class WebDavClient : IWebDavClient
{
    /// <summary>
    /// 统一负责 HTTP 发送、客户端所有权及请求超时和取消传播的调度器。
    /// </summary>
    private readonly WebDavDispatcher _dispatcher;
    /// <summary>
    /// 允许单个 DAV XML 文档包含的最大字符总数，用于限制异常或恶意的超大响应。
    /// </summary>
    private readonly long _maxXmlCharacters;

    /// <summary>
    /// 使用默认网络配置创建客户端，并拥有其内部 HttpClient。
    /// </summary>
    /// <remarks>未配置 BaseAddress，因此需传入绝对 HTTP(S) URI；默认请求超时 100 秒，XML 上限为 64 × 1024 × 1024 个字符。</remarks>
    public WebDavClient() : this(new WebDavClientParams()) { }

    /// <summary>
    /// 按指定连接配置创建并拥有独立的 HTTP 客户端。
    /// </summary>
    /// <remarks>显式凭据优先于系统默认凭据；内部 HttpClient 的头部计时关闭，由调度器将超时延续到正文阶段。</remarks>
    /// <param name="parameters">用于创建客户端的地址、认证、代理、超时与 XML 上限配置。</param>
    /// <exception cref="ArgumentNullException">parameters 为 null。</exception>
    /// <exception cref="ArgumentOutOfRangeException">请求超时或 XML 字符上限不合法。</exception>
    /// <exception cref="ArgumentException">基地址不是允许的 HTTP(S) URI，或包含凭据及片段。</exception>
    public WebDavClient(WebDavClientParams parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        ValidateTimeout(parameters.Timeout ?? TimeSpan.FromSeconds(100));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(parameters.MaxXmlCharacters);
        if (parameters.BaseAddress is not null)
            ValidateAbsoluteUri(parameters.BaseAddress);
        // Range 的偏移与长度针对原始文件字节。关闭自动解压，避免响应头仍描述压缩前后不同的字节区间。
        // 自动重定向由配置显式开启；通用重定向可能改变 DAV 方法，不能默认代替协议层的地址处理。
        HttpClientHandler handler = new()
        {
            AutomaticDecompression = DecompressionMethods.None,
            AllowAutoRedirect = parameters.AllowAutoRedirect,
            PreAuthenticate = parameters.PreAuthenticate,
            UseDefaultCredentials = parameters.UseDefaultCredentials,
            UseProxy = parameters.UseProxy,
            Proxy = parameters.Proxy
        };
        if (parameters.Credentials is not null)
        {
            handler.UseDefaultCredentials = false;
            handler.Credentials = parameters.Credentials;
        }
        // HttpClient 在 ResponseHeadersRead 模式下只等待响应头，自己的超时不能覆盖之后的流式读取。
        // 内部关闭这层计时，统一由调度器创建的关联令牌控制请求头、正文传输以及调用方读取等待。
        HttpClient client = new(handler)
        {
            BaseAddress = parameters.BaseAddress,
            Timeout = Timeout.InfiniteTimeSpan
        };
        try
        {
            foreach (KeyValuePair<string, string> header in parameters.DefaultRequestHeaders)
                client.DefaultRequestHeaders.Add(header.Key, header.Value);
        }
        catch
        {
            client.Dispose();
            throw;
        }
        _dispatcher = new WebDavDispatcher(client, true, parameters.Timeout ?? TimeSpan.FromSeconds(100));
        _maxXmlCharacters = parameters.MaxXmlCharacters;
    }

    /// <summary>
    /// 使用宿主提供的 HttpClient 创建 WebDAV 客户端，并明确约定连接所有权。
    /// </summary>
    /// <remarks>默认不释放传入的 HttpClient，但释放 WebDavClient 仍会取消它发起的请求。宿主 HttpClient 的超时可能先于本库的完整请求超时生效。</remarks>
    /// <param name="httpClient">已经配置好的 HttpClient，可与宿主的连接池及处理器配置共享。</param>
    /// <param name="disposeHttpClient">true 表示把 HttpClient 所有权转交给本客户端；默认 false，仅借用。</param>
    /// <param name="requestTimeout">覆盖本次请求全部生命周期的默认超时；null 沿用 HttpClient.Timeout，可使用 Timeout.InfiniteTimeSpan。</param>
    /// <param name="maxXmlCharacters">单个 DAV XML 文档允许的最大字符总数，必须为正数，默认 64 × 1024 × 1024。</param>
    /// <exception cref="ArgumentNullException">httpClient 为 null。</exception>
    /// <exception cref="ArgumentOutOfRangeException">请求超时或 XML 字符上限不合法。</exception>
    /// <exception cref="ArgumentException">HttpClient 的基地址不符合本库的 URI 约定。</exception>
    public WebDavClient(HttpClient httpClient, bool disposeHttpClient = false, TimeSpan? requestTimeout = null,
        long maxXmlCharacters = 64 * 1024 * 1024)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ValidateTimeout(requestTimeout ?? httpClient.Timeout);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxXmlCharacters);
        if (httpClient.BaseAddress is not null)
            ValidateAbsoluteUri(httpClient.BaseAddress);
        _dispatcher = new WebDavDispatcher(httpClient, disposeHttpClient, requestTimeout ?? httpClient.Timeout);
        _maxXmlCharacters = maxXmlCharacters;
    }

    /// <summary>
    /// 获取解析相对请求 URI 使用的 HTTP 基地址。
    /// </summary>
    /// <value>未配置时为 null，只能发送绝对请求 URI；相对地址按标准 URI 规则解析。</value>
    public Uri? BaseAddress => _dispatcher.BaseAddress;

    /// <summary>
    /// 异步发送原始 HTTP 或 DAV 扩展请求，将请求取消延续到响应正文。
    /// </summary>
    /// <remarks>用于 ACL 等扩展或需要自定义解析的响应；仅负责 URI 校验与网络生命周期，不自动解析 XML，不把普通 HTTP 失败状态转成异常。</remarks>
    /// <param name="request">由调用方拥有的原始 HTTP 请求，必须设置请求 URI；调用方负责释放。</param>
    /// <param name="cancellationToken">取消当前操作及响应正文读取的令牌，默认 CancellationToken.None。</param>
    /// <returns>响应头到达后完成的任务；调用方拥有返回的 HTTP 响应及传入的请求，两者均须释放。</returns>
    /// <exception cref="OperationCanceledException">调用方取消、客户端释放或请求超时使操作中止。</exception>
    /// <exception cref="HttpRequestException">发生网络连接或 HTTP 传输失败；普通 HTTP 错误状态通常保留在结果中。</exception>
    public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.RequestUri = ResolveUri(request.RequestUri ?? throw new ArgumentException("A request URI is required.", nameof(request)));
        return _dispatcher.SendAsync(request, cancellationToken);
    }

    /// <summary>
    /// 异步查询资源的 DAV 属性，完整读取多状态文档后返回资源快照。
    /// </summary>
    /// <remarks>成功响应必须为合法 HTTP 207。可选属性 404 保留在 PropertyStatuses，资源级失败影响整体结果；需要流式处理时使用 EnumerateAsync。 此重载使用该操作的默认请求参数。</remarks>
    /// <param name="requestUri">请求资源的标准 URI 引用，可为绝对地址或相对 BaseAddress 的已编码路径；普通文件名可先用 WebDavUri.EncodePath 编码。</param>
    /// <returns>异步查询任务；结果包含资源、逐属性状态和响应头快照。</returns>
    /// <exception cref="OperationCanceledException">调用方取消、客户端释放或请求超时使操作中止。</exception>
    /// <exception cref="HttpRequestException">发生网络连接或 HTTP 传输失败；普通 HTTP 错误状态通常保留在结果中。</exception>
    /// <exception cref="System.IO.InvalidDataException">成功 HTTP 状态不符合 DAV 协议，或响应缺少必需结构及确认信息。</exception>
    public Task<PropfindResponse> Propfind(string requestUri)
    {
        return Propfind(CreateUri(requestUri));
    }

    /// <summary>
    /// 异步查询资源的 DAV 属性，完整读取多状态文档后返回资源快照。
    /// </summary>
    /// <remarks>成功响应必须为合法 HTTP 207。可选属性 404 保留在 PropertyStatuses，资源级失败影响整体结果；需要流式处理时使用 EnumerateAsync。 此重载使用该操作的默认请求参数。</remarks>
    /// <param name="requestUri">请求资源的标准 URI 引用，可为绝对地址或相对 BaseAddress 的已编码路径；普通文件名可先用 WebDavUri.EncodePath 编码。</param>
    /// <returns>异步查询任务；结果包含资源、逐属性状态和响应头快照。</returns>
    /// <exception cref="OperationCanceledException">调用方取消、客户端释放或请求超时使操作中止。</exception>
    /// <exception cref="HttpRequestException">发生网络连接或 HTTP 传输失败；普通 HTTP 错误状态通常保留在结果中。</exception>
    /// <exception cref="System.IO.InvalidDataException">成功 HTTP 状态不符合 DAV 协议，或响应缺少必需结构及确认信息。</exception>
    public Task<PropfindResponse> Propfind(Uri requestUri)
    {
        return Propfind(requestUri, new PropfindParameters());
    }

    /// <summary>
    /// 异步查询资源的 DAV 属性，完整读取多状态文档后返回资源快照。
    /// </summary>
    /// <remarks>成功响应必须为合法 HTTP 207。可选属性 404 保留在 PropertyStatuses，资源级失败影响整体结果；需要流式处理时使用 EnumerateAsync。</remarks>
    /// <param name="requestUri">请求资源的标准 URI 引用，可为绝对地址或相对 BaseAddress 的已编码路径；普通文件名可先用 WebDavUri.EncodePath 编码。</param>
    /// <param name="parameters">本次 Propfind 操作的请求参数，包括通用 HTTP 条件、请求头及取消设置。 不应为 null。</param>
    /// <returns>异步查询任务；结果包含资源、逐属性状态和响应头快照。</returns>
    /// <exception cref="OperationCanceledException">调用方取消、客户端释放或请求超时使操作中止。</exception>
    /// <exception cref="HttpRequestException">发生网络连接或 HTTP 传输失败；普通 HTTP 错误状态通常保留在结果中。</exception>
    /// <exception cref="System.IO.InvalidDataException">成功 HTTP 状态不符合 DAV 协议，或响应缺少必需结构及确认信息。</exception>
    public Task<PropfindResponse> Propfind(string requestUri, PropfindParameters parameters)
    {
        return Propfind(CreateUri(requestUri), parameters);
    }

    /// <summary>
    /// 异步查询资源的 DAV 属性，完整读取多状态文档后返回资源快照。
    /// </summary>
    /// <remarks>成功响应必须为合法 HTTP 207。可选属性 404 保留在 PropertyStatuses，资源级失败影响整体结果；需要流式处理时使用 EnumerateAsync。</remarks>
    /// <param name="requestUri">请求资源的标准 URI 引用，可为绝对地址或相对 BaseAddress 的已编码路径；普通文件名可先用 WebDavUri.EncodePath 编码。</param>
    /// <param name="parameters">本次 Propfind 操作的请求参数，包括通用 HTTP 条件、请求头及取消设置。 不应为 null。</param>
    /// <returns>异步查询任务；结果包含资源、逐属性状态和响应头快照。</returns>
    /// <exception cref="OperationCanceledException">调用方取消、客户端释放或请求超时使操作中止。</exception>
    /// <exception cref="HttpRequestException">发生网络连接或 HTTP 传输失败；普通 HTTP 错误状态通常保留在结果中。</exception>
    /// <exception cref="System.IO.InvalidDataException">成功 HTTP 状态不符合 DAV 协议，或响应缺少必需结构及确认信息。</exception>
    public async Task<PropfindResponse> Propfind(Uri requestUri, PropfindParameters parameters)
    {
        using HttpRequestMessage request = CreatePropfind(requestUri, parameters);
        return await SendPropertyRequestAsync(request, parameters.CancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 异步逐条枚举 PROPFIND 资源，避免一次性物化全部目录项。
    /// </summary>
    /// <remarks>完整枚举后才能确认文档结尾有效；已产出条目后仍可能遇到截断错误。原子快照应先完整收集，或使用 Propfind。</remarks>
    /// <param name="requestUri">请求资源的标准 URI 引用，可为绝对地址或相对 BaseAddress 的已编码路径；普通文件名可先用 WebDavUri.EncodePath 编码。</param>
    /// <param name="parameters">本次 EnumerateAsync 操作的请求参数，包括通用 HTTP 条件、请求头及取消设置。 null 使用默认参数。</param>
    /// <param name="cancellationToken">取消当前操作及响应正文读取的令牌，默认 CancellationToken.None。</param>
    /// <returns>按服务器顺序返回资源的异步序列；开始枚举时才发送请求，结束或释放枚举器时关闭网络资源。</returns>
    /// <exception cref="OperationCanceledException">调用方取消、客户端释放或请求超时使操作中止。</exception>
    /// <exception cref="HttpRequestException">发生网络连接或 HTTP 传输失败；普通 HTTP 错误状态通常保留在结果中。</exception>
    /// <exception cref="System.IO.InvalidDataException">成功 HTTP 状态不符合 DAV 协议，或响应缺少必需结构及确认信息。</exception>
    public async IAsyncEnumerable<WebDavResource> EnumerateAsync(string requestUri, PropfindParameters? parameters = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        parameters ??= new PropfindParameters();
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(parameters.CancellationToken, cancellationToken);
        using HttpRequestMessage request = CreatePropfind(CreateUri(requestUri), parameters);
        using HttpResponseMessage response = await SendAsync(request, linked.Token).ConfigureAwait(false);
        _ = response.EnsureSuccessStatusCode();
        if ((int)response.StatusCode != 207)
            throw new InvalidDataException("PROPFIND did not return HTTP 207.");
        await foreach (XElement? element in WebDavXml.ReadResponsesAsync(response, _maxXmlCharacters, linked.Token).ConfigureAwait(false))
            yield return WebDavXml.ParseResource(element);
    }

    /// <summary>
    /// 构建 PROPFIND 请求，处理空正文模式、默认 Depth 和请求头覆盖。
    /// </summary>
    /// <param name="uri">已转换为 Uri 的资源地址，可相对基地址解析。</param>
    /// <param name="parameters">查询模式、属性、命名空间、深度和通用条件。</param>
    /// <returns>调用方需要释放的独立 HTTP 请求对象。</returns>
    private HttpRequestMessage CreatePropfind(Uri uri, PropfindParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        // 无正文是协议定义的隐式 allprop，不能用一个不完整的 propfind 元素代替。
        HttpContent? content = parameters.RequestType == PropfindRequestType.AllPropertiesImplied ? null
            : XmlContent(PropfindRequestBuilder.BuildRequest(parameters.RequestType, parameters.CustomProperties, parameters.Namespaces), parameters.ContentType);
        HttpRequestMessage request = CreateRequest(uri, "PROPFIND", null, content);
        try
        {
            request.Headers.Add("Depth", DepthHeaderHelper.GetValueForPropfind(parameters.ApplyTo ?? ApplyTo.Propfind.ResourceAndChildren));
            parameters.Apply(request);
            return request;
        }
        catch
        {
            request.Dispose();
            throw;
        }
    }

    /// <summary>
    /// 异步设置或删除资源属性，并检查服务器是否确认全部请求属性。
    /// </summary>
    /// <remarks>字符串属性按文本转义，结构化属性使用完整 XElement；空修改或成功结果漏报属性确认会作为错误报告。</remarks>
    /// <param name="requestUri">请求资源的标准 URI 引用，可为绝对地址或相对 BaseAddress 的已编码路径；普通文件名可先用 WebDavUri.EncodePath 编码。</param>
    /// <param name="parameters">本次 Proppatch 操作的请求参数，包括通用 HTTP 条件、请求头及取消设置。 不应为 null。</param>
    /// <returns>异步修改任务；结果保留逐属性及资源状态，任一属性修改失败会使 IsSuccessful 为 false。</returns>
    /// <exception cref="OperationCanceledException">调用方取消、客户端释放或请求超时使操作中止。</exception>
    /// <exception cref="HttpRequestException">发生网络连接或 HTTP 传输失败；普通 HTTP 错误状态通常保留在结果中。</exception>
    /// <exception cref="System.IO.InvalidDataException">成功 HTTP 状态不符合 DAV 协议，或响应缺少必需结构及确认信息。</exception>
    public Task<ProppatchResponse> Proppatch(string requestUri, ProppatchParameters parameters)
    {
        return Proppatch(CreateUri(requestUri), parameters);
    }

    /// <summary>
    /// 异步设置或删除资源属性，并检查服务器是否确认全部请求属性。
    /// </summary>
    /// <remarks>字符串属性按文本转义，结构化属性使用完整 XElement；空修改或成功结果漏报属性确认会作为错误报告。</remarks>
    /// <param name="requestUri">请求资源的标准 URI 引用，可为绝对地址或相对 BaseAddress 的已编码路径；普通文件名可先用 WebDavUri.EncodePath 编码。</param>
    /// <param name="parameters">本次 Proppatch 操作的请求参数，包括通用 HTTP 条件、请求头及取消设置。 不应为 null。</param>
    /// <returns>异步修改任务；结果保留逐属性及资源状态，任一属性修改失败会使 IsSuccessful 为 false。</returns>
    /// <exception cref="OperationCanceledException">调用方取消、客户端释放或请求超时使操作中止。</exception>
    /// <exception cref="HttpRequestException">发生网络连接或 HTTP 传输失败；普通 HTTP 错误状态通常保留在结果中。</exception>
    /// <exception cref="System.IO.InvalidDataException">成功 HTTP 状态不符合 DAV 协议，或响应缺少必需结构及确认信息。</exception>
    public async Task<ProppatchResponse> Proppatch(Uri requestUri, ProppatchParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        if (parameters.PropertiesToSet.Count == 0 && parameters.XmlPropertiesToSet.Count == 0 && parameters.PropertiesToRemove.Count == 0)
            throw new ArgumentException("At least one property update is required.", nameof(parameters));
        XElement root = ProppatchRequestBuilder.BuildRequest(parameters.PropertiesToSet, parameters.PropertiesToRemove, parameters.Namespaces);
        // 标量值已在构建器中按文本转义；结构化值复制完整元素，保留元素属性与混合内容。
        // 两种表示保持明确，普通字符串中的尖括号不会被当成 XML 执行或展开。
        if (parameters.XmlPropertiesToSet.Count > 0)
            root.Add(new XElement(WebDavXml.Dav + "set", new XElement(WebDavXml.Dav + "prop", parameters.XmlPropertiesToSet.Select(value => new XElement(value)))));
        using HttpRequestMessage request = CreateRequest(requestUri, "PROPPATCH", null, XmlContent(root, parameters.ContentType));
        AddLock(request, parameters.LockToken);
        parameters.Apply(request);
        var minimal = GetRequestPreferences(request).Return == WebDavReturnPreference.Minimal;
        using HttpResponseMessage response = await SendAsync(request, parameters.CancellationToken).ConfigureAwait(false);
        WebDavDocumentResponse document = await ReadDocumentResponseAsync(response, parameters.CancellationToken).ConfigureAwait(false);
        IReadOnlyCollection<WebDavResource> resources = document.Resources;
        if (response.IsSuccessStatusCode && (int)response.StatusCode != 207
            && !(minimal && response.StatusCode is HttpStatusCode.OK or HttpStatusCode.NoContent))
            throw new InvalidDataException("Expected HTTP 207, or a successful minimal PROPPATCH response.");
        if ((int)response.StatusCode == 207)
        {
            // HTTP 207 只表示存在逐项结果，不表示每个修改已成功。
            // 对成功资源要求设置、结构化设置及删除涉及的每个属性都得到确认；漏报不能算成功。
            // 资源本身明确失败时保留失败状态，由响应模型的 IsSuccessful 统一报告。
            HashSet<XName> expected = parameters.PropertiesToSet.Keys.Concat(parameters.XmlPropertiesToSet.Select(value => value.Name))
                .Concat(parameters.PropertiesToRemove).ToHashSet();
            if (resources.Count == 0 || resources.Any(resource => (resource.StatusCode is null or (>= 200 and < 300))
                && !expected.IsSubsetOf(resource.PropertyStatuses.Select(status => status.Name))))
                throw new InvalidDataException("PROPPATCH did not acknowledge all requested properties.");
        }
        return new ProppatchResponse((int)response.StatusCode, response.ReasonPhrase, resources.SelectMany(resource => resource.PropertyStatuses))
        {
            Resources = resources,
            Error = document.Error,
            Document = document.Document,
            Representation = document.Representation
        }.CaptureHeaders<ProppatchResponse>(response);
    }

    /// <summary>
    /// 异步在指定 URI 创建 WebDAV 集合。
    /// </summary>
    /// <remarks>只创建指定集合，不递归创建父目录；目标存在或父目录缺失时由服务器返回对应状态。</remarks>
    /// <param name="requestUri">请求资源的标准 URI 引用，可为绝对地址或相对 BaseAddress 的已编码路径；普通文件名可先用 WebDavUri.EncodePath 编码。</param>
    /// <returns>异步创建任务；结果包含 HTTP 状态及可能的多状态资源错误。</returns>
    /// <exception cref="OperationCanceledException">调用方取消、客户端释放或请求超时使操作中止。</exception>
    /// <exception cref="HttpRequestException">发生网络连接或 HTTP 传输失败；普通 HTTP 错误状态通常保留在结果中。</exception>
    public Task<WebDavResponse> Mkcol(string requestUri)
    {
        return Mkcol(CreateUri(requestUri));
    }

    /// <summary>
    /// 异步在指定 URI 创建 WebDAV 集合。
    /// </summary>
    /// <remarks>只创建指定集合，不递归创建父目录；目标存在或父目录缺失时由服务器返回对应状态。</remarks>
    /// <param name="requestUri">请求资源的标准 URI 引用，可为绝对地址或相对 BaseAddress 的已编码路径；普通文件名可先用 WebDavUri.EncodePath 编码。</param>
    /// <returns>异步创建任务；结果包含 HTTP 状态及可能的多状态资源错误。</returns>
    /// <exception cref="OperationCanceledException">调用方取消、客户端释放或请求超时使操作中止。</exception>
    /// <exception cref="HttpRequestException">发生网络连接或 HTTP 传输失败；普通 HTTP 错误状态通常保留在结果中。</exception>
    public Task<WebDavResponse> Mkcol(Uri requestUri)
    {
        return Mkcol(requestUri, new MkColParameters());
    }

    /// <summary>
    /// 异步在指定 URI 创建 WebDAV 集合。
    /// </summary>
    /// <remarks>只创建指定集合，不递归创建父目录；目标存在或父目录缺失时由服务器返回对应状态。</remarks>
    /// <param name="requestUri">请求资源的标准 URI 引用，可为绝对地址或相对 BaseAddress 的已编码路径；普通文件名可先用 WebDavUri.EncodePath 编码。</param>
    /// <param name="parameters">本次 Mkcol 操作的请求参数，包括通用 HTTP 条件、请求头及取消设置。 不应为 null。</param>
    /// <returns>异步创建任务；结果包含 HTTP 状态及可能的多状态资源错误。</returns>
    /// <exception cref="OperationCanceledException">调用方取消、客户端释放或请求超时使操作中止。</exception>
    /// <exception cref="HttpRequestException">发生网络连接或 HTTP 传输失败；普通 HTTP 错误状态通常保留在结果中。</exception>
    public Task<WebDavResponse> Mkcol(string requestUri, MkColParameters parameters)
    {
        return Mkcol(CreateUri(requestUri), parameters);
    }

    /// <summary>
    /// 异步在指定 URI 创建 WebDAV 集合。
    /// </summary>
    /// <remarks>只创建指定集合，不递归创建父目录；目标存在或父目录缺失时由服务器返回对应状态。</remarks>
    /// <param name="requestUri">请求资源的标准 URI 引用，可为绝对地址或相对 BaseAddress 的已编码路径；普通文件名可先用 WebDavUri.EncodePath 编码。</param>
    /// <param name="parameters">本次 Mkcol 操作的请求参数，包括通用 HTTP 条件、请求头及取消设置。 不应为 null。</param>
    /// <returns>异步创建任务；结果包含 HTTP 状态及可能的多状态资源错误。</returns>
    /// <exception cref="OperationCanceledException">调用方取消、客户端释放或请求超时使操作中止。</exception>
    /// <exception cref="HttpRequestException">发生网络连接或 HTTP 传输失败；普通 HTTP 错误状态通常保留在结果中。</exception>
    public async Task<WebDavResponse> Mkcol(Uri requestUri, MkColParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        using HttpRequestMessage request = CreateRequest(requestUri, "MKCOL");
        AddLock(request, parameters.LockToken);
        parameters.Apply(request);
        return await SendMutationAsync(request, parameters.CancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 异步删除文件或集合，保留集合内部资源的删除失败结果。
    /// </summary>
    /// <remarks>集合删除范围由服务器按协议处理；本库不把 404 自动解释为成功，也不自动重试删除。</remarks>
    /// <param name="requestUri">请求资源的标准 URI 引用，可为绝对地址或相对 BaseAddress 的已编码路径；普通文件名可先用 WebDavUri.EncodePath 编码。</param>
    /// <returns>异步删除任务；HTTP 207 时结果为 WebDavMultiStatusResponse，内部失败可通过 IsSuccessful 发现。</returns>
    /// <exception cref="OperationCanceledException">调用方取消、客户端释放或请求超时使操作中止。</exception>
    /// <exception cref="HttpRequestException">发生网络连接或 HTTP 传输失败；普通 HTTP 错误状态通常保留在结果中。</exception>
    public Task<WebDavResponse> Delete(string requestUri)
    {
        return Delete(CreateUri(requestUri));
    }

    /// <summary>
    /// 异步删除文件或集合，保留集合内部资源的删除失败结果。
    /// </summary>
    /// <remarks>集合删除范围由服务器按协议处理；本库不把 404 自动解释为成功，也不自动重试删除。</remarks>
    /// <param name="requestUri">请求资源的标准 URI 引用，可为绝对地址或相对 BaseAddress 的已编码路径；普通文件名可先用 WebDavUri.EncodePath 编码。</param>
    /// <returns>异步删除任务；HTTP 207 时结果为 WebDavMultiStatusResponse，内部失败可通过 IsSuccessful 发现。</returns>
    /// <exception cref="OperationCanceledException">调用方取消、客户端释放或请求超时使操作中止。</exception>
    /// <exception cref="HttpRequestException">发生网络连接或 HTTP 传输失败；普通 HTTP 错误状态通常保留在结果中。</exception>
    public Task<WebDavResponse> Delete(Uri requestUri)
    {
        return Delete(requestUri, new DeleteParameters());
    }

    /// <summary>
    /// 异步删除文件或集合，保留集合内部资源的删除失败结果。
    /// </summary>
    /// <remarks>集合删除范围由服务器按协议处理；本库不把 404 自动解释为成功，也不自动重试删除。</remarks>
    /// <param name="requestUri">请求资源的标准 URI 引用，可为绝对地址或相对 BaseAddress 的已编码路径；普通文件名可先用 WebDavUri.EncodePath 编码。</param>
    /// <param name="parameters">本次 Delete 操作的请求参数，包括通用 HTTP 条件、请求头及取消设置。 不应为 null。</param>
    /// <returns>异步删除任务；HTTP 207 时结果为 WebDavMultiStatusResponse，内部失败可通过 IsSuccessful 发现。</returns>
    /// <exception cref="OperationCanceledException">调用方取消、客户端释放或请求超时使操作中止。</exception>
    /// <exception cref="HttpRequestException">发生网络连接或 HTTP 传输失败；普通 HTTP 错误状态通常保留在结果中。</exception>
    public Task<WebDavResponse> Delete(string requestUri, DeleteParameters parameters)
    {
        return Delete(CreateUri(requestUri), parameters);
    }

    /// <summary>
    /// 异步删除文件或集合，保留集合内部资源的删除失败结果。
    /// </summary>
    /// <remarks>集合删除范围由服务器按协议处理；本库不把 404 自动解释为成功，也不自动重试删除。</remarks>
    /// <param name="requestUri">请求资源的标准 URI 引用，可为绝对地址或相对 BaseAddress 的已编码路径；普通文件名可先用 WebDavUri.EncodePath 编码。</param>
    /// <param name="parameters">本次 Delete 操作的请求参数，包括通用 HTTP 条件、请求头及取消设置。 不应为 null。</param>
    /// <returns>异步删除任务；HTTP 207 时结果为 WebDavMultiStatusResponse，内部失败可通过 IsSuccessful 发现。</returns>
    /// <exception cref="OperationCanceledException">调用方取消、客户端释放或请求超时使操作中止。</exception>
    /// <exception cref="HttpRequestException">发生网络连接或 HTTP 传输失败；普通 HTTP 错误状态通常保留在结果中。</exception>
    public async Task<WebDavResponse> Delete(Uri requestUri, DeleteParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        using HttpRequestMessage request = CreateRequest(requestUri, "DELETE");
        AddLock(request, parameters.LockToken);
        parameters.Apply(request);
        return await SendMutationAsync(request, parameters.CancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 异步打开原始文件正文流，支持单段字节范围与 HTTP 版本条件。
    /// </summary>
    /// <remarks>发送 Translate: f 和 Accept-Encoding: identity；拒绝收到的非 identity 内容编码，206 时校验 Content-Range。服务器可忽略 Range 返回完整 200，调用方必须检查状态。</remarks>
    /// <param name="requestUri">请求资源的标准 URI 引用，可为绝对地址或相对 BaseAddress 的已编码路径；普通文件名可先用 WebDavUri.EncodePath 编码。</param>
    /// <returns>包含下载流和响应头快照的异步结果；调用方应使用 using 或 await using 释放。</returns>
    /// <exception cref="OperationCanceledException">调用方取消、客户端释放或请求超时使操作中止。</exception>
    /// <exception cref="HttpRequestException">发生网络连接或 HTTP 传输失败；普通 HTTP 错误状态通常保留在结果中。</exception>
    /// <exception cref="System.IO.InvalidDataException">分段范围不合法，或原始文件响应包含不允许的内容编码。</exception>
    public Task<WebDavStreamResponse> GetRawFile(string requestUri)
    {
        return GetRawFile(CreateUri(requestUri));
    }

    /// <summary>
    /// 异步打开原始文件正文流，支持单段字节范围与 HTTP 版本条件。
    /// </summary>
    /// <remarks>发送 Translate: f 和 Accept-Encoding: identity；拒绝收到的非 identity 内容编码，206 时校验 Content-Range。服务器可忽略 Range 返回完整 200，调用方必须检查状态。</remarks>
    /// <param name="requestUri">请求资源的标准 URI 引用，可为绝对地址或相对 BaseAddress 的已编码路径；普通文件名可先用 WebDavUri.EncodePath 编码。</param>
    /// <returns>包含下载流和响应头快照的异步结果；调用方应使用 using 或 await using 释放。</returns>
    /// <exception cref="OperationCanceledException">调用方取消、客户端释放或请求超时使操作中止。</exception>
    /// <exception cref="HttpRequestException">发生网络连接或 HTTP 传输失败；普通 HTTP 错误状态通常保留在结果中。</exception>
    /// <exception cref="System.IO.InvalidDataException">分段范围不合法，或原始文件响应包含不允许的内容编码。</exception>
    public Task<WebDavStreamResponse> GetRawFile(Uri requestUri)
    {
        return GetRawFile(requestUri, new GetFileParameters());
    }

    /// <summary>
    /// 异步打开原始文件正文流，支持单段字节范围与 HTTP 版本条件。
    /// </summary>
    /// <remarks>发送 Translate: f 和 Accept-Encoding: identity；拒绝收到的非 identity 内容编码，206 时校验 Content-Range。服务器可忽略 Range 返回完整 200，调用方必须检查状态。</remarks>
    /// <param name="requestUri">请求资源的标准 URI 引用，可为绝对地址或相对 BaseAddress 的已编码路径；普通文件名可先用 WebDavUri.EncodePath 编码。</param>
    /// <param name="parameters">本次 GetRawFile 操作的请求参数，包括通用 HTTP 条件、请求头及取消设置。 不应为 null。</param>
    /// <returns>包含下载流和响应头快照的异步结果；调用方应使用 using 或 await using 释放。</returns>
    /// <exception cref="OperationCanceledException">调用方取消、客户端释放或请求超时使操作中止。</exception>
    /// <exception cref="HttpRequestException">发生网络连接或 HTTP 传输失败；普通 HTTP 错误状态通常保留在结果中。</exception>
    /// <exception cref="System.IO.InvalidDataException">分段范围不合法，或原始文件响应包含不允许的内容编码。</exception>
    public Task<WebDavStreamResponse> GetRawFile(string requestUri, GetFileParameters parameters)
    {
        return GetRawFile(CreateUri(requestUri), parameters);
    }

    /// <summary>
    /// 异步打开原始文件正文流，支持单段字节范围与 HTTP 版本条件。
    /// </summary>
    /// <remarks>发送 Translate: f 和 Accept-Encoding: identity；拒绝收到的非 identity 内容编码，206 时校验 Content-Range。服务器可忽略 Range 返回完整 200，调用方必须检查状态。</remarks>
    /// <param name="requestUri">请求资源的标准 URI 引用，可为绝对地址或相对 BaseAddress 的已编码路径；普通文件名可先用 WebDavUri.EncodePath 编码。</param>
    /// <param name="parameters">本次 GetRawFile 操作的请求参数，包括通用 HTTP 条件、请求头及取消设置。 不应为 null。</param>
    /// <returns>包含下载流和响应头快照的异步结果；调用方应使用 using 或 await using 释放。</returns>
    /// <exception cref="OperationCanceledException">调用方取消、客户端释放或请求超时使操作中止。</exception>
    /// <exception cref="HttpRequestException">发生网络连接或 HTTP 传输失败；普通 HTTP 错误状态通常保留在结果中。</exception>
    /// <exception cref="System.IO.InvalidDataException">分段范围不合法，或原始文件响应包含不允许的内容编码。</exception>
    public Task<WebDavStreamResponse> GetRawFile(Uri requestUri, GetFileParameters parameters)
    {
        return GetFile(requestUri, false, parameters);
    }

    /// <summary>
    /// 异步请求可由服务器处理后的文件正文，并保留流式响应。
    /// </summary>
    /// <remarks>Translate: t 仅提示服务器允许处理，实际语义由服务器决定；方法不会等待或缓冲整个文件正文。</remarks>
    /// <param name="requestUri">请求资源的标准 URI 引用，可为绝对地址或相对 BaseAddress 的已编码路径；普通文件名可先用 WebDavUri.EncodePath 编码。</param>
    /// <returns>包含下载流及响应头的异步结果，调用方需检查 HTTP 状态并及时释放。</returns>
    /// <exception cref="OperationCanceledException">调用方取消、客户端释放或请求超时使操作中止。</exception>
    /// <exception cref="HttpRequestException">发生网络连接或 HTTP 传输失败；普通 HTTP 错误状态通常保留在结果中。</exception>
    /// <exception cref="System.IO.InvalidDataException">分段范围不合法，或原始文件响应包含不允许的内容编码。</exception>
    public Task<WebDavStreamResponse> GetProcessedFile(string requestUri)
    {
        return GetProcessedFile(CreateUri(requestUri));
    }

    /// <summary>
    /// 异步请求可由服务器处理后的文件正文，并保留流式响应。
    /// </summary>
    /// <remarks>Translate: t 仅提示服务器允许处理，实际语义由服务器决定；方法不会等待或缓冲整个文件正文。</remarks>
    /// <param name="requestUri">请求资源的标准 URI 引用，可为绝对地址或相对 BaseAddress 的已编码路径；普通文件名可先用 WebDavUri.EncodePath 编码。</param>
    /// <returns>包含下载流及响应头的异步结果，调用方需检查 HTTP 状态并及时释放。</returns>
    /// <exception cref="OperationCanceledException">调用方取消、客户端释放或请求超时使操作中止。</exception>
    /// <exception cref="HttpRequestException">发生网络连接或 HTTP 传输失败；普通 HTTP 错误状态通常保留在结果中。</exception>
    /// <exception cref="System.IO.InvalidDataException">分段范围不合法，或原始文件响应包含不允许的内容编码。</exception>
    public Task<WebDavStreamResponse> GetProcessedFile(Uri requestUri)
    {
        return GetProcessedFile(requestUri, new GetFileParameters());
    }

    /// <summary>
    /// 异步请求可由服务器处理后的文件正文，并保留流式响应。
    /// </summary>
    /// <remarks>Translate: t 仅提示服务器允许处理，实际语义由服务器决定；方法不会等待或缓冲整个文件正文。</remarks>
    /// <param name="requestUri">请求资源的标准 URI 引用，可为绝对地址或相对 BaseAddress 的已编码路径；普通文件名可先用 WebDavUri.EncodePath 编码。</param>
    /// <param name="parameters">本次 GetProcessedFile 操作的请求参数，包括通用 HTTP 条件、请求头及取消设置。 不应为 null。</param>
    /// <returns>包含下载流及响应头的异步结果，调用方需检查 HTTP 状态并及时释放。</returns>
    /// <exception cref="OperationCanceledException">调用方取消、客户端释放或请求超时使操作中止。</exception>
    /// <exception cref="HttpRequestException">发生网络连接或 HTTP 传输失败；普通 HTTP 错误状态通常保留在结果中。</exception>
    /// <exception cref="System.IO.InvalidDataException">分段范围不合法，或原始文件响应包含不允许的内容编码。</exception>
    public Task<WebDavStreamResponse> GetProcessedFile(string requestUri, GetFileParameters parameters)
    {
        return GetProcessedFile(CreateUri(requestUri), parameters);
    }

    /// <summary>
    /// 异步请求可由服务器处理后的文件正文，并保留流式响应。
    /// </summary>
    /// <remarks>Translate: t 仅提示服务器允许处理，实际语义由服务器决定；方法不会等待或缓冲整个文件正文。</remarks>
    /// <param name="requestUri">请求资源的标准 URI 引用，可为绝对地址或相对 BaseAddress 的已编码路径；普通文件名可先用 WebDavUri.EncodePath 编码。</param>
    /// <param name="parameters">本次 GetProcessedFile 操作的请求参数，包括通用 HTTP 条件、请求头及取消设置。 不应为 null。</param>
    /// <returns>包含下载流及响应头的异步结果，调用方需检查 HTTP 状态并及时释放。</returns>
    /// <exception cref="OperationCanceledException">调用方取消、客户端释放或请求超时使操作中止。</exception>
    /// <exception cref="HttpRequestException">发生网络连接或 HTTP 传输失败；普通 HTTP 错误状态通常保留在结果中。</exception>
    /// <exception cref="System.IO.InvalidDataException">分段范围不合法，或原始文件响应包含不允许的内容编码。</exception>
    public Task<WebDavStreamResponse> GetProcessedFile(Uri requestUri, GetFileParameters parameters)
    {
        return GetFile(requestUri, true, parameters);
    }

    /// <summary>
    /// 将已经校验的 GET 响应转换为接管其所有权的流式下载结果。
    /// </summary>
    /// <remarks>响应取得后，若打开正文流失败，立即关闭原始响应；仅成功返回时才把所有权交给 WebDavStreamResponse。</remarks>
    /// <param name="requestUri">要读取的资源 URI。</param>
    /// <param name="translate">是否请求服务器允许处理文件正文。</param>
    /// <param name="parameters">范围、版本条件和请求取消设置。</param>
    /// <returns>包含正文流和响应快照的异步结果。</returns>
    private async Task<WebDavStreamResponse> GetFile(Uri requestUri, bool translate, GetFileParameters parameters)
    {
        HttpResponseMessage response = await GetFileResponse(requestUri, translate, parameters).ConfigureAwait(false);
        try
        {
            if (!response.IsSuccessStatusCode)
            {
                // 文件正文保持流式；失败正文通常很小，先有界保存便可同时提供 DAV:error 和仍可读取的错误原文。
                WebDavDocumentResponse document = await ReadDocumentResponseAsync(response, parameters.CancellationToken, true).ConfigureAwait(false);
                return new WebDavStreamResponse(response, new MemoryStream(document.Representation ?? [], writable: false))
                {
                    Error = document.Error
                };
            }
            // 打开正文流也可能失败或取消。只有成功构造结果后才移交响应所有权，异常路径必须关闭响应。
            Stream stream = await response.Content.ReadAsStreamAsync(parameters.CancellationToken).ConfigureAwait(false);
            return new WebDavStreamResponse(response, stream);
        }
        catch
        {
            response.Dispose();
            throw;
        }
    }

    /// <summary>
    /// 异步发送 GET 并校验正文编码及分段范围，返回原始 HTTP 响应。
    /// </summary>
    /// <remarks>translate 为 false 时检查原始字节编码；取消和超时继续覆盖正文读取阶段，而不只覆盖获取响应头。</remarks>
    /// <param name="requestUri">请求资源的标准 URI 引用，可为绝对地址或相对 BaseAddress 的已编码路径；普通文件名可先用 WebDavUri.EncodePath 编码。</param>
    /// <param name="translate">true 发送 Translate: t，允许服务器处理；false 发送 f 请求原始字节并检查内容编码。</param>
    /// <param name="parameters">本次 GetFileResponse 操作的请求参数，包括通用 HTTP 条件、请求头及取消设置。 不应为 null。</param>
    /// <returns>响应头可用后完成的任务；调用方拥有并必须释放返回的 HttpResponseMessage。</returns>
    /// <exception cref="OperationCanceledException">调用方取消、客户端释放或请求超时使操作中止。</exception>
    /// <exception cref="HttpRequestException">发生网络连接或 HTTP 传输失败；普通 HTTP 错误状态通常保留在结果中。</exception>
    /// <exception cref="System.IO.InvalidDataException">分段范围不合法，或原始文件响应包含不允许的内容编码。</exception>
    public async Task<HttpResponseMessage> GetFileResponse(Uri requestUri, bool translate, GetFileParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        using HttpRequestMessage request = CreateRequest(requestUri, "GET");
        request.Headers.Add("Translate", translate ? "t" : "f");
        // 明确要求不压缩的字节，保证 Range、Content-Length 与调用方看到的内容处于同一字节空间。
        request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("identity"));
        request.Headers.Range = parameters.Range;
        request.Headers.IfRange = parameters.IfRange;
        parameters.Apply(request);
        HttpResponseMessage response = await SendAsync(request, parameters.CancellationToken).ConfigureAwait(false);
        try
        {
            if (!translate && response.Content.Headers.ContentEncoding.Any(value => !value.Equals("identity", StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException("The server returned encoded bytes for a raw file download.");
            ValidateRange(request, response);
            return response;
        }
        catch
        {
            response.Dispose();
            throw;
        }
    }

    /// <summary>
    /// 异步将成功的原始下载正文复制到调用方目标流，并检查已知长度。
    /// </summary>
    /// <remarks>每块写入成功后报告累计字节；存在 Content-Length 时检查实际总数。目标流由调用方管理，异常发生时可能已写入部分数据。</remarks>
    /// <param name="requestUri">请求资源的标准 URI 引用，可为绝对地址或相对 BaseAddress 的已编码路径；普通文件名可先用 WebDavUri.EncodePath 编码。</param>
    /// <param name="destination">由调用方管理的可写目标流，从当前位置开始写入；本方法不关闭该流。</param>
    /// <param name="parameters">本次 DownloadFileAsync 操作的请求参数，包括通用 HTTP 条件、请求头及取消设置。 null 使用默认参数。</param>
    /// <param name="progress">每块数据成功写入目标后的累计字节进度；null 不报告。</param>
    /// <returns>异步复制任务；结果保留 HTTP 状态和响应头，网络正文在返回前已释放。</returns>
    /// <exception cref="OperationCanceledException">调用方取消、客户端释放或请求超时使操作中止。</exception>
    /// <exception cref="HttpRequestException">服务器返回非成功 HTTP 状态，或发生网络连接及传输失败。</exception>
    /// <exception cref="System.IO.IOException">正文复制失败，或实际下载字节数与 Content-Length 不符。</exception>
    public async Task<WebDavResponse> DownloadFileAsync(string requestUri, Stream destination, GetFileParameters? parameters = null,
        IProgress<long>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(destination);
        if (!destination.CanWrite)
            throw new ArgumentException("Download destination must be writable.", nameof(destination));
        parameters ??= new GetFileParameters();
        using WebDavStreamResponse response = await GetRawFile(requestUri, parameters).ConfigureAwait(false);
        _ = response.EnsureSuccessStatusCode();
        var buffer = ArrayPool<byte>.Shared.Rent(81920);
        try
        {
            long total = 0;
            int count;
            while ((count = await response.Stream.ReadAsync(buffer.AsMemory(), parameters.CancellationToken).ConfigureAwait(false)) != 0)
            {
                await destination.WriteAsync(buffer.AsMemory(0, count), parameters.CancellationToken).ConfigureAwait(false);
                total += count;
                progress?.Report(total);
            }
            if (response.ContentLength is { } expected && total != expected)
                // 正常读到流末尾仍可能是提前结束；已知长度时不能把不完整文件交给上层当作成功下载。
                throw new IOException("Downloaded content length differs from Content-Length.");
            return response;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    /// <summary>
    /// 异步通过 PUT 创建或覆盖文件内容，并返回服务器确认结果。
    /// </summary>
    /// <remarks>默认按服务器 PUT 语义覆盖目标；仅创建时设置 IfNoneMatch 为 *，按版本覆盖时设置 IfMatch。上传字节进度不代表服务器事务已提交。 可定位源流在 HTTP 正文重发时恢复到初始位置；不可定位流不保证可重放。</remarks>
    /// <param name="requestUri">请求资源的标准 URI 引用，可为绝对地址或相对 BaseAddress 的已编码路径；普通文件名可先用 WebDavUri.EncodePath 编码。</param>
    /// <param name="stream">从当前位置开始读取的可读源流；默认保留源流，所有权由 PutFileParameters.LeaveOpen 控制。</param>
    /// <returns>异步上传任务；结果保留 HTTP 状态、响应头和可能的逐资源错误。</returns>
    /// <exception cref="OperationCanceledException">调用方取消、客户端释放或请求超时使操作中止。</exception>
    /// <exception cref="HttpRequestException">发生网络连接或 HTTP 传输失败；普通 HTTP 错误状态通常保留在结果中。</exception>
    public Task<WebDavResponse> PutFile(string requestUri, Stream stream)
    {
        return PutFile(CreateUri(requestUri), stream);
    }

    /// <summary>
    /// 异步通过 PUT 创建或覆盖文件内容，并返回服务器确认结果。
    /// </summary>
    /// <remarks>默认按服务器 PUT 语义覆盖目标；仅创建时设置 IfNoneMatch 为 *，按版本覆盖时设置 IfMatch。上传字节进度不代表服务器事务已提交。 可定位源流在 HTTP 正文重发时恢复到初始位置；不可定位流不保证可重放。</remarks>
    /// <param name="requestUri">请求资源的标准 URI 引用，可为绝对地址或相对 BaseAddress 的已编码路径；普通文件名可先用 WebDavUri.EncodePath 编码。</param>
    /// <param name="stream">从当前位置开始读取的可读源流；默认保留源流，所有权由 PutFileParameters.LeaveOpen 控制。</param>
    /// <returns>异步上传任务；结果保留 HTTP 状态、响应头和可能的逐资源错误。</returns>
    /// <exception cref="OperationCanceledException">调用方取消、客户端释放或请求超时使操作中止。</exception>
    /// <exception cref="HttpRequestException">发生网络连接或 HTTP 传输失败；普通 HTTP 错误状态通常保留在结果中。</exception>
    public Task<WebDavResponse> PutFile(Uri requestUri, Stream stream)
    {
        return PutFile(requestUri, stream, new PutFileParameters());
    }

    /// <summary>
    /// 异步通过 PUT 创建或覆盖文件内容，并返回服务器确认结果。
    /// </summary>
    /// <remarks>默认按服务器 PUT 语义覆盖目标；仅创建时设置 IfNoneMatch 为 *，按版本覆盖时设置 IfMatch。上传字节进度不代表服务器事务已提交。 可定位源流在 HTTP 正文重发时恢复到初始位置；不可定位流不保证可重放。</remarks>
    /// <param name="requestUri">请求资源的标准 URI 引用，可为绝对地址或相对 BaseAddress 的已编码路径；普通文件名可先用 WebDavUri.EncodePath 编码。</param>
    /// <param name="stream">从当前位置开始读取的可读源流；默认保留源流，所有权由 PutFileParameters.LeaveOpen 控制。</param>
    /// <param name="contentType">上传内容的合法 MIME 媒体类型；只标记类型，不对原始字节重新编码。</param>
    /// <returns>异步上传任务；结果保留 HTTP 状态、响应头和可能的逐资源错误。</returns>
    /// <exception cref="OperationCanceledException">调用方取消、客户端释放或请求超时使操作中止。</exception>
    /// <exception cref="HttpRequestException">发生网络连接或 HTTP 传输失败；普通 HTTP 错误状态通常保留在结果中。</exception>
    public Task<WebDavResponse> PutFile(string requestUri, Stream stream, string contentType)
    {
        return PutFile(CreateUri(requestUri), stream, contentType);
    }

    /// <summary>
    /// 异步通过 PUT 创建或覆盖文件内容，并返回服务器确认结果。
    /// </summary>
    /// <remarks>默认按服务器 PUT 语义覆盖目标；仅创建时设置 IfNoneMatch 为 *，按版本覆盖时设置 IfMatch。上传字节进度不代表服务器事务已提交。 可定位源流在 HTTP 正文重发时恢复到初始位置；不可定位流不保证可重放。</remarks>
    /// <param name="requestUri">请求资源的标准 URI 引用，可为绝对地址或相对 BaseAddress 的已编码路径；普通文件名可先用 WebDavUri.EncodePath 编码。</param>
    /// <param name="stream">从当前位置开始读取的可读源流；默认保留源流，所有权由 PutFileParameters.LeaveOpen 控制。</param>
    /// <param name="contentType">上传内容的合法 MIME 媒体类型；只标记类型，不对原始字节重新编码。</param>
    /// <returns>异步上传任务；结果保留 HTTP 状态、响应头和可能的逐资源错误。</returns>
    /// <exception cref="OperationCanceledException">调用方取消、客户端释放或请求超时使操作中止。</exception>
    /// <exception cref="HttpRequestException">发生网络连接或 HTTP 传输失败；普通 HTTP 错误状态通常保留在结果中。</exception>
    public Task<WebDavResponse> PutFile(Uri requestUri, Stream stream, string contentType)
    {
        return PutFile(requestUri, stream, new PutFileParameters { ContentType = MediaTypeHeaderValue.Parse(contentType) });
    }

    /// <summary>
    /// 异步通过 PUT 创建或覆盖文件内容，并返回服务器确认结果。
    /// </summary>
    /// <remarks>默认按服务器 PUT 语义覆盖目标；仅创建时设置 IfNoneMatch 为 *，按版本覆盖时设置 IfMatch。上传字节进度不代表服务器事务已提交。 可定位源流在 HTTP 正文重发时恢复到初始位置；不可定位流不保证可重放。</remarks>
    /// <param name="requestUri">请求资源的标准 URI 引用，可为绝对地址或相对 BaseAddress 的已编码路径；普通文件名可先用 WebDavUri.EncodePath 编码。</param>
    /// <param name="stream">从当前位置开始读取的可读源流；默认保留源流，所有权由 PutFileParameters.LeaveOpen 控制。</param>
    /// <param name="parameters">本次 PutFile 操作的请求参数，包括通用 HTTP 条件、请求头及取消设置。 不应为 null。</param>
    /// <returns>异步上传任务；结果保留 HTTP 状态、响应头和可能的逐资源错误。</returns>
    /// <exception cref="OperationCanceledException">调用方取消、客户端释放或请求超时使操作中止。</exception>
    /// <exception cref="HttpRequestException">发生网络连接或 HTTP 传输失败；普通 HTTP 错误状态通常保留在结果中。</exception>
    public Task<WebDavResponse> PutFile(string requestUri, Stream stream, PutFileParameters parameters)
    {
        return PutFile(CreateUri(requestUri), stream, parameters);
    }

    /// <summary>
    /// 异步通过 PUT 创建或覆盖文件内容，并返回服务器确认结果。
    /// </summary>
    /// <remarks>默认按服务器 PUT 语义覆盖目标；仅创建时设置 IfNoneMatch 为 *，按版本覆盖时设置 IfMatch。上传字节进度不代表服务器事务已提交。 可定位源流在 HTTP 正文重发时恢复到初始位置；不可定位流不保证可重放。</remarks>
    /// <param name="requestUri">请求资源的标准 URI 引用，可为绝对地址或相对 BaseAddress 的已编码路径；普通文件名可先用 WebDavUri.EncodePath 编码。</param>
    /// <param name="stream">从当前位置开始读取的可读源流；默认保留源流，所有权由 PutFileParameters.LeaveOpen 控制。</param>
    /// <param name="parameters">本次 PutFile 操作的请求参数，包括通用 HTTP 条件、请求头及取消设置。 不应为 null。</param>
    /// <returns>异步上传任务；结果保留 HTTP 状态、响应头和可能的逐资源错误。</returns>
    /// <exception cref="OperationCanceledException">调用方取消、客户端释放或请求超时使操作中止。</exception>
    /// <exception cref="HttpRequestException">发生网络连接或 HTTP 传输失败；普通 HTTP 错误状态通常保留在结果中。</exception>
    public async Task<WebDavResponse> PutFile(Uri requestUri, Stream stream, PutFileParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        // UploadContent 记录初始位置，并把正文释放和源流所有权分开：默认释放正文但保留调用方的流。
        using UploadContent content = new(stream, parameters);
        return await PutFile(requestUri, content, parameters).ConfigureAwait(false);
    }
    /// <summary>
    /// 异步通过 PUT 创建或覆盖文件内容，并返回服务器确认结果。
    /// </summary>
    /// <remarks>默认按服务器 PUT 语义覆盖目标；仅创建时设置 IfNoneMatch 为 *，按版本覆盖时设置 IfMatch。上传字节进度不代表服务器事务已提交。 传入的 HttpContent 随请求释放；若需保留其内部流，应使用不关闭底层流的正文包装。</remarks>
    /// <param name="requestUri">请求资源的标准 URI 引用，可为绝对地址或相对 BaseAddress 的已编码路径；普通文件名可先用 WebDavUri.EncodePath 编码。</param>
    /// <param name="content">要发送的 HTTP 正文；随已构造的 PUT 请求管理和释放，包括正文自身拥有的流。</param>
    /// <returns>异步上传任务；结果保留 HTTP 状态、响应头和可能的逐资源错误。</returns>
    /// <exception cref="OperationCanceledException">调用方取消、客户端释放或请求超时使操作中止。</exception>
    /// <exception cref="HttpRequestException">发生网络连接或 HTTP 传输失败；普通 HTTP 错误状态通常保留在结果中。</exception>
    public Task<WebDavResponse> PutFile(string requestUri, HttpContent content)
    {
        return PutFile(CreateUri(requestUri), content);
    }

    /// <summary>
    /// 异步通过 PUT 创建或覆盖文件内容，并返回服务器确认结果。
    /// </summary>
    /// <remarks>默认按服务器 PUT 语义覆盖目标；仅创建时设置 IfNoneMatch 为 *，按版本覆盖时设置 IfMatch。上传字节进度不代表服务器事务已提交。 传入的 HttpContent 随请求释放；若需保留其内部流，应使用不关闭底层流的正文包装。</remarks>
    /// <param name="requestUri">请求资源的标准 URI 引用，可为绝对地址或相对 BaseAddress 的已编码路径；普通文件名可先用 WebDavUri.EncodePath 编码。</param>
    /// <param name="content">要发送的 HTTP 正文；随已构造的 PUT 请求管理和释放，包括正文自身拥有的流。</param>
    /// <returns>异步上传任务；结果保留 HTTP 状态、响应头和可能的逐资源错误。</returns>
    /// <exception cref="OperationCanceledException">调用方取消、客户端释放或请求超时使操作中止。</exception>
    /// <exception cref="HttpRequestException">发生网络连接或 HTTP 传输失败；普通 HTTP 错误状态通常保留在结果中。</exception>
    public Task<WebDavResponse> PutFile(Uri requestUri, HttpContent content)
    {
        return PutFile(requestUri, content, new PutFileParameters());
    }

    /// <summary>
    /// 异步通过 PUT 创建或覆盖文件内容，并返回服务器确认结果。
    /// </summary>
    /// <remarks>默认按服务器 PUT 语义覆盖目标；仅创建时设置 IfNoneMatch 为 *，按版本覆盖时设置 IfMatch。上传字节进度不代表服务器事务已提交。 传入的 HttpContent 随请求释放；若需保留其内部流，应使用不关闭底层流的正文包装。</remarks>
    /// <param name="requestUri">请求资源的标准 URI 引用，可为绝对地址或相对 BaseAddress 的已编码路径；普通文件名可先用 WebDavUri.EncodePath 编码。</param>
    /// <param name="content">要发送的 HTTP 正文；随已构造的 PUT 请求管理和释放，包括正文自身拥有的流。</param>
    /// <param name="parameters">本次 PutFile 操作的请求参数，包括通用 HTTP 条件、请求头及取消设置。 不应为 null。</param>
    /// <returns>异步上传任务；结果保留 HTTP 状态、响应头和可能的逐资源错误。</returns>
    /// <exception cref="OperationCanceledException">调用方取消、客户端释放或请求超时使操作中止。</exception>
    /// <exception cref="HttpRequestException">发生网络连接或 HTTP 传输失败；普通 HTTP 错误状态通常保留在结果中。</exception>
    public Task<WebDavResponse> PutFile(string requestUri, HttpContent content, PutFileParameters parameters)
    {
        return PutFile(CreateUri(requestUri), content, parameters);
    }

    /// <summary>
    /// 异步通过 PUT 创建或覆盖文件内容，并返回服务器确认结果。
    /// </summary>
    /// <remarks>默认按服务器 PUT 语义覆盖目标；仅创建时设置 IfNoneMatch 为 *，按版本覆盖时设置 IfMatch。上传字节进度不代表服务器事务已提交。 传入的 HttpContent 随请求释放；若需保留其内部流，应使用不关闭底层流的正文包装。</remarks>
    /// <param name="requestUri">请求资源的标准 URI 引用，可为绝对地址或相对 BaseAddress 的已编码路径；普通文件名可先用 WebDavUri.EncodePath 编码。</param>
    /// <param name="content">要发送的 HTTP 正文；随已构造的 PUT 请求管理和释放，包括正文自身拥有的流。</param>
    /// <param name="parameters">本次 PutFile 操作的请求参数，包括通用 HTTP 条件、请求头及取消设置。 不应为 null。</param>
    /// <returns>异步上传任务；结果保留 HTTP 状态、响应头和可能的逐资源错误。</returns>
    /// <exception cref="OperationCanceledException">调用方取消、客户端释放或请求超时使操作中止。</exception>
    /// <exception cref="HttpRequestException">发生网络连接或 HTTP 传输失败；普通 HTTP 错误状态通常保留在结果中。</exception>
    public async Task<WebDavResponse> PutFile(Uri requestUri, HttpContent content, PutFileParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(parameters);
        using HttpRequestMessage request = CreateRequest(requestUri, "PUT", null, content);
        content.Headers.ContentType = parameters.ContentType;
        AddLock(request, parameters.LockToken);
        parameters.Apply(request);
        return await SendMutationAsync(request, parameters.CancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 异步复制源资源到目标 URI，支持集合深度、覆盖策略及目标锁条件。
    /// </summary>
    /// <remarks>Destination 使用解析后的绝对 URI；目标锁通过带目标 URI 标记的 If 条件提交，不会误作为针对源资源的条件。 此重载使用该操作的默认请求参数。</remarks>
    /// <param name="sourceUri">源资源的 URI 引用，按客户端基地址解析；不使用本地文件系统路径规则。</param>
    /// <param name="destUri">目标资源的 URI 引用，发送前解析成 Destination 需要的绝对 URI。</param>
    /// <returns>异步复制任务；多状态响应保留已返回的资源和属性错误。</returns>
    /// <exception cref="OperationCanceledException">调用方取消、客户端释放或请求超时使操作中止。</exception>
    /// <exception cref="HttpRequestException">发生网络连接或 HTTP 传输失败；普通 HTTP 错误状态通常保留在结果中。</exception>
    public Task<WebDavResponse> Copy(string sourceUri, string destUri)
    {
        return Copy(CreateUri(sourceUri), CreateUri(destUri));
    }

    /// <summary>
    /// 异步复制源资源到目标 URI，支持集合深度、覆盖策略及目标锁条件。
    /// </summary>
    /// <remarks>Destination 使用解析后的绝对 URI；目标锁通过带目标 URI 标记的 If 条件提交，不会误作为针对源资源的条件。 此重载使用该操作的默认请求参数。</remarks>
    /// <param name="sourceUri">源资源的 URI 引用，按客户端基地址解析；不使用本地文件系统路径规则。</param>
    /// <param name="destUri">目标资源的 URI 引用，发送前解析成 Destination 需要的绝对 URI。</param>
    /// <returns>异步复制任务；多状态响应保留已返回的资源和属性错误。</returns>
    /// <exception cref="OperationCanceledException">调用方取消、客户端释放或请求超时使操作中止。</exception>
    /// <exception cref="HttpRequestException">发生网络连接或 HTTP 传输失败；普通 HTTP 错误状态通常保留在结果中。</exception>
    public Task<WebDavResponse> Copy(Uri sourceUri, Uri destUri)
    {
        return Copy(sourceUri, destUri, new CopyParameters());
    }

    /// <summary>
    /// 异步复制源资源到目标 URI，支持集合深度、覆盖策略及目标锁条件。
    /// </summary>
    /// <remarks>Destination 使用解析后的绝对 URI；目标锁通过带目标 URI 标记的 If 条件提交，不会误作为针对源资源的条件。</remarks>
    /// <param name="sourceUri">源资源的 URI 引用，按客户端基地址解析；不使用本地文件系统路径规则。</param>
    /// <param name="destUri">目标资源的 URI 引用，发送前解析成 Destination 需要的绝对 URI。</param>
    /// <param name="parameters">本次 Copy 操作的请求参数，包括通用 HTTP 条件、请求头及取消设置。 不应为 null。</param>
    /// <returns>异步复制任务；多状态响应保留已返回的资源和属性错误。</returns>
    /// <exception cref="OperationCanceledException">调用方取消、客户端释放或请求超时使操作中止。</exception>
    /// <exception cref="HttpRequestException">发生网络连接或 HTTP 传输失败；普通 HTTP 错误状态通常保留在结果中。</exception>
    public Task<WebDavResponse> Copy(string sourceUri, string destUri, CopyParameters parameters)
    {
        return Copy(CreateUri(sourceUri), CreateUri(destUri), parameters);
    }

    /// <summary>
    /// 异步复制源资源到目标 URI，支持集合深度、覆盖策略及目标锁条件。
    /// </summary>
    /// <remarks>Destination 使用解析后的绝对 URI；目标锁通过带目标 URI 标记的 If 条件提交，不会误作为针对源资源的条件。</remarks>
    /// <param name="sourceUri">源资源的 URI 引用，按客户端基地址解析；不使用本地文件系统路径规则。</param>
    /// <param name="destUri">目标资源的 URI 引用，发送前解析成 Destination 需要的绝对 URI。</param>
    /// <param name="parameters">本次 Copy 操作的请求参数，包括通用 HTTP 条件、请求头及取消设置。 不应为 null。</param>
    /// <returns>异步复制任务；多状态响应保留已返回的资源和属性错误。</returns>
    /// <exception cref="OperationCanceledException">调用方取消、客户端释放或请求超时使操作中止。</exception>
    /// <exception cref="HttpRequestException">发生网络连接或 HTTP 传输失败；普通 HTTP 错误状态通常保留在结果中。</exception>
    public async Task<WebDavResponse> Copy(Uri sourceUri, Uri destUri, CopyParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        using HttpRequestMessage request = CreateRequest(sourceUri, "COPY");
        Uri destination = ResolveUri(destUri);
        request.Headers.Add("Destination", destination.AbsoluteUri);
        request.Headers.Add("Depth", DepthHeaderHelper.GetValueForCopy(parameters.ApplyTo ?? ApplyTo.Copy.ResourceAndAncestors));
        request.Headers.Add("Overwrite", parameters.Overwrite ? "T" : "F");
        if (parameters.DestLockToken is not null)
            // 目标资源不是当前请求 URI，必须使用带目标 URI 标记的 If 条件。
            request.Headers.Add("If", IfHeaderHelper.Tagged(destination, parameters.DestLockToken));
        parameters.Apply(request);
        return await SendMutationAsync(request, parameters.CancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 异步移动源资源到目标 URI，按各自 URI 提交源锁与目标锁。
    /// </summary>
    /// <remarks>两组锁条件放入同一个 If 头并分别标记资源；默认允许覆盖，需要拒绝覆盖时设置 Overwrite 为 false。 此重载使用该操作的默认请求参数。</remarks>
    /// <param name="sourceUri">源资源的 URI 引用，按客户端基地址解析；不使用本地文件系统路径规则。</param>
    /// <param name="destUri">目标资源的 URI 引用，发送前解析成 Destination 需要的绝对 URI。</param>
    /// <returns>异步移动任务；多状态内部错误会使 IsSuccessful 为 false，不能仅依据 HTTP 207 判断成功。</returns>
    /// <exception cref="OperationCanceledException">调用方取消、客户端释放或请求超时使操作中止。</exception>
    /// <exception cref="HttpRequestException">发生网络连接或 HTTP 传输失败；普通 HTTP 错误状态通常保留在结果中。</exception>
    public Task<WebDavResponse> Move(string sourceUri, string destUri)
    {
        return Move(CreateUri(sourceUri), CreateUri(destUri));
    }

    /// <summary>
    /// 异步移动源资源到目标 URI，按各自 URI 提交源锁与目标锁。
    /// </summary>
    /// <remarks>两组锁条件放入同一个 If 头并分别标记资源；默认允许覆盖，需要拒绝覆盖时设置 Overwrite 为 false。 此重载使用该操作的默认请求参数。</remarks>
    /// <param name="sourceUri">源资源的 URI 引用，按客户端基地址解析；不使用本地文件系统路径规则。</param>
    /// <param name="destUri">目标资源的 URI 引用，发送前解析成 Destination 需要的绝对 URI。</param>
    /// <returns>异步移动任务；多状态内部错误会使 IsSuccessful 为 false，不能仅依据 HTTP 207 判断成功。</returns>
    /// <exception cref="OperationCanceledException">调用方取消、客户端释放或请求超时使操作中止。</exception>
    /// <exception cref="HttpRequestException">发生网络连接或 HTTP 传输失败；普通 HTTP 错误状态通常保留在结果中。</exception>
    public Task<WebDavResponse> Move(Uri sourceUri, Uri destUri)
    {
        return Move(sourceUri, destUri, new MoveParameters());
    }

    /// <summary>
    /// 异步移动源资源到目标 URI，按各自 URI 提交源锁与目标锁。
    /// </summary>
    /// <remarks>两组锁条件放入同一个 If 头并分别标记资源；默认允许覆盖，需要拒绝覆盖时设置 Overwrite 为 false。</remarks>
    /// <param name="sourceUri">源资源的 URI 引用，按客户端基地址解析；不使用本地文件系统路径规则。</param>
    /// <param name="destUri">目标资源的 URI 引用，发送前解析成 Destination 需要的绝对 URI。</param>
    /// <param name="parameters">本次 Move 操作的请求参数，包括通用 HTTP 条件、请求头及取消设置。 不应为 null。</param>
    /// <returns>异步移动任务；多状态内部错误会使 IsSuccessful 为 false，不能仅依据 HTTP 207 判断成功。</returns>
    /// <exception cref="OperationCanceledException">调用方取消、客户端释放或请求超时使操作中止。</exception>
    /// <exception cref="HttpRequestException">发生网络连接或 HTTP 传输失败；普通 HTTP 错误状态通常保留在结果中。</exception>
    public Task<WebDavResponse> Move(string sourceUri, string destUri, MoveParameters parameters)
    {
        return Move(CreateUri(sourceUri), CreateUri(destUri), parameters);
    }

    /// <summary>
    /// 异步移动源资源到目标 URI，按各自 URI 提交源锁与目标锁。
    /// </summary>
    /// <remarks>两组锁条件放入同一个 If 头并分别标记资源；默认允许覆盖，需要拒绝覆盖时设置 Overwrite 为 false。</remarks>
    /// <param name="sourceUri">源资源的 URI 引用，按客户端基地址解析；不使用本地文件系统路径规则。</param>
    /// <param name="destUri">目标资源的 URI 引用，发送前解析成 Destination 需要的绝对 URI。</param>
    /// <param name="parameters">本次 Move 操作的请求参数，包括通用 HTTP 条件、请求头及取消设置。 不应为 null。</param>
    /// <returns>异步移动任务；多状态内部错误会使 IsSuccessful 为 false，不能仅依据 HTTP 207 判断成功。</returns>
    /// <exception cref="OperationCanceledException">调用方取消、客户端释放或请求超时使操作中止。</exception>
    /// <exception cref="HttpRequestException">发生网络连接或 HTTP 传输失败；普通 HTTP 错误状态通常保留在结果中。</exception>
    public async Task<WebDavResponse> Move(Uri sourceUri, Uri destUri, MoveParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        using HttpRequestMessage request = CreateRequest(sourceUri, "MOVE");
        Uri destination = ResolveUri(destUri);
        request.Headers.Add("Destination", destination.AbsoluteUri);
        request.Headers.Add("Overwrite", parameters.Overwrite ? "T" : "F");
        // 源锁与目标锁不能作为两条无标记 If 头发送，否则会作用到错误资源，或改变条件之间的含义。
        // 先各自绑定绝对 URI，再拼成一个 If 字段，保留每组条件的资源身份。
        List<string> conditions = [];
        if (parameters.SourceLockToken is not null)
            conditions.Add(IfHeaderHelper.Tagged(ResolveUri(sourceUri), parameters.SourceLockToken));
        if (parameters.DestLockToken is not null)
            conditions.Add(IfHeaderHelper.Tagged(destination, parameters.DestLockToken));
        if (conditions.Count > 0)
            request.Headers.Add("If", string.Join(" ", conditions));
        parameters.Apply(request);
        return await SendMutationAsync(request, parameters.CancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 异步创建共享或独占写锁，或刷新已有令牌对应的写锁。
    /// </summary>
    /// <remarks>LockToken 非 null 时使用空正文续期且不发送 Depth；新锁默认覆盖全部后代。锁租约与请求超时独立，服务器可能授予不同租期。 此重载使用该操作的默认请求参数。</remarks>
    /// <param name="requestUri">请求资源的标准 URI 引用，可为绝对地址或相对 BaseAddress 的已编码路径；普通文件名可先用 WebDavUri.EncodePath 编码。</param>
    /// <returns>异步锁操作任务；普通成功响应包含活动锁，集合 HTTP 207 响应保留逐资源状态。</returns>
    /// <exception cref="OperationCanceledException">调用方取消、客户端释放或请求超时使操作中止。</exception>
    /// <exception cref="HttpRequestException">发生网络连接或 HTTP 传输失败；普通 HTTP 错误状态通常保留在结果中。</exception>
    /// <exception cref="System.IO.InvalidDataException">成功 HTTP 状态不符合 DAV 协议，或响应缺少必需结构及确认信息。</exception>
    public Task<LockResponse> Lock(string requestUri)
    {
        return Lock(CreateUri(requestUri));
    }

    /// <summary>
    /// 异步创建共享或独占写锁，或刷新已有令牌对应的写锁。
    /// </summary>
    /// <remarks>LockToken 非 null 时使用空正文续期且不发送 Depth；新锁默认覆盖全部后代。锁租约与请求超时独立，服务器可能授予不同租期。 此重载使用该操作的默认请求参数。</remarks>
    /// <param name="requestUri">请求资源的标准 URI 引用，可为绝对地址或相对 BaseAddress 的已编码路径；普通文件名可先用 WebDavUri.EncodePath 编码。</param>
    /// <returns>异步锁操作任务；普通成功响应包含活动锁，集合 HTTP 207 响应保留逐资源状态。</returns>
    /// <exception cref="OperationCanceledException">调用方取消、客户端释放或请求超时使操作中止。</exception>
    /// <exception cref="HttpRequestException">发生网络连接或 HTTP 传输失败；普通 HTTP 错误状态通常保留在结果中。</exception>
    /// <exception cref="System.IO.InvalidDataException">成功 HTTP 状态不符合 DAV 协议，或响应缺少必需结构及确认信息。</exception>
    public Task<LockResponse> Lock(Uri requestUri)
    {
        return Lock(requestUri, new LockParameters());
    }

    /// <summary>
    /// 异步创建共享或独占写锁，或刷新已有令牌对应的写锁。
    /// </summary>
    /// <remarks>LockToken 非 null 时使用空正文续期且不发送 Depth；新锁默认覆盖全部后代。锁租约与请求超时独立，服务器可能授予不同租期。</remarks>
    /// <param name="requestUri">请求资源的标准 URI 引用，可为绝对地址或相对 BaseAddress 的已编码路径；普通文件名可先用 WebDavUri.EncodePath 编码。</param>
    /// <param name="parameters">本次 Lock 操作的请求参数，包括通用 HTTP 条件、请求头及取消设置。 不应为 null。</param>
    /// <returns>异步锁操作任务；普通成功响应包含活动锁，集合 HTTP 207 响应保留逐资源状态。</returns>
    /// <exception cref="OperationCanceledException">调用方取消、客户端释放或请求超时使操作中止。</exception>
    /// <exception cref="HttpRequestException">发生网络连接或 HTTP 传输失败；普通 HTTP 错误状态通常保留在结果中。</exception>
    /// <exception cref="System.IO.InvalidDataException">成功 HTTP 状态不符合 DAV 协议，或响应缺少必需结构及确认信息。</exception>
    public Task<LockResponse> Lock(string requestUri, LockParameters parameters)
    {
        return Lock(CreateUri(requestUri), parameters);
    }

    /// <summary>
    /// 异步创建共享或独占写锁，或刷新已有令牌对应的写锁。
    /// </summary>
    /// <remarks>LockToken 非 null 时使用空正文续期且不发送 Depth；新锁默认覆盖全部后代。锁租约与请求超时独立，服务器可能授予不同租期。</remarks>
    /// <param name="requestUri">请求资源的标准 URI 引用，可为绝对地址或相对 BaseAddress 的已编码路径；普通文件名可先用 WebDavUri.EncodePath 编码。</param>
    /// <param name="parameters">本次 Lock 操作的请求参数，包括通用 HTTP 条件、请求头及取消设置。 不应为 null。</param>
    /// <returns>异步锁操作任务；普通成功响应包含活动锁，集合 HTTP 207 响应保留逐资源状态。</returns>
    /// <exception cref="OperationCanceledException">调用方取消、客户端释放或请求超时使操作中止。</exception>
    /// <exception cref="HttpRequestException">发生网络连接或 HTTP 传输失败；普通 HTTP 错误状态通常保留在结果中。</exception>
    /// <exception cref="System.IO.InvalidDataException">成功 HTTP 状态不符合 DAV 协议，或响应缺少必需结构及确认信息。</exception>
    public async Task<LockResponse> Lock(Uri requestUri, LockParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        using HttpRequestMessage request = CreateRequest(requestUri, "LOCK");
        // 续期必须没有 lockinfo 正文，也不重新定义 Depth、锁模式或所有者；只有新建锁才发送这些信息。
        if (parameters.LockToken is not null)
            AddLock(request, parameters.LockToken);
        else
        {
            request.Headers.Add("Depth", DepthHeaderHelper.GetValueForLock(parameters.ApplyTo ?? ApplyTo.Lock.ResourceAndAncestors));
            request.Content = XmlContent(LockRequestBuilder.BuildRequestBody(parameters), parameters.ContentType);
        }
        if (parameters.Timeout is { } timeout)
        {
            if (timeout != Timeout.InfiniteTimeSpan && (timeout <= TimeSpan.Zero || timeout.TotalSeconds > uint.MaxValue))
                throw new ArgumentOutOfRangeException(nameof(parameters), "Lock timeout must be positive, <= uint.MaxValue seconds, or infinite.");
            // 锁租约使用整数秒，亚秒部分向上取整；固定区域防止小数分隔符等本地格式进入协议头。
            request.Headers.Add("Timeout", timeout == Timeout.InfiniteTimeSpan ? "Infinite"
                : "Second-" + Math.Ceiling(timeout.TotalSeconds).ToString(CultureInfo.InvariantCulture));
        }
        parameters.Apply(request);
        using HttpResponseMessage response = await SendAsync(request, parameters.CancellationToken).ConfigureAwait(false);
        IReadOnlyCollection<ActiveLock> locks = [];
        IReadOnlyCollection<WebDavResource> resources = [];
        if ((int)response.StatusCode == 207)
        {
            // 集合加锁失败也可能返回 multistatus，不能把所有 2xx 都当作普通 DAV:prop 锁发现文档。
            resources = await ReadResourcesAsync(response, parameters.CancellationToken).ConfigureAwait(false);
            if (resources.Count == 0)
                throw new InvalidDataException("LOCK returned an empty multistatus.");
        }
        else if (response.IsSuccessStatusCode)
        {
            XElement root = await WebDavXml.ReadDocumentAsync(response, _maxXmlCharacters, parameters.CancellationToken).ConfigureAwait(false);
            if (root.Name != WebDavXml.Dav + "prop")
                throw new InvalidDataException("LOCK did not return DAV:prop.");
            locks = WebDavXml.ParseLockDiscovery(root.Element(WebDavXml.Dav + "lockdiscovery"));
            if (locks.Count == 0)
                throw new InvalidDataException("LOCK did not return an active lock.");
        }
        return new LockResponse((int)response.StatusCode, response.ReasonPhrase, locks)
        {
            Resources = resources,
            Error = response.IsSuccessStatusCode ? null
                : await WebDavXml.ReadErrorAsync(response, _maxXmlCharacters, parameters.CancellationToken).ConfigureAwait(false)
        }.CaptureHeaders<LockResponse>(response);
    }

    /// <summary>
    /// 异步释放指定资源上由锁令牌标识的写锁。
    /// </summary>
    /// <remarks>规范化尖括号后通过 Lock-Token 头提交令牌，不能使用 If 条件代替该解锁请求头。</remarks>
    /// <param name="requestUri">请求资源的标准 URI 引用，可为绝对地址或相对 BaseAddress 的已编码路径；普通文件名可先用 WebDavUri.EncodePath 编码。</param>
    /// <param name="lockToken">服务器提供的锁令牌 URI，可以带外层尖括号；发送时校验并统一格式。</param>
    /// <returns>异步解锁任务；结果保留服务器 HTTP 状态和响应头。</returns>
    /// <exception cref="OperationCanceledException">调用方取消、客户端释放或请求超时使操作中止。</exception>
    /// <exception cref="HttpRequestException">发生网络连接或 HTTP 传输失败；普通 HTTP 错误状态通常保留在结果中。</exception>
    public Task<WebDavResponse> Unlock(string requestUri, string lockToken)
    {
        return Unlock(CreateUri(requestUri), lockToken);
    }

    /// <summary>
    /// 异步释放指定资源上由锁令牌标识的写锁。
    /// </summary>
    /// <remarks>规范化尖括号后通过 Lock-Token 头提交令牌，不能使用 If 条件代替该解锁请求头。</remarks>
    /// <param name="requestUri">请求资源的标准 URI 引用，可为绝对地址或相对 BaseAddress 的已编码路径；普通文件名可先用 WebDavUri.EncodePath 编码。</param>
    /// <param name="lockToken">服务器提供的锁令牌 URI，可以带外层尖括号；发送时校验并统一格式。</param>
    /// <returns>异步解锁任务；结果保留服务器 HTTP 状态和响应头。</returns>
    /// <exception cref="OperationCanceledException">调用方取消、客户端释放或请求超时使操作中止。</exception>
    /// <exception cref="HttpRequestException">发生网络连接或 HTTP 传输失败；普通 HTTP 错误状态通常保留在结果中。</exception>
    public Task<WebDavResponse> Unlock(Uri requestUri, string lockToken)
    {
        return Unlock(requestUri, new UnlockParameters(lockToken));
    }

    /// <summary>
    /// 异步释放指定资源上由锁令牌标识的写锁。
    /// </summary>
    /// <remarks>规范化尖括号后通过 Lock-Token 头提交令牌，不能使用 If 条件代替该解锁请求头。</remarks>
    /// <param name="requestUri">请求资源的标准 URI 引用，可为绝对地址或相对 BaseAddress 的已编码路径；普通文件名可先用 WebDavUri.EncodePath 编码。</param>
    /// <param name="parameters">本次 Unlock 操作的请求参数，包括通用 HTTP 条件、请求头及取消设置。 不应为 null。</param>
    /// <returns>异步解锁任务；结果保留服务器 HTTP 状态和响应头。</returns>
    /// <exception cref="OperationCanceledException">调用方取消、客户端释放或请求超时使操作中止。</exception>
    /// <exception cref="HttpRequestException">发生网络连接或 HTTP 传输失败；普通 HTTP 错误状态通常保留在结果中。</exception>
    public Task<WebDavResponse> Unlock(string requestUri, UnlockParameters parameters)
    {
        return Unlock(CreateUri(requestUri), parameters);
    }

    /// <summary>
    /// 异步释放指定资源上由锁令牌标识的写锁。
    /// </summary>
    /// <remarks>规范化尖括号后通过 Lock-Token 头提交令牌，不能使用 If 条件代替该解锁请求头。</remarks>
    /// <param name="requestUri">请求资源的标准 URI 引用，可为绝对地址或相对 BaseAddress 的已编码路径；普通文件名可先用 WebDavUri.EncodePath 编码。</param>
    /// <param name="parameters">本次 Unlock 操作的请求参数，包括通用 HTTP 条件、请求头及取消设置。 不应为 null。</param>
    /// <returns>异步解锁任务；结果保留服务器 HTTP 状态和响应头。</returns>
    /// <exception cref="OperationCanceledException">调用方取消、客户端释放或请求超时使操作中止。</exception>
    /// <exception cref="HttpRequestException">发生网络连接或 HTTP 传输失败；普通 HTTP 错误状态通常保留在结果中。</exception>
    public async Task<WebDavResponse> Unlock(Uri requestUri, UnlockParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        using HttpRequestMessage request = CreateRequest(requestUri, "UNLOCK");
        request.Headers.Add("Lock-Token", "<" + IfHeaderHelper.Normalize(parameters.LockToken) + ">");
        parameters.Apply(request);
        return await SendMutationAsync(request, parameters.CancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 异步执行 DAV:basicsearch 单属性 LIKE 检索，并完整读取命中资源。
    /// </summary>
    /// <remarks>服务器必须支持 SEARCH 扩展；XML 文本会转义，但 LIKE 通配符仍保持服务器的匹配语义。</remarks>
    /// <param name="requestUri">请求资源的标准 URI 引用，可为绝对地址或相对 BaseAddress 的已编码路径；普通文件名可先用 WebDavUri.EncodePath 编码。</param>
    /// <param name="parameters">本次 Search 操作的请求参数，包括通用 HTTP 条件、请求头及取消设置。 不应为 null。</param>
    /// <returns>异步检索任务；与 PROPFIND 一样保留资源及逐属性状态。</returns>
    /// <exception cref="OperationCanceledException">调用方取消、客户端释放或请求超时使操作中止。</exception>
    /// <exception cref="HttpRequestException">发生网络连接或 HTTP 传输失败；普通 HTTP 错误状态通常保留在结果中。</exception>
    /// <exception cref="System.IO.InvalidDataException">成功 HTTP 状态不符合 DAV 协议，或响应缺少必需结构及确认信息。</exception>
    public Task<PropfindResponse> Search(string requestUri, SearchParameters parameters)
    {
        return Search(CreateUri(requestUri), parameters);
    }

    /// <summary>
    /// 异步执行 DAV:basicsearch 单属性 LIKE 检索，并完整读取命中资源。
    /// </summary>
    /// <remarks>服务器必须支持 SEARCH 扩展；XML 文本会转义，但 LIKE 通配符仍保持服务器的匹配语义。</remarks>
    /// <param name="requestUri">请求资源的标准 URI 引用，可为绝对地址或相对 BaseAddress 的已编码路径；普通文件名可先用 WebDavUri.EncodePath 编码。</param>
    /// <param name="parameters">本次 Search 操作的请求参数，包括通用 HTTP 条件、请求头及取消设置。 不应为 null。</param>
    /// <returns>异步检索任务；与 PROPFIND 一样保留资源及逐属性状态。</returns>
    /// <exception cref="OperationCanceledException">调用方取消、客户端释放或请求超时使操作中止。</exception>
    /// <exception cref="HttpRequestException">发生网络连接或 HTTP 传输失败；普通 HTTP 错误状态通常保留在结果中。</exception>
    /// <exception cref="System.IO.InvalidDataException">成功 HTTP 状态不符合 DAV 协议，或响应缺少必需结构及确认信息。</exception>
    public async Task<PropfindResponse> Search(Uri requestUri, SearchParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        using HttpRequestMessage request = CreateRequest(requestUri, "SEARCH", parameters, XmlContent(SearchRequestBuilder.BuildRequest(parameters)));
        return await SendPropertyRequestAsync(request, parameters.CancellationToken).ConfigureAwait(false);
    }

    /// <summary>发送属性查询并统一读取多状态资源、错误和响应头。</summary>
    /// <param name="request">由调用方释放的 PROPFIND 或 SEARCH 请求。</param>
    /// <param name="token">覆盖发送和正文读取的取消令牌。</param>
    /// <returns>已脱离网络响应生命周期的属性查询结果。</returns>
    private async Task<PropfindResponse> SendPropertyRequestAsync(HttpRequestMessage request, CancellationToken token)
    {
        using HttpResponseMessage response = await SendAsync(request, token).ConfigureAwait(false);
        IReadOnlyCollection<WebDavResource> resources = await ReadResourcesAsync(response, token, true).ConfigureAwait(false);
        return new PropfindResponse((int)response.StatusCode, response.ReasonPhrase, resources)
        {
            Error = response.IsSuccessStatusCode ? null
                : await WebDavXml.ReadErrorAsync(response, _maxXmlCharacters, token).ConfigureAwait(false)
        }.CaptureHeaders<PropfindResponse>(response);
    }

    /// <summary>
    /// 异步发送通用 XML REPORT，保留完整响应根和可用的 DAV:response 资源条目。
    /// </summary>
    /// <remarks>Document 保留根级属性、sync-token 和未知扩展；HTTP 200 可返回其他 XML 根，207 必须返回 DAV:multistatus。</remarks>
    /// <param name="requestUri">请求资源的标准 URI 引用，可为绝对地址或相对 BaseAddress 的已编码路径；普通文件名可先用 WebDavUri.EncodePath 编码。</param>
    /// <param name="report">完整 XML REPORT 请求元素；转换为 UTF-8 正文，不修改原元素。</param>
    /// <param name="options">通用 HTTP 条件、自定义请求头与取消设置；null 使用默认选项。</param>
    /// <returns>包含完整 XML 文档、根级字段、结构化错误和资源投影的报告结果。</returns>
    /// <exception cref="OperationCanceledException">调用方取消、客户端释放或请求超时使操作中止。</exception>
    /// <exception cref="HttpRequestException">发生网络连接或 HTTP 传输失败；普通 HTTP 错误状态通常保留在结果中。</exception>
    /// <exception cref="System.IO.InvalidDataException">成功 HTTP 状态不符合 DAV 协议，或响应缺少必需结构及确认信息。</exception>
    public Task<WebDavReportResponse> ReportAsync(string requestUri, XElement report, WebDavRequestOptions? options = null)
    {
        return ReportDocumentAsync(requestUri, report, options);
    }

    /// <summary>
    /// 异步发送 HEAD，仅取得状态和响应头，不下载文件正文。
    /// </summary>
    /// <remarks>可读取 Content-Length、Content-Type 和 ETag；404、401 等状态保留在结果中，由调用方处理。</remarks>
    /// <param name="requestUri">请求资源的标准 URI 引用，可为绝对地址或相对 BaseAddress 的已编码路径；普通文件名可先用 WebDavUri.EncodePath 编码。</param>
    /// <param name="options">通用 HTTP 条件、自定义请求头与取消设置；null 使用默认选项。</param>
    /// <returns>包含状态码及响应头快照的异步结果，原始 HTTP 响应已释放。</returns>
    /// <exception cref="OperationCanceledException">调用方取消、客户端释放或请求超时使操作中止。</exception>
    /// <exception cref="HttpRequestException">发生网络连接或 HTTP 传输失败；普通 HTTP 错误状态通常保留在结果中。</exception>
    public Task<WebDavResponse> HeadAsync(string requestUri, WebDavRequestOptions? options = null)
    {
        return HeadAsync(CreateUri(requestUri), options);
    }

    /// <summary>
    /// 异步发送 HEAD，仅取得状态和响应头，不下载文件正文。
    /// </summary>
    /// <remarks>可读取 Content-Length、Content-Type 和 ETag；404、401 等状态保留在结果中，由调用方处理。</remarks>
    /// <param name="requestUri">请求资源的标准 URI 引用，可为绝对地址或相对 BaseAddress 的已编码路径；普通文件名可先用 WebDavUri.EncodePath 编码。</param>
    /// <param name="options">通用 HTTP 条件、自定义请求头与取消设置；null 使用默认选项。</param>
    /// <returns>包含状态码及响应头快照的异步结果，原始 HTTP 响应已释放。</returns>
    /// <exception cref="OperationCanceledException">调用方取消、客户端释放或请求超时使操作中止。</exception>
    /// <exception cref="HttpRequestException">发生网络连接或 HTTP 传输失败；普通 HTTP 错误状态通常保留在结果中。</exception>
    public async Task<WebDavResponse> HeadAsync(Uri requestUri, WebDavRequestOptions? options = null)
    {
        options ??= new WebDavRequestOptions();
        using HttpRequestMessage request = CreateRequest(requestUri, "HEAD", options);
        using HttpResponseMessage response = await SendAsync(request, options.CancellationToken).ConfigureAwait(false);
        return new WebDavResponse((int)response.StatusCode, response.ReasonPhrase)
        {
            Error = response.IsSuccessStatusCode ? null
                : await WebDavXml.ReadErrorAsync(response, _maxXmlCharacters, options.CancellationToken).ConfigureAwait(false)
        }.CaptureHeaders<WebDavResponse>(response);
    }

    /// <summary>
    /// 异步发送 OPTIONS，查询当前资源声明的 DAV 能力及允许方法。
    /// </summary>
    /// <remarks>只解析服务器声明，不逐一尝试各项操作，也不据此推断其他资源的访问权限。</remarks>
    /// <param name="requestUri">请求资源的标准 URI 引用，可为绝对地址或相对 BaseAddress 的已编码路径；普通文件名可先用 WebDavUri.EncodePath 编码。</param>
    /// <param name="options">通用 HTTP 条件、自定义请求头与取消设置；null 使用默认选项。</param>
    /// <returns>包含 HTTP 状态、DAV 合规标识和 Allow 方法列表的异步结果。</returns>
    /// <exception cref="OperationCanceledException">调用方取消、客户端释放或请求超时使操作中止。</exception>
    /// <exception cref="HttpRequestException">发生网络连接或 HTTP 传输失败；普通 HTTP 错误状态通常保留在结果中。</exception>
    public Task<OptionsResponse> OptionsAsync(string requestUri, WebDavRequestOptions? options = null)
    {
        return OptionsAsync(CreateUri(requestUri), options);
    }

    /// <summary>
    /// 异步发送 OPTIONS，查询当前资源声明的 DAV 能力及允许方法。
    /// </summary>
    /// <remarks>只解析服务器声明，不逐一尝试各项操作，也不据此推断其他资源的访问权限。</remarks>
    /// <param name="requestUri">请求资源的标准 URI 引用，可为绝对地址或相对 BaseAddress 的已编码路径；普通文件名可先用 WebDavUri.EncodePath 编码。</param>
    /// <param name="options">通用 HTTP 条件、自定义请求头与取消设置；null 使用默认选项。</param>
    /// <returns>包含 HTTP 状态、DAV 合规标识和 Allow 方法列表的异步结果。</returns>
    /// <exception cref="OperationCanceledException">调用方取消、客户端释放或请求超时使操作中止。</exception>
    /// <exception cref="HttpRequestException">发生网络连接或 HTTP 传输失败；普通 HTTP 错误状态通常保留在结果中。</exception>
    public async Task<OptionsResponse> OptionsAsync(Uri requestUri, WebDavRequestOptions? options = null)
    {
        options ??= new WebDavRequestOptions();
        using HttpRequestMessage request = CreateRequest(requestUri, "OPTIONS", options);
        using HttpResponseMessage response = await SendAsync(request, options.CancellationToken).ConfigureAwait(false);
        return new OptionsResponse((int)response.StatusCode, response.ReasonPhrase)
        {
            Error = response.IsSuccessStatusCode ? null
                : await WebDavXml.ReadErrorAsync(response, _maxXmlCharacters, options.CancellationToken).ConfigureAwait(false)
        }.CaptureHeaders<OptionsResponse>(response);
    }

    /// <summary>
    /// 异步通过 HEAD 判断资源是否存在，仅将 HTTP 404 解释为不存在。
    /// </summary>
    /// <remarks>认证失败、访问禁止及重定向不能当成资源不存在，避免上层据此错误创建或删除文件。</remarks>
    /// <param name="requestUri">请求资源的标准 URI 引用，可为绝对地址或相对 BaseAddress 的已编码路径；普通文件名可先用 WebDavUri.EncodePath 编码。</param>
    /// <param name="cancellationToken">取消当前操作及响应正文读取的令牌，默认 CancellationToken.None。</param>
    /// <returns>HEAD 为 2xx 时返回 true，404 时返回 false；其余非成功状态抛出 HttpRequestException。</returns>
    /// <exception cref="OperationCanceledException">调用方取消、客户端释放或请求超时使操作中止。</exception>
    /// <exception cref="HttpRequestException">HEAD 返回除 404 以外的非成功状态，或发生网络传输失败。</exception>
    public async Task<bool> ExistsAsync(string requestUri, CancellationToken cancellationToken = default)
    {
        WebDavResponse response = await HeadAsync(requestUri, new WebDavRequestOptions { CancellationToken = cancellationToken }).ConfigureAwait(false);
        if (response.StatusCode == 404)
            return false;
        _ = response.EnsureSuccessStatusCode();
        return true;
    }

    /// <summary>
    /// 发送修改类请求，解析可选的 HTTP 207 并在返回前释放原响应。
    /// </summary>
    /// <param name="request">已经包含锁、覆盖及 HTTP 条件的修改请求。</param>
    /// <param name="token">覆盖发送和多状态正文读取的取消令牌。</param>
    /// <returns>保留响应头和逐资源结果的 WebDavMultiStatusResponse。</returns>
    private async Task<WebDavResponse> SendMutationAsync(HttpRequestMessage request, CancellationToken token)
    {
        var preserveRepresentation = GetRequestPreferences(request).Return == WebDavReturnPreference.Representation;
        using HttpResponseMessage response = await SendAsync(request, token).ConfigureAwait(false);
        WebDavDocumentResponse result = await ReadDocumentResponseAsync(response, token, preserveRepresentation).ConfigureAwait(false);
        result.TreatPropertyErrorsAsFailure = true;
        return result;
    }

    /// <summary>
    /// 完整读取 HTTP 207 的资源列表，并按需拒绝错误的成功状态。
    /// </summary>
    /// <remarks>若 requireMultiStatus 为 true，成功的 200 HTML 等内容不能被解释为成功空目录；非成功 HTTP 状态仍由上层保留报告。</remarks>
    /// <param name="response">需要解析或判定状态的原始响应。</param>
    /// <param name="token">发送后继续控制正文解析的请求令牌。</param>
    /// <param name="requireMultiStatus">是否要求成功 HTTP 响应必须使用 207，多用于属性类操作。</param>
    /// <returns>只读资源集合；不要求多状态且收到非 207 响应时为空集合。</returns>
    private async Task<IReadOnlyCollection<WebDavResource>> ReadResourcesAsync(HttpResponseMessage response, CancellationToken token, bool requireMultiStatus = false)
    {
        if ((int)response.StatusCode != 207)
        {
            // 登录页等服务器可能返回 HTTP 200 和 HTML。属性查询必须验证 207，避免把这类内容解释成空目录。
            // HTTP 401/404 等真实失败则由调用方读取状态，不为了强行解析错误正文而掩盖原始原因。
            return requireMultiStatus && response.IsSuccessStatusCode
                ? throw new InvalidDataException("Expected HTTP 207 for a DAV property response.")
                : [];
        }
        List<WebDavResource> resources = [];
        await foreach (XElement? element in WebDavXml.ReadResponsesAsync(response, _maxXmlCharacters, token).ConfigureAwait(false))
            resources.Add(WebDavXml.ParseResource(element));
        return resources.AsReadOnly();
    }

    /// <summary>
    /// 解析 URI 并创建独立请求，应用可选正文和通用请求设置。
    /// </summary>
    /// <remarks>应用选项过程中发生异常时关闭已创建请求，连同其接管的正文一并清理。</remarks>
    /// <param name="uri">可为绝对或相对形式的资源 URI。</param>
    /// <param name="method">HTTP 或 DAV 方法名称，例如 PROPFIND、PUT 或 REPORT。</param>
    /// <param name="options">可选的通用 HTTP 条件与自定义头。</param>
    /// <param name="content">由新请求接管的可选正文。</param>
    /// <returns>需要调用方释放的 HttpRequestMessage。</returns>
    private HttpRequestMessage CreateRequest(Uri uri, string method, WebDavRequestOptions? options = null, HttpContent? content = null)
    {
        HttpRequestMessage request = new(new HttpMethod(method), ResolveUri(uri))
        {
            Content = content
        };
        try
        {
            options?.Apply(request);
            return request;
        }
        catch
        {
            request.Dispose();
            throw;
        }
    }

    /// <summary>
    /// 把 XML 字符串转换为 UTF-8 HTTP 正文，并复制可选媒体类型。
    /// </summary>
    /// <remarks>复制媒体类型以避免共享可变头对象；缺少字符集时补 utf-8。正文编码固定为 UTF-8，不会根据自定义字符集重新编码。</remarks>
    /// <param name="xml">已经构建完成的 XML 文本。</param>
    /// <param name="mediaType">可选媒体类型；null 使用 application/xml; charset=utf-8。</param>
    /// <returns>由后续请求负责释放的 XML HttpContent。</returns>
    private static HttpContent XmlContent(string xml, MediaTypeHeaderValue? mediaType = null)
    {
        StringContent content = new(xml, Encoding.UTF8, "application/xml");
        if (mediaType is not null)
        {
            content.Headers.ContentType = MediaTypeHeaderValue.Parse(mediaType.ToString());
            content.Headers.ContentType.CharSet ??= "utf-8";
        }
        return content;
    }

    /// <summary>在发送边界一次性序列化已构建的 XML 元素。</summary>
    /// <param name="xml">待发送的完整根元素。</param>
    /// <param name="mediaType">可选 XML 媒体类型。</param>
    /// <returns>使用 UTF-8 编码的请求正文。</returns>
    private static HttpContent XmlContent(XElement xml, MediaTypeHeaderValue? mediaType = null)
    {
        return XmlContent(xml.ToString(SaveOptions.DisableFormatting), mediaType);
    }

    /// <summary>
    /// 在存在锁令牌时，为请求 URI 添加无标记的 If 锁条件。
    /// </summary>
    /// <param name="request">需要提交资源锁的请求。</param>
    /// <param name="token">可选锁令牌；null 时不添加任何头。</param>
    private static void AddLock(HttpRequestMessage request, string? token)
    {
        if (token is not null)
            request.Headers.Add("If", IfHeaderHelper.GetHeaderValue(token));
    }

    /// <summary>
    /// 按标准 URI 规则把相对地址与 BaseAddress 组合，并校验最终绝对地址。
    /// </summary>
    /// <param name="uri">要解析的绝对或相对资源 URI。</param>
    /// <returns>可交给 HTTP 发送或 Destination 头使用的合法绝对 URI。</returns>
    /// <exception cref="ArgumentNullException">uri 为 null。</exception>
    /// <exception cref="InvalidOperationException">相对 URI 未配置 BaseAddress。</exception>
    /// <exception cref="ArgumentException">最终地址不是允许的 HTTP(S) 资源 URI。</exception>
    private Uri ResolveUri(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        // 使用 URI 相对解析规则，禁止借用 Path.Combine；Windows 的反斜杠及盘符语义不适用于 DAV 地址。
        Uri result = uri.IsAbsoluteUri ? uri : _dispatcher.BaseAddress is { } address ? new Uri(address, uri)
            : throw new InvalidOperationException("A relative URI requires BaseAddress.");
        ValidateAbsoluteUri(result);
        return result;
    }

    /// <summary>
    /// 检查 URI 使用 HTTP(S) 方案，且不包含内嵌凭据或片段。
    /// </summary>
    /// <param name="uri">待校验的最终绝对请求地址。</param>
    /// <exception cref="ArgumentException">不是绝对 HTTP(S) URI，或包含 UserInfo、Fragment。</exception>
    private static void ValidateAbsoluteUri(Uri uri)
    {
        if (!uri.IsAbsoluteUri || uri.Scheme is not ("http" or "https") || uri.UserInfo.Length != 0 || uri.Fragment.Length != 0)
            throw new ArgumentException("An HTTP(S) URI without embedded credentials or fragments is required.", nameof(uri));
    }

    /// <summary>
    /// 把字符串解释为标准绝对或相对 URI，不重复编码已转义的路径。
    /// </summary>
    /// <param name="uri">标准 URI 引用字符串，不是未经编码的本地文件路径。</param>
    /// <returns>保留输入 URI 语义的 Uri 对象。</returns>
    /// <exception cref="ArgumentNullException">uri 为 null。</exception>
    /// <exception cref="UriFormatException">输入不能解释为合法 URI。</exception>
    private static Uri CreateUri(string uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        return new Uri(uri, UriKind.RelativeOrAbsolute);
    }

    /// <summary>
    /// 验证请求超时为可由计时器支持的正时长，或显式无限时长。
    /// </summary>
    /// <param name="timeout">待检查的请求时长，允许 Timeout.InfiniteTimeSpan。</param>
    /// <exception cref="ArgumentOutOfRangeException">非无限超时小于等于零，或超过计时器允许的毫秒上限。</exception>
    private static void ValidateTimeout(TimeSpan timeout)
    {
        if (timeout != Timeout.InfiniteTimeSpan && (timeout <= TimeSpan.Zero || timeout.TotalMilliseconds > uint.MaxValue - 1))
            throw new ArgumentOutOfRangeException(nameof(timeout));
    }

    /// <summary>
    /// 校验 206 响应的字节区间、正文长度和单段请求范围是否相容。
    /// </summary>
    /// <remarks>200 表示服务器合法忽略 Range，留给调用方处理；206 必须具备 bytes 区间，若提供总长度则终点不能越界，已声明本段长度必须与区间一致。</remarks>
    /// <param name="request">包含最终 Range 头的 GET 请求，已应用自定义头覆盖。</param>
    /// <param name="response">待检查的服务器文件响应。</param>
    /// <exception cref="System.IO.InvalidDataException">Content-Range 缺失、单位或区间不合法、长度不一致，或返回范围不匹配请求。</exception>
    private static void ValidateRange(HttpRequestMessage request, HttpResponseMessage response)
    {
        if (response.StatusCode != HttpStatusCode.PartialContent)
            return; // 服务器可合法忽略 Range 并返回完整 200，是否需要回退由下载调用方根据状态决定。
        // 206 必须给出有效 bytes 区间。终点是包含式位置，因此本段长度为 To - From + 1。
        // 总长度可未知，但一旦提供，就不能返回越过文件末尾的区间。
        ContentRangeHeaderValue? range = response.Content.Headers.ContentRange;
        (long from, long to) = ByteRangeRequestValidator.GetInterval(range, response.Content.Headers.ContentLength);
        if (request.Headers.Range is not { Ranges.Count: 1 } requested)
            return;
        RangeItemHeaderValue item = requested.Ranges.Single();
        // 普通区间核对起点和上界；后缀区间没有 From，需在总长度已知时反推出应有的起点。
        if ((item.From.HasValue && item.From != from)
            || (item.To.HasValue && item.From.HasValue && to > item.To)
            || (!item.From.HasValue && range!.Length is { } length && from != Math.Max(0, length - item.To!.Value)))
            throw new InvalidDataException("HTTP Content-Range does not match the requested byte range.");
    }

    /// <summary>
    /// 释放客户端请求调度器，取消在途请求及其响应正文。
    /// </summary>
    /// <remarks>自建 HttpClient 随之释放；宿主注入且未转交所有权的 HttpClient 保持可用。已经返回的流式响应仍应由调用方释放。</remarks>
    public void Dispose()
    {
        _dispatcher.Dispose();
    }
}
