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
/// 提供各 WebDAV 方法对应的 Depth 作用范围枚举。
/// </summary>
/// <remarks>PROPFIND 支持 0、1、infinity；COPY 和 LOCK 支持 0、infinity。</remarks>
public static class ApplyTo
{
    /// <summary>
    /// 指定 PROPFIND 查询当前资源、直接子项或全部后代的范围。
    /// </summary>
    public enum Propfind
    {
        /// <summary>
        /// 只查询当前资源，对应 Depth: 0。
        /// </summary>
        ResourceOnly,
        /// <summary>
        /// 查询当前资源和直接子项，对应 Depth: 1。
        /// </summary>
        ResourceAndChildren,
        /// <summary>
        /// 查询当前资源及全部后代，对应 Depth: infinity；该名称为历史兼容保留，实际不表示祖先目录。
        /// </summary>
        ResourceAndAncestors,
        /// <summary>
        /// 查询当前资源及全部后代，是 ResourceAndAncestors 的同值语义别名。
        /// </summary>
        ResourceAndDescendants = ResourceAndAncestors
    }

    /// <summary>
    /// 指定 COPY 仅复制集合本身，还是同时复制全部后代。
    /// </summary>
    public enum Copy
    {
        /// <summary>
        /// 只复制集合本身，对应 Depth: 0。
        /// </summary>
        ResourceOnly,
        /// <summary>
        /// 同时复制集合全部后代，对应 Depth: infinity；该历史名称实际不表示祖先目录。
        /// </summary>
        ResourceAndAncestors,
        /// <summary>
        /// 作用于当前资源及全部后代，是 ResourceAndAncestors 的同值语义别名。
        /// </summary>
        ResourceAndDescendants = ResourceAndAncestors
    }

    /// <summary>
    /// 指定 LOCK 仅锁定当前资源，还是覆盖集合的全部后代。
    /// </summary>
    public enum Lock
    {
        /// <summary>
        /// 只锁定当前资源，对应 Depth: 0。
        /// </summary>
        ResourceOnly,
        /// <summary>
        /// 锁同时覆盖集合全部后代，对应 Depth: infinity；该历史名称实际不表示祖先目录。
        /// </summary>
        ResourceAndAncestors,
        /// <summary>
        /// 作用于当前资源及全部后代，是 ResourceAndAncestors 的同值语义别名。
        /// </summary>
        ResourceAndDescendants = ResourceAndAncestors
    }
}
