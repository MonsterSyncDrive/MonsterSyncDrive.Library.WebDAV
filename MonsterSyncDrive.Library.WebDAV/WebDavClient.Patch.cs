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
using MonsterSyncDrive.WebDAV.Request;
using MonsterSyncDrive.WebDAV.Response;

namespace MonsterSyncDrive.WebDAV;

/// <summary>提供补丁能力发现、标准 PATCH、SabreDAV 局部写入及流式写操作表示。</summary>
public sealed partial class WebDavClient
{
    /// <summary>通过 OPTIONS 读取 PATCH 支持及 Accept-Patch 补丁媒体类型。</summary>
    /// <param name="requestUri">要查询的资源 URI 引用。</param>
    /// <param name="options">通用请求条件。</param>
    /// <returns>包含允许方法和补丁格式的能力响应。</returns>
    public Task<PatchCapabilitiesResponse> DiscoverPatchAsync(string requestUri, WebDavRequestOptions? options = null) =>
        DiscoverPatchAsync(CreateUri(requestUri), options);

    /// <summary>查询 URI 的 PATCH 方法及格式支持。</summary>
    /// <param name="requestUri">资源地址。</param>
    /// <param name="options">通用请求条件。</param>
    /// <returns>可查询具体补丁格式的能力结果。</returns>
    public async Task<PatchCapabilitiesResponse> DiscoverPatchAsync(Uri requestUri, WebDavRequestOptions? options = null) =>
        new(await OptionsAsync(requestUri, options).ConfigureAwait(false));

    /// <summary>发送指定补丁格式的流式 PATCH，保留成功或条件失败的返回表示。</summary>
    /// <param name="requestUri">目标资源 URI 引用。</param>
    /// <param name="source">补丁源流，从当前位置开始读取。</param>
    /// <param name="parameters">明确补丁媒体类型和并发条件的设置。</param>
    /// <returns>调用方需要释放的响应表示流。</returns>
    public Task<WebDavWriteResponse> PatchAsync(string requestUri, Stream source, PatchParameters parameters) =>
        PatchAsync(CreateUri(requestUri), source, parameters);

    /// <summary>流式发送 PATCH，默认保留补丁源流。</summary>
    /// <param name="requestUri">目标资源地址。</param>
    /// <param name="source">可读补丁源流。</param>
    /// <param name="parameters">补丁格式、锁、进度及版本条件。</param>
    /// <returns>保留资源表示及响应头的流式结果。</returns>
    public async Task<WebDavWriteResponse> PatchAsync(Uri requestUri, Stream source, PatchParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        using var content = new UploadContent(source, parameters);
        return await PatchAsync(requestUri, content, parameters).ConfigureAwait(false);
    }

    /// <summary>使用自定义补丁正文发送 PATCH。</summary>
    /// <param name="requestUri">目标 URI 引用。</param>
    /// <param name="content">操作接管并释放的补丁正文。</param>
    /// <param name="parameters">补丁格式和通用条件。</param>
    /// <returns>保留任意返回正文的流式结果。</returns>
    public Task<WebDavWriteResponse> PatchAsync(string requestUri, HttpContent content, PatchParameters parameters) =>
        PatchAsync(CreateUri(requestUri), content, parameters);

    /// <summary>发送任意服务器支持的补丁媒体类型，保留 412 等失败响应的表示。</summary>
    /// <param name="requestUri">资源地址。</param>
    /// <param name="content">由本次请求拥有的 HTTP 正文。</param>
    /// <param name="parameters">必须明确补丁格式的请求参数。</param>
    /// <returns>未自动判定 HTTP 成功的响应表示流。</returns>
    public Task<WebDavWriteResponse> PatchAsync(Uri requestUri, HttpContent content, PatchParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        return SendPatchingWriteAsync(requestUri, "PATCH", content, parameters);
    }

    /// <summary>使用 SabreDAV 私有补丁格式覆盖指定区间或追加文件内容。</summary>
    /// <param name="requestUri">资源 URI 引用。</param>
    /// <param name="source">补丁数据流，默认保留。</param>
    /// <param name="parameters">偏移、结束位置或追加模式及声明长度。</param>
    /// <returns>调用方拥有的流式响应。</returns>
    public Task<WebDavWriteResponse> SabreDavPartialUpdateAsync(string requestUri, Stream source, SabreDavPartialUpdateParameters parameters) =>
        SabreDavPartialUpdateAsync(CreateUri(requestUri), source, parameters);

