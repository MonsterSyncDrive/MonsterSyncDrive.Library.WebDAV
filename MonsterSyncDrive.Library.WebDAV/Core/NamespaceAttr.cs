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
namespace MonsterSyncDrive.Library.WebDAV.Core;

/// <summary>
/// 描述请求 XML 中显式声明的命名空间及其可选前缀。
/// </summary>
/// <remarks>默认命名空间使用空前缀或 null；属性身份仍由完整 XName 决定，前缀只影响 XML 的书写形式。</remarks>
public class NamespaceAttr
{
    /// <summary>
    /// 创建默认 XML 命名空间声明，Prefix 保持 null。
    /// </summary>
    /// <param name="defaultNamespace">要声明为默认命名空间的 URI。</param>
    public NamespaceAttr(string defaultNamespace)
    {
        Namespace = defaultNamespace;
    }

    /// <summary>
    /// 创建指定前缀的 XML 命名空间声明。
    /// </summary>
    /// <param name="prefix">要绑定的 XML 命名空间前缀；空前缀表示默认命名空间。</param>
    /// <param name="namespace">命名空间 URI，与属性 XName 的命名空间保持一致。</param>
    public NamespaceAttr(string prefix, string @namespace)
    {
        Prefix = prefix;
        Namespace = @namespace;
    }

    /// <summary>
    /// 获取命名空间前缀。
    /// </summary>
    /// <value>null 或空字符串表示声明默认命名空间。</value>
    public string? Prefix
    {
        get;
    }

    /// <summary>
    /// 获取 XML 命名空间 URI，作为属性完整名称的一部分使用。
    /// </summary>
    public string Namespace
    {
        get;
    }
}
