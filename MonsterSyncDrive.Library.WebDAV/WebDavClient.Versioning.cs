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
using MonsterSyncDrive.Library.WebDAV.Core.Versioning;
using MonsterSyncDrive.Library.WebDAV.Request;
using MonsterSyncDrive.Library.WebDAV.Request.Versioning;
using MonsterSyncDrive.Library.WebDAV.Response.Versioning;
using System.Xml.Linq;

namespace MonsterSyncDrive.WebDAV;

/// <summary>提供 RFC3253 的版本控制方法、能力发现和标准报告。</summary>
public sealed partial class WebDavClient
{
    /// <summary>启用版本控制，或以已有版本初始化版本受控资源。</summary>
    /// <param name="requestUri">版本受控资源 URI。</param>
    /// <param name="parameters">可选版本选择及通用条件。</param>
    /// <returns>完整版本响应，保留 XML、响应头与逐资源状态。</returns>
    public Task<WebDavVersioningResponse> VersionControlAsync(string requestUri, VersionControlParameters? parameters = null)
    {
        parameters ??= new VersionControlParameters();
        // §6.7 的 version 包装层用于选择既有历史中的版本；直接把 href 放在根下不符合该扩展的结构。
        XElement? body = parameters.Version is null && parameters.ExtensionElements.Count == 0 ? null
            : VersioningXml.AddExtensions(new XElement(WebDavXml.Dav + "version-control",
                parameters.Version is null ? null : new XElement(WebDavXml.Dav + "version", VersioningXml.Href(parameters.Version))), parameters.ExtensionElements);
        return SendVersioningRequestAsync(requestUri, "VERSION-CONTROL", body, parameters, "version-control-response", true);
    }

    /// <summary>检出版本受控资源或创建工作资源，并支持活动和未保留检出。</summary>
    /// <param name="requestUri">资源或版本 URI，工作资源位置从响应 Location 读取。</param>
    /// <param name="parameters">检出、分支、活动及通用条件。</param>
    /// <returns>完整版本响应，保留 XML、响应头与逐资源状态。</returns>
    public Task<WebDavVersioningResponse> CheckoutAsync(string requestUri, CheckoutParameters? parameters = null)
    {
        parameters ??= new CheckoutParameters();
        return SendVersioningRequestAsync(requestUri, "CHECKOUT", parameters.ToXml(), parameters, "checkout-response", true);
    }

    /// <summary>检入当前修改生成新版本，可选择继续保持检出。</summary>
    /// <param name="requestUri">当前已检出的资源或工作资源 URI。</param>
    /// <param name="parameters">保持检出、允许分支及通用条件。</param>
    /// <returns>完整版本响应，保留 XML、响应头与逐资源状态。</returns>
    public Task<WebDavVersioningResponse> CheckinAsync(string requestUri, CheckinParameters? parameters = null)
    {
        parameters ??= new CheckinParameters();
        XElement body = new(WebDavXml.Dav + "checkin",
            parameters.KeepCheckedOut ? new XElement(WebDavXml.Dav + "keep-checked-out") : null,
            parameters.ForkOk ? new XElement(WebDavXml.Dav + "fork-ok") : null);
        return SendVersioningRequestAsync(requestUri, "CHECKIN",
            VersioningXml.AddExtensions(body, parameters.ExtensionElements), parameters, "checkin-response", true);
    }

    /// <summary>撤销检出，恢复服务器定义的检入状态，不自动提交当前修改。</summary>
    /// <param name="requestUri">要撤销检出的资源 URI。</param>
    /// <param name="options">锁、版本条件和取消选项。</param>
    /// <returns>完整版本响应，保留 XML、响应头与逐资源状态。</returns>
    public Task<WebDavVersioningResponse> UncheckoutAsync(string requestUri, VersioningRequestOptions? options = null)
    {
        options ??= new VersioningRequestOptions();
        return SendVersioningRequestAsync(requestUri, "UNCHECKOUT", VersioningOptionalBody("uncheckout", options), options, "uncheckout-response", true);
    }

