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

namespace MonsterSyncDrive.Library.WebDAV.Request.RedirectReferences;

/// <summary>指定 RFC4437 引用资源向客户端宣告的重定向生命周期。</summary>
public enum RedirectReferenceLifetime
{
    /// <summary>临时引用，服务器正常重定向时使用 302。</summary>
    Temporary,
    /// <summary>永久引用，服务器正常重定向时使用 301。</summary>
    Permanent
}

/// <summary>配置创建或修改重定向引用；目标原文允许绝对 URI 或相对 URI 引用。</summary>
public sealed class RedirectReferenceParameters : WebDavRequestOptions
{
    /// <summary>获取或设置引用目标；创建时必需，修改时 null 表示不改变目标。允许其他 URI 方案和相对引用，不把目标限制为 HTTP。</summary>
    public Uri? Target
    {
        get; set;
    }
    /// <summary>获取或设置生命周期；创建时 null 使用服务器规定的 temporary，修改时 null 不改变。</summary>
    public RedirectReferenceLifetime? Lifetime
    {
        get; set;
    }
    /// <summary>获取或设置附加到标准正文后的扩展元素，发送前深复制。</summary>
    public IReadOnlyCollection<XElement> ExtensionElements { get; set; } = [];
}

