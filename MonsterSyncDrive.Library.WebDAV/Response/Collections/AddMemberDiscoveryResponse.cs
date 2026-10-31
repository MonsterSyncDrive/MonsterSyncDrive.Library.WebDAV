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
using MonsterSyncDrive.Library.WebDAV.Core;

namespace MonsterSyncDrive.Library.WebDAV.Response.Collections;

/// <summary>保存集合 Add-Member 能力发现的 HTTP 状态及经过同源校验的上传端点。</summary>
public sealed class AddMemberDiscoveryResponse : PropfindResponse
{
    /// <summary>创建能力发现结果。</summary>
    /// <param name="statusCode">外层 HTTP 状态。</param>
    /// <param name="description">HTTP 原因短语。</param>
    /// <param name="collectionUri">查询的集合绝对 URI。</param>
    /// <param name="addMemberUri">经过校验的 Add-Member URI；null 表示属性未成功提供。</param>
    /// <param name="resources">发现查询的资源条目，保留资源级与属性级失败。</param>
    public AddMemberDiscoveryResponse(int statusCode, string? description, Uri collectionUri, Uri? addMemberUri,
        IEnumerable<WebDavResource>? resources = null) : base(statusCode, description, resources ?? [])
    {
        CollectionUri = collectionUri;
        AddMemberUri = addMemberUri;
    }

    /// <summary>获取查询的集合绝对 URI。</summary>
    public Uri CollectionUri
    {
        get;
    }

    /// <summary>获取同源 Add-Member URI；null 表示当前响应未提供可用端点。</summary>
    public Uri? AddMemberUri
    {
        get;
    }

    /// <summary>获取响应是否成功提供了可用的创建成员端点。</summary>
    public bool IsSupported => IsSuccessful && AddMemberUri is not null;
}