    /// <summary>用固定长度的流式 SabreDAV PATCH 更新资源字节。</summary>
    /// <param name="requestUri">资源地址。</param>
    /// <param name="source">可读补丁源流；不可定位时必须提供 ContentLength。</param>
    /// <param name="parameters">局部写入、锁和版本条件。</param>
    /// <returns>保留任意返回表示的响应。</returns>
    public async Task<WebDavWriteResponse> SabreDavPartialUpdateAsync(Uri requestUri, Stream source, SabreDavPartialUpdateParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        using var content = new UploadContent(source, parameters);
        return await SabreDavPartialUpdateAsync(requestUri, content, parameters).ConfigureAwait(false);
    }

    /// <summary>使用自定义固定长度正文执行 SabreDAV 覆盖或追加。</summary>
    /// <param name="requestUri">资源 URI 引用。</param>
    /// <param name="content">由操作释放的正文。</param>
    /// <param name="parameters">区间与通用条件。</param>
    /// <returns>调用方需要关闭的响应表示。</returns>
    public Task<WebDavWriteResponse> SabreDavPartialUpdateAsync(string requestUri, HttpContent content, SabreDavPartialUpdateParameters parameters) =>
        SabreDavPartialUpdateAsync(CreateUri(requestUri), content, parameters);

    /// <summary>检查正文长度与包含式区间后，发送 SabreDAV 专用 PATCH。</summary>
    /// <param name="requestUri">目标资源地址。</param>
    /// <param name="content">具有已知或显式声明长度的正文。</param>
    /// <param name="parameters">覆盖、追加和条件设置。</param>
    /// <returns>未缓冲完整返回表示的响应。</returns>
    public async Task<WebDavWriteResponse> SabreDavPartialUpdateAsync(Uri requestUri, HttpContent content, SabreDavPartialUpdateParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(parameters);
        try
        {
            var knownLength = content.Headers.ContentLength;
            if (parameters.ContentLength is { } declared && (declared < 0 || knownLength is { } actual && declared != actual))
                throw new ArgumentException("Declared PATCH Content-Length differs from the available source length.");
            var length = parameters.ContentLength ?? knownLength
                ?? throw new ArgumentException("SabreDAV partial updates require an explicit Content-Length.");
            var range = parameters.CreateRange(length);
            content.Headers.ContentLength = length;
            // SabreDAV 必须使用自己的媒体类型；不能将它包装成可对任意 WebDAV 服务器通用的字节更新协议。
            return await SendPatchingWriteAsync(requestUri, "PATCH", content, parameters,
                [new("X-Update-Range", range)], new MediaTypeHeaderValue("application/x-sabredav-partialupdate")).ConfigureAwait(false);
        }
        catch { content.Dispose(); throw; }
    }

    /// <summary>以 PUT 上传文件，并流式保留服务器采用 return=representation 后返回的当前资源。</summary>
    /// <param name="requestUri">资源 URI 引用。</param>
    /// <param name="source">从当前位置开始读取的源流。</param>
    /// <param name="parameters">普通 PUT 参数，可设置 Prefer 和版本条件。</param>
    /// <returns>成功或 412 等条件失败时的当前资源表示流。</returns>
    public Task<WebDavWriteResponse> PutFileWithResponseAsync(string requestUri, Stream source, PutFileParameters? parameters = null) =>
        PutFileWithResponseAsync(CreateUri(requestUri), source, parameters);

    /// <summary>上传文件并保留不受文件大小限制的响应表示流。</summary>
    /// <param name="requestUri">资源地址。</param>
    /// <param name="source">可读源流，默认不关闭。</param>
    /// <param name="parameters">PUT 内容类型、所有权和条件。</param>
    /// <returns>需要调用方关闭的响应。</returns>
    public async Task<WebDavWriteResponse> PutFileWithResponseAsync(Uri requestUri, Stream source, PutFileParameters? parameters = null)
    {
        parameters ??= new PutFileParameters();
        using var content = new UploadContent(source, parameters);
        return await PutFileWithResponseAsync(requestUri, content, parameters).ConfigureAwait(false);
    }

