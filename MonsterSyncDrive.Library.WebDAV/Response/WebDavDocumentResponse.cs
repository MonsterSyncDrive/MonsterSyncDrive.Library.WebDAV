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

namespace MonsterSyncDrive.WebDAV.Response;

/// <summary>
/// 保存 DAV 扩展方法的完整 XML 文档、逐资源结果及可选的原始返回表示。
/// </summary>
/// <remarks>Document 保留报告根节点和未知扩展，避免同步令牌等信息在资源投影时丢失。网络响应在返回前已释放。</remarks>
public class WebDavDocumentResponse : WebDavMultiStatusResponse
{
    /// <summary>用于区分修改操作的逐属性失败与查询操作的可选属性失败。</summary>
    internal bool TreatPropertyErrorsAsFailure { get; set; }

    /// <summary>记录实际发送请求是否选择最小返回，用于校验允许省略属性的扩展响应。</summary>
    /// <remarks>在默认请求头与单次覆盖应用之后确定，不以服务器是否回显 Preference-Applied 为前提。</remarks>
    internal bool WasMinimalResponseRequested { get; set; }

    /// <summary>保存最终发送的 Depth 字段，供 REPORT 解释直接结果或按资源封装的结果。</summary>
    internal string? RequestedDepth { get; set; }

    /// <summary>获取完整响应 XML 根元素；空正文或非 XML 表示时为 null。</summary>
    public XElement? Document { get; }

    /// <summary>获取需要保留的原始返回表示字节；普通 XML 报告使用 Document，避免重复保存正文。</summary>
    /// <remarks>读取受客户端文档大小限制保护；大文件返回表示可使用流式写入接口或 SendAsync。</remarks>
    public byte[]? Representation { get; internal set; }

    /// <summary>获取 HTTP 响应状态与逐资源结果是否成功。</summary>
    /// <remarks>普通报告的可选属性失败不影响结果；修改操作可以启用逐属性失败检查。</remarks>
    public override bool IsSuccessful => StatusCode is >= 200 and < 300
        && Resources.All(resource => (resource.StatusCode is null or >= 200 and < 300)
            && (!TreatPropertyErrorsAsFailure || resource.PropertyStatuses.All(status => status.IsSuccessful)));

    /// <summary>从 HTTP 状态和完整文档构造扩展响应。</summary>
    /// <param name="statusCode">服务器返回的 HTTP 状态码。</param>
    /// <param name="description">可选 HTTP 状态说明。</param>
    /// <param name="document">完整 XML 根元素，非 XML 或空正文时为 null。</param>
    public WebDavDocumentResponse(int statusCode, string? description, XElement? document)
        : base(statusCode, description, ParseResources(document))
    {
        Document = document is null ? null : new XElement(document);
    }

    /// <summary>在保留完整根文档的同时物化 DAV:response 条目。</summary>
    /// <param name="document">可能为多状态文档的 XML 根。</param>
    /// <returns>多状态资源集合，其他文档根对应空集合。</returns>
    private static IReadOnlyCollection<WebDavResource> ParseResources(XElement? document) =>
        document?.Name == WebDavXml.Dav + "multistatus"
            ? document.Elements(WebDavXml.Dav + "response").Select(WebDavXml.ParseResource).ToArray()
            : [];
}

