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

/// <summary>保存 DeltaV 方法和报告的完整文档、版本属性快照及逐资源错误。</summary>
public sealed class WebDavVersioningResponse : WebDavResponse
{
    /// <summary>保存是否将属性错误也视为修改失败；属性查询允许可选字段返回 404。</summary>
    private readonly bool _checkPropertyErrors;
    /// <summary>获取完整响应文档，包含 Depth 外层封装及所有未知扩展。</summary>
    public XElement? Document { get; }
    /// <summary>获取最外层资源结果；带 Depth 的 REPORT 在这里保存报告目标，不是报告内部的版本。</summary>
    public IReadOnlyCollection<WebDavResource> Resources { get; }
    /// <summary>获取按目标资源分组的原报告及内部版本；普通修改和属性查询为空集合。</summary>
    public IReadOnlyCollection<WebDavVersioningReport> Reports { get; }
    /// <summary>获取是否存在因实际 return=minimal 偏好而省略的报告；这些目标的结果不可推断为空或无变化。</summary>
    public bool HasOmittedReports => Reports.Any(report => report.WasOmittedByMinimalPreference);
    /// <summary>获取报告内部全部版本，或普通属性查询返回的版本信息；保留每项所属资源快照。</summary>
    /// <remarks>需要区分不同报告目标时使用 Reports；扁平集合不根据版本 href 去重。</remarks>
    public IReadOnlyCollection<WebDavVersioningInfo> Items { get; }
    /// <summary>获取服务器分配的版本或工作资源 Location；保留绝对或相对 URI 形式。</summary>
    public string? Location => GetHeader("Location");
    /// <summary>获取所有成功 compare-baseline 报告中的新增、删除和变化版本；分组可从 Reports 读取。</summary>
    public IReadOnlyCollection<BaselineVersionChange> BaselineChanges =>
        Array.AsReadOnly(Reports.SelectMany(report => report.BaselineChanges).ToArray());
    /// <summary>获取所有成功 merge-preview 报告中的更新、冲突和忽略条目；分组可从 Reports 读取。</summary>
    public IReadOnlyCollection<VersionMergePreview> MergePreviews =>
        Array.AsReadOnly(Reports.SelectMany(report => report.MergePreviews).ToArray());
    /// <summary>获取所有成功 latest-activity-version 报告的版本 href，按服务器报告顺序保存。</summary>
    public IReadOnlyCollection<string> LatestActivityVersions =>
        Array.AsReadOnly(Reports.Select(report => report.LatestActivityVersion).OfType<string>().ToArray());
    /// <summary>获取唯一的 latest-activity-version 结果；未返回、失败或多目标结果时为 null，多个结果使用 LatestActivityVersions。</summary>
    public string? LatestActivityVersion => LatestActivityVersions is { Count: 1 } values ? values.Single() : null;
    /// <summary>获取外层 HTTP、逐资源、可用报告及按需逐属性检查均成功的结果；任一报告被 minimal 省略时为 false。</summary>
    /// <remarks>合法的 minimal 省略不抛出解析异常，StatusCode 仍保留 207；HasOmittedReports 和各报告的 IsReportAvailable 区分数据不可用与真实错误。</remarks>
    public override bool IsSuccessful => base.IsSuccessful && Resources.All(resource =>
        (resource.StatusCode is null or >= 200 and < 300) && (!_checkPropertyErrors || resource.PropertyStatuses.All(status => status.IsSuccessful)))
        && Reports.All(report => report.IsSuccessful);

    /// <summary>从完整 XML 响应创建版本控制结果，同时区分 Depth 封装和报告自身的多状态。</summary>
    /// <param name="source">已经物化并释放网络资源的通用响应。</param>
    /// <param name="checkPropertyErrors">修改操作检查逐属性失败，属性查询允许可选字段失败。</param>
    /// <param name="reportName">原 REPORT 请求根名称；普通方法为 null。</param>
    /// <param name="expectedReportRoot">报告自身的标准响应根，可能也是 multistatus。</param>
    /// <param name="depthWrapped">最终请求存在 Depth 头时为 true，包括显式 Depth:0。</param>
    /// <param name="target">直接报告所作用的请求 URI。</param>
    internal WebDavVersioningResponse(WebDavDocumentResponse source, bool checkPropertyErrors = false,
        XName? reportName = null, XName? expectedReportRoot = null, bool depthWrapped = false, string? target = null)
        : base(source.StatusCode, source.Description)
    {
        Headers = source.Headers;
        Error = source.Error;
        Document = source.Document is null ? null : new XElement(source.Document);
        Resources = source.Resources;
        Reports = ReadReports(source, reportName, expectedReportRoot, depthWrapped, target);
        Items = Array.AsReadOnly((reportName is null
            ? Resources.Select(resource => new WebDavVersioningInfo(resource))
            : Reports.SelectMany(report => report.Items)).ToArray());
        _checkPropertyErrors = checkPropertyErrors;
    }