    /// <summary>设置、新增或移除大小写敏感版本标签。</summary>
    /// <param name="requestUri">版本或版本受控资源 URI。</param>
    /// <param name="parameters">标签名称、动作、深度和条件。</param>
    /// <returns>完整版本响应，保留 XML、响应头与逐资源状态。</returns>
    public Task<WebDavVersioningResponse> LabelAsync(string requestUri, LabelParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentException.ThrowIfNullOrEmpty(parameters.LabelName);
        string action = parameters.Action switch
        {
            VersionLabelAction.Set => "set",
            VersionLabelAction.Add => "add",
            VersionLabelAction.Remove => "remove",
            _ => throw new ArgumentOutOfRangeException(nameof(parameters))
        };
        XElement body = new(WebDavXml.Dav + "label", new XElement(WebDavXml.Dav + action,
            new XElement(WebDavXml.Dav + "label-name", parameters.LabelName)));
        return SendVersioningRequestAsync(requestUri, "LABEL", VersioningXml.AddExtensions(body, parameters.ExtensionElements),
            parameters, "label-response", true);
    }

    /// <summary>创建服务器工作区，保留扩展响应和新资源状态。</summary>
    /// <param name="requestUri">工作区创建地址，应位于服务器声明的工作区集合内。</param>
    /// <param name="options">通用条件及可选扩展正文。</param>
    /// <returns>完整版本响应，保留 XML、响应头与逐资源状态。</returns>
    public Task<WebDavVersioningResponse> MakeWorkspaceAsync(string requestUri, VersioningRequestOptions? options = null)
    {
        options ??= new VersioningRequestOptions();
        return SendVersioningRequestAsync(requestUri, "MKWORKSPACE", VersioningOptionalBody("mkworkspace", options),
            options, "mkworkspace-response", true);
    }

    /// <summary>创建用于组织多个资源修改的服务器活动。</summary>
    /// <param name="requestUri">新活动地址，应位于服务器声明的活动集合内。</param>
    /// <param name="options">通用条件及可选扩展正文。</param>
    /// <returns>完整版本响应，保留 XML、响应头与逐资源状态。</returns>
    public Task<WebDavVersioningResponse> MakeActivityAsync(string requestUri, VersioningRequestOptions? options = null)
    {
        options ??= new VersioningRequestOptions();
        return SendVersioningRequestAsync(requestUri, "MKACTIVITY", VersioningOptionalBody("mkactivity", options),
            options, "mkactivity-response", true);
    }

    /// <summary>将集合置于基线控制，或使用已有基线初始化配置。</summary>
    /// <param name="requestUri">要纳入基线控制的集合 URI。</param>
    /// <param name="parameters">已有基线或默认新建基线的设置。</param>
    /// <returns>完整版本响应，保留 XML、响应头与逐资源状态。</returns>
    public Task<WebDavVersioningResponse> BaselineControlAsync(string requestUri, BaselineControlParameters? parameters = null)
    {
        parameters ??= new BaselineControlParameters();
        // 按 §12.6 的规范正文和 DTD 使用 baseline/href。该节示例省略 baseline 包装层，
        // 与同节的显式结构定义冲突，客户端以规范性定义为准。
        XElement? body = parameters.Baseline is null && parameters.ExtensionElements.Count == 0 ? null
            : VersioningXml.AddExtensions(new XElement(WebDavXml.Dav + "baseline-control",
                parameters.Baseline is null ? null : new XElement(WebDavXml.Dav + "baseline", VersioningXml.Href(parameters.Baseline))),
                parameters.ExtensionElements);
        return SendVersioningRequestAsync(requestUri, "BASELINE-CONTROL", body, parameters, "baseline-control-response", true);
    }

