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
using MonsterSyncDrive.Library.WebDAV.Request.RedirectReferences;
using System.Xml.Linq;

namespace MonsterSyncDrive.Library.WebDAV.Core.RedirectReferences;

/// <summary>描述服务器上的 RFC4437 重定向引用资源，不表示已跟随的普通 HTTP 重定向。</summary>
public sealed class WebDavRedirectReference
{
    /// <summary>获取完整资源属性和逐属性状态，包含未知扩展。</summary>
    public WebDavResource Resource
    {
        get;
    }
    /// <summary>获取 resourcetype 是否声明 DAV:redirectref。</summary>
    public bool IsRedirectReference
    {
        get;
    }
    /// <summary>获取 reftarget 中的目标 URI 引用原文，未返回时为 null。</summary>
    public string? Target
    {
        get;
    }
    /// <summary>获取标准 temporary 或 permanent 生命周期，未返回或扩展类型时为 null。</summary>
    public RedirectReferenceLifetime? Lifetime
    {
        get;
    }
    /// <summary>获取完整生命周期元素名称，保留未来扩展的命名空间和名称。</summary>
    public XName? LifetimeType
    {
        get;
    }
    /// <summary>获取查询引用时必须显式请求的属性名称。</summary>
    public static IReadOnlyCollection<XName> PropertyNames
    {
        get;
    } = Array.AsReadOnly(new[]
    {
        WebDavXml.Dav + "resourcetype", WebDavXml.Dav + "reftarget", WebDavXml.Dav + "redirect-lifetime"
    });

    /// <summary>从成功返回的属性解析引用定义，结构损坏时抛出 InvalidDataException。</summary>
    /// <param name="resource">一次 PROPFIND 的资源快照。</param>
    public WebDavRedirectReference(WebDavResource resource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        Resource = resource;
        IsRedirectReference = VersioningXml.Property(resource, "resourcetype")?.Element(WebDavXml.Dav + "redirectref") is not null;
        XElement? target = VersioningXml.Property(resource, "reftarget");
        if (target is not null)
        {
            XElement[] hrefs = target.Elements(WebDavXml.Dav + "href").ToArray();
            if (hrefs.Length != 1 || string.IsNullOrWhiteSpace(hrefs[0].Value) || !Uri.TryCreate(hrefs[0].Value, UriKind.RelativeOrAbsolute, out _))
                throw new InvalidDataException("DAV:reftarget must contain exactly one valid URI reference.");
            Target = hrefs[0].Value;
        }
        XElement? lifetime = VersioningXml.Property(resource, "redirect-lifetime");
        if (lifetime is not null)
        {
            XElement[] children = lifetime.Elements().ToArray();
            if (children.Length != 1)
                throw new InvalidDataException("DAV:redirect-lifetime requires one lifetime element.");
            LifetimeType = children[0].Name;
            Lifetime = LifetimeType == WebDavXml.Dav + "temporary" ? RedirectReferenceLifetime.Temporary
                : LifetimeType == WebDavXml.Dav + "permanent" ? RedirectReferenceLifetime.Permanent : null;
        }
    }

    /// <summary>将相对目标按引用资源当前 URI 解析；不访问目标或自动跟随网络重定向。</summary>
    /// <param name="referenceUri">引用资源的绝对 HTTP(S) URI，而非其父集合 URI。</param>
    /// <returns>解析后的绝对目标，目标未知时为 null。</returns>
    public Uri? ResolveTarget(Uri referenceUri)
    {
        ArgumentNullException.ThrowIfNull(referenceUri);
        return !referenceUri.IsAbsoluteUri
            ? throw new ArgumentException("An absolute reference URI is required.", nameof(referenceUri))
            : Target is null ? null : new Uri(referenceUri, Target);
    }
}