    /// <summary>读取原报告，按 RFC3253 §3.6 只在最终发送 Depth 时解开一层逐资源封装。</summary>
    /// <param name="source">外层 HTTP 和 XML 结果。</param>
    /// <param name="reportName">请求报告的完整名称。</param>
    /// <param name="expectedRoot">报告自身应有的响应根。</param>
    /// <param name="depthWrapped">最终请求是否包含 Depth。</param>
    /// <param name="target">没有外层 href 时使用的直接请求 URI。</param>
    /// <returns>按目标资源保存的报告和失败条目。</returns>
    private static IReadOnlyCollection<WebDavVersioningReport> ReadReports(WebDavDocumentResponse source,
        XName? reportName, XName? expectedRoot, bool depthWrapped, string? target)
    {
        if (reportName is null || source.StatusCode is < 200 or >= 300) return [];
        if (expectedRoot is null) throw new InvalidDataException("A DeltaV report response root is required.");
        if (!depthWrapped)
        {
            if (source.Document?.Name != expectedRoot)
                throw new InvalidDataException("Unexpected DeltaV report document.");
            return Array.AsReadOnly(new[] { new WebDavVersioningReport(target, reportName, source.Document,
                source.StatusCode, source.Description, null, source.Error) });
        }
        if (source.StatusCode != 207 || source.Document?.Name != WebDavXml.Dav + "multistatus")
            throw new InvalidDataException("A REPORT with a Depth header requires a 207 DAV:multistatus wrapper.");

        var result = new List<WebDavVersioningReport>();
        var outerResources = source.Resources.ToArray();
        var index = 0;
        foreach (var response in source.Document.Elements(WebDavXml.Dav + "response"))
        {
            var resource = outerResources[index++];
            var countBefore = result.Count;
            foreach (var propstat in response.Elements(WebDavXml.Dav + "propstat"))
            {
                var status = WebDavXml.ReadStatus(propstat.Element(WebDavXml.Dav + "status"));
                var prop = propstat.Element(WebDavXml.Dav + "prop")
                    ?? throw new InvalidDataException("A DeltaV report propstat requires DAV:prop.");
                var reportRoots = prop.Elements(expectedRoot).ToArray();
                // 原报告根作为一个报告属性出现，不能把任意 Descendants 都当作报告或版本。
                // 否则 expand-property 的嵌套资源和外层目标会被混在同一结果集中。
                foreach (var report in reportRoots)
                    result.Add(new WebDavVersioningReport(resource.Uri, reportName, report, status,
                        propstat.Element(WebDavXml.Dav + "responsedescription")?.Value, resource,
                        ReadReportError(propstat) ?? resource.Error));
                if (status is >= 200 and < 300 && reportRoots.Length == 0)
                {
                    // RFC8144 §2.1 只允许把所有 404 属性省略后替换成 200 + 空 prop。
                    // 缺失数据不等于一份内容为空的报告：保存专门标记，不合成虚假的报告根。
                    if (source.WasMinimalResponseRequested && status == 200 && !prop.HasElements && string.IsNullOrWhiteSpace(prop.Value))
                        result.Add(new WebDavVersioningReport(resource.Uri, reportName, null, status,
                            propstat.Element(WebDavXml.Dav + "responsedescription")?.Value, resource,
                            ReadReportError(propstat) ?? resource.Error, true));
                    else
                        throw new InvalidDataException("A successful Depth report propstat does not contain the requested report.");
                }
                if (status is < 200 or >= 300 && reportRoots.Length == 0)
                    result.Add(new WebDavVersioningReport(resource.Uri, reportName, null, status,
                        propstat.Element(WebDavXml.Dav + "responsedescription")?.Value, resource,
                        ReadReportError(propstat) ?? resource.Error));
            }
            if (result.Count == countBefore)
            {
                // RFC8144 的另一种合法替换是资源级 200。严格限定实际 minimal 请求，
                // 且不能将任意 2xx、损坏的 propstat 或未识别报告误当成可省略内容。
                if (source.WasMinimalResponseRequested && resource.StatusCode == 200
                    && !response.Elements(WebDavXml.Dav + "propstat").Any())
                {
                    result.Add(new WebDavVersioningReport(resource.Uri, reportName, null, 200,
                        resource.ResponseDescription, resource, resource.Error, true));
                    continue;
                }
                if (resource.StatusCode is null or >= 200 and < 300)
                    throw new InvalidDataException("A successful Depth report resource does not contain its report.");
                result.Add(new WebDavVersioningReport(resource.Uri, reportName, null, resource.StatusCode.Value,
                    resource.ResponseDescription, resource, resource.Error));
            }
        }
        if (result.Count == 0)
            throw new InvalidDataException("A Depth report must include a result for its target resource.");
        return result.AsReadOnly();
    }

    /// <summary>兼容 RFC4918 的直接 DAV:error 和 RFC3253 的 responsedescription/error。</summary>
    /// <param name="container">对应资源或 propstat，不跨越到其他嵌套资源。</param>
    /// <returns>结构化 DAV 错误，未提供时为 null。</returns>
    private static WebDavError? ReadReportError(XElement container) =>
        WebDavXml.ParseError(container.Element(WebDavXml.Dav + "error")
            ?? container.Element(WebDavXml.Dav + "responsedescription")?.Element(WebDavXml.Dav + "error"));
}

/// <summary>描述 compare-baseline 报告中的一项新增、删除或变化版本。</summary>
/// <param name="Kind">DAV 变化元素名，保留扩展类型。</param>
/// <param name="Versions">变化涉及的版本 URI，changed-version 通常为两个。</param>
public sealed record BaselineVersionChange(XName Kind, IReadOnlyCollection<string> Versions);

/// <summary>描述 merge-preview 报告中的更新、冲突或忽略条目。</summary>
/// <param name="Kind">报告条目类型的完整名称。</param>
/// <param name="Target">目标资源 href，无目标时为 null。</param>
/// <param name="Version">参与合并的版本 href。</param>
/// <param name="CommonAncestor">冲突的共同祖先版本 href，无共同祖先字段时为 null。</param>
public sealed record VersionMergePreview(XName Kind, string? Target, string? Version, string? CommonAncestor);
