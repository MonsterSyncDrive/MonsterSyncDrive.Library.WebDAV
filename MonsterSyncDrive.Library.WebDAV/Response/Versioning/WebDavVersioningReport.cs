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
using MonsterSyncDrive.Library.WebDAV.Core.Versioning;
using System.Xml.Linq;

namespace MonsterSyncDrive.Library.WebDAV.Response.Versioning;

/// <summary>保存某个目标资源的一份 DeltaV 报告，适用于直接报告及带 Depth 的逐资源报告封装。</summary>
/// <remarks>Target 标识报告作用的资源；Resources 和 Items 标识报告内部的版本或查询结果，两种资源层次不可混淆。</remarks>
public sealed class WebDavVersioningReport : WebDavResponse
{
    /// <summary>获取报告作用的目标资源 URI，保留服务器 href 的相对或绝对形式。</summary>
    public string? Target
    {
        get;
    }
    /// <summary>获取原请求报告的完整名称，例如 DAV:version-tree 或 DAV:merge-preview。</summary>
    public XName ReportName
    {
        get;
    }
    /// <summary>获取 Depth 外层的资源和逐属性错误快照；直接报告没有该封装，值为 null。</summary>
    public WebDavResource? Resource
    {
        get;
    }
    /// <summary>获取此资源原始报告根，不含外层 multistatus；return=minimal 省略报告时为 null，失败报告可保留空属性元素。</summary>
    public XElement? Document
    {
        get;
    }
    /// <summary>获取报告是否按实际发送的 return=minimal 偏好被省略；此时 HTTP 状态可以为 200，但不能推断报告为空或资源没有变化。</summary>
    public bool WasOmittedByMinimalPreference
    {
        get;
    }
    /// <summary>获取服务器是否成功返回了可读取的原报告文档；与仅表示请求已处理的 HTTP 2xx 状态不同。</summary>
    public bool IsReportAvailable => base.IsSuccessful && !WasOmittedByMinimalPreference && Document is not null;
    /// <summary>获取报告内部 multistatus 的版本或查询结果资源，不包含 Depth 外层的目标资源。</summary>
    public IReadOnlyCollection<WebDavResource> Resources
    {
        get;
    }
    /// <summary>获取报告内部资源对应的版本信息。</summary>
    public IReadOnlyCollection<WebDavVersioningInfo> Items
    {
        get;
    }
    /// <summary>获取本份基线比较报告的新增、删除和变化版本。</summary>
    public IReadOnlyCollection<BaselineVersionChange> BaselineChanges => ReadBaselineChanges();
    /// <summary>获取本份合并预览报告的更新、冲突和忽略条目。</summary>
    public IReadOnlyCollection<VersionMergePreview> MergePreviews => ReadMergePreviews();
    /// <summary>获取本份 latest-activity-version 报告的版本 href；其他报告及失败报告返回 null。</summary>
    public string? LatestActivityVersion => IsSuccessful && Document?.Name == WebDavXml.Dav + "latest-activity-version-report"
        ? RequiredHref(Document) : null;
    /// <summary>获取可用报告及其内部资源状态是否成功；被 minimal 省略时为 false，内部可选版本属性的 404 不使整份报告失败。</summary>
    /// <remarks>WasOmittedByMinimalPreference 区分合法省略与协议失败；StatusCode 仍保留服务器的 200，调用方可按需求重新请求完整报告。</remarks>
    public override bool IsSuccessful => IsReportAvailable && Resources.All(resource => resource.StatusCode is null or (>= 200 and < 300));

