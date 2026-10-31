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

/// <summary>实现 RFC4437 引用资源的创建、修改和原资源属性发现。</summary>
public sealed partial class WebDavClient
{
    /// <summary>创建指定目标和生命周期的重定向引用资源。</summary>
    /// <param name="requestUri">新引用资源的 URI。</param>
    /// <param name="parameters">必需目标、可选生命周期及通用条件。</param>
    /// <returns>引用创建结果及完整响应 XML。</returns>
    public Task<WebDavRedirectReferenceResponse> CreateRedirectReferenceAsync(string requestUri, RedirectReferenceParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(parameters.Target);
        return ChangeRedirectReferenceAsync(requestUri, "MKREDIRECTREF", "mkredirectref", parameters);
    }

    /// <summary>修改引用目标或生命周期；未指定字段按 RFC4437 保持原值。</summary>
    /// <param name="requestUri">已有引用资源 URI。</param>
    /// <param name="parameters">要修改的目标或生命周期；两个字段均省略时为协议允许的空更新。</param>
    /// <returns>引用修改结果及完整响应 XML。</returns>
    public Task<WebDavRedirectReferenceResponse> UpdateRedirectReferenceAsync(string requestUri, RedirectReferenceParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        return ChangeRedirectReferenceAsync(requestUri, "UPDATEREDIRECTREF", "updateredirectref", parameters);
    }

    /// <summary>操作引用本身，查询 resourcetype、reftarget 和 redirect-lifetime。</summary>
    /// <param name="requestUri">引用资源 URI。</param>
    /// <param name="options">条件和取消设置；自动发送 Apply-To-Redirect-Ref:T 与 Depth:0。</param>
    /// <returns>引用定义及所有逐属性状态，不访问引用目标。</returns>
    public async Task<WebDavRedirectReferenceResponse> GetRedirectReferenceAsync(string requestUri, WebDavRequestOptions? options = null)
    {
        var body = new XElement(WebDavXml.Dav + "propfind", VersioningXml.Prop(WebDavRedirectReference.PropertyNames));
        var source = await SendDocumentRequestAsync(CreateUri(requestUri), "PROPFIND", body, options,
            [new("Apply-To-Redirect-Ref", "T"), new("Depth", "0")]).ConfigureAwait(false);
        if (source.StatusCode is >= 200 and < 300 && (source.StatusCode != 207 || source.Document?.Name != WebDavXml.Dav + "multistatus"))
            throw new InvalidDataException("Redirect reference discovery requires DAV:multistatus.");
        return new WebDavRedirectReferenceResponse(source, false);
    }

    /// <summary>按标准顺序构建目标和生命周期，默认修改引用本身并禁止缓存修改请求。</summary>
    /// <param name="requestUri">引用 URI。</param>
    /// <param name="method">创建或修改方法。</param>
    /// <param name="root">标准 XML 根名。</param>
    /// <param name="parameters">已验证的强类型设置。</param>
    /// <returns>完整引用结果。</returns>
    private async Task<WebDavRedirectReferenceResponse> ChangeRedirectReferenceAsync(string requestUri, string method, string root, RedirectReferenceParameters parameters)
    {
        XElement? lifetime = parameters.Lifetime switch
        {
            null => null,
            RedirectReferenceLifetime.Temporary => new XElement(WebDavXml.Dav + "temporary"),
            RedirectReferenceLifetime.Permanent => new XElement(WebDavXml.Dav + "permanent"),
            _ => throw new ArgumentOutOfRangeException(nameof(parameters), "Unknown redirect lifetime.")
        };
        var body = VersioningXml.AddExtensions(new XElement(WebDavXml.Dav + root,
            parameters.Target is null ? null : new XElement(WebDavXml.Dav + "reftarget", VersioningXml.Href(parameters.Target)),
            lifetime is null ? null : new XElement(WebDavXml.Dav + "redirect-lifetime", lifetime)), parameters.ExtensionElements);
        var source = await SendDocumentRequestAsync(CreateUri(requestUri), method, body, parameters,
            [new("Apply-To-Redirect-Ref", "T"), new("Cache-Control", "no-cache")]).ConfigureAwait(false);
        if (source.StatusCode is >= 200 and < 300 && source.Document is { } document
            && document.Name != WebDavXml.Dav + (root + "-response")
            && !(source.StatusCode == 207 && document.Name == WebDavXml.Dav + "multistatus"))
            throw new InvalidDataException("Unexpected redirect reference response document.");
        return new WebDavRedirectReferenceResponse(source, true);
    }
}

/// <summary>声明 RFC4437 引用创建、修改和发现操作。</summary>
public partial interface IWebDavClient
{
    /// <summary>创建引用资源，由服务器在普通访问时执行重定向。</summary>
    /// <param name="requestUri">新引用 URI。</param>
    /// <param name="parameters">目标、生命周期和通用条件。</param>
    /// <returns>创建结果。</returns>
    Task<WebDavRedirectReferenceResponse> CreateRedirectReferenceAsync(string requestUri, RedirectReferenceParameters parameters);
    /// <summary>修改引用资源目标或生命周期。</summary>
    /// <param name="requestUri">引用 URI。</param>
    /// <param name="parameters">需要修改的字段和通用条件。</param>
    /// <returns>修改结果。</returns>
    Task<WebDavRedirectReferenceResponse> UpdateRedirectReferenceAsync(string requestUri, RedirectReferenceParameters parameters);
    /// <summary>查询引用资源本身的标准属性。</summary>
    /// <param name="requestUri">引用 URI。</param>
    /// <param name="options">通用请求条件和取消设置。</param>
    /// <returns>结构化引用定义与原始属性状态。</returns>
    Task<WebDavRedirectReferenceResponse> GetRedirectReferenceAsync(string requestUri, WebDavRequestOptions? options = null);
}

