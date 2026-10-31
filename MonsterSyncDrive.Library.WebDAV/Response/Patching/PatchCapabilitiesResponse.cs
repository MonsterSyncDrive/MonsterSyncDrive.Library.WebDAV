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
using MonsterSyncDrive.Library.WebDAV.Helpers;
using System.Net.Http.Headers;

namespace MonsterSyncDrive.Library.WebDAV.Response.Patching;

/// <summary>保存 OPTIONS 返回的 PATCH 能力及支持的补丁媒体类型。</summary>
public sealed class PatchCapabilitiesResponse : WebDavResponse
{
    /// <summary>从已有 OPTIONS 响应创建能力快照。</summary>
    /// <param name="response">包含 Allow 和可选 Accept-Patch 的响应。</param>
    public PatchCapabilitiesResponse(WebDavResponse response)
        : base((response ?? throw new ArgumentNullException(nameof(response))).StatusCode, response.Description)
    {
        ArgumentNullException.ThrowIfNull(response);
        Headers = response.Headers;
        Error = response.Error;
        AllowedMethods = (GetHeader("Allow") ?? "").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        string? accept = GetHeader("Accept-Patch");
        AcceptedMediaTypes = accept is null ? [] : PreferenceHeaderParser.Split(accept, ',', allowEmptyItems: true)
            .Select(MediaTypeHeaderValue.Parse).ToArray();
    }

    /// <summary>获取服务器允许的方法名称集合。</summary>
    public IReadOnlyCollection<string> AllowedMethods
    {
        get;
    }

    /// <summary>获取服务器声明支持的补丁格式；没有 Accept-Patch 时为空集合。</summary>
    public IReadOnlyCollection<MediaTypeHeaderValue> AcceptedMediaTypes
    {
        get;
    }

    /// <summary>获取 Allow 包含 PATCH 或存在 Accept-Patch 声明时的 PATCH 支持状态。</summary>
    public bool SupportsPatch => AcceptedMediaTypes.Count > 0 || AllowedMethods.Contains("PATCH", StringComparer.OrdinalIgnoreCase);

    /// <summary>判断服务器是否声明接受指定的补丁媒体类型，忽略媒体类型名称大小写。</summary>
    /// <param name="mediaType">待检查的完整媒体类型。</param>
    /// <returns>Accept-Patch 中存在匹配的主类型和子类型时为 true。</returns>
    public bool Accepts(string mediaType)
    {
        MediaTypeHeaderValue parsed = MediaTypeHeaderValue.Parse(mediaType);
        return AcceptedMediaTypes.Any(value => string.Equals(value.MediaType, parsed.MediaType, StringComparison.OrdinalIgnoreCase));
    }
}