    /// <summary>以自定义 PUT 正文上传资源并保留返回表示。</summary>
    /// <param name="requestUri">资源 URI 引用。</param>
    /// <param name="content">由请求释放的正文。</param>
    /// <param name="parameters">上传及 Prefer 设置。</param>
    /// <returns>响应状态、位置和表示流。</returns>
    public Task<WebDavWriteResponse> PutFileWithResponseAsync(string requestUri, HttpContent content, PutFileParameters? parameters = null) =>
        PutFileWithResponseAsync(CreateUri(requestUri), content, parameters);

    /// <summary>发送 PUT，保持返回正文的独立所有权及完整请求取消作用域。</summary>
    /// <param name="requestUri">资源地址。</param>
    /// <param name="content">操作接管的上传正文。</param>
    /// <param name="parameters">上传参数；null 使用默认。</param>
    /// <returns>不自动消费正文的流式结果。</returns>
    public Task<WebDavWriteResponse> PutFileWithResponseAsync(Uri requestUri, HttpContent content, PutFileParameters? parameters = null) =>
        SendPatchingWriteAsync(requestUri, "PUT", content, parameters ?? new PutFileParameters());

    /// <summary>发送有上传正文、但必须把返回表示移交给调用方的写操作。</summary>
    /// <param name="requestUri">目标资源地址。</param>
    /// <param name="method">PATCH 或 PUT 方法名称。</param>
    /// <param name="content">当前操作负责释放的上传正文。</param>
    /// <param name="parameters">上传、HTTP 条件和 Prefer 设置。</param>
    /// <param name="protocolHeaders">局部更新等协议要求的额外请求头。</param>
    /// <param name="forcedMediaType">需要强制使用的协议媒体类型；null 使用参数中的类型。</param>
    /// <returns>保留实际响应正文的写操作结果。</returns>
    private async Task<WebDavWriteResponse> SendPatchingWriteAsync(Uri requestUri, string method, HttpContent content,
        PutFileParameters parameters, IReadOnlyCollection<KeyValuePair<string, string>>? protocolHeaders = null,
        MediaTypeHeaderValue? forcedMediaType = null)
    {
        ArgumentNullException.ThrowIfNull(content);
        try
        {
            using var request = CreateRequest(requestUri, method, null, content);
            content.Headers.ContentType = forcedMediaType ?? parameters.ContentType;
            AddLock(request, parameters.LockToken);
            foreach (var header in protocolHeaders ?? []) request.Headers.Add(header.Key, header.Value);
            parameters.Apply(request);
            var response = await SendAsync(request, parameters.CancellationToken).ConfigureAwait(false);
            try
            {
                // 请求与上传正文可在返回前释放；响应正文拥有独立的 HTTP 生命周期，不能随此处的 using 关闭。
                var stream = await response.Content.ReadAsStreamAsync(parameters.CancellationToken).ConfigureAwait(false);
                return new WebDavWriteResponse(response, stream, request.RequestUri!);
            }
            catch { response.Dispose(); throw; }
        }
        catch { content.Dispose(); throw; }
    }
}