    /// <summary>根据指定版本或标签更新当前资源，返回被修改资源的多状态结果。</summary>
    /// <param name="requestUri">待更新的版本受控资源或集合 URI。</param>
    /// <param name="parameters">互斥的版本或标签选择，以及返回属性和深度。</param>
    /// <returns>完整版本响应，保留 XML、响应头与逐资源状态。</returns>
    public Task<WebDavVersioningResponse> UpdateVersionAsync(string requestUri, UpdateVersionParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        if (parameters.Version is not null && parameters.LabelName is not null)
            throw new ArgumentException("UPDATE version and label-name are mutually exclusive.", nameof(parameters));
        if (parameters.LabelName is not null)
            ArgumentException.ThrowIfNullOrEmpty(parameters.LabelName);
        XElement body = new(WebDavXml.Dav + "update",
            parameters.Version is null ? null : new XElement(WebDavXml.Dav + "version", VersioningXml.Href(parameters.Version)),
            parameters.LabelName is null ? null : new XElement(WebDavXml.Dav + "label-name", parameters.LabelName),
            parameters.Properties.Count == 0 ? null : VersioningXml.Prop(parameters.Properties));
        return SendVersioningRequestAsync(requestUri, "UPDATE", VersioningXml.AddExtensions(body, parameters.ExtensionElements),
            parameters, "multistatus", true);
    }

    /// <summary>将版本、配置或活动合并到目标资源，并保留需要客户端处理的合并结果。</summary>
    /// <param name="requestUri">合并目标资源或工作区 URI。</param>
    /// <param name="parameters">合并源、禁止自动行为、检出设置及返回属性。</param>
    /// <returns>完整版本响应，保留 XML、响应头与逐资源状态。</returns>
    public Task<WebDavVersioningResponse> MergeAsync(string requestUri, MergeParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        if (parameters.Sources.Count == 0)
            throw new ArgumentException("MERGE requires at least one source.", nameof(parameters));
        XElement body = new(WebDavXml.Dav + "merge", VersioningXml.Hrefs("source", parameters.Sources),
            parameters.NoAutoMerge ? new XElement(WebDavXml.Dav + "no-auto-merge") : null,
            parameters.NoCheckout ? new XElement(WebDavXml.Dav + "no-checkout") : null,
            parameters.CheckinActivity ? new XElement(WebDavXml.Dav + "checkin-activity") : null,
            parameters.Properties.Count == 0 ? null : VersioningXml.Prop(parameters.Properties));
        // RFC3253 允许 MERGE 复用 CHECKOUT 的合法子元素；先复制子元素，避免把 checkout 外层根误嵌入 merge。
        if (parameters.Checkout is not null)
            body.Add(parameters.Checkout.ToXml().Elements().Select(element => new XElement(element)));
        return SendVersioningRequestAsync(requestUri, "MERGE", VersioningXml.AddExtensions(body, parameters.ExtensionElements),
            parameters, "multistatus", true);
    }

    /// <summary>显式查询版本控制、工作区、基线、活动及支持能力属性。</summary>
    /// <param name="requestUri">资源、版本、版本历史、活动或集合 URI。</param>
    /// <param name="options">标签选择和通用查询条件。</param>
    /// <returns>完整版本响应，保留 XML、响应头与逐资源状态。</returns>
    public Task<WebDavVersioningResponse> GetVersioningInfoAsync(string requestUri, VersioningRequestOptions? options = null)
    {
        options ??= new VersioningRequestOptions();
        XElement body = new(WebDavXml.Dav + "propfind", VersioningXml.Prop(WebDavVersioningInfo.PropertyNames));
        return SendVersioningRequestAsync(requestUri, "PROPFIND", body, options, "multistatus", false, true);
    }

    /// <summary>通过 version-tree 报告读取版本历史与前驱、后继、标签等属性。</summary>
    /// <param name="requestUri">版本或版本受控资源 URI，不是厂商私有版本目录。</param>
    /// <param name="properties">需要返回的完整属性名，null 请求常用版本属性。</param>
    /// <param name="options">深度、标签选择和取消条件。</param>
    /// <returns>完整版本响应，保留 XML、响应头与逐资源状态。</returns>
    public Task<WebDavVersioningResponse> GetVersionHistoryAsync(string requestUri, IReadOnlyCollection<XName>? properties = null, VersioningRequestOptions? options = null)
    {
        XElement body = new(WebDavXml.Dav + "version-tree", VersioningXml.Prop(properties ?? WebDavVersioningInfo.PropertyNames));
        return SendVersioningRequestAsync(requestUri, "REPORT", body, options, "multistatus", false);
    }

