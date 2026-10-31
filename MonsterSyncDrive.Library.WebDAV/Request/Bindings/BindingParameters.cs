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

namespace MonsterSyncDrive.Library.WebDAV.Request.Bindings;

/// <summary>配置 BIND 或 REBIND，改变集合成员绑定，不复制资源内容。</summary>
public sealed class BindingParameters : WebDavRequestOptions
{
    /// <summary>获取或设置未转义的单个绑定名称；发送时按 URI 路径段编码，不得包含斜杠、点段或控制字符。</summary>
    public string Segment { get; set; } = "";
    /// <summary>获取或设置已存在资源的 URI；BIND 添加别名，REBIND 将此源绑定移动到新名称。</summary>
    public Uri? Source
    {
        get; set;
    }
    /// <summary>获取或设置目标绑定存在时是否覆盖，默认 true；资源身份由服务器维护。</summary>
    public bool Overwrite { get; set; } = true;
    /// <summary>获取或设置附加在标准正文后的扩展元素，发送时深复制。</summary>
    public IReadOnlyCollection<XElement> ExtensionElements { get; set; } = [];

    /// <summary>验证用户名称并编码为 RFC3986 的一个路径段，避免绑定名称意外成为多级路径。</summary>
    /// <param name="segment">未转义的绑定名称。</param>
    /// <returns>可放入 DAV:segment 的转义路径段。</returns>
    internal static string EncodeSegment(string segment)
    {
        ArgumentException.ThrowIfNullOrEmpty(segment);
        return segment is "." or ".." || segment.Contains('/') || segment.Any(char.IsControl)
            ? throw new ArgumentException("A binding name must be one non-dot URI path segment.", nameof(segment))
            : Uri.EscapeDataString(segment);
    }
}

