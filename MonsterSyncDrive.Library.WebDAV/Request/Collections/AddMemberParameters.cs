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
namespace MonsterSyncDrive.WebDAV.Request;

/// <summary>配置 RFC 5995 由服务器分配子资源 URI 的 POST 上传。</summary>
public sealed class AddMemberParameters : PutFileParameters
{
    /// <summary>获取或设置已知的 Add-Member URI；null 时先查询集合的受保护 DAV:add-member 属性。</summary>
    /// <remarks>该 URI 必须与目标集合具有相同的 HTTP(S) 方案、主机和端口，避免向伪造属性指向的其他服务器上传。</remarks>
    public Uri? AddMemberUri { get; set; }

    /// <summary>获取或设置建议的普通文件名，按 UTF-8 百分号编码后写入 Slug；服务器可以忽略建议。</summary>
    public string? SuggestedName { get; set; }
}
