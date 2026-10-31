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
using MonsterSyncDrive.Library.WebDAV.Core.Versioning;
using System.Xml.Linq;

namespace MonsterSyncDrive.Library.WebDAV.Request.Versioning;

/// <summary>指定支持深度操作的 DeltaV 请求作用范围。</summary>
public enum VersioningDepth
{
    /// <summary>只作用于请求资源，对应 Depth: 0。</summary>
    ResourceOnly,
    /// <summary>作用于请求资源和直接子资源，对应 Depth: 1。</summary>
    ResourceAndChildren,
    /// <summary>作用于请求资源和全部后代，对应 Depth: infinity。</summary>
    ResourceAndDescendants
}

/// <summary>保存版本操作的通用 HTTP 选项、标签选择器和扩展正文节点。</summary>
public class VersioningRequestOptions : WebDavRequestOptions
{
    /// <summary>获取或设置支持深度的操作范围；null 时省略 Depth。</summary>
    public VersioningDepth? Depth
    {
        get; set;
    }
    /// <summary>获取或设置未编码的版本标签；发送 Label 请求头时按 RFC3253 做 UTF-8 URI 编码，null 时省略。</summary>
    public string? LabelSelector
    {
        get; set;
    }
    /// <summary>获取或设置要附加到标准正文中的扩展元素；发送时复制完整 XML。</summary>
    public IReadOnlyCollection<XElement> ExtensionElements { get; set; } = [];
}

/// <summary>配置 VERSION-CONTROL 初始化版本控制，或以已有版本初始化版本受控资源。</summary>
public sealed class VersionControlParameters : VersioningRequestOptions
{
    /// <summary>获取或设置已有版本 URI；null 时由服务器创建初始版本及版本历史。</summary>
    public Uri? Version
    {
        get; set;
    }
}

/// <summary>配置原地检出或创建工作资源，并可关联活动或请求未保留检出。</summary>
public sealed class CheckoutParameters : VersioningRequestOptions
{
    /// <summary>获取或设置是否允许服务器创建版本分支。</summary>
    public bool ForkOk
    {
        get; set;
    }
    /// <summary>获取或设置对版本受控资源的选定版本执行检出，而非默认原地检出。</summary>
    public bool ApplyToVersion
    {
        get; set;
    }
    /// <summary>获取或设置是否请求未保留检出，允许并行开发的服务器语义。</summary>
    public bool Unreserved
    {
        get; set;
    }
    /// <summary>获取或设置关联的活动 URI；与 CreateActivity 互斥。</summary>
    public IReadOnlyCollection<Uri> Activities { get; set; } = [];
    /// <summary>获取或设置是否要求服务器创建新活动；与 Activities 非空互斥。</summary>
    public bool CreateActivity
    {
        get; set;
    }

    /// <summary>构造检出正文，验证活动选择的互斥约束。</summary>
    /// <returns>完整 DAV:checkout 根元素。</returns>
    internal XElement ToXml()
    {
        if (CreateActivity && Activities.Count != 0)
            throw new ArgumentException("An activity-set cannot contain both new and href.");
        XElement root = new(WebDavXml.Dav + "checkout");
        if (ForkOk)
            root.Add(new XElement(WebDavXml.Dav + "fork-ok"));
        if (ApplyToVersion)
            root.Add(new XElement(WebDavXml.Dav + "apply-to-version"));
        if (Unreserved)
            root.Add(new XElement(WebDavXml.Dav + "unreserved"));
        if (CreateActivity)
            root.Add(new XElement(WebDavXml.Dav + "activity-set", new XElement(WebDavXml.Dav + "new")));
        else if (Activities.Count != 0)
            root.Add(VersioningXml.Hrefs("activity-set", Activities));
        return VersioningXml.AddExtensions(root, ExtensionElements);
    }
}

/// <summary>配置检入后是否保持检出状态，以及是否允许分支。</summary>
public sealed class CheckinParameters : VersioningRequestOptions
{
    /// <summary>获取或设置检入完成后继续保持版本受控资源为检出状态。</summary>
    public bool KeepCheckedOut
    {
        get; set;
    }
    /// <summary>获取或设置是否接受可能产生的版本分支。</summary>
    public bool ForkOk
    {
        get; set;
    }
}

