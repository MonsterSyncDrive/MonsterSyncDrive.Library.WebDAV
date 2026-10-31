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

/// <summary>保存服务器对 ACL 的限制及必须存在的主体；未知扩展仍保留在原始 XML 中。</summary>
public sealed class WebDavAclRestrictions
{
    /// <summary>保存限制元素副本。</summary>
    private readonly XElement _element;

    /// <summary>从 DAV:acl-restrictions 建立限制快照。</summary>
    /// <param name="element">限制元素；null 表示没有取得限制描述，不推断服务器不存在限制。</param>
    public WebDavAclRestrictions(XElement? element)
    {
        if (element is not null && element.Name != WebDavXml.Dav + "acl-restrictions")
            throw new ArgumentException("Expected DAV:acl-restrictions.", nameof(element));
        IsAvailable = element is not null;
        _element = element is null ? new XElement(WebDavXml.Dav + "acl-restrictions") : new XElement(element);
        GrantOnly = _element.Element(WebDavXml.Dav + "grant-only") is not null;
        NoInvert = _element.Element(WebDavXml.Dav + "no-invert") is not null;
        DenyBeforeGrant = _element.Element(WebDavXml.Dav + "deny-before-grant") is not null;
        RequiredPrincipals = Array.AsReadOnly((_element.Element(WebDavXml.Dav + "required-principal")?.Elements() ?? [])
            .Select(WebDavPrincipal.Parse).ToArray());
    }

    /// <summary>获取服务器是否实际返回了限制属性。</summary>
    public bool IsAvailable
    {
        get;
    }
    /// <summary>获取服务器是否禁止 deny 条目。</summary>
    public bool GrantOnly
    {
        get;
    }
    /// <summary>获取服务器是否禁止 invert 反选主体。</summary>
    public bool NoInvert
    {
        get;
    }
    /// <summary>获取服务器是否要求所有可编辑 deny 排在 grant 前面。</summary>
    public bool DenyBeforeGrant
    {
        get;
    }
    /// <summary>获取 ACL 中必须仍然存在的主体选择器。</summary>
    public IReadOnlyList<WebDavPrincipal> RequiredPrincipals
    {
        get;
    }

    /// <summary>返回完整限制 XML 副本，供调用方识别其他服务器扩展。</summary>
    /// <returns>独立的 DAV:acl-restrictions 元素。</returns>
    public XElement ToXElement()
    {
        return new(_element);
    }
}

