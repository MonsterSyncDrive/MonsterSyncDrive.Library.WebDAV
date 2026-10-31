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

/// <summary>提供 RFC 3744 定义的权限名称；服务器也可以通过 XName 使用自定义权限。</summary>
public static class WebDavPrivileges
{
    /// <summary>读取资源内容及可公开属性。</summary>
    public static readonly XName Read = WebDavXml.Dav + "read";
    /// <summary>写入资源的聚合权限，具体子权限以服务器支持树为准。</summary>
    public static readonly XName Write = WebDavXml.Dav + "write";
    /// <summary>修改资源内容。</summary>
    public static readonly XName WriteContent = WebDavXml.Dav + "write-content";
    /// <summary>修改可写资源属性。</summary>
    public static readonly XName WriteProperties = WebDavXml.Dav + "write-properties";
    /// <summary>移除其他主体创建的写锁。</summary>
    public static readonly XName Unlock = WebDavXml.Dav + "unlock";
    /// <summary>读取资源 ACL。</summary>
    public static readonly XName ReadAcl = WebDavXml.Dav + "read-acl";
    /// <summary>读取当前用户的有效权限集合。</summary>
    public static readonly XName ReadCurrentUserPrivilegeSet = WebDavXml.Dav + "read-current-user-privilege-set";
    /// <summary>修改资源可编辑的 ACL 条目。</summary>
    public static readonly XName WriteAcl = WebDavXml.Dav + "write-acl";
    /// <summary>在集合中创建资源绑定。</summary>
    public static readonly XName Bind = WebDavXml.Dav + "bind";
    /// <summary>移除集合内的资源绑定。</summary>
    public static readonly XName Unbind = WebDavXml.Dav + "unbind";
    /// <summary>服务器权限树的聚合根；若服务器标为 abstract，不能直接写入 ACE。</summary>
    public static readonly XName All = WebDavXml.Dav + "all";
}

/// <summary>表示服务器支持权限树中的一项，支持聚合及抽象权限。</summary>
public sealed class WebDavSupportedPrivilege
{
    /// <summary>从已验证的权限信息建立不可变节点。</summary>
    /// <param name="name">权限完整名称。</param>
    /// <param name="isAbstract">是否只用于聚合描述而不能写入 ACE。</param>
    /// <param name="description">服务器提供的人类可读说明。</param>
    /// <param name="language">说明的 xml:lang。</param>
    /// <param name="children">被此权限聚合的直接子权限。</param>
    private WebDavSupportedPrivilege(XName name, bool isAbstract, string? description, string? language, IReadOnlyList<WebDavSupportedPrivilege> children)
    {
        Name = name; IsAbstract = isAbstract; Description = description; Language = language; Children = children;
    }

    /// <summary>获取权限完整名称。</summary>
    public XName Name { get; }
    /// <summary>获取该权限是否只能描述树结构，而不能直接授予或拒绝。</summary>
    public bool IsAbstract { get; }
    /// <summary>获取人类可读说明。</summary>
    public string? Description { get; }
    /// <summary>获取说明的语言标记。</summary>
    public string? Language { get; }
    /// <summary>获取此权限聚合的直接子权限。</summary>
    public IReadOnlyList<WebDavSupportedPrivilege> Children { get; }

    /// <summary>枚举当前节点及其所有后代，保留服务器声明的树顺序。</summary>
    /// <returns>深度优先的权限序列。</returns>
    public IEnumerable<WebDavSupportedPrivilege> DescendantsAndSelf()
    {
        var pending = new Stack<WebDavSupportedPrivilege>();
        pending.Push(this);
        while (pending.TryPop(out var current))
        {
            yield return current;
            for (var index = current.Children.Count - 1; index >= 0; index--)
                pending.Push(current.Children[index]);
        }
    }

    /// <summary>解析 supported-privilege 节点，保留嵌套聚合与抽象标志。</summary>
    /// <param name="element">DAV:supported-privilege 元素。</param>
    /// <returns>权限树节点。</returns>
    public static WebDavSupportedPrivilege Parse(XElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return ParseNode(element, 0);
    }

    /// <summary>在有限层数内递归解析权限树，防止恶意深层 XML 耗尽调用栈。</summary>
    /// <param name="element">当前权限元素。</param>
    /// <param name="depth">当前树深度。</param>
    /// <returns>权限节点。</returns>
    private static WebDavSupportedPrivilege ParseNode(XElement element, int depth)
    {
        if (depth > 256 || element.Name != WebDavXml.Dav + "supported-privilege")
            throw new InvalidDataException("Invalid or excessively nested supported privilege.");
        var names = element.Element(WebDavXml.Dav + "privilege")?.Elements().ToArray();
        if (names is not { Length: 1 })
            throw new InvalidDataException("Supported privilege must contain exactly one privilege name.");
        var description = element.Element(WebDavXml.Dav + "description");
        return new WebDavSupportedPrivilege(names[0].Name, element.Element(WebDavXml.Dav + "abstract") is not null,
            description?.Value, (string?)description?.Attribute(XNamespace.Xml + "lang"),
            Array.AsReadOnly(element.Elements(WebDavXml.Dav + "supported-privilege").Select(child => ParseNode(child, depth + 1)).ToArray()));
    }
}