    /// <summary>通过 expand-property 递归展开 href 属性，保留内嵌资源结果的完整 XML。</summary>
    /// <param name="requestUri">支持 REPORT 的资源 URI。</param>
    /// <param name="properties">递归属性选择器，不能为空引用。</param>
    /// <param name="options">通用报告条件。</param>
    /// <returns>完整版本响应，保留 XML、响应头与逐资源状态。</returns>
    public Task<WebDavVersioningResponse> ExpandVersionPropertiesAsync(string requestUri, IReadOnlyCollection<ExpandedVersionProperty> properties, VersioningRequestOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(properties);
        XElement body = new(WebDavXml.Dav + "expand-property", properties.Select(property => property.ToXml()));
        return SendVersioningRequestAsync(requestUri, "REPORT", body, options, "multistatus", false);
    }

    /// <summary>在集合中查找属于指定版本历史的版本受控资源。</summary>
    /// <param name="requestUri">查找起始集合 URI。</param>
    /// <param name="histories">至少一个版本历史 URI。</param>
    /// <param name="properties">结果需要返回的属性，null 使用常用版本属性。</param>
    /// <param name="options">深度与通用报告条件。</param>
    /// <returns>完整版本响应，保留 XML、响应头与逐资源状态。</returns>
    public Task<WebDavVersioningResponse> LocateVersionsByHistoryAsync(string requestUri, IReadOnlyCollection<Uri> histories, IReadOnlyCollection<XName>? properties = null, VersioningRequestOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(histories);
        if (histories.Count == 0)
            throw new ArgumentException("At least one version history is required.", nameof(histories));
        XElement body = new(WebDavXml.Dav + "locate-by-history", VersioningXml.Hrefs("version-history-set", histories),
            VersioningXml.Prop(properties ?? WebDavVersioningInfo.PropertyNames));
        return SendVersioningRequestAsync(requestUri, "REPORT", body, options, "multistatus", false);
    }

    /// <summary>预览合并，取得更新、冲突和忽略的源版本，不修改服务器内容。</summary>
    /// <param name="requestUri">合并目标资源 URI。</param>
    /// <param name="source">一个待预览的合并源 URI。</param>
    /// <param name="options">通用报告条件。</param>
    /// <returns>完整版本响应，保留 XML、响应头与逐资源状态。</returns>
    public Task<WebDavVersioningResponse> PreviewMergeAsync(string requestUri, Uri source, VersioningRequestOptions? options = null)
    {
        XElement body = new(WebDavXml.Dav + "merge-preview", new XElement(WebDavXml.Dav + "source", VersioningXml.Href(source)));
        return SendVersioningRequestAsync(requestUri, "REPORT", body, options, "merge-preview-report", false);
    }

    /// <summary>比较两个基线，报告新增、删除和版本变化。</summary>
    /// <param name="requestUri">基准基线 URI。</param>
    /// <param name="compareBaseline">被比较的基线 URI。</param>
    /// <param name="options">通用报告条件。</param>
    /// <returns>完整版本响应，保留 XML、响应头与逐资源状态。</returns>
    public Task<WebDavVersioningResponse> CompareBaselinesAsync(string requestUri, Uri compareBaseline, VersioningRequestOptions? options = null)
    {
        XElement body = new(WebDavXml.Dav + "compare-baseline", VersioningXml.Href(compareBaseline));
        return SendVersioningRequestAsync(requestUri, "REPORT", body, options, "compare-baseline-report", false);
    }

