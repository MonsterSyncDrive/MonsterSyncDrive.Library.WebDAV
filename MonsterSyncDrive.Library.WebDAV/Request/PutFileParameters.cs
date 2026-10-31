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
using MonsterSyncDrive.WebDAV.Core;
using System.Net.Http.Headers;

namespace MonsterSyncDrive.WebDAV.Request;

/// <summary>
/// 配置 PUT 的媒体类型、锁条件、源流所有权和上传进度。
/// </summary>
/// <remarks>从源流当前位置开始上传；传输期间不要修改参数、源流位置或源数据。</remarks>
public class PutFileParameters : WebDavRequestOptions
{
    /// <summary>
    /// 获取或设置上传操作结束后是否保留调用方的源流。
    /// </summary>
    /// <value>默认 true；false 时释放上传正文会关闭源流，包括失败和取消后的清理阶段。</value>
    public bool LeaveOpen { get; set; } = true;
    /// <summary>
    /// 获取或设置上传正文的累计字节进度接收器。
    /// </summary>
    /// <value>默认 null；每次成功写入一块数据后报告，本次 HTTP 正文重发时从零重新累计，不表示整个服务器事务已完成。</value>
    public IProgress<long>? Progress { get; set; }
    /// <summary>
    /// 初始化 PutFileParameters 的默认请求设置。
    /// </summary>
    /// <remarks>内容类型为 application/octet-stream，默认借用源流，未设置锁或进度回调。</remarks>
    public PutFileParameters()
    {
        ContentType = MediaTypes.BinaryDataMediaType;
    }

    /// <summary>
    /// 获取或设置 PUT 请求正文的媒体类型。
    /// </summary>
    /// <value>默认 application/octet-stream；设置文件的实际媒体类型不会对源字节进行编码转换。</value>
    public MediaTypeHeaderValue ContentType { get; set; }

    /// <summary>
    /// 获取或设置覆盖受写锁保护资源时的锁令牌。
    /// </summary>
    /// <value>默认 null；存在时添加 If 条件，可同时使用 IfMatch 防止覆盖其他内容版本。</value>
    public string? LockToken { get; set; }
}
