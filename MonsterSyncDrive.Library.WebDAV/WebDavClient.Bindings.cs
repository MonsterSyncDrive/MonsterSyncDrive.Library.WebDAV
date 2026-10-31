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

/// <summary>实现 RFC5842 资源绑定，使同一资源能够通过多个 URI 访问。</summary>
public sealed partial class WebDavClient
{
    /// <summary>在指定集合创建到现有资源的绑定，不复制资源。</summary>
    /// <param name="collectionUri">接收新绑定的集合 URI。</param>
    /// <param name="parameters">源资源、绑定名称、覆盖与条件设置。</param>
    /// <returns>完整状态和绑定属性结果。</returns>
    public Task<WebDavBindingResponse> BindAsync(string collectionUri, BindingParameters parameters) =>
        ChangeBindingAsync(collectionUri, "BIND", "bind", parameters);

    /// <summary>将已有绑定移动到指定集合和名称，保留资源的持久 resource-id。</summary>
    /// <param name="collectionUri">接收绑定的目标集合 URI。</param>
    /// <param name="parameters">源绑定 URI、目标名称、覆盖与条件设置。</param>
    /// <returns>完整状态和绑定属性结果。</returns>
    public Task<WebDavBindingResponse> RebindAsync(string collectionUri, BindingParameters parameters) =>
        ChangeBindingAsync(collectionUri, "REBIND", "rebind", parameters);

    /// <summary>仅解除集合中的一个绑定；服务器按剩余绑定和协议决定资源生命周期。</summary>
    /// <param name="collectionUri">包含绑定的集合 URI。</param>
    /// <param name="segment">未转义的单个绑定名称。</param>
    /// <param name="options">锁条件、请求头和取消设置。</param>
    /// <returns>解除结果，包含可能的逐资源错误。</returns>
    public async Task<WebDavBindingResponse> UnbindAsync(string collectionUri, string segment, WebDavRequestOptions? options = null)
    {
        var body = new XElement(WebDavXml.Dav + "unbind", new XElement(WebDavXml.Dav + "segment", BindingParameters.EncodeSegment(segment)));
        var response = await SendDocumentRequestAsync(CreateUri(collectionUri), "UNBIND", body, options,
            [new("Cache-Control", "no-cache")]).ConfigureAwait(false);
        ValidateBindingDocument(response, "unbind-response", false);
        return new WebDavBindingResponse(response, true);
    }

    /// <summary>显式查询 resource-id 与 parent-set；深度无限时保留 208/508 而不递归重试。</summary>
    /// <param name="requestUri">待查询资源或集合 URI。</param>
    /// <param name="depth">资源、直接子项或全部后代的深度。</param>
    /// <param name="options">查询条件和取消设置。</param>
    /// <returns>带全部 URI 别名、资源身份、父绑定和循环标记的快照。</returns>
    public async Task<WebDavBindingResponse> GetBindingInfoAsync(string requestUri, ApplyTo.Propfind depth = ApplyTo.Propfind.ResourceOnly, WebDavRequestOptions? options = null)
    {
        var depthHeader = depth switch
        {
            ApplyTo.Propfind.ResourceOnly => "0", ApplyTo.Propfind.ResourceAndChildren => "1",
            ApplyTo.Propfind.ResourceAndDescendants => "infinity", _ => throw new ArgumentOutOfRangeException(nameof(depth))
        };
        var body = new XElement(WebDavXml.Dav + "propfind", VersioningXml.Prop(WebDavBindingInfo.PropertyNames));
        var response = await SendDocumentRequestAsync(CreateUri(requestUri), "PROPFIND", body, options,
            [new("Depth", depthHeader), new("DAV", "bind")]).ConfigureAwait(false);
        ValidateBindingDocument(response, "multistatus", true);
        return new WebDavBindingResponse(response, false);
    }

