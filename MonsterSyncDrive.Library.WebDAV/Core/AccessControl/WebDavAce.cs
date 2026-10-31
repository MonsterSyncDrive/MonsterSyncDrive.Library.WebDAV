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

namespace MonsterSyncDrive.Library.WebDAV.Core.AccessControl;

/// <summary>指定 ACE 是授予权限还是拒绝权限。</summary>
public enum WebDavAceAction
{
    /// <summary>授予列出的权限。</summary>
    Grant,
    /// <summary>拒绝列出的权限。</summary>
    Deny
}

/// <summary>表示一个有序 ACL 条目，保留保护、继承来源和服务器扩展 XML。</summary>
public sealed class WebDavAce
{
    /// <summary>保存完整 ACE，用于不丢失未知服务器扩展的读改写。</summary>
    private readonly XElement _element;

    /// <summary>创建一个可写的授予或拒绝条目。</summary>
    /// <param name="principal">用户、组或伪主体，包括可选反选。</param>
    /// <param name="action">授予或拒绝。</param>
    /// <param name="privileges">非空的完整权限名称集合，可包含服务器自定义权限。</param>
    public WebDavAce(WebDavPrincipal principal, WebDavAceAction action, IEnumerable<XName> privileges)
    {
        ArgumentNullException.ThrowIfNull(principal);
        ArgumentNullException.ThrowIfNull(privileges);
        if (!Enum.IsDefined(action))
            throw new ArgumentOutOfRangeException(nameof(action));
        XName[] names = privileges.Distinct().ToArray();
        if (names.Length == 0 || names.Any(name => name is null))
            throw new ArgumentException("At least one privilege is required.", nameof(privileges));
        Principal = principal;
        Action = action;
        Privileges = Array.AsReadOnly(names);
        _element = new XElement(WebDavXml.Dav + "ace", principal.ToXElement(),
            new XElement(WebDavXml.Dav + (action == WebDavAceAction.Grant ? "grant" : "deny"),
                names.Select(name => new XElement(WebDavXml.Dav + "privilege", new XElement(name)))));
    }

    /// <summary>从服务器条目构造快照。</summary>
    /// <param name="element">完整 ACE 元素。</param>
    private WebDavAce(XElement element)
    {
        _element = new XElement(element);
        if (element.Name != WebDavXml.Dav + "ace")
            throw new InvalidDataException("Expected DAV:ace.");
        XElement[] principals = element.Elements().Where(node => node.Name == WebDavXml.Dav + "principal" || node.Name == WebDavXml.Dav + "invert").ToArray();
        XElement[] actions = element.Elements().Where(node => node.Name == WebDavXml.Dav + "grant" || node.Name == WebDavXml.Dav + "deny").ToArray();
        if (principals.Length != 1 || actions.Length != 1)
            throw new InvalidDataException("An ACE needs one principal and exactly one grant or deny.");
        Principal = WebDavPrincipal.Parse(principals[0]);
        Action = actions[0].Name.LocalName == "grant" ? WebDavAceAction.Grant : WebDavAceAction.Deny;
        XElement[] privilegeElements = actions[0].Elements(WebDavXml.Dav + "privilege").ToArray();
        XName[] names = privilegeElements.Select(value =>
        {
            XElement[] children = value.Elements().ToArray();
            return children.Length != 1 ? throw new InvalidDataException("DAV:privilege must contain one privilege name.") : children[0].Name;
        }).ToArray();
        if (names.Length == 0)
            throw new InvalidDataException("An ACE cannot contain an empty grant or deny.");
        Privileges = Array.AsReadOnly(names);
        IsProtected = element.Element(WebDavXml.Dav + "protected") is not null;
        XElement? inherited = element.Element(WebDavXml.Dav + "inherited");
        InheritedFrom = inherited?.Element(WebDavXml.Dav + "href")?.Value;
        if (inherited is not null && string.IsNullOrWhiteSpace(InheritedFrom))
            throw new InvalidDataException("Inherited ACE has no source href.");
    }

    /// <summary>获取此条目匹配的主体选择器。</summary>
    public WebDavPrincipal Principal
    {
        get;
    }

    /// <summary>获取授予或拒绝模式。</summary>
    public WebDavAceAction Action
    {
        get;
    }

    /// <summary>获取此条目列出的权限完整名称，保留原有顺序。</summary>
    public IReadOnlyList<XName> Privileges
    {
        get;
    }

    /// <summary>获取此条目是否由服务器保护，不能通过 ACL 请求修改。</summary>
    public bool IsProtected
    {
        get;
    }

    /// <summary>获取继承条目的源资源 href；非继承条目为 null。</summary>
    public string? InheritedFrom
    {
        get;
    }

    /// <summary>获取是否为当前资源可编辑的条目。</summary>
    public bool IsEditable => !IsProtected && InheritedFrom is null;

    /// <summary>解析服务器 ACE，保留完整 XML 供后续往返。</summary>
    /// <param name="element">完整 DAV:ace。</param>
    /// <returns>ACL 条目快照。</returns>
    public static WebDavAce Parse(XElement element)
    {
        return new(element ?? throw new ArgumentNullException(nameof(element)));
    }

    /// <summary>返回不共享内部节点的完整 ACE。</summary>
    /// <returns>包括保护与继承标志的独立 XML 元素。</returns>
    public XElement ToXElement()
    {
        return new(_element);
    }

    /// <summary>在保留同一 ACE 扩展信息的前提下替换权限列表。</summary>
    /// <param name="privileges">新的非空权限集合。</param>
    /// <returns>更新后的条目副本。</returns>
    internal WebDavAce WithPrivileges(IEnumerable<XName> privileges)
    {
        XName[] names = privileges.Distinct().ToArray();
        if (names.Length == 0)
            throw new ArgumentException("Privileges cannot be empty.", nameof(privileges));
        XElement copy = ToXElement();
        XElement action = copy.Element(WebDavXml.Dav + (Action == WebDavAceAction.Grant ? "grant" : "deny"))!;
        action.Elements(WebDavXml.Dav + "privilege").Remove();
        action.Add(names.Select(name => new XElement(WebDavXml.Dav + "privilege", new XElement(name))));
        return Parse(copy);
    }
}