    /// <summary>物化一个资源的报告及可选 Depth 封装上下文。</summary>
    /// <param name="target">报告目标 URI。</param>
    /// <param name="reportName">原请求报告的完整名称。</param>
    /// <param name="document">原始报告 XML 根或失败属性。</param>
    /// <param name="statusCode">报告属性或直接 HTTP 状态。</param>
    /// <param name="description">报告级状态说明。</param>
    /// <param name="resource">可选 Depth 外层资源。</param>
    /// <param name="error">HTTP、资源或 propstat 的 DAV:error。</param>
    /// <param name="wasOmittedByMinimalPreference">实际 return=minimal 导致全部报告属性被省略时为 true。</param>
    internal WebDavVersioningReport(string? target, XName reportName, XElement? document, int statusCode,
        string? description, WebDavResource? resource, WebDavError? error, bool wasOmittedByMinimalPreference = false) : base(statusCode, description)
    {
        Target = target;
        ReportName = reportName;
        Resource = resource;
        Error = error;
        WasOmittedByMinimalPreference = wasOmittedByMinimalPreference;
        Document = document is null ? null : new XElement(document);
        // 只解析原报告的直接 response。expand-property 中属性内的嵌套资源仍保留在 XML，
        // 不能把任意后代 response 全部扁平化，否则会丢失其属性路径和资源所属层次。
        Resources = statusCode is >= 200 and < 300 && Document?.Name == WebDavXml.Dav + "multistatus"
            ? Array.AsReadOnly(Document.Elements(WebDavXml.Dav + "response").Select(WebDavXml.ParseResource).ToArray())
            : [];
        Items = Array.AsReadOnly(Resources.Select(value => new WebDavVersioningInfo(value)).ToArray());
    }

    /// <summary>读取基线比较条目，校验变化类型对应的 href 数量并保留顺序。</summary>
    /// <returns>标准版本变化条目，未知扩展保留在 Document 中。</returns>
    private IReadOnlyCollection<BaselineVersionChange> ReadBaselineChanges()
    {
        if (!IsSuccessful || Document?.Name != WebDavXml.Dav + "compare-baseline-report")
            return [];
        List<BaselineVersionChange> result = [];
        foreach (XElement entry in Document.Elements())
        {
            if (entry.Name.Namespace != WebDavXml.Dav || entry.Name.LocalName is not ("added-version" or "deleted-version" or "changed-version"))
                continue;
            var hrefs = entry.Elements(WebDavXml.Dav + "href").Select(element => element.Value).ToArray();
            if (hrefs.Length != (entry.Name.LocalName == "changed-version" ? 2 : 1) || hrefs.Any(string.IsNullOrWhiteSpace))
                throw new InvalidDataException("Malformed compare-baseline entry.");
            result.Add(new BaselineVersionChange(entry.Name, Array.AsReadOnly(hrefs)));
        }
        return result.AsReadOnly();
    }

    /// <summary>读取合并预览并区分目标、源版本和冲突共同祖先。</summary>
    /// <returns>标准合并预览条目，不主动访问 href 或执行合并。</returns>
    private IReadOnlyCollection<VersionMergePreview> ReadMergePreviews()
    {
        if (!IsSuccessful || Document?.Name != WebDavXml.Dav + "merge-preview-report")
            return [];
        List<VersionMergePreview> result = [];
        foreach (XElement entry in Document.Elements())
        {
            if (entry.Name.Namespace != WebDavXml.Dav || entry.Name.LocalName is not ("update-preview" or "conflict-preview" or "ignore-preview"))
                continue;
            var target = entry.Name.LocalName == "ignore-preview" ? null : RequiredHref(entry.Element(WebDavXml.Dav + "target"));
            var version = RequiredHref(entry.Element(WebDavXml.Dav + "version"));
            var ancestor = entry.Name.LocalName == "conflict-preview" ? RequiredHref(entry.Element(WebDavXml.Dav + "common-ancestor")) : null;
            result.Add(new VersionMergePreview(entry.Name, target, version, ancestor));
        }
        return result.AsReadOnly();
    }

    /// <summary>读取协议必需且唯一的非空 href，拒绝损坏的成功报告。</summary>
    /// <param name="container">应直接包含 href 的标准元素。</param>
    /// <returns>服务器提供的 URI 引用原文。</returns>
    private static string RequiredHref(XElement? container)
    {
        XElement[]? values = container?.Elements(WebDavXml.Dav + "href").ToArray();
        return values is not { Length: 1 } || string.IsNullOrWhiteSpace(values[0].Value)
            ? throw new InvalidDataException("A DeltaV report requires exactly one href.")
            : values[0].Value;
    }
}