    /// <summary>共用 BIND/REBIND 的单段名称校验、源 URI 构建和覆盖语义。</summary>
    /// <param name="collectionUri">目标集合。</param>
    /// <param name="method">BIND 或 REBIND。</param>
    /// <param name="root">请求根名称。</param>
    /// <param name="parameters">强类型绑定设置。</param>
    /// <returns>已物化的绑定响应。</returns>
    private async Task<WebDavBindingResponse> ChangeBindingAsync(string collectionUri, string method, string root, BindingParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(parameters.Source);
        var body = VersioningXml.AddExtensions(new XElement(WebDavXml.Dav + root,
            new XElement(WebDavXml.Dav + "segment", BindingParameters.EncodeSegment(parameters.Segment)),
            VersioningXml.Href(parameters.Source)), parameters.ExtensionElements);
        var response = await SendDocumentRequestAsync(CreateUri(collectionUri), method, body, parameters,
            [new("Overwrite", parameters.Overwrite ? "T" : "F"), new("Cache-Control", "no-cache")]).ConfigureAwait(false);
        ValidateBindingDocument(response, root + "-response", false);
        return new WebDavBindingResponse(response, true);
    }

    /// <summary>校验成功文档形状，保留 HTTP 错误和合法多状态失败供调用者检查。</summary>
    /// <param name="response">HTTP 和 XML 结果。</param>
    /// <param name="expected">预期 DAV 根名。</param>
    /// <param name="requireMultiStatus">查询必须收到 207 多状态文档。</param>
    private static void ValidateBindingDocument(WebDavDocumentResponse response, string expected, bool requireMultiStatus)
    {
        if (response.StatusCode is < 200 or >= 300) return;
        if (requireMultiStatus && (response.StatusCode != 207 || response.Document?.Name != WebDavXml.Dav + "multistatus"))
            throw new InvalidDataException("Binding discovery requires DAV:multistatus.");
        if (response.Document is { } document && document.Name != WebDavXml.Dav + expected
            && !(response.StatusCode == 207 && document.Name == WebDavXml.Dav + "multistatus"))
            throw new InvalidDataException("Unexpected binding response document.");
    }
}

/// <summary>声明 RFC5842 绑定修改与发现操作。</summary>
public partial interface IWebDavClient
{
    /// <summary>在集合创建到现有资源的绑定。</summary>
    /// <param name="collectionUri">目标集合 URI。</param>
    /// <param name="parameters">源资源、绑定名称和请求条件。</param>
    /// <returns>绑定结果。</returns>
    Task<WebDavBindingResponse> BindAsync(string collectionUri, BindingParameters parameters);
    /// <summary>将源绑定移动到新的集合及名称。</summary>
    /// <param name="collectionUri">目标集合 URI。</param>
    /// <param name="parameters">源绑定 URI、目标名称和请求条件。</param>
    /// <returns>重绑定结果。</returns>
    Task<WebDavBindingResponse> RebindAsync(string collectionUri, BindingParameters parameters);
    /// <summary>解除集合中的一个绑定。</summary>
    /// <param name="collectionUri">包含绑定的集合 URI。</param>
    /// <param name="segment">未转义的绑定名称。</param>
    /// <param name="options">请求条件和取消设置。</param>
    /// <returns>解除结果。</returns>
    Task<WebDavBindingResponse> UnbindAsync(string collectionUri, string segment, WebDavRequestOptions? options = null);
    /// <summary>查询持久资源身份、父绑定和循环状态。</summary>
    /// <param name="requestUri">查询资源 URI。</param>
    /// <param name="depth">查询深度。</param>
    /// <param name="options">请求条件和取消设置。</param>
    /// <returns>绑定信息及 208/508 标记。</returns>
    Task<WebDavBindingResponse> GetBindingInfoAsync(string requestUri, ApplyTo.Propfind depth = ApplyTo.Propfind.ResourceOnly, WebDavRequestOptions? options = null);
}

