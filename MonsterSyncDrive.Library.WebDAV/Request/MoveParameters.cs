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

/// <summary>
/// 配置 MOVE 的覆盖策略，以及源资源和目标资源各自的锁条件。
/// </summary>
public class MoveParameters : WebDavRequestOptions
{
    /// <summary>
    /// 初始化 MoveParameters 的默认请求设置。
    /// </summary>
    /// <remarks>Overwrite 为 true，Headers 为空集合，取消令牌为 None，源锁和目标锁未指定。</remarks>
    public MoveParameters()
    {
        Overwrite = true;
    }

    /// <summary>
    /// 获取或设置源资源的写锁令牌。
    /// </summary>
    /// <value>默认 null；非 null 时绑定源资源绝对 URI，避免与目标锁条件混淆。</value>
    public string? SourceLockToken { get; set; }

    /// <summary>
    /// 获取或设置目标资源的写锁令牌。
    /// </summary>
    /// <value>默认 null；非 null 时绑定目标资源绝对 URI，和源锁放入同一 If 头。</value>
    public string? DestLockToken { get; set; }

    /// <summary>
    /// 获取或设置是否允许覆盖已存在的移动目标。
    /// </summary>
    /// <value>默认 true，发送 Overwrite: T；false 发送 F。</value>
    public bool Overwrite { get; set; }
}
