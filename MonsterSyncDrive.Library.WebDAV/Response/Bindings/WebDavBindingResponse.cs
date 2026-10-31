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

/// <summary>保存绑定修改或发现结果，并显式暴露循环相关的 208 与 508 状态。</summary>
public sealed class WebDavBindingResponse : WebDavResponse
{
    /// <summary>保存是否检查修改操作的逐属性失败。</summary>
    private readonly bool _mutation;
    /// <summary>获取完整 XML 根及未知扩展。</summary>
    public XElement? Document { get; }
    /// <summary>获取全部资源条目，不丢弃具有相同 resource-id 的不同 URI。</summary>
    public IReadOnlyCollection<WebDavResource> Resources { get; }
    /// <summary>获取资源标识和父绑定的结构化快照。</summary>
    public IReadOnlyCollection<WebDavBindingInfo> Bindings { get; }
    /// <summary>获取是否出现 208 Already Reported，表示该资源后代已在本次响应其他位置报告。</summary>
    public bool HasAlreadyReported => Resources.Any(resource => resource.StatusCode == 208 || resource.PropertyStatuses.Any(status => status.StatusCode == 208));
    /// <summary>获取外层、逐资源或逐属性是否出现 508 Loop Detected。</summary>
    public bool HasLoopDetected => StatusCode == 508 || Resources.Any(resource => resource.StatusCode == 508 || resource.PropertyStatuses.Any(status => status.StatusCode == 508));
    /// <summary>获取响应和所有需要检查的内部结果是否成功；208 属于成功，508 属于失败。</summary>
    public override bool IsSuccessful => base.IsSuccessful && !HasLoopDetected && Resources.All(resource =>
        (resource.StatusCode is null or >= 200 and < 300) && (!_mutation || resource.PropertyStatuses.All(status => status.IsSuccessful)));

    /// <summary>物化绑定响应，不执行客户端递归或根据 parent-set 自动扩展网络请求。</summary>
    /// <param name="source">共享传输层返回的完整响应。</param>
    /// <param name="mutation">是否将逐属性修改错误视为失败。</param>
    internal WebDavBindingResponse(WebDavDocumentResponse source, bool mutation) : base(source.StatusCode, source.Description)
    {
        Headers = source.Headers;
        Error = source.Error;
        Document = source.Document is null ? null : new XElement(source.Document);
        Resources = source.Resources;
        // 同一资源的多个 URI 是 BIND 的目的。保留别名和 208 标记，既不重复遍历，也不擅自删去结果。
        Bindings = Array.AsReadOnly(Resources.Select(resource => new WebDavBindingInfo(resource)).ToArray());
        _mutation = mutation;
    }
}

