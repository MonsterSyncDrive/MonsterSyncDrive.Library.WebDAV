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

/// <summary>表示 RFC 3744 的 ACL 主体选择器种类。</summary>
public enum WebDavPrincipalKind
{
    /// <summary>通过 DAV:href 标识特定用户或组主体。</summary>
    Href,
    /// <summary>匹配全部主体，包含未认证访问者。</summary>
    All,
    /// <summary>匹配已经认证的主体。</summary>
    Authenticated,
    /// <summary>匹配未认证访问者。</summary>
    Unauthenticated,
    /// <summary>匹配当前主体资源本身。</summary>
    Self,
    /// <summary>由资源属性的 href 值间接确定主体，例如 DAV:owner。</summary>
    Property,
    /// <summary>服务器扩展的选择器；保留 XML 供无损读改写，不推断其权限语义。</summary>
    Extension
}

/// <summary>保存 ACL 主体选择器，包括可选的 DAV:invert 反选。</summary>
/// <remarks>主体 URI 保留服务器形式。相等比较使用选择器结构，不擅自把两个不同用户 URI 当成同一账户。</remarks>
public sealed class WebDavPrincipal : IEquatable<WebDavPrincipal>
{
    /// <summary>保存选择器元素的独立副本，避免调用方修改快照。</summary>
    private readonly XElement _selector;

    /// <summary>使用已校验的选择器建立主体。</summary>
    /// <param name="selector">href、all、property 等选择器元素。</param>
    /// <param name="kind">选择器种类。</param>
    /// <param name="inverted">是否通过 DAV:invert 反选该主体。</param>
    private WebDavPrincipal(XElement selector, WebDavPrincipalKind kind, bool inverted)
    {
        _selector = new XElement(selector);
        Kind = kind;
        IsInverted = inverted;
    }

    /// <summary>获取主体选择器种类。</summary>
    public WebDavPrincipalKind Kind
    {
        get;
    }

    /// <summary>获取该主体是否使用反选。</summary>
    public bool IsInverted
    {
        get;
    }

    /// <summary>获取 href 主体的原始 URI；其他主体返回 null。</summary>
    public string? Href => Kind == WebDavPrincipalKind.Href ? _selector.Value : null;

    /// <summary>获取属性间接主体的完整属性名称；其他主体返回 null。</summary>
    public XName? PropertyName => Kind == WebDavPrincipalKind.Property ? _selector.Elements().Single().Name : null;

