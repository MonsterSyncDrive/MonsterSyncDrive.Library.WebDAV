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
/// 指定 PROPFIND 正文采用的属性查询形式。
/// </summary>
public enum PropfindRequestType
{
    /// <summary>
    /// 发送 DAV:allprop，额外指定的自定义属性使用 DAV:include 请求。
    /// </summary>
    AllProperties,
    /// <summary>
    /// 发送 DAV:prop，只查询 CustomProperties 中列出的属性；空列表仍发送空的 prop 元素。
    /// </summary>
    NamedProperties,
    /// <summary>
    /// 省略 PROPFIND 正文，按协议采用隐式 allprop 查询。
    /// </summary>
    AllPropertiesImplied,
    /// <summary>
    /// 发送 DAV:propname，只请求属性名称而不请求属性值。
    /// </summary>
    PropertyNames,
}
