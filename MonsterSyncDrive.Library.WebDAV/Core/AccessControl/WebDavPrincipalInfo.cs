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

namespace MonsterSyncDrive.WebDAV.Core.AccessControl;

/// <summary>表示用户或组主体的标准属性快照，不把身份发现当成账户创建或删除。</summary>
public sealed class WebDavPrincipalInfo
{
    /// <summary>从主体资源的成功属性构建身份信息。</summary>
    /// <param name="resource">查询得到的主体资源快照。</param>
    public WebDavPrincipalInfo(WebDavResource resource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        Resource = resource;
        DisplayName = Property("displayname")?.Value;
        PrincipalUrl = Property("principal-URL")?.Element(WebDavXml.Dav + "href")?.Value;
        AlternateUris = Hrefs(Property("alternate-URI-set"));
        var group = Property("group-member-set");
        IsGroup = group is not null;
        GroupMembers = Hrefs(group);
        GroupMembership = Hrefs(Property("group-membership"));
        PrincipalCollections = Hrefs(Property("principal-collection-set"));
    }

    /// <summary>获取包含所有属性及逐项状态的原始资源模型。</summary>
    public WebDavResource Resource { get; }
    /// <summary>获取主体显示名称。</summary>
    public string? DisplayName { get; }
    /// <summary>获取 ACL 中应使用的规范主体 URI，而不是显示名称或登录名。</summary>
    public string? PrincipalUrl { get; }
    /// <summary>获取主体的替代描述 URI。</summary>
    public IReadOnlyList<string> AlternateUris { get; }
    /// <summary>获取是否实际返回了 group-member-set，从而识别包括空组在内的组主体。</summary>
    public bool IsGroup { get; }
    /// <summary>获取组的直接成员 URI；不自动展开嵌套组。</summary>
    public IReadOnlyList<string> GroupMembers { get; }
    /// <summary>获取此主体直接加入的组 URI；不推断间接组关系。</summary>
    public IReadOnlyList<string> GroupMembership { get; }
    /// <summary>获取服务器提供的主体集合 URI，可用于后续用户和组搜索。</summary>
    public IReadOnlyList<string> PrincipalCollections { get; }

    /// <summary>查找成功属性的独立 XML 副本。</summary>
    /// <param name="name">DAV 属性本地名称。</param>
    /// <returns>属性元素，缺失或失败时为 null。</returns>
    private XElement? Property(string name) => Resource.Properties.FirstOrDefault(property => property.Name == WebDavXml.Dav + name)?.ToXElement();

    /// <summary>读取 URI 集合，保留服务器提供的形式。</summary>
    /// <param name="element">包含 href 子元素的属性。</param>
    /// <returns>独立的只读 URI 列表。</returns>
    internal static IReadOnlyList<string> Hrefs(XElement? element) =>
        Array.AsReadOnly((element?.Elements(WebDavXml.Dav + "href") ?? []).Select(value =>
        {
            if (string.IsNullOrWhiteSpace(value.Value)) throw new InvalidDataException("Principal property contains an empty href.");
            return value.Value;
        }).ToArray());
}