    /// <summary>创建通过 URI 标识的用户或组主体。</summary>
    /// <param name="href">绝对 URI 或服务器返回的相对 URI，不应为空。</param>
    /// <returns>对应 URI 的主体选择器。</returns>
    public static WebDavPrincipal ForHref(string href)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(href);
        return href.Any(char.IsControl) || !Uri.TryCreate(href, UriKind.RelativeOrAbsolute, out _)
            ? throw new ArgumentException("Principal href must be a valid URI reference.", nameof(href))
            : new WebDavPrincipal(new XElement(WebDavXml.Dav + "href", href), WebDavPrincipalKind.Href, false);
    }

    /// <summary>创建匹配全部主体的选择器。</summary>
    /// <returns>DAV:all 主体。</returns>
    public static WebDavPrincipal All()
    {
        return Simple("all", WebDavPrincipalKind.All);
    }

    /// <summary>创建匹配已认证主体的选择器。</summary>
    /// <returns>DAV:authenticated 主体。</returns>
    public static WebDavPrincipal Authenticated()
    {
        return Simple("authenticated", WebDavPrincipalKind.Authenticated);
    }

    /// <summary>创建匹配未认证访问者的选择器。</summary>
    /// <returns>DAV:unauthenticated 主体。</returns>
    public static WebDavPrincipal Unauthenticated()
    {
        return Simple("unauthenticated", WebDavPrincipalKind.Unauthenticated);
    }

    /// <summary>创建匹配当前主体资源自身的选择器。</summary>
    /// <returns>DAV:self 主体。</returns>
    public static WebDavPrincipal Self()
    {
        return Simple("self", WebDavPrincipalKind.Self);
    }

    /// <summary>创建由资源属性间接确定的主体。</summary>
    /// <param name="name">属性完整名称，例如 DAV:owner 或 DAV:group。</param>
    /// <returns>DAV:property 主体。</returns>
    public static WebDavPrincipal ForProperty(XName name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return new WebDavPrincipal(new XElement(WebDavXml.Dav + "property", new XElement(name)), WebDavPrincipalKind.Property, false);
    }

    /// <summary>返回相同主体选择器的反选或取消反选副本。</summary>
    /// <returns>反转 IsInverted 后的新主体。</returns>
    public WebDavPrincipal Invert()
    {
        return new(_selector, Kind, !IsInverted);
    }

    /// <summary>转换为可放入 ACE 的 principal 或 invert 元素。</summary>
    /// <returns>不共享内部节点的完整 XML 元素。</returns>
    public XElement ToXElement()
    {
        XElement principal = new(WebDavXml.Dav + "principal", new XElement(_selector));
        return IsInverted ? new XElement(WebDavXml.Dav + "invert", principal) : principal;
    }

    /// <summary>从 principal、invert 或属性内的主体选择器解析主体。</summary>
    /// <param name="element">完整主体节点。</param>
    /// <returns>保留选择器与反选信息的主体。</returns>
    /// <exception cref="InvalidDataException">标准选择器的元素数量或内容不合法。</exception>
    public static WebDavPrincipal Parse(XElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        bool inverted = element.Name == WebDavXml.Dav + "invert";
        var container = inverted ? Single(element, WebDavXml.Dav + "principal") : element;
        var selector = container.Name == WebDavXml.Dav + "principal" ? Single(container) : container;
        var kind = selector.Name.Namespace == WebDavXml.Dav ? selector.Name.LocalName switch
        {
            "href" => WebDavPrincipalKind.Href,
            "all" => WebDavPrincipalKind.All,
            "authenticated" => WebDavPrincipalKind.Authenticated,
            "unauthenticated" => WebDavPrincipalKind.Unauthenticated,
            "self" => WebDavPrincipalKind.Self,
            "property" => WebDavPrincipalKind.Property,
            _ => WebDavPrincipalKind.Extension
        } : WebDavPrincipalKind.Extension;
        if (kind == WebDavPrincipalKind.Href && (string.IsNullOrWhiteSpace(selector.Value) || selector.HasElements))
            throw new InvalidDataException("ACL principal href is missing or malformed.");
        if (kind == WebDavPrincipalKind.Property)
            _ = Single(selector);
        return kind is WebDavPrincipalKind.All or WebDavPrincipalKind.Authenticated or WebDavPrincipalKind.Unauthenticated or WebDavPrincipalKind.Self
            && (selector.HasElements || !string.IsNullOrWhiteSpace(selector.Value))
            ? throw new InvalidDataException("ACL pseudo-principal must be empty.")
            : new WebDavPrincipal(selector, kind, inverted);
    }

    /// <summary>比较主体选择器的名称、值及反选，而不依赖 XML 前缀。</summary>
    /// <param name="other">待比较主体。</param>
    /// <returns>结构及反选都相同时为 true。</returns>
    public bool Equals(WebDavPrincipal? other)
    {
        return other is not null && Kind == other.Kind && IsInverted == other.IsInverted
        && (Kind switch
        {
            WebDavPrincipalKind.Href => string.Equals(Href, other.Href, StringComparison.Ordinal),
            WebDavPrincipalKind.Property => PropertyName == other.PropertyName,
            WebDavPrincipalKind.Extension => XNode.DeepEquals(_selector, other._selector),
            _ => true
        });
    }

    /// <summary>比较对象是否为相同主体。</summary>
    /// <param name="obj">待比较对象。</param>
    /// <returns>对象为相同主体时为 true。</returns>
    public override bool Equals(object? obj)
    {
        return obj is WebDavPrincipal principal && Equals(principal);
    }

    /// <summary>计算与结构相等规则兼容的哈希码。</summary>
    /// <returns>主体名称、内容及反选状态的哈希。</returns>
    public override int GetHashCode()
    {
        return Kind switch
        {
            WebDavPrincipalKind.Href => HashCode.Combine(Kind, Href, IsInverted),
            WebDavPrincipalKind.Property => HashCode.Combine(Kind, PropertyName, IsInverted),
            WebDavPrincipalKind.Extension => HashCode.Combine(_selector.Name, _selector.Value, IsInverted),
            _ => HashCode.Combine(Kind, IsInverted)
        };
    }

    /// <summary>返回用于调试的主体 XML。</summary>
    /// <returns>不格式化的主体 XML。</returns>
    public override string ToString()
    {
        return ToXElement().ToString(SaveOptions.DisableFormatting);
    }

    /// <summary>建立无内容的标准伪主体。</summary>
    /// <param name="name">DAV 选择器本地名称。</param>
    /// <param name="kind">选择器种类。</param>
    /// <returns>新的非反选主体。</returns>
    private static WebDavPrincipal Simple(string name, WebDavPrincipalKind kind)
    {
        return new(new XElement(WebDavXml.Dav + name), kind, false);
    }

    /// <summary>读取且验证恰好一个元素，忽略可扩展的文本空白。</summary>
    /// <param name="element">父节点。</param>
    /// <param name="name">可选的期望元素名。</param>
    /// <returns>唯一的子元素。</returns>
    private static XElement Single(XElement element, XName? name = null)
    {
        var children = element.Elements().ToArray();
        return children.Length != 1 || (name is not null && children[0].Name != name)
            ? throw new InvalidDataException("ACL principal selector must contain exactly one expected element.")
            : children[0];
    }
}

