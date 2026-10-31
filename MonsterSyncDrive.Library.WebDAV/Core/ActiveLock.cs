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
/// 描述 DAV:lockdiscovery 返回的一项活动写锁。
/// </summary>
/// <remarks>通过 Builder 创建快照；服务器未提供或无法识别的可选信息保留为 null，不推断锁范围、租约或所有者。</remarks>
public class ActiveLock
{
    /// <summary>
    /// 创建供活动锁构建器组装属性的空实例。
    /// </summary>
    /// <remarks>构造函数私有，外部通过 ActiveLock.Builder 分步提供服务器锁信息。</remarks>
    private ActiveLock()
    {
    }

    /// <summary>
    /// 获取锁的覆盖深度；0 仅当前资源，infinity 包含集合全部后代。
    /// </summary>
    /// <value>null 表示响应未提供或未能识别深度。</value>
    public ApplyTo.Lock? ApplyTo { get; private set; }

    /// <summary>
    /// 获取服务器报告的共享或独占锁模式。
    /// </summary>
    /// <value>null 表示未提供或无法识别。</value>
    public LockScope? LockScope { get; private set; }

    /// <summary>
    /// 获取用于续期、修改条件及解锁的锁令牌 URI。
    /// </summary>
    /// <value>保存 DAV:locktoken/DAV:href 的内容，不含 HTTP 头外层尖括号；缺失时为 null。</value>
    public string? LockToken { get; private set; }

    /// <summary>
    /// 获取锁所有者说明，可以是普通文本或绝对 URI。
    /// </summary>
    /// <value>null 表示未返回所有者；该信息不能替代身份认证。</value>
    public LockOwner? Owner { get; private set; }

    /// <summary>
    /// 获取 DAV:lockroot/DAV:href 标识的锁根资源。
    /// </summary>
    /// <value>保留服务器提供的 URI 形式，缺失时为 null。</value>
    public string? LockRoot { get; private set; }

    /// <summary>
    /// 获取服务器报告的锁剩余租约。
    /// </summary>
    /// <value>有限租约使用 TimeSpan；Infinite 使用 Timeout.InfiniteTimeSpan，缺失或无法识别时为 null。</value>
    public TimeSpan? Timeout { get; private set; }

    /// <summary>获取服务器报告的完整锁类型名称，例如 DAV:write。</summary>
    public XName? LockType { get; private set; }

    /// <summary>
    /// 收集活动锁属性并构造活动锁快照。
    /// </summary>
    /// <remarks>With 方法支持链式调用；Build 只组装数据，不访问服务器，也不验证锁是否仍有效。</remarks>
    public class Builder
    {
        /// <summary>
        /// 构建期间保存的锁的覆盖深度；0 仅当前资源，infinity 包含集合全部后代。
        /// </summary>
        /// <remarks>Build 将此值写入新快照；字段本身不触发 HTTP 操作。</remarks>
        private ApplyTo.Lock? _applyTo;
        /// <summary>
        /// 构建期间保存的服务器报告的共享或独占锁模式。
        /// </summary>
        /// <remarks>Build 将此值写入新快照；字段本身不触发 HTTP 操作。</remarks>
        private LockScope? _lockScope;
        /// <summary>
        /// 构建期间保存的用于续期、修改条件及解锁的锁令牌 URI。
        /// </summary>
        /// <remarks>Build 将此值写入新快照；字段本身不触发 HTTP 操作。</remarks>
        private string? _lockToken;
        /// <summary>
        /// 构建期间保存的锁所有者说明，可以是普通文本或绝对 URI。
        /// </summary>
        /// <remarks>Build 将此值写入新快照；字段本身不触发 HTTP 操作。</remarks>
        private LockOwner? _owner;
        /// <summary>
        /// 构建期间保存的 DAV:lockroot/DAV:href 标识的锁根资源。
        /// </summary>
        /// <remarks>Build 将此值写入新快照；字段本身不触发 HTTP 操作。</remarks>
        private string? _lockRoot;
        /// <summary>
        /// 构建期间保存的服务器报告的锁剩余租约。
        /// </summary>
        /// <remarks>Build 将此值写入新快照；字段本身不触发 HTTP 操作。</remarks>
        private TimeSpan? _timeout;

        /// <summary>构建期间保存的完整锁类型名称。</summary>
        private XName? _lockType;

        /// <summary>设置服务器报告的锁类型。</summary>
        /// <param name="lockType">完整 XML 类型名称，未提供时可为 null。</param>
        /// <returns>当前构建器。</returns>
        public Builder WithLockType(XName? lockType) { _lockType = lockType; return this; }

        /// <summary>
        /// 设置活动锁覆盖的资源深度。
        /// </summary>
        /// <param name="applyTo">服务器返回的锁深度；null 表示未知。</param>
        /// <returns>当前构建器，可继续链式设置其他属性。</returns>
        public Builder WithApplyTo(ApplyTo.Lock? applyTo)
        {
            _applyTo = applyTo;
            return this;
        }

        /// <summary>
        /// 设置活动锁的共享或独占模式。
        /// </summary>
        /// <param name="lockScope">服务器报告的锁模式；null 表示未知。</param>
        /// <returns>当前构建器，可继续链式设置其他属性。</returns>
        public Builder WithLockScope(LockScope? lockScope)
        {
            _lockScope = lockScope;
            return this;
        }

        /// <summary>
        /// 设置识别活动锁的令牌 URI。
        /// </summary>
        /// <param name="lockToken">DAV:locktoken/DAV:href 的文本，不应添加 HTTP 头的外层尖括号；null 表示缺失。</param>
        /// <returns>当前构建器，可继续链式设置其他属性。</returns>
        public Builder WithLockToken(string? lockToken)
        {
            _lockToken = lockToken;
            return this;
        }

        /// <summary>
        /// 设置服务器返回的锁所有者说明。
        /// </summary>
        /// <param name="owner">普通文本或 URI 形式的所有者；null 表示未提供。</param>
        /// <returns>当前构建器，可继续链式设置其他属性。</returns>
        public Builder WithOwner(LockOwner? owner)
        {
            _owner = owner;
            return this;
        }

        /// <summary>
        /// 设置活动锁所覆盖的根资源 URI。
        /// </summary>
        /// <param name="lockRoot">保留服务器 href 形式的锁根；null 表示未提供。</param>
        /// <returns>当前构建器，可继续链式设置其他属性。</returns>
        public Builder WithLockRoot(string? lockRoot)
        {
            _lockRoot = lockRoot;
            return this;
        }

        /// <summary>
        /// 设置服务器报告的活动锁租约。
        /// </summary>
        /// <param name="timeout">有限时长、Timeout.InfiniteTimeSpan 或未知时的 null。</param>
        /// <returns>当前构建器，可继续链式设置其他属性。</returns>
        public Builder WithTimeout(TimeSpan? timeout)
        {
            _timeout = timeout;
            return this;
        }

        /// <summary>
        /// 按当前构建状态创建新的ActiveLock数据快照。
        /// </summary>
        /// <remarks>保留所有未设置的可选值为 null；不验证租约或重新查询服务器。</remarks>
        /// <returns>包含当前构建值的新快照实例。</returns>
        public ActiveLock Build() => new()
        {
            ApplyTo = _applyTo,
            LockScope = _lockScope,
            LockToken = _lockToken,
            Owner = _owner,
            LockRoot = _lockRoot,
            Timeout = _timeout,
            LockType = _lockType
        };
    }
}
