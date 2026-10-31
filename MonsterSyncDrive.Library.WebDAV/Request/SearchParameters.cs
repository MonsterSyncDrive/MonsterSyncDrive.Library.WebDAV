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
using MonsterSyncDrive.WebDAV.Core;

namespace MonsterSyncDrive.WebDAV.Request;

/// <summary>RFC 5323 搜索参数，兼容旧的单属性 LIKE，同时支持完整标准表达式、多个范围及排序。</summary>
/// <remarks>高级 Filter 与旧 SearchProperty/SearchKeyword 不可混用；无过滤查询需要显式设置 MatchAll，防止遗漏条件误扫全库。</remarks>
public class SearchParameters : WebDavRequestOptions
{
    /// <summary>初始化独立集合，默认搜索 / 的全部后代。</summary>
    public SearchParameters()
    {
        Scope = "/";
        Namespaces = [];
        SelectProperties = [];
    }

    /// <summary>获取或设置旧式单范围的 URI；Scopes 非空时以 Scopes 为准。</summary>
    public string Scope { get; set; }

    /// <summary>获取或设置单范围深度，默认 infinity。</summary>
    public ApplyTo.Propfind ScopeDepth { get; set; } = ApplyTo.Propfind.ResourceAndDescendants;

    /// <summary>获取或设置单范围是否包含历史版本。</summary>
    public bool IncludeVersions { get; set; }

    /// <summary>获取或设置多个完整搜索范围；空集合使用 Scope、ScopeDepth 和 IncludeVersions。</summary>
    public IReadOnlyCollection<WebDavSearchScope> Scopes { get; set; } = [];

    /// <summary>获取或设置旧式 LIKE 的属性完整名称。</summary>
    public XName? SearchProperty { get; set; }

    /// <summary>获取或设置旧式 LIKE 的非空模式文本。</summary>
    public string? SearchKeyword { get; set; }

    /// <summary>获取或设置旧式 LIKE 是否忽略大小写；null 保留服务器默认。</summary>
    public bool? Caseless { get; set; }

    /// <summary>获取或设置完整的标准搜索表达式，与旧式 LIKE 参数互斥。</summary>
    public WebDavSearchExpression? Filter { get; set; }

    /// <summary>获取或设置是否显式请求不带 where 的全范围查询，不能同时指定过滤条件。</summary>
    public bool MatchAll { get; set; }

    /// <summary>获取或设置根元素的显式命名空间声明。</summary>
    public IReadOnlyCollection<NamespaceAttr> Namespaces { get; set; }

    /// <summary>获取或设置返回属性；空集合发送 allprop。</summary>
    public IReadOnlyCollection<XName> SelectProperties { get; set; }

    /// <summary>获取或设置按优先级排列的排序键，支持普通属性及相关性分数。</summary>
    public IReadOnlyCollection<WebDavSearchOrder> OrderBy { get; set; } = [];

    /// <summary>获取或设置请求的最大结果数；null 不限制，服务器按规范仍可忽略此建议。</summary>
    public ulong? MaxResults { get; set; }

    /// <summary>验证搜索条件、范围与各集合的一致性，不默默忽略冲突设置。</summary>
    /// <exception cref="ArgumentException">过滤条件缺失或互相冲突，或者集合包含无效元素。</exception>
    public void AssertParametersAreValid()
    {
        AssertScopesAreValid();
        ArgumentNullException.ThrowIfNull(Namespaces);
        ArgumentNullException.ThrowIfNull(SelectProperties);
        ArgumentNullException.ThrowIfNull(OrderBy);
        if (Namespaces.Any(value => value is null) || SelectProperties.Any(value => value is null) || OrderBy.Any(value => value is null))
            throw new ArgumentException("Search collections cannot contain null elements.");
        var hasLegacy = SearchProperty is not null || SearchKeyword is not null;
        if ((Filter is not null && (hasLegacy || MatchAll)) || (MatchAll && hasLegacy))
            throw new ArgumentException("Advanced, legacy and match-all conditions cannot be mixed.");
        if (Filter is not null || MatchAll)
            return;
        if (SearchProperty is null || string.IsNullOrEmpty(SearchKeyword))
            throw new ArgumentException("Set Filter, MatchAll, or both SearchProperty and SearchKeyword.");
    }

    /// <summary>验证搜索及 QSD 共用的范围设置。</summary>
    /// <remarks>QSD 没有 where，因此不能复用要求过滤条件的完整验证。</remarks>
    internal void AssertScopesAreValid()
    {
        ArgumentNullException.ThrowIfNull(Scopes);
        if (Scopes.Any(value => value is null))
            throw new ArgumentException("Search scopes cannot contain null elements.", nameof(Scopes));
        if (Scopes.Count == 0)
            _ = new WebDavSearchScope(Scope, ScopeDepth, IncludeVersions);
    }
}