    /// <summary>查询某活动在指定版本历史中创建的最新版本。</summary>
    /// <param name="requestUri">版本历史 URI。</param>
    /// <param name="activity">待查询的活动 URI。</param>
    /// <param name="options">通用报告条件。</param>
    /// <returns>完整版本响应，保留 XML、响应头与逐资源状态。</returns>
    public Task<WebDavVersioningResponse> GetLatestActivityVersionAsync(string requestUri, Uri activity, VersioningRequestOptions? options = null)
    {
        XElement body = new(WebDavXml.Dav + "latest-activity-version", VersioningXml.Href(activity));
        return SendVersioningRequestAsync(requestUri, "REPORT", body, options, "latest-activity-version-report", false);
    }

    /// <summary>查询 DeltaV DAV 功能标识，以及版本历史、工作区和活动可创建位置。</summary>
    /// <param name="requestUri">支持版本控制的服务器资源 URI。</param>
    /// <param name="options">通用 HTTP 条件和取消令牌。</param>
    /// <returns>包含能力标识及创建集合位置的异步结果。</returns>
    public async Task<WebDavVersioningCapabilities> GetVersioningCapabilitiesAsync(string requestUri, WebDavRequestOptions? options = null)
    {
        XElement body = new(WebDavXml.Dav + "options", new XElement(WebDavXml.Dav + "version-history-collection-set"),
            new XElement(WebDavXml.Dav + "workspace-collection-set"), new XElement(WebDavXml.Dav + "activity-collection-set"));
        var response = await SendDocumentRequestAsync(CreateUri(requestUri), "OPTIONS", body, options).ConfigureAwait(false);
        return new WebDavVersioningCapabilities(response);
    }

    /// <summary>发送 DeltaV 请求，区分普通方法可选 XML 正文和报告必须具备的标准根元素。</summary>
    /// <param name="requestUri">操作目标。</param>
    /// <param name="method">DAV 方法名称。</param>
    /// <param name="body">可选 XML 正文。</param>
    /// <param name="options">条件、深度及扩展选项。</param>
    /// <param name="expectedRoot">成功 XML 的 DAV 根名称。</param>
    /// <param name="mutation">是否检查逐属性修改失败。</param>
    /// <param name="forceDepthZero">属性发现始终只查询当前资源。</param>
    /// <returns>保留完整文档和版本模型的结果。</returns>
    private async Task<WebDavVersioningResponse> SendVersioningRequestAsync(string requestUri, string method, XElement? body,
        VersioningRequestOptions? options, string expectedRoot, bool mutation, bool forceDepthZero = false)
    {
        options ??= new VersioningRequestOptions();
        List<KeyValuePair<string, string>> headers = [new("Cache-Control", "no-cache")];
        if (forceDepthZero)
            headers.Add(new("Depth", "0"));
        else if (options.Depth is { } depth)
            headers.Add(new("Depth", depth switch
            {
                VersioningDepth.ResourceOnly => "0",
                VersioningDepth.ResourceAndChildren => "1",
                VersioningDepth.ResourceAndDescendants => "infinity",
                _ => throw new ArgumentOutOfRangeException(nameof(options))
            }));
        // Label 是 HTTP 请求头中的 URI 转义 UTF-8 字符串；XML label-name 则保留原始文本。
        // 自定义 Headers 最后覆盖，允许调用方提交自己已编码的字段，避免再编码一次。
        if (options.LabelSelector is not null)
            headers.Add(new("Label", Uri.EscapeDataString(options.LabelSelector)));
        using var request = CreateRequest(CreateUri(requestUri), method, null,
            body is null ? null : XmlContent(body));
        foreach (var header in headers)
            request.Headers.Add(header.Key, header.Value);
        options.Apply(request);
        // HttpClient 默认头在发送时才合并。按相同整字段覆盖规则预览最终头，
        // 可以在产生检出副作用前发现 Label 与 apply-to-version 的标准互斥条件。
        string? effectiveDepth = _dispatcher.GetEffectiveHeaderValue(request, "Depth");
        string? effectiveLabel = _dispatcher.GetEffectiveHeaderValue(request, "Label");
        if (method == "CHECKOUT" && effectiveLabel is not null && body?.Element(WebDavXml.Dav + "apply-to-version") is not null)
            throw new ArgumentException("CHECKOUT cannot contain both a Label header and DAV:apply-to-version.", nameof(options));
        var preference = GetRequestPreferences(request).Return;
        using var message = await SendAsync(request, options.CancellationToken).ConfigureAwait(false);
        var response = await ReadDocumentResponseAsync(message, options.CancellationToken,
            preference == WebDavReturnPreference.Representation).ConfigureAwait(false);
        response.WasMinimalResponseRequested = preference == WebDavReturnPreference.Minimal;
        response.RequestedDepth = effectiveDepth;
        if (response.StatusCode is >= 200 and < 300)
        {
            if (method == "REPORT" && effectiveDepth is not null)
            {
                // 显式 Depth:0 也会改变封装：外层207保存每个目标，prop 内才是该目标原报告。
                if (response.StatusCode != 207 || response.Document?.Name != WebDavXml.Dav + "multistatus")
                    throw new InvalidDataException("A REPORT with a Depth header requires a 207 DAV:multistatus wrapper.");
            }
            else
            {
                // UPDATE/MERGE 和自身为多状态的报告仍必须真正返回207，不能接受200的伪成功。
                if (expectedRoot == "multistatus" && (response.StatusCode != 207 || response.Document?.Name != WebDavXml.Dav + expectedRoot))
                    throw new InvalidDataException(method + " did not return DAV:multistatus.");
                if (method == "REPORT" && response.Document?.Name != WebDavXml.Dav + expectedRoot)
                    throw new InvalidDataException("Unexpected DeltaV REPORT document.");
                if (response.Document is { } document && document.Name != WebDavXml.Dav + expectedRoot
                    && !(response.StatusCode == 207 && document.Name == WebDavXml.Dav + "multistatus"))
                    throw new InvalidDataException("Unexpected DeltaV method response document.");
            }
        }
        return new WebDavVersioningResponse(response, mutation, method == "REPORT" ? body?.Name : null,
            method == "REPORT" ? WebDavXml.Dav + expectedRoot : null, method == "REPORT" && effectiveDepth is not null,
            request.RequestUri?.AbsoluteUri);
    }

