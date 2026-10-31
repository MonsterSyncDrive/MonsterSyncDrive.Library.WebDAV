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

namespace MonsterSyncDrive.WebDAV.Request;

/// <summary>
/// 配置 COPY 的深度、覆盖策略和目标资源锁条件。
/// </summary>
public class CopyParameters : WebDavRequestOptions
{
    /// <summary>
    /// 初始化 CopyParameters 的默认请求设置。
    /// </summary>
    /// <remarks>Overwrite 为 true，Headers 为空集合，取消令牌为 None；未指定深度时客户端使用 infinity。</remarks>
    public CopyParameters()
    {
        Overwrite = true;
    }

    /// <summary>
    /// 获取或设置 COPY 的集合复制深度。
    /// </summary>
    /// <value>null 使用 infinity，复制集合及全部后代；ResourceOnly 对应 Depth: 0。</value>
    public ApplyTo.Copy? ApplyTo { get; set; }

    /// <summary>
    /// 获取或设置目标资源的锁令牌。
    /// </summary>
    /// <value>非 null 时构造绑定绝对目标 URI 的 If 条件；null 表示不提交目标锁。</value>
    public string? DestLockToken { get; set; }

    /// <summary>
    /// 获取或设置是否允许覆盖已经存在的复制目标。
    /// </summary>
    /// <value>默认 true，发送 Overwrite: T；false 发送 F，由服务器在目标存在时拒绝复制。</value>
    public bool Overwrite { get; set; }
}
