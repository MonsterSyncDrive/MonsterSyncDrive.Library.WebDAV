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
using MonsterSyncDrive.Library.WebDAV.Request;
using MonsterSyncDrive.Library.WebDAV.Response;

namespace MonsterSyncDrive.WebDAV;

/// <summary>
/// 定义可并发使用的异步 WebDAV 客户端接口。
/// </summary>
/// <remarks>HTTP 失败保留在响应状态中；网络、取消、非法 URI 和 XML 错误抛出异常。流式结果和原始 HTTP 响应需要调用方及时释放。</remarks>
public partial interface IWebDavClient : IDisposable
{
    /// <summary>
    /// 获取解析相对请求 URI 使用的 HTTP 基地址。
    /// </summary>
    /// <value>未配置时为 null，只能发送绝对请求 URI；相对地址按标准 URI 规则解析。</value>
    Uri? BaseAddress
    {
        get;
    }
    /// <summary>
    /// 异步发送原始 HTTP 或 DAV 扩展请求，将请求取消延续到响应正文。
    /// </summary>
    /// <remarks>用于 ACL 等扩展或需要自定义解析的响应；仅负责 URI 校验与网络生命周期，不自动解析 XML，不把普通 HTTP 失败状态转成异常。</remarks>
    /// <param name="request">由调用方拥有的原始 HTTP 请求，必须设置请求 URI；调用方负责释放。</param>
    /// <param name="cancellationToken">取消当前操作及响应正文读取的令牌，默认 CancellationToken.None。</param>
    /// <returns>响应头到达后完成的任务；调用方拥有返回的 HTTP 响应及传入的请求，两者均须释放。</returns>
    /// <exception cref="OperationCanceledException">调用方取消、客户端释放或请求超时使操作中止。</exception>
    /// <exception cref="HttpRequestException">发生网络连接或 HTTP 传输失败；普通 HTTP 错误状态通常保留在结果中。</exception>
    Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken = default);
    /// <summary>
    /// 异步发送 HEAD，仅取得状态和响应头，不下载文件正文。
    /// </summary>
    /// <remarks>可读取 Content-Length、Content-Type 和 ETag；404、401 等状态保留在结果中，由调用方处理。</remarks>
    /// <param name="requestUri">请求资源的标准 URI 引用，可为绝对地址或相对 BaseAddress 的已编码路径；普通文件名可先用 WebDavUri.EncodePath 编码。</param>
    /// <param name="options">通用 HTTP 条件、自定义请求头与取消设置；null 使用默认选项。</param>
    /// <returns>包含状态码及响应头快照的异步结果，原始 HTTP 响应已释放。</returns>
    /// <exception cref="OperationCanceledException">调用方取消、客户端释放或请求超时使操作中止。</exception>
    /// <exception cref="HttpRequestException">发生网络连接或 HTTP 传输失败；普通 HTTP 错误状态通常保留在结果中。</exception>
    Task<WebDavResponse> HeadAsync(string requestUri, WebDavRequestOptions? options = null);
    /// <summary>
    /// 异步发送 HEAD，仅取得状态和响应头，不下载文件正文。
    /// </summary>
    /// <remarks>可读取 Content-Length、Content-Type 和 ETag；404、401 等状态保留在结果中，由调用方处理。</remarks>
    /// <param name="requestUri">请求资源的标准 URI 引用，可为绝对地址或相对 BaseAddress 的已编码路径；普通文件名可先用 WebDavUri.EncodePath 编码。</param>
    /// <param name="options">通用 HTTP 条件、自定义请求头与取消设置；null 使用默认选项。</param>
    /// <returns>包含状态码及响应头快照的异步结果，原始 HTTP 响应已释放。</returns>
    /// <exception cref="OperationCanceledException">调用方取消、客户端释放或请求超时使操作中止。</exception>
    /// <exception cref="HttpRequestException">发生网络连接或 HTTP 传输失败；普通 HTTP 错误状态通常保留在结果中。</exception>
    Task<WebDavResponse> HeadAsync(Uri requestUri, WebDavRequestOptions? options = null);
    /// <summary>
    /// 异步发送 OPTIONS，查询当前资源声明的 DAV 能力及允许方法。
    /// </summary>
    /// <remarks>只解析服务器声明，不逐一尝试各项操作，也不据此推断其他资源的访问权限。</remarks>
    /// <param name="requestUri">请求资源的标准 URI 引用，可为绝对地址或相对 BaseAddress 的已编码路径；普通文件名可先用 WebDavUri.EncodePath 编码。</param>
    /// <param name="options">通用 HTTP 条件、自定义请求头与取消设置；null 使用默认选项。</param>
    /// <returns>包含 HTTP 状态、DAV 合规标识和 Allow 方法列表的异步结果。</returns>
    /// <exception cref="OperationCanceledException">调用方取消、客户端释放或请求超时使操作中止。</exception>
    /// <exception cref="HttpRequestException">发生网络连接或 HTTP 传输失败；普通 HTTP 错误状态通常保留在结果中。</exception>
    Task<OptionsResponse> OptionsAsync(string requestUri, WebDavRequestOptions? options = null);
    /// <summary>
    /// 异步发送 OPTIONS，查询当前资源声明的 DAV 能力及允许方法。
    /// </summary>
    /// <remarks>只解析服务器声明，不逐一尝试各项操作，也不据此推断其他资源的访问权限。</remarks>
    /// <param name="requestUri">请求资源的标准 URI 引用，可为绝对地址或相对 BaseAddress 的已编码路径；普通文件名可先用 WebDavUri.EncodePath 编码。</param>
    /// <param name="options">通用 HTTP 条件、自定义请求头与取消设置；null 使用默认选项。</param>
    /// <returns>包含 HTTP 状态、DAV 合规标识和 Allow 方法列表的异步结果。</returns>
    /// <exception cref="OperationCanceledException">调用方取消、客户端释放或请求超时使操作中止。</exception>
    /// <exception cref="HttpRequestException">发生网络连接或 HTTP 传输失败；普通 HTTP 错误状态通常保留在结果中。</exception>
    Task<OptionsResponse> OptionsAsync(Uri requestUri, WebDavRequestOptions? options = null);
    /// <summary>
    /// 异步通过 HEAD 判断资源是否存在，仅将 HTTP 404 解释为不存在。
    /// </summary>
    /// <remarks>认证失败、访问禁止及重定向不能当成资源不存在，避免上层据此错误创建或删除文件。</remarks>
    /// <param name="requestUri">请求资源的标准 URI 引用，可为绝对地址或相对 BaseAddress 的已编码路径；普通文件名可先用 WebDavUri.EncodePath 编码。</param>
    /// <param name="cancellationToken">取消当前操作及响应正文读取的令牌，默认 CancellationToken.None。</param>
    /// <returns>HEAD 为 2xx 时返回 true，404 时返回 false；其余非成功状态抛出 HttpRequestException。</returns>
    /// <exception cref="OperationCanceledException">调用方取消、客户端释放或请求超时使操作中止。</exception>
    /// <exception cref="HttpRequestException">HEAD 返回除 404 以外的非成功状态，或发生网络传输失败。</exception>
    Task<bool> ExistsAsync(string requestUri, CancellationToken cancellationToken = default);
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
    IAsyncEnumerable<WebDavResource> EnumerateAsync(string requestUri,
        PropfindParameters? parameters = null, CancellationToken cancellationToken = default);
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
    /// <exception cref="HttpRequestException">发生网络连接或 HTTP 传输失败；普通 HTTP 错误状态通常保留在结果中。</exception>
    /// <exception cref="System.IO.IOException">正文复制失败，或实际下载字节数与 Content-Length 不符。</exception>
    Task<WebDavResponse> DownloadFileAsync(string requestUri, Stream destination,
        GetFileParameters? parameters = null, IProgress<long>? progress = null);
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
    Task<WebDavReportResponse> ReportAsync(string requestUri, System.Xml.Linq.XElement report, WebDavRequestOptions? options = null);
    /// <summary>
    /// 异步查询资源的 DAV 属性，完整读取多状态文档后返回资源快照。
    /// </summary>
    /// <remarks>成功响应必须为合法 HTTP 207。可选属性 404 保留在 PropertyStatuses，资源级失败影响整体结果；需要流式处理时使用 EnumerateAsync。 此重载使用该操作的默认请求参数。</remarks>
    /// <param name="requestUri">请求资源的标准 URI 引用，可为绝对地址或相对 BaseAddress 的已编码路径；普通文件名可先用 WebDavUri.EncodePath 编码。</param>
    /// <returns>异步查询任务；结果包含资源、逐属性状态和响应头快照。</returns>
    /// <exception cref="OperationCanceledException">调用方取消、客户端释放或请求超时使操作中止。</exception>
    /// <exception cref="HttpRequestException">发生网络连接或 HTTP 传输失败；普通 HTTP 错误状态通常保留在结果中。</exception>
    /// <exception cref="System.IO.InvalidDataException">成功 HTTP 状态不符合 DAV 协议，或响应缺少必需结构及确认信息。</exception>
    Task<PropfindResponse> Propfind(string requestUri);

    /// <summary>
    /// 异步查询资源的 DAV 属性，完整读取多状态文档后返回资源快照。
    /// </summary>
    /// <remarks>成功响应必须为合法 HTTP 207。可选属性 404 保留在 PropertyStatuses，资源级失败影响整体结果；需要流式处理时使用 EnumerateAsync。 此重载使用该操作的默认请求参数。</remarks>
    /// <param name="requestUri">请求资源的标准 URI 引用，可为绝对地址或相对 BaseAddress 的已编码路径；普通文件名可先用 WebDavUri.EncodePath 编码。</param>
    /// <returns>异步查询任务；结果包含资源、逐属性状态和响应头快照。</returns>
    /// <exception cref="OperationCanceledException">调用方取消、客户端释放或请求超时使操作中止。</exception>
    /// <exception cref="HttpRequestException">发生网络连接或 HTTP 传输失败；普通 HTTP 错误状态通常保留在结果中。</exception>
    /// <exception cref="System.IO.InvalidDataException">成功 HTTP 状态不符合 DAV 协议，或响应缺少必需结构及确认信息。</exception>
    Task<PropfindResponse> Propfind(Uri requestUri);

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
    Task<PropfindResponse> Propfind(string requestUri, PropfindParameters parameters);

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
    Task<PropfindResponse> Propfind(Uri requestUri, PropfindParameters parameters);

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
    Task<ProppatchResponse> Proppatch(string requestUri, ProppatchParameters parameters);

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
    Task<ProppatchResponse> Proppatch(Uri requestUri, ProppatchParameters parameters);

    /// <summary>
    /// 异步在指定 URI 创建 WebDAV 集合。
    /// </summary>
    /// <remarks>只创建指定集合，不递归创建父目录；目标存在或父目录缺失时由服务器返回对应状态。</remarks>
    /// <param name="requestUri">请求资源的标准 URI 引用，可为绝对地址或相对 BaseAddress 的已编码路径；普通文件名可先用 WebDavUri.EncodePath 编码。</param>
    /// <returns>异步创建任务；结果包含 HTTP 状态及可能的多状态资源错误。</returns>
    /// <exception cref="OperationCanceledException">调用方取消、客户端释放或请求超时使操作中止。</exception>
    /// <exception cref="HttpRequestException">发生网络连接或 HTTP 传输失败；普通 HTTP 错误状态通常保留在结果中。</exception>
    Task<WebDavResponse> Mkcol(string requestUri);

    /// <summary>
    /// 异步在指定 URI 创建 WebDAV 集合。
    /// </summary>
    /// <remarks>只创建指定集合，不递归创建父目录；目标存在或父目录缺失时由服务器返回对应状态。</remarks>
    /// <param name="requestUri">请求资源的标准 URI 引用，可为绝对地址或相对 BaseAddress 的已编码路径；普通文件名可先用 WebDavUri.EncodePath 编码。</param>
    /// <returns>异步创建任务；结果包含 HTTP 状态及可能的多状态资源错误。</returns>
    /// <exception cref="OperationCanceledException">调用方取消、客户端释放或请求超时使操作中止。</exception>
    /// <exception cref="HttpRequestException">发生网络连接或 HTTP 传输失败；普通 HTTP 错误状态通常保留在结果中。</exception>
    Task<WebDavResponse> Mkcol(Uri requestUri);

    /// <summary>
    /// 异步在指定 URI 创建 WebDAV 集合。
    /// </summary>
    /// <remarks>只创建指定集合，不递归创建父目录；目标存在或父目录缺失时由服务器返回对应状态。</remarks>
    /// <param name="requestUri">请求资源的标准 URI 引用，可为绝对地址或相对 BaseAddress 的已编码路径；普通文件名可先用 WebDavUri.EncodePath 编码。</param>
    /// <param name="parameters">本次 Mkcol 操作的请求参数，包括通用 HTTP 条件、请求头及取消设置。 不应为 null。</param>
    /// <returns>异步创建任务；结果包含 HTTP 状态及可能的多状态资源错误。</returns>
    /// <exception cref="OperationCanceledException">调用方取消、客户端释放或请求超时使操作中止。</exception>
    /// <exception cref="HttpRequestException">发生网络连接或 HTTP 传输失败；普通 HTTP 错误状态通常保留在结果中。</exception>
    Task<WebDavResponse> Mkcol(string requestUri, MkColParameters parameters);

    /// <summary>
    /// 异步在指定 URI 创建 WebDAV 集合。
    /// </summary>
    /// <remarks>只创建指定集合，不递归创建父目录；目标存在或父目录缺失时由服务器返回对应状态。</remarks>
    /// <param name="requestUri">请求资源的标准 URI 引用，可为绝对地址或相对 BaseAddress 的已编码路径；普通文件名可先用 WebDavUri.EncodePath 编码。</param>
    /// <param name="parameters">本次 Mkcol 操作的请求参数，包括通用 HTTP 条件、请求头及取消设置。 不应为 null。</param>
    /// <returns>异步创建任务；结果包含 HTTP 状态及可能的多状态资源错误。</returns>
    /// <exception cref="OperationCanceledException">调用方取消、客户端释放或请求超时使操作中止。</exception>
    /// <exception cref="HttpRequestException">发生网络连接或 HTTP 传输失败；普通 HTTP 错误状态通常保留在结果中。</exception>
    Task<WebDavResponse> Mkcol(Uri requestUri, MkColParameters parameters);

    /// <summary>
    /// 异步打开原始文件正文流，支持单段字节范围与 HTTP 版本条件。
    /// </summary>
    /// <remarks>发送 Translate: f 和 Accept-Encoding: identity；拒绝收到的非 identity 内容编码，206 时校验 Content-Range。服务器可忽略 Range 返回完整 200，调用方必须检查状态。</remarks>
    /// <param name="requestUri">请求资源的标准 URI 引用，可为绝对地址或相对 BaseAddress 的已编码路径；普通文件名可先用 WebDavUri.EncodePath 编码。</param>
    /// <returns>包含下载流和响应头快照的异步结果；调用方应使用 using 或 await using 释放。</returns>
    /// <exception cref="OperationCanceledException">调用方取消、客户端释放或请求超时使操作中止。</exception>
    /// <exception cref="HttpRequestException">发生网络连接或 HTTP 传输失败；普通 HTTP 错误状态通常保留在结果中。</exception>
    /// <exception cref="System.IO.InvalidDataException">分段范围不合法，或原始文件响应包含不允许的内容编码。</exception>
    Task<WebDavStreamResponse> GetRawFile(string requestUri);

    /// <summary>
    /// 异步打开原始文件正文流，支持单段字节范围与 HTTP 版本条件。
    /// </summary>
    /// <remarks>发送 Translate: f 和 Accept-Encoding: identity；拒绝收到的非 identity 内容编码，206 时校验 Content-Range。服务器可忽略 Range 返回完整 200，调用方必须检查状态。</remarks>
    /// <param name="requestUri">请求资源的标准 URI 引用，可为绝对地址或相对 BaseAddress 的已编码路径；普通文件名可先用 WebDavUri.EncodePath 编码。</param>
    /// <returns>包含下载流和响应头快照的异步结果；调用方应使用 using 或 await using 释放。</returns>
    /// <exception cref="OperationCanceledException">调用方取消、客户端释放或请求超时使操作中止。</exception>
    /// <exception cref="HttpRequestException">发生网络连接或 HTTP 传输失败；普通 HTTP 错误状态通常保留在结果中。</exception>
    /// <exception cref="System.IO.InvalidDataException">分段范围不合法，或原始文件响应包含不允许的内容编码。</exception>
    Task<WebDavStreamResponse> GetRawFile(Uri requestUri);

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
    Task<WebDavStreamResponse> GetRawFile(string requestUri, GetFileParameters parameters);

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
    Task<WebDavStreamResponse> GetRawFile(Uri requestUri, GetFileParameters parameters);

    /// <summary>
    /// 异步请求可由服务器处理后的文件正文，并保留流式响应。
    /// </summary>
    /// <remarks>Translate: t 仅提示服务器允许处理，实际语义由服务器决定；方法不会等待或缓冲整个文件正文。</remarks>
    /// <param name="requestUri">请求资源的标准 URI 引用，可为绝对地址或相对 BaseAddress 的已编码路径；普通文件名可先用 WebDavUri.EncodePath 编码。</param>
    /// <returns>包含下载流及响应头的异步结果，调用方需检查 HTTP 状态并及时释放。</returns>
    /// <exception cref="OperationCanceledException">调用方取消、客户端释放或请求超时使操作中止。</exception>
    /// <exception cref="HttpRequestException">发生网络连接或 HTTP 传输失败；普通 HTTP 错误状态通常保留在结果中。</exception>
    /// <exception cref="System.IO.InvalidDataException">分段范围不合法，或原始文件响应包含不允许的内容编码。</exception>
    Task<WebDavStreamResponse> GetProcessedFile(string requestUri);

    /// <summary>
    /// 异步请求可由服务器处理后的文件正文，并保留流式响应。
    /// </summary>
    /// <remarks>Translate: t 仅提示服务器允许处理，实际语义由服务器决定；方法不会等待或缓冲整个文件正文。</remarks>
    /// <param name="requestUri">请求资源的标准 URI 引用，可为绝对地址或相对 BaseAddress 的已编码路径；普通文件名可先用 WebDavUri.EncodePath 编码。</param>
    /// <returns>包含下载流及响应头的异步结果，调用方需检查 HTTP 状态并及时释放。</returns>
    /// <exception cref="OperationCanceledException">调用方取消、客户端释放或请求超时使操作中止。</exception>
    /// <exception cref="HttpRequestException">发生网络连接或 HTTP 传输失败；普通 HTTP 错误状态通常保留在结果中。</exception>
    /// <exception cref="System.IO.InvalidDataException">分段范围不合法，或原始文件响应包含不允许的内容编码。</exception>
    Task<WebDavStreamResponse> GetProcessedFile(Uri requestUri);

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
    Task<WebDavStreamResponse> GetProcessedFile(string requestUri, GetFileParameters parameters);

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
    Task<WebDavStreamResponse> GetProcessedFile(Uri requestUri, GetFileParameters parameters);

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
    Task<HttpResponseMessage> GetFileResponse(Uri requestUri, bool translate, GetFileParameters parameters);

    /// <summary>
    /// 异步删除文件或集合，保留集合内部资源的删除失败结果。
    /// </summary>
    /// <remarks>集合删除范围由服务器按协议处理；本库不把 404 自动解释为成功，也不自动重试删除。</remarks>
    /// <param name="requestUri">请求资源的标准 URI 引用，可为绝对地址或相对 BaseAddress 的已编码路径；普通文件名可先用 WebDavUri.EncodePath 编码。</param>
    /// <returns>异步删除任务；HTTP 207 时结果为 WebDavMultiStatusResponse，内部失败可通过 IsSuccessful 发现。</returns>
    /// <exception cref="OperationCanceledException">调用方取消、客户端释放或请求超时使操作中止。</exception>
    /// <exception cref="HttpRequestException">发生网络连接或 HTTP 传输失败；普通 HTTP 错误状态通常保留在结果中。</exception>
    Task<WebDavResponse> Delete(string requestUri);

    /// <summary>
    /// 异步删除文件或集合，保留集合内部资源的删除失败结果。
    /// </summary>
    /// <remarks>集合删除范围由服务器按协议处理；本库不把 404 自动解释为成功，也不自动重试删除。</remarks>
    /// <param name="requestUri">请求资源的标准 URI 引用，可为绝对地址或相对 BaseAddress 的已编码路径；普通文件名可先用 WebDavUri.EncodePath 编码。</param>
    /// <returns>异步删除任务；HTTP 207 时结果为 WebDavMultiStatusResponse，内部失败可通过 IsSuccessful 发现。</returns>
    /// <exception cref="OperationCanceledException">调用方取消、客户端释放或请求超时使操作中止。</exception>
    /// <exception cref="HttpRequestException">发生网络连接或 HTTP 传输失败；普通 HTTP 错误状态通常保留在结果中。</exception>
    Task<WebDavResponse> Delete(Uri requestUri);

    /// <summary>
    /// 异步删除文件或集合，保留集合内部资源的删除失败结果。
    /// </summary>
    /// <remarks>集合删除范围由服务器按协议处理；本库不把 404 自动解释为成功，也不自动重试删除。</remarks>
    /// <param name="requestUri">请求资源的标准 URI 引用，可为绝对地址或相对 BaseAddress 的已编码路径；普通文件名可先用 WebDavUri.EncodePath 编码。</param>
    /// <param name="parameters">本次 Delete 操作的请求参数，包括通用 HTTP 条件、请求头及取消设置。 不应为 null。</param>
    /// <returns>异步删除任务；HTTP 207 时结果为 WebDavMultiStatusResponse，内部失败可通过 IsSuccessful 发现。</returns>
    /// <exception cref="OperationCanceledException">调用方取消、客户端释放或请求超时使操作中止。</exception>
    /// <exception cref="HttpRequestException">发生网络连接或 HTTP 传输失败；普通 HTTP 错误状态通常保留在结果中。</exception>
    Task<WebDavResponse> Delete(string requestUri, DeleteParameters parameters);

    /// <summary>
    /// 异步删除文件或集合，保留集合内部资源的删除失败结果。
    /// </summary>
    /// <remarks>集合删除范围由服务器按协议处理；本库不把 404 自动解释为成功，也不自动重试删除。</remarks>
    /// <param name="requestUri">请求资源的标准 URI 引用，可为绝对地址或相对 BaseAddress 的已编码路径；普通文件名可先用 WebDavUri.EncodePath 编码。</param>
    /// <param name="parameters">本次 Delete 操作的请求参数，包括通用 HTTP 条件、请求头及取消设置。 不应为 null。</param>
    /// <returns>异步删除任务；HTTP 207 时结果为 WebDavMultiStatusResponse，内部失败可通过 IsSuccessful 发现。</returns>
    /// <exception cref="OperationCanceledException">调用方取消、客户端释放或请求超时使操作中止。</exception>
    /// <exception cref="HttpRequestException">发生网络连接或 HTTP 传输失败；普通 HTTP 错误状态通常保留在结果中。</exception>
    Task<WebDavResponse> Delete(Uri requestUri, DeleteParameters parameters);

    /// <summary>
    /// 异步通过 PUT 创建或覆盖文件内容，并返回服务器确认结果。
    /// </summary>
    /// <remarks>默认按服务器 PUT 语义覆盖目标；仅创建时设置 IfNoneMatch 为 *，按版本覆盖时设置 IfMatch。上传字节进度不代表服务器事务已提交。 可定位源流在 HTTP 正文重发时恢复到初始位置；不可定位流不保证可重放。</remarks>
    /// <param name="requestUri">请求资源的标准 URI 引用，可为绝对地址或相对 BaseAddress 的已编码路径；普通文件名可先用 WebDavUri.EncodePath 编码。</param>
    /// <param name="stream">从当前位置开始读取的可读源流；默认保留源流，所有权由 PutFileParameters.LeaveOpen 控制。</param>
    /// <returns>异步上传任务；结果保留 HTTP 状态、响应头和可能的逐资源错误。</returns>
    /// <exception cref="OperationCanceledException">调用方取消、客户端释放或请求超时使操作中止。</exception>
    /// <exception cref="HttpRequestException">发生网络连接或 HTTP 传输失败；普通 HTTP 错误状态通常保留在结果中。</exception>
    Task<WebDavResponse> PutFile(string requestUri, Stream stream);

    /// <summary>
    /// 异步通过 PUT 创建或覆盖文件内容，并返回服务器确认结果。
    /// </summary>
    /// <remarks>默认按服务器 PUT 语义覆盖目标；仅创建时设置 IfNoneMatch 为 *，按版本覆盖时设置 IfMatch。上传字节进度不代表服务器事务已提交。 可定位源流在 HTTP 正文重发时恢复到初始位置；不可定位流不保证可重放。</remarks>
    /// <param name="requestUri">请求资源的标准 URI 引用，可为绝对地址或相对 BaseAddress 的已编码路径；普通文件名可先用 WebDavUri.EncodePath 编码。</param>
    /// <param name="stream">从当前位置开始读取的可读源流；默认保留源流，所有权由 PutFileParameters.LeaveOpen 控制。</param>
    /// <returns>异步上传任务；结果保留 HTTP 状态、响应头和可能的逐资源错误。</returns>
    /// <exception cref="OperationCanceledException">调用方取消、客户端释放或请求超时使操作中止。</exception>
    /// <exception cref="HttpRequestException">发生网络连接或 HTTP 传输失败；普通 HTTP 错误状态通常保留在结果中。</exception>
    Task<WebDavResponse> PutFile(Uri requestUri, Stream stream);

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
    Task<WebDavResponse> PutFile(string requestUri, Stream stream, string contentType);

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
    Task<WebDavResponse> PutFile(Uri requestUri, Stream stream, string contentType);

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
    Task<WebDavResponse> PutFile(string requestUri, Stream stream, PutFileParameters parameters);

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
    Task<WebDavResponse> PutFile(Uri requestUri, Stream stream, PutFileParameters parameters);

    /// <summary>
    /// 异步通过 PUT 创建或覆盖文件内容，并返回服务器确认结果。
    /// </summary>
    /// <remarks>默认按服务器 PUT 语义覆盖目标；仅创建时设置 IfNoneMatch 为 *，按版本覆盖时设置 IfMatch。上传字节进度不代表服务器事务已提交。 传入的 HttpContent 随请求释放；若需保留其内部流，应使用不关闭底层流的正文包装。</remarks>
    /// <param name="requestUri">请求资源的标准 URI 引用，可为绝对地址或相对 BaseAddress 的已编码路径；普通文件名可先用 WebDavUri.EncodePath 编码。</param>
    /// <param name="content">要发送的 HTTP 正文；随已构造的 PUT 请求管理和释放，包括正文自身拥有的流。</param>
    /// <returns>异步上传任务；结果保留 HTTP 状态、响应头和可能的逐资源错误。</returns>
    /// <exception cref="OperationCanceledException">调用方取消、客户端释放或请求超时使操作中止。</exception>
    /// <exception cref="HttpRequestException">发生网络连接或 HTTP 传输失败；普通 HTTP 错误状态通常保留在结果中。</exception>
    Task<WebDavResponse> PutFile(string requestUri, HttpContent content);

    /// <summary>
    /// 异步通过 PUT 创建或覆盖文件内容，并返回服务器确认结果。
    /// </summary>
    /// <remarks>默认按服务器 PUT 语义覆盖目标；仅创建时设置 IfNoneMatch 为 *，按版本覆盖时设置 IfMatch。上传字节进度不代表服务器事务已提交。 传入的 HttpContent 随请求释放；若需保留其内部流，应使用不关闭底层流的正文包装。</remarks>
    /// <param name="requestUri">请求资源的标准 URI 引用，可为绝对地址或相对 BaseAddress 的已编码路径；普通文件名可先用 WebDavUri.EncodePath 编码。</param>
    /// <param name="content">要发送的 HTTP 正文；随已构造的 PUT 请求管理和释放，包括正文自身拥有的流。</param>
    /// <returns>异步上传任务；结果保留 HTTP 状态、响应头和可能的逐资源错误。</returns>
    /// <exception cref="OperationCanceledException">调用方取消、客户端释放或请求超时使操作中止。</exception>
    /// <exception cref="HttpRequestException">发生网络连接或 HTTP 传输失败；普通 HTTP 错误状态通常保留在结果中。</exception>
    Task<WebDavResponse> PutFile(Uri requestUri, HttpContent content);

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
    Task<WebDavResponse> PutFile(string requestUri, HttpContent content, PutFileParameters parameters);

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
    Task<WebDavResponse> PutFile(Uri requestUri, HttpContent content, PutFileParameters parameters);

    /// <summary>
    /// 异步复制源资源到目标 URI，支持集合深度、覆盖策略及目标锁条件。
    /// </summary>
    /// <remarks>Destination 使用解析后的绝对 URI；目标锁通过带目标 URI 标记的 If 条件提交，不会误作为针对源资源的条件。 此重载使用该操作的默认请求参数。</remarks>
    /// <param name="sourceUri">源资源的 URI 引用，按客户端基地址解析；不使用本地文件系统路径规则。</param>
    /// <param name="destUri">目标资源的 URI 引用，发送前解析成 Destination 需要的绝对 URI。</param>
    /// <returns>异步复制任务；多状态响应保留已返回的资源和属性错误。</returns>
    /// <exception cref="OperationCanceledException">调用方取消、客户端释放或请求超时使操作中止。</exception>
    /// <exception cref="HttpRequestException">发生网络连接或 HTTP 传输失败；普通 HTTP 错误状态通常保留在结果中。</exception>
    Task<WebDavResponse> Copy(string sourceUri, string destUri);

    /// <summary>
    /// 异步复制源资源到目标 URI，支持集合深度、覆盖策略及目标锁条件。
    /// </summary>
    /// <remarks>Destination 使用解析后的绝对 URI；目标锁通过带目标 URI 标记的 If 条件提交，不会误作为针对源资源的条件。 此重载使用该操作的默认请求参数。</remarks>
    /// <param name="sourceUri">源资源的 URI 引用，按客户端基地址解析；不使用本地文件系统路径规则。</param>
    /// <param name="destUri">目标资源的 URI 引用，发送前解析成 Destination 需要的绝对 URI。</param>
    /// <returns>异步复制任务；多状态响应保留已返回的资源和属性错误。</returns>
    /// <exception cref="OperationCanceledException">调用方取消、客户端释放或请求超时使操作中止。</exception>
    /// <exception cref="HttpRequestException">发生网络连接或 HTTP 传输失败；普通 HTTP 错误状态通常保留在结果中。</exception>
    Task<WebDavResponse> Copy(Uri sourceUri, Uri destUri);

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
    Task<WebDavResponse> Copy(string sourceUri, string destUri, CopyParameters parameters);

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
    Task<WebDavResponse> Copy(Uri sourceUri, Uri destUri, CopyParameters parameters);

    /// <summary>
    /// 异步移动源资源到目标 URI，按各自 URI 提交源锁与目标锁。
    /// </summary>
    /// <remarks>两组锁条件放入同一个 If 头并分别标记资源；默认允许覆盖，需要拒绝覆盖时设置 Overwrite 为 false。 此重载使用该操作的默认请求参数。</remarks>
    /// <param name="sourceUri">源资源的 URI 引用，按客户端基地址解析；不使用本地文件系统路径规则。</param>
    /// <param name="destUri">目标资源的 URI 引用，发送前解析成 Destination 需要的绝对 URI。</param>
    /// <returns>异步移动任务；多状态内部错误会使 IsSuccessful 为 false，不能仅依据 HTTP 207 判断成功。</returns>
    /// <exception cref="OperationCanceledException">调用方取消、客户端释放或请求超时使操作中止。</exception>
    /// <exception cref="HttpRequestException">发生网络连接或 HTTP 传输失败；普通 HTTP 错误状态通常保留在结果中。</exception>
    Task<WebDavResponse> Move(string sourceUri, string destUri);

    /// <summary>
    /// 异步移动源资源到目标 URI，按各自 URI 提交源锁与目标锁。
    /// </summary>
    /// <remarks>两组锁条件放入同一个 If 头并分别标记资源；默认允许覆盖，需要拒绝覆盖时设置 Overwrite 为 false。 此重载使用该操作的默认请求参数。</remarks>
    /// <param name="sourceUri">源资源的 URI 引用，按客户端基地址解析；不使用本地文件系统路径规则。</param>
    /// <param name="destUri">目标资源的 URI 引用，发送前解析成 Destination 需要的绝对 URI。</param>
    /// <returns>异步移动任务；多状态内部错误会使 IsSuccessful 为 false，不能仅依据 HTTP 207 判断成功。</returns>
    /// <exception cref="OperationCanceledException">调用方取消、客户端释放或请求超时使操作中止。</exception>
    /// <exception cref="HttpRequestException">发生网络连接或 HTTP 传输失败；普通 HTTP 错误状态通常保留在结果中。</exception>
    Task<WebDavResponse> Move(Uri sourceUri, Uri destUri);

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
    Task<WebDavResponse> Move(string sourceUri, string destUri, MoveParameters parameters);

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
    Task<WebDavResponse> Move(Uri sourceUri, Uri destUri, MoveParameters parameters);

    /// <summary>
    /// 异步创建共享或独占写锁，或刷新已有令牌对应的写锁。
    /// </summary>
    /// <remarks>LockToken 非 null 时使用空正文续期且不发送 Depth；新锁默认覆盖全部后代。锁租约与请求超时独立，服务器可能授予不同租期。 此重载使用该操作的默认请求参数。</remarks>
    /// <param name="requestUri">请求资源的标准 URI 引用，可为绝对地址或相对 BaseAddress 的已编码路径；普通文件名可先用 WebDavUri.EncodePath 编码。</param>
    /// <returns>异步锁操作任务；普通成功响应包含活动锁，集合 HTTP 207 响应保留逐资源状态。</returns>
    /// <exception cref="OperationCanceledException">调用方取消、客户端释放或请求超时使操作中止。</exception>
    /// <exception cref="HttpRequestException">发生网络连接或 HTTP 传输失败；普通 HTTP 错误状态通常保留在结果中。</exception>
    /// <exception cref="System.IO.InvalidDataException">成功 HTTP 状态不符合 DAV 协议，或响应缺少必需结构及确认信息。</exception>
    Task<LockResponse> Lock(string requestUri);

    /// <summary>
    /// 异步创建共享或独占写锁，或刷新已有令牌对应的写锁。
    /// </summary>
    /// <remarks>LockToken 非 null 时使用空正文续期且不发送 Depth；新锁默认覆盖全部后代。锁租约与请求超时独立，服务器可能授予不同租期。 此重载使用该操作的默认请求参数。</remarks>
    /// <param name="requestUri">请求资源的标准 URI 引用，可为绝对地址或相对 BaseAddress 的已编码路径；普通文件名可先用 WebDavUri.EncodePath 编码。</param>
    /// <returns>异步锁操作任务；普通成功响应包含活动锁，集合 HTTP 207 响应保留逐资源状态。</returns>
    /// <exception cref="OperationCanceledException">调用方取消、客户端释放或请求超时使操作中止。</exception>
    /// <exception cref="HttpRequestException">发生网络连接或 HTTP 传输失败；普通 HTTP 错误状态通常保留在结果中。</exception>
    /// <exception cref="System.IO.InvalidDataException">成功 HTTP 状态不符合 DAV 协议，或响应缺少必需结构及确认信息。</exception>
    Task<LockResponse> Lock(Uri requestUri);

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
    Task<LockResponse> Lock(string requestUri, LockParameters parameters);

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
    Task<LockResponse> Lock(Uri requestUri, LockParameters parameters);

    /// <summary>
    /// 异步释放指定资源上由锁令牌标识的写锁。
    /// </summary>
    /// <remarks>规范化尖括号后通过 Lock-Token 头提交令牌，不能使用 If 条件代替该解锁请求头。</remarks>
    /// <param name="requestUri">请求资源的标准 URI 引用，可为绝对地址或相对 BaseAddress 的已编码路径；普通文件名可先用 WebDavUri.EncodePath 编码。</param>
    /// <param name="lockToken">服务器提供的锁令牌 URI，可以带外层尖括号；发送时校验并统一格式。</param>
    /// <returns>异步解锁任务；结果保留服务器 HTTP 状态和响应头。</returns>
    /// <exception cref="OperationCanceledException">调用方取消、客户端释放或请求超时使操作中止。</exception>
    /// <exception cref="HttpRequestException">发生网络连接或 HTTP 传输失败；普通 HTTP 错误状态通常保留在结果中。</exception>
    Task<WebDavResponse> Unlock(string requestUri, string lockToken);

    /// <summary>
    /// 异步释放指定资源上由锁令牌标识的写锁。
    /// </summary>
    /// <remarks>规范化尖括号后通过 Lock-Token 头提交令牌，不能使用 If 条件代替该解锁请求头。</remarks>
    /// <param name="requestUri">请求资源的标准 URI 引用，可为绝对地址或相对 BaseAddress 的已编码路径；普通文件名可先用 WebDavUri.EncodePath 编码。</param>
    /// <param name="lockToken">服务器提供的锁令牌 URI，可以带外层尖括号；发送时校验并统一格式。</param>
    /// <returns>异步解锁任务；结果保留服务器 HTTP 状态和响应头。</returns>
    /// <exception cref="OperationCanceledException">调用方取消、客户端释放或请求超时使操作中止。</exception>
    /// <exception cref="HttpRequestException">发生网络连接或 HTTP 传输失败；普通 HTTP 错误状态通常保留在结果中。</exception>
    Task<WebDavResponse> Unlock(Uri requestUri, string lockToken);

    /// <summary>
    /// 异步释放指定资源上由锁令牌标识的写锁。
    /// </summary>
    /// <remarks>规范化尖括号后通过 Lock-Token 头提交令牌，不能使用 If 条件代替该解锁请求头。</remarks>
    /// <param name="requestUri">请求资源的标准 URI 引用，可为绝对地址或相对 BaseAddress 的已编码路径；普通文件名可先用 WebDavUri.EncodePath 编码。</param>
    /// <param name="parameters">本次 Unlock 操作的请求参数，包括通用 HTTP 条件、请求头及取消设置。 不应为 null。</param>
    /// <returns>异步解锁任务；结果保留服务器 HTTP 状态和响应头。</returns>
    /// <exception cref="OperationCanceledException">调用方取消、客户端释放或请求超时使操作中止。</exception>
    /// <exception cref="HttpRequestException">发生网络连接或 HTTP 传输失败；普通 HTTP 错误状态通常保留在结果中。</exception>
    Task<WebDavResponse> Unlock(string requestUri, UnlockParameters parameters);

    /// <summary>
    /// 异步释放指定资源上由锁令牌标识的写锁。
    /// </summary>
    /// <remarks>规范化尖括号后通过 Lock-Token 头提交令牌，不能使用 If 条件代替该解锁请求头。</remarks>
    /// <param name="requestUri">请求资源的标准 URI 引用，可为绝对地址或相对 BaseAddress 的已编码路径；普通文件名可先用 WebDavUri.EncodePath 编码。</param>
    /// <param name="parameters">本次 Unlock 操作的请求参数，包括通用 HTTP 条件、请求头及取消设置。 不应为 null。</param>
    /// <returns>异步解锁任务；结果保留服务器 HTTP 状态和响应头。</returns>
    /// <exception cref="OperationCanceledException">调用方取消、客户端释放或请求超时使操作中止。</exception>
    /// <exception cref="HttpRequestException">发生网络连接或 HTTP 传输失败；普通 HTTP 错误状态通常保留在结果中。</exception>
    Task<WebDavResponse> Unlock(Uri requestUri, UnlockParameters parameters);

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
    Task<PropfindResponse> Search(string requestUri, SearchParameters parameters);

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
    Task<PropfindResponse> Search(Uri requestUri, SearchParameters parameters);
}