/// <summary>定义补丁更新、能力发现和流式返回资源表示的客户端扩展。</summary>
public partial interface IWebDavClient
{
    /// <summary>发现 PATCH 允许方法和补丁媒体类型。</summary>
    /// <param name="requestUri">资源 URI 引用。</param>
    /// <param name="options">请求条件。</param>
    /// <returns>补丁能力响应。</returns>
    Task<PatchCapabilitiesResponse> DiscoverPatchAsync(string requestUri, WebDavRequestOptions? options = null);
    /// <summary>发现 PATCH 能力。</summary>
    /// <param name="requestUri">资源 URI。</param>
    /// <param name="options">请求条件。</param>
    /// <returns>补丁能力响应。</returns>
    Task<PatchCapabilitiesResponse> DiscoverPatchAsync(Uri requestUri, WebDavRequestOptions? options = null);
    /// <summary>通过补丁源流更新资源。</summary>
    /// <param name="requestUri">资源 URI 引用。</param>
    /// <param name="source">补丁源流。</param>
    /// <param name="parameters">补丁格式和条件。</param>
    /// <returns>响应表示流。</returns>
    Task<WebDavWriteResponse> PatchAsync(string requestUri, Stream source, PatchParameters parameters);
    /// <summary>通过补丁源流更新资源。</summary>
    /// <param name="requestUri">资源 URI。</param>
    /// <param name="source">补丁源流。</param>
    /// <param name="parameters">补丁格式和条件。</param>
    /// <returns>响应表示流。</returns>
    Task<WebDavWriteResponse> PatchAsync(Uri requestUri, Stream source, PatchParameters parameters);
    /// <summary>使用自定义 HTTP 补丁正文更新资源。</summary>
    /// <param name="requestUri">资源 URI 引用。</param>
    /// <param name="content">由操作释放的正文。</param>
    /// <param name="parameters">补丁格式和条件。</param>
    /// <returns>响应表示流。</returns>
    Task<WebDavWriteResponse> PatchAsync(string requestUri, HttpContent content, PatchParameters parameters);
    /// <summary>使用自定义 HTTP 补丁正文更新资源。</summary>
    /// <param name="requestUri">资源 URI。</param>
    /// <param name="content">由操作释放的正文。</param>
    /// <param name="parameters">补丁格式和条件。</param>
    /// <returns>响应表示流。</returns>
    Task<WebDavWriteResponse> PatchAsync(Uri requestUri, HttpContent content, PatchParameters parameters);
    /// <summary>使用 SabreDAV 格式对指定区间覆盖或追加。</summary>
    /// <param name="requestUri">资源 URI 引用。</param>
    /// <param name="source">源流。</param>
    /// <param name="parameters">字节区间和条件。</param>
    /// <returns>响应表示流。</returns>
    Task<WebDavWriteResponse> SabreDavPartialUpdateAsync(string requestUri, Stream source, SabreDavPartialUpdateParameters parameters);
    /// <summary>使用 SabreDAV 格式覆盖或追加。</summary>
    /// <param name="requestUri">资源 URI。</param>
    /// <param name="source">源流。</param>
    /// <param name="parameters">字节区间和条件。</param>
    /// <returns>响应表示流。</returns>
    Task<WebDavWriteResponse> SabreDavPartialUpdateAsync(Uri requestUri, Stream source, SabreDavPartialUpdateParameters parameters);
    /// <summary>使用固定长度正文执行 SabreDAV 局部写入。</summary>
    /// <param name="requestUri">资源 URI 引用。</param>
    /// <param name="content">固定长度正文。</param>
    /// <param name="parameters">字节区间和条件。</param>
    /// <returns>响应表示流。</returns>
    Task<WebDavWriteResponse> SabreDavPartialUpdateAsync(string requestUri, HttpContent content, SabreDavPartialUpdateParameters parameters);
    /// <summary>使用固定长度正文执行 SabreDAV 局部写入。</summary>
    /// <param name="requestUri">资源 URI。</param>
    /// <param name="content">固定长度正文。</param>
    /// <param name="parameters">字节区间和条件。</param>
    /// <returns>响应表示流。</returns>
    Task<WebDavWriteResponse> SabreDavPartialUpdateAsync(Uri requestUri, HttpContent content, SabreDavPartialUpdateParameters parameters);
    /// <summary>上传文件并流式保留返回表示。</summary>
    /// <param name="requestUri">资源 URI 引用。</param>
    /// <param name="source">源流。</param>
    /// <param name="parameters">上传设置。</param>
    /// <returns>响应表示流。</returns>
    Task<WebDavWriteResponse> PutFileWithResponseAsync(string requestUri, Stream source, PutFileParameters? parameters = null);
    /// <summary>上传文件并流式保留返回表示。</summary>
    /// <param name="requestUri">资源 URI。</param>
    /// <param name="source">源流。</param>
    /// <param name="parameters">上传设置。</param>
    /// <returns>响应表示流。</returns>
    Task<WebDavWriteResponse> PutFileWithResponseAsync(Uri requestUri, Stream source, PutFileParameters? parameters = null);
    /// <summary>通过 HTTP 正文上传并保留返回表示。</summary>
    /// <param name="requestUri">资源 URI 引用。</param>
    /// <param name="content">上传正文。</param>
    /// <param name="parameters">上传设置。</param>
    /// <returns>响应表示流。</returns>
    Task<WebDavWriteResponse> PutFileWithResponseAsync(string requestUri, HttpContent content, PutFileParameters? parameters = null);
    /// <summary>通过 HTTP 正文上传并保留返回表示。</summary>
    /// <param name="requestUri">资源 URI。</param>
    /// <param name="content">上传正文。</param>
    /// <param name="parameters">上传设置。</param>
    /// <returns>响应表示流。</returns>
    Task<WebDavWriteResponse> PutFileWithResponseAsync(Uri requestUri, HttpContent content, PutFileParameters? parameters = null);
}
