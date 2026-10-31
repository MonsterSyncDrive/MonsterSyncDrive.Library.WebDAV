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

namespace MonsterSyncDrive.WebDAV.Core;

/// <summary>解析 RFC3253 的版本、工作资源、版本历史、工作区、活动和基线属性快照。</summary>
/// <remarks>只读取成功 propstat 的属性；完整原始属性仍可从 Resource 访问，不把缺失属性推断为服务器不支持该能力。</remarks>
public sealed class WebDavVersioningInfo
{
    /// <summary>获取包含所有原始成功属性及失败属性状态的资源快照。</summary>
    public WebDavResource Resource { get; }

    /// <summary>从资源快照读取版本控制信息。</summary>
    /// <param name="resource">由 PROPFIND 或版本报告返回的资源。</param>
    public WebDavVersioningInfo(WebDavResource resource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        Resource = resource;
    }

    /// <summary>获取版本显示名称，不同于版本资源 URI。</summary>
    public string? VersionName => VersioningXml.Property(Resource, "version-name")?.Value;

    /// <summary>获取创建版本者的显示名称。</summary>
    public string? CreatorDisplayName => VersioningXml.Property(Resource, "creator-displayname")?.Value;

    /// <summary>获取服务器保存的版本说明。</summary>
    public string? Comment => VersioningXml.Property(Resource, "comment")?.Value;

    /// <summary>获取当前检入版本的 href；未成功返回该属性时为 null。</summary>
    public string? CheckedIn => SingleHref("checked-in");

    /// <summary>获取当前检出所基于版本的 href。</summary>
    public string? CheckedOut => SingleHref("checked-out");

    /// <summary>获取所属版本历史资源的 href。</summary>
    public string? VersionHistory => SingleHref("version-history");

    /// <summary>获取版本历史根版本的 href。</summary>
    public string? RootVersion => SingleHref("root-version");

    /// <summary>获取所属工作区资源的 href。</summary>
    public string? Workspace => SingleHref("workspace");

    /// <summary>获取集合对应的版本受控配置资源 href。</summary>
    public string? VersionControlledConfiguration => SingleHref("version-controlled-configuration");

    /// <summary>获取基线捕获的集合 href。</summary>
    public string? BaselineCollection => SingleHref("baseline-collection");

    /// <summary>获取版本受控配置直接控制的集合 href。</summary>
    public string? BaselineControlledCollection => SingleHref("baseline-controlled-collection");

    /// <summary>获取工作区中当前已检出资源的 href 集合。</summary>
    public IReadOnlyCollection<string> WorkspaceCheckouts => VersioningXml.ReadHrefs(Resource, "workspace-checkout-set");

    /// <summary>获取工作区内所有受基线控制集合的 href 集合。</summary>
    public IReadOnlyCollection<string> BaselineControlledCollections => VersioningXml.ReadHrefs(Resource, "baseline-controlled-collection-set");

    /// <summary>获取当前基线直接引用的子基线 href；不递归加载，避免重复网络请求。</summary>
    public IReadOnlyCollection<string> Subbaselines => VersioningXml.ReadHrefs(Resource, "subbaseline-set");

    /// <summary>获取当前活动选择的各版本历史最新版本 href。</summary>
    public IReadOnlyCollection<string> ActivityVersions => VersioningXml.ReadHrefs(Resource, "activity-version-set");

    /// <summary>获取当前活动及其子活动关联的已检出资源 href。</summary>
    public IReadOnlyCollection<string> ActivityCheckouts => VersioningXml.ReadHrefs(Resource, "activity-checkout-set");

    /// <summary>获取当前活动包含的直接子活动 href。</summary>
    public IReadOnlyCollection<string> Subactivities => VersioningXml.ReadHrefs(Resource, "subactivity-set");

    /// <summary>获取当前工作区默认用于后续检出的活动 href。</summary>
    public IReadOnlyCollection<string> CurrentActivities => VersioningXml.ReadHrefs(Resource, "current-activity-set");

    /// <summary>获取当前活动正在使用的工作区 href。</summary>
    public IReadOnlyCollection<string> CurrentWorkspaces => VersioningXml.ReadHrefs(Resource, "current-workspace-set");

    /// <summary>获取服务器声明的未保留检出状态；缺失时为 null，损坏的 XML 布尔值被拒绝。</summary>
    public bool? Unreserved
    {
        get
        {
            var value = VersioningXml.Property(Resource, "unreserved")?.Value.Trim();
            return value switch
            {
                null => null, "true" or "1" => true, "false" or "0" => false,
                _ => throw new InvalidDataException("DAV:unreserved must be an XML boolean.")
            };
        }
    }