    /// <summary>仅存在扩展元素时为可省略正文的方法创建正确的 DAV 根。</summary>
    /// <param name="name">正文根本地名。</param>
    /// <param name="options">带扩展节点的选项。</param>
    /// <returns>扩展正文，没有扩展时为 null。</returns>
    private static XElement? VersioningOptionalBody(string name, VersioningRequestOptions options)
    {
        return options.ExtensionElements.Count == 0 ? null : VersioningXml.AddExtensions(new XElement(WebDavXml.Dav + name), options.ExtensionElements);
    }
}

/// <summary>声明 DeltaV 版本控制、工作区、基线、活动和报告客户端操作。</summary>
public partial interface IWebDavClient
{
    /// <summary>启用版本控制，或以已有版本初始化版本受控资源。</summary>
    /// <param name="requestUri">版本受控资源 URI。</param>
    /// <param name="parameters">可选版本选择及通用条件。</param>
    /// <returns>完整版本响应，保留 XML、响应头与逐资源状态。</returns>
    Task<WebDavVersioningResponse> VersionControlAsync(string requestUri, VersionControlParameters? parameters = null);

    /// <summary>检出版本受控资源或创建工作资源，并支持活动和未保留检出。</summary>
    /// <param name="requestUri">资源或版本 URI，工作资源位置从响应 Location 读取。</param>
    /// <param name="parameters">检出、分支、活动及通用条件。</param>
    /// <returns>完整版本响应，保留 XML、响应头与逐资源状态。</returns>
    Task<WebDavVersioningResponse> CheckoutAsync(string requestUri, CheckoutParameters? parameters = null);