/// <summary>指定标签关联的修改动作。</summary>
public enum VersionLabelAction
{
    /// <summary>设置标签，允许按服务器规则移除原有选择并重新绑定。</summary>
    Set,
    /// <summary>新增标签；同一历史已有冲突标签时由服务器拒绝。</summary>
    Add,
    /// <summary>移除该版本的指定标签。</summary>
    Remove
}

/// <summary>配置一个版本标签的增、改或删除，并保留标签原始大小写。</summary>
public sealed class LabelParameters : VersioningRequestOptions
{
    /// <summary>获取或设置标签名称；必须非空，内容由 XML 序列化器转义。</summary>
    public string LabelName { get; set; } = "";
    /// <summary>获取或设置标签动作，默认 Set。</summary>
    public VersionLabelAction Action
    {
        get; set;
    }
}

/// <summary>配置 UPDATE 使用版本 URI 或标签更新版本受控资源。</summary>
public sealed class UpdateVersionParameters : VersioningRequestOptions
{
    /// <summary>获取或设置要更新到的版本 URI；与 LabelName 互斥。</summary>
    public Uri? Version
    {
        get; set;
    }
    /// <summary>获取或设置选择版本的标签；与 Version 互斥。</summary>
    public string? LabelName
    {
        get; set;
    }
    /// <summary>获取或设置修改结果需要返回的属性名称。</summary>
    public IReadOnlyCollection<XName> Properties { get; set; } = [];
}

/// <summary>配置 MERGE 的源、自动合并及检出行为，支持活动合并。</summary>
public sealed class MergeParameters : VersioningRequestOptions
{
    /// <summary>获取或设置一个或多个合并源，可以是版本、工作区或服务器支持的活动 URI。</summary>
    public IReadOnlyCollection<Uri> Sources { get; set; } = [];
    /// <summary>获取或设置禁止服务器自动合并内容，要求显式冲突处理。</summary>
    public bool NoAutoMerge
    {
        get; set;
    }
    /// <summary>获取或设置禁止自动检出需要修改的目标。</summary>
    public bool NoCheckout
    {
        get; set;
    }
    /// <summary>获取或设置合并前先检入源活动；仅支持活动的服务器可执行。</summary>
    public bool CheckinActivity
    {
        get; set;
    }
    /// <summary>获取或设置合并时的检出参数，可指定 fork、活动及未保留检出。</summary>
    public CheckoutParameters? Checkout
    {
        get; set;
    }
    /// <summary>获取或设置修改资源需要返回的属性名称。</summary>
    public IReadOnlyCollection<XName> Properties { get; set; } = [];
}

/// <summary>配置基线控制；未指定基线时由服务器捕获当前配置状态。</summary>
public sealed class BaselineControlParameters : VersioningRequestOptions
{
    /// <summary>获取或设置用于初始化配置的已有基线 URI。</summary>
    public Uri? Baseline
    {
        get; set;
    }
}

/// <summary>描述 expand-property 报告的一项属性及其递归展开路径。</summary>
public sealed class ExpandedVersionProperty
{
    /// <summary>获取需要请求或展开的完整属性名称。</summary>
    public XName Name
    {
        get;
    }
    /// <summary>获取该属性 href 指向资源上继续展开的属性集合。</summary>
    public IReadOnlyCollection<ExpandedVersionProperty> Children
    {
        get;
    }

    /// <summary>创建带可选递归子选择器的属性选择。</summary>
    /// <param name="name">包含命名空间和本地名称的属性名。</param>
    /// <param name="children">需要在所引用资源上继续请求的属性。</param>
    public ExpandedVersionProperty(XName name, params ExpandedVersionProperty[] children)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(children);
        Name = name;
        Children = Array.AsReadOnly(children.ToArray());
    }

    /// <summary>递归生成 property 选择器；属性名称与命名空间通过 XML 特性表达。</summary>
    /// <returns>完整 DAV:property 元素。</returns>
    internal XElement ToXml()
    {
        return new(WebDavXml.Dav + "property",
        new XAttribute("name", Name.LocalName), new XAttribute("namespace", Name.NamespaceName),
        Children.Select(child => child.ToXml()));
    }
}

