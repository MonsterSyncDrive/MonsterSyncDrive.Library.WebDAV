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
namespace MonsterSyncDrive.Library.WebDAV.Request;

/// <summary>
/// 配置释放资源锁时的锁令牌和通用请求条件。
/// </summary>
public class UnlockParameters : WebDavRequestOptions
{
    /// <summary>
    /// 保存要解锁的令牌，并初始化独立的请求头和默认取消设置。
    /// </summary>
    /// <remarks>构造阶段仅检查 null；完整 URI 格式和尖括号规范化在发送 UNLOCK 时处理。</remarks>
    /// <param name="lockToken">服务器提供的锁令牌 URI；发送时会规范化尖括号并检查格式。</param>
    /// <exception cref="ArgumentNullException">lockToken 为 null。</exception>
    public UnlockParameters(string lockToken)
    {
        ArgumentNullException.ThrowIfNull(lockToken);

        LockToken = lockToken;
    }

    /// <summary>
    /// 获取要释放的锁令牌 URI。
    /// </summary>
    /// <value>发送时规范化外层尖括号，使用 Lock-Token 头；不会使用 If 头代替解锁令牌。</value>
    public string LockToken
    {
        get;
    }
}