    /// <summary>检入当前修改生成新版本，可选择继续保持检出。</summary>
    /// <param name="requestUri">当前已检出的资源或工作资源 URI。</param>
    /// <param name="parameters">保持检出、允许分支及通用条件。</param>
    /// <returns>完整版本响应，保留 XML、响应头与逐资源状态。</returns>
    Task<WebDavVersioningResponse> CheckinAsync(string requestUri, CheckinParameters? parameters = null);

    /// <summary>撤销检出，恢复服务器定义的检入状态，不自动提交当前修改。</summary>
    /// <param name="requestUri">要撤销检出的资源 URI。</param>
    /// <param name="options">锁、版本条件和取消选项。</param>
    /// <returns>完整版本响应，保留 XML、响应头与逐资源状态。</returns>
    Task<WebDavVersioningResponse> UncheckoutAsync(string requestUri, VersioningRequestOptions? options = null);

    /// <summary>设置、新增或移除大小写敏感版本标签。</summary>
    /// <param name="requestUri">版本或版本受控资源 URI。</param>
    /// <param name="parameters">标签名称、动作、深度和条件。</param>
    /// <returns>完整版本响应，保留 XML、响应头与逐资源状态。</returns>
    Task<WebDavVersioningResponse> LabelAsync(string requestUri, LabelParameters parameters);

    /// <summary>创建服务器工作区，保留扩展响应和新资源状态。</summary>
    /// <param name="requestUri">工作区创建地址，应位于服务器声明的工作区集合内。</param>
    /// <param name="options">通用条件及可选扩展正文。</param>
    /// <returns>完整版本响应，保留 XML、响应头与逐资源状态。</returns>
    Task<WebDavVersioningResponse> MakeWorkspaceAsync(string requestUri, VersioningRequestOptions? options = null);

    /// <summary>创建用于组织多个资源修改的服务器活动。</summary>
    /// <param name="requestUri">新活动地址，应位于服务器声明的活动集合内。</param>
    /// <param name="options">通用条件及可选扩展正文。</param>
    /// <returns>完整版本响应，保留 XML、响应头与逐资源状态。</returns>
    Task<WebDavVersioningResponse> MakeActivityAsync(string requestUri, VersioningRequestOptions? options = null);

    /// <summary>将集合置于基线控制，或使用已有基线初始化配置。</summary>
    /// <param name="requestUri">要纳入基线控制的集合 URI。</param>
    /// <param name="parameters">已有基线或默认新建基线的设置。</param>
    /// <returns>完整版本响应，保留 XML、响应头与逐资源状态。</returns>
    Task<WebDavVersioningResponse> BaselineControlAsync(string requestUri, BaselineControlParameters? parameters = null);

    /// <summary>根据指定版本或标签更新当前资源，返回被修改资源的多状态结果。</summary>
    /// <param name="requestUri">待更新的版本受控资源或集合 URI。</param>
    /// <param name="parameters">互斥的版本或标签选择，以及返回属性和深度。</param>
    /// <returns>完整版本响应，保留 XML、响应头与逐资源状态。</returns>
    Task<WebDavVersioningResponse> UpdateVersionAsync(string requestUri, UpdateVersionParameters parameters);

    /// <summary>将版本、配置或活动合并到目标资源，并保留需要客户端处理的合并结果。</summary>
    /// <param name="requestUri">合并目标资源或工作区 URI。</param>
    /// <param name="parameters">合并源、禁止自动行为、检出设置及返回属性。</param>
    /// <returns>完整版本响应，保留 XML、响应头与逐资源状态。</returns>
    Task<WebDavVersioningResponse> MergeAsync(string requestUri, MergeParameters parameters);

    /// <summary>显式查询版本控制、工作区、基线、活动及支持能力属性。</summary>
    /// <param name="requestUri">资源、版本、版本历史、活动或集合 URI。</param>
    /// <param name="options">标签选择和通用查询条件。</param>
    /// <returns>完整版本响应，保留 XML、响应头与逐资源状态。</returns>
    Task<WebDavVersioningResponse> GetVersioningInfoAsync(string requestUri, VersioningRequestOptions? options = null);

