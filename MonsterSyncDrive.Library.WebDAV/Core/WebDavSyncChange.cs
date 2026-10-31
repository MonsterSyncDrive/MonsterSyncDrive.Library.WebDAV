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
/// 描述 RFC 6578 中一个成员 URL 的同步结果类别。
/// </summary>
public enum WebDavSyncChangeKind
{
    /// <summary>资源新增或内容/属性发生变化；协议本身不区分新增与修改。</summary>
    AddedOrModified,

    /// <summary>该成员 URL 自上次令牌后已移除，对应资源级 404。</summary>
    Deleted,

    /// <summary>子集合不能在当前同步范围中遍历，需要调用方另行同步或发现。</summary>
    UnsupportedCollection,

    /// <summary>服务器为该成员返回了其他资源级错误。</summary>
    Failed
}

/// <summary>
/// 保留一个同步成员的 URI、变更分类以及完整资源与属性状态。
/// </summary>
public sealed class WebDavSyncChange
{
    /// <summary>
    /// 根据资源快照创建已分类的同步条目。
    /// </summary>
    /// <param name="resource">已经解析的成员资源，不应为 null。</param>
    /// <param name="resolvedUri">相对于报告请求解析后的绝对成员 URI。</param>
    /// <param name="kind">由同步协议形态判断的结果类别。</param>
    internal WebDavSyncChange(WebDavResource resource, Uri resolvedUri, WebDavSyncChangeKind kind)
    {
        Resource = resource;
        ResolvedUri = resolvedUri;
        Kind = kind;
    }

    /// <summary>获取服务器返回的完整资源和逐属性结果。</summary>
    public WebDavResource Resource
    {
        get;
    }

    /// <summary>获取经过 URI 规则解析的绝对成员地址，不使用本地文件路径规则。</summary>
    public Uri ResolvedUri
    {
        get;
    }

    /// <summary>获取新增或修改、删除、不可遍历或失败的分类。</summary>
    public WebDavSyncChangeKind Kind
    {
        get;
    }

    /// <summary>获取此条目是否为协议定义的删除记录。</summary>
    public bool IsDeleted => Kind == WebDavSyncChangeKind.Deleted;

    /// <summary>获取此条目是否需要额外恢复或遍历处理，而不能作为完整同步成功提交。</summary>
    public bool IsFailure => Kind is WebDavSyncChangeKind.UnsupportedCollection or WebDavSyncChangeKind.Failed;
}