    /// <summary>获取直接前驱版本 href 集合。</summary>
    public IReadOnlyCollection<string> Predecessors => VersioningXml.ReadHrefs(Resource, "predecessor-set");

    /// <summary>获取直接后继版本 href 集合。</summary>
    public IReadOnlyCollection<string> Successors => VersioningXml.ReadHrefs(Resource, "successor-set");

    /// <summary>获取版本历史中的版本资源 href 集合。</summary>
    public IReadOnlyCollection<string> Versions => VersioningXml.ReadHrefs(Resource, "version-set");

    /// <summary>获取基于该版本检出的工作或受控资源 href 集合。</summary>
    public IReadOnlyCollection<string> CheckedOutResources => VersioningXml.ReadHrefs(Resource, "checkout-set");

    /// <summary>获取关联活动 href 集合。</summary>
    public IReadOnlyCollection<string> Activities => VersioningXml.ReadHrefs(Resource, "activity-set");

    /// <summary>获取尚需客户端确认合并的版本 href 集合。</summary>
    public IReadOnlyCollection<string> MergeSources => VersioningXml.ReadHrefs(Resource, "merge-set");

    /// <summary>获取服务器已自动合并但尚待确认的版本 href 集合。</summary>
    public IReadOnlyCollection<string> AutomaticallyMergedVersions => VersioningXml.ReadHrefs(Resource, "auto-merge-set");

    /// <summary>获取当前版本关联的大小写敏感标签。</summary>
    public IReadOnlyCollection<string> Labels => VersioningXml.Property(Resource, "label-name-set")?.Elements(WebDavXml.Dav + "label-name").Select(element => element.Value).ToArray() ?? [];

    /// <summary>获取自动版本策略的元素名称，保留未知扩展策略。</summary>
    public XName? AutoVersion => VersioningXml.Property(Resource, "auto-version")?.Elements().SingleOrDefault()?.Name;

    /// <summary>获取DAV:resourcetype 中全部完整类型名称。</summary>
    public IReadOnlyCollection<XName> ResourceTypes => VersioningXml.Property(Resource, "resourcetype")?.Elements().Select(element => element.Name).ToArray() ?? [];

    /// <summary>获取supported-method-set 中声明的方法名称。</summary>
    public IReadOnlyCollection<string> SupportedMethods => VersioningXml.Property(Resource, "supported-method-set")?.Elements(WebDavXml.Dav + "supported-method").Select(element => element.Attribute("name")?.Value).OfType<string>().ToArray() ?? [];

    /// <summary>获取supported-report-set 中声明的完整报告名称。</summary>
    public IReadOnlyCollection<XName> SupportedReports => VersioningXml.Property(Resource, "supported-report-set")?.Elements(WebDavXml.Dav + "supported-report").Elements(WebDavXml.Dav + "report").SelectMany(element => element.Elements()).Select(element => element.Name).ToArray() ?? [];

    /// <summary>获取常用版本控制属性名称，供属性发现和版本历史报告显式请求。</summary>
    public static IReadOnlyCollection<XName> PropertyNames { get; } = Array.AsReadOnly(new[]
    {
        "resourcetype", "version-name", "creator-displayname", "comment", "checked-in", "checked-out",
        "version-history", "root-version", "version-set", "predecessor-set", "successor-set", "checkout-set",
        "label-name-set", "workspace", "workspace-checkout-set", "version-controlled-configuration",
        "baseline-collection", "baseline-controlled-collection", "baseline-controlled-collection-set", "subbaseline-set",
        "activity-set", "activity-checkout-set", "activity-version-set", "subactivity-set", "current-activity-set", "current-workspace-set",
        "merge-set", "auto-merge-set", "auto-version", "supported-method-set", "supported-report-set",
        "supported-live-property-set", "workspace-collection-set", "activity-collection-set", "unreserved"
    }.Select(name => WebDavXml.Dav + name).ToArray());

    /// <summary>解析单 href 属性；存在但结构错误时报告协议错误，而非静默忽略。</summary>
    /// <param name="name">DAV 属性的本地名称。</param>
    /// <returns>服务器提供的 URI 引用，属性缺失时为 null。</returns>
    private string? SingleHref(string name)
    {
        var property = VersioningXml.Property(Resource, name);
        if (property is null) return null;
        var values = property.Elements(WebDavXml.Dav + "href").ToArray();
        if (values.Length != 1 || string.IsNullOrWhiteSpace(values[0].Value))
            throw new InvalidDataException("Invalid DAV:" + name + " href property.");
        return values[0].Value;
    }
}