    /// <summary>通过 version-tree 报告读取版本历史与前驱、后继、标签等属性。</summary>
    /// <param name="requestUri">版本或版本受控资源 URI，不是厂商私有版本目录。</param>
    /// <param name="properties">需要返回的完整属性名，null 请求常用版本属性。</param>
    /// <param name="options">深度、标签选择和取消条件。</param>
    /// <returns>完整版本响应，保留 XML、响应头与逐资源状态。</returns>
    Task<WebDavVersioningResponse> GetVersionHistoryAsync(string requestUri, IReadOnlyCollection<XName>? properties = null, VersioningRequestOptions? options = null);

    /// <summary>通过 expand-property 递归展开 href 属性，保留内嵌资源结果的完整 XML。</summary>
    /// <param name="requestUri">支持 REPORT 的资源 URI。</param>
    /// <param name="properties">递归属性选择器，不能为空引用。</param>
    /// <param name="options">通用报告条件。</param>
    /// <returns>完整版本响应，保留 XML、响应头与逐资源状态。</returns>
    Task<WebDavVersioningResponse> ExpandVersionPropertiesAsync(string requestUri, IReadOnlyCollection<ExpandedVersionProperty> properties, VersioningRequestOptions? options = null);

    /// <summary>在集合中查找属于指定版本历史的版本受控资源。</summary>
    /// <param name="requestUri">查找起始集合 URI。</param>
    /// <param name="histories">至少一个版本历史 URI。</param>
    /// <param name="properties">结果需要返回的属性，null 使用常用版本属性。</param>
    /// <param name="options">深度与通用报告条件。</param>
    /// <returns>完整版本响应，保留 XML、响应头与逐资源状态。</returns>
    Task<WebDavVersioningResponse> LocateVersionsByHistoryAsync(string requestUri, IReadOnlyCollection<Uri> histories, IReadOnlyCollection<XName>? properties = null, VersioningRequestOptions? options = null);

    /// <summary>预览合并，取得更新、冲突和忽略的源版本，不修改服务器内容。</summary>
    /// <param name="requestUri">合并目标资源 URI。</param>
    /// <param name="source">一个待预览的合并源 URI。</param>
    /// <param name="options">通用报告条件。</param>
    /// <returns>完整版本响应，保留 XML、响应头与逐资源状态。</returns>
    Task<WebDavVersioningResponse> PreviewMergeAsync(string requestUri, Uri source, VersioningRequestOptions? options = null);

    /// <summary>比较两个基线，报告新增、删除和版本变化。</summary>
    /// <param name="requestUri">基准基线 URI。</param>
    /// <param name="compareBaseline">被比较的基线 URI。</param>
    /// <param name="options">通用报告条件。</param>
    /// <returns>完整版本响应，保留 XML、响应头与逐资源状态。</returns>
    Task<WebDavVersioningResponse> CompareBaselinesAsync(string requestUri, Uri compareBaseline, VersioningRequestOptions? options = null);

    /// <summary>查询某活动在指定版本历史中创建的最新版本。</summary>
    /// <param name="requestUri">版本历史 URI。</param>
    /// <param name="activity">待查询的活动 URI。</param>
    /// <param name="options">通用报告条件。</param>
    /// <returns>完整版本响应，保留 XML、响应头与逐资源状态。</returns>
    Task<WebDavVersioningResponse> GetLatestActivityVersionAsync(string requestUri, Uri activity, VersioningRequestOptions? options = null);

    /// <summary>查询 DeltaV DAV 功能标识，以及版本历史、工作区和活动可创建位置。</summary>
    /// <param name="requestUri">支持版本控制的服务器资源 URI。</param>
    /// <param name="options">通用 HTTP 条件和取消令牌。</param>
    /// <returns>包含能力标识及创建集合位置的异步结果。</returns>
    Task<WebDavVersioningCapabilities> GetVersioningCapabilitiesAsync(string requestUri, WebDavRequestOptions? options = null);

}

