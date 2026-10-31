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

namespace MonsterSyncDrive.WebDAV.Core;

/// <summary>
/// 表示 HTTP 207 响应中单个属性的状态码和可选说明。
/// </summary>
/// <remarks>同一资源可以同时包含成功与失败的属性，调用方应根据操作检查逐项结果。</remarks>
public class WebDavPropertyStatus
{
    /// <summary>
    /// 保存属性完整名称、HTTP 状态码及可选说明。
    /// </summary>
    /// <remarks>不推断其他属性或资源的结果；缺省说明为 null。</remarks>
    /// <param name="name">本条状态对应的完整属性名称，包含命名空间和本地名称。</param>
    /// <param name="statusCode">HTTP 响应的整数状态码，例如 200、207、404 或 423。</param>
    public WebDavPropertyStatus(XName name, int statusCode)
        : this(name, statusCode, null)
    {
    }

    /// <summary>
    /// 保存属性完整名称、HTTP 状态码及可选说明。
    /// </summary>
    /// <remarks>不推断其他属性或资源的结果；缺省说明为 null。</remarks>
    /// <param name="name">本条状态对应的完整属性名称，包含命名空间和本地名称。</param>
    /// <param name="statusCode">HTTP 响应的整数状态码，例如 200、207、404 或 423。</param>
    /// <param name="description">HTTP 原因短语或服务器状态说明；没有提供时可为 null。</param>
    public WebDavPropertyStatus(XName name, int statusCode, string? description)
    {
        ArgumentNullException.ThrowIfNull(name);
        Name = name;
        StatusCode = statusCode;
        Description = description;
    }

    /// <summary>
    /// 获取本条状态对应的完整属性名称。
    /// </summary>
    public XName Name { get; }

    /// <summary>
    /// 获取 DAV:propstat 中对应属性的 HTTP 状态码。
    /// </summary>
    public int StatusCode { get; }

    /// <summary>
    /// 获取服务器对该属性状态提供的可选说明。
    /// </summary>
    /// <value>没有提供说明时为 null。</value>
    public string? Description { get; }

    /// <summary>获取当前属性所在 propstat 的结构化错误详情，未提供时为 null。</summary>
    public WebDavError? Error { get; internal set; }

    /// <summary>
    /// 获取属性状态是否为 HTTP 200 至 299。
    /// </summary>
    /// <value>该结果仅描述当前属性，不代表整个多状态请求的结果。</value>
    public virtual bool IsSuccessful => StatusCode is >= 200 and <= 299;

    /// <summary>
    /// 返回当前WebDavPropertyStatus的状态或属性概要，便于日志记录和调试。
    /// </summary>
    /// <returns>包含当前对象关键字段的描述字符串，不触发网络请求。</returns>
    public override string ToString() => $"{{ Name: {Name}, StatusCode: {StatusCode}, Description: {Description} }}";
}
