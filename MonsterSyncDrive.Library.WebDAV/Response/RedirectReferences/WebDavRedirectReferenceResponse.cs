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
using MonsterSyncDrive.Library.WebDAV.Core.RedirectReferences;
using System.Xml.Linq;

namespace MonsterSyncDrive.Library.WebDAV.Response.RedirectReferences;

/// <summary>保存引用创建、修改或发现的完整状态以及结构化引用属性。</summary>
public sealed class WebDavRedirectReferenceResponse : WebDavResponse
{
    /// <summary>保存是否将逐属性修改错误视为失败。</summary>
    private readonly bool _mutation;
    /// <summary>获取完整 XML 文档及未知扩展。</summary>
    public XElement? Document
    {
        get;
    }
    /// <summary>获取全部资源结果，包括逐资源和逐属性错误。</summary>
    public IReadOnlyCollection<WebDavResource> Resources
    {
        get;
    }
    /// <summary>获取结构化引用快照，原资源完整保留。</summary>
    public IReadOnlyCollection<WebDavRedirectReference> References
    {
        get;
    }
    /// <summary>获取返回的 Location，普通 3xx 错误不会被本模型当作成功。</summary>
    public string? Location => GetHeader("Location");
    /// <summary>获取 Redirect-Ref 头中引用创建时指定的目标 URI 原文；可能为相对引用，并非引用资源自身地址。</summary>
    public string? RedirectReferenceLocation => GetHeader("Redirect-Ref");
    /// <summary>获取 Redirect-Ref 头中的引用目标，与 RedirectReferenceLocation 相同，名称明确表达 RFC4437 的目标语义。</summary>
    public string? RedirectReferenceTarget => RedirectReferenceLocation;
    /// <summary>获取外层 HTTP 和需要检查的内部结果是否成功。</summary>
    public override bool IsSuccessful => base.IsSuccessful && Resources.All(resource =>
        (resource.StatusCode is null or (>= 200 and < 300)) && (!_mutation || resource.PropertyStatuses.All(status => status.IsSuccessful)));

    /// <summary>从共享传输层响应复制引用结果，无网络资源生命周期依赖。</summary>
    /// <param name="source">完整 HTTP/XML 响应。</param>
    /// <param name="mutation">修改操作是否检查全部逐属性结果。</param>
    internal WebDavRedirectReferenceResponse(WebDavDocumentResponse source, bool mutation) : base(source.StatusCode, source.Description)
    {
        Headers = source.Headers;
        Error = source.Error;
        Document = source.Document is null ? null : new XElement(source.Document);
        Resources = source.Resources;
        References = Array.AsReadOnly(Resources.Select(resource => new WebDavRedirectReference(resource)).ToArray());
        _mutation = mutation;
    }
}

