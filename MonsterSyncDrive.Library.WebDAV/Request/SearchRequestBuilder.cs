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
using System.Globalization;
using System.Xml.Linq;

namespace MonsterSyncDrive.Library.WebDAV.Request;

/// <summary>把类型化搜索参数转换为 RFC 5323 的 select/from/where/orderby/limit XML。</summary>
internal static class SearchRequestBuilder
{
    /// <summary>构建完整搜索正文；普通字符串只作为文本进入 XML。</summary>
    /// <param name="parameters">待验证并转换为请求的搜索参数。</param>
    /// <returns>完整搜索请求根元素。</returns>
    public static XElement BuildRequest(SearchParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        parameters.AssertParametersAreValid();
        XElement root = new(WebDavXml.Dav + "searchrequest", new XAttribute(XNamespace.Xmlns + "D", "DAV:"));
        foreach (NamespaceAttr ns in parameters.Namespaces)
            root.SetAttributeValue(string.IsNullOrEmpty(ns.Prefix) ? "xmlns" : XNamespace.Xmlns + ns.Prefix, ns.Namespace);
        XElement query = new(WebDavXml.Dav + "basicsearch",
            new XElement(WebDavXml.Dav + "select", parameters.SelectProperties.Count == 0
                ? new XElement(WebDavXml.Dav + "allprop")
                : new XElement(WebDavXml.Dav + "prop", parameters.SelectProperties.Select(name => new XElement(name)))),
            BuildFrom(parameters));
        WebDavSearchExpression? filter = parameters.Filter ?? (parameters.MatchAll ? null
            : WebDavSearchExpression.Like(parameters.SearchProperty!, parameters.SearchKeyword!, parameters.Caseless));
        if (filter is not null)
            query.Add(new XElement(WebDavXml.Dav + "where", filter.ToXElement()));
        // DTD 顺序固定：where 后才是 orderby 与 limit，不能按参数赋值顺序随意排列。
        if (parameters.OrderBy.Count > 0)
            query.Add(new XElement(WebDavXml.Dav + "orderby", parameters.OrderBy.Select(order => order.ToXElement())));
        if (parameters.MaxResults.HasValue)
            query.Add(new XElement(WebDavXml.Dav + "limit", new XElement(WebDavXml.Dav + "nresults",
                parameters.MaxResults.Value.ToString(CultureInfo.InvariantCulture))));
        root.Add(query);
        return root;
    }

    /// <summary>生成一个或多个范围，供实际搜索和查询模式发现共用。</summary>
    /// <param name="parameters">提供范围配置的参数，不要求存在过滤条件。</param>
    /// <returns>DAV:from 元素。</returns>
    internal static XElement BuildFrom(SearchParameters parameters)
    {
        parameters.AssertScopesAreValid();
        IReadOnlyCollection<WebDavSearchScope> scopes = parameters.Scopes.Count > 0 ? parameters.Scopes
            : [new WebDavSearchScope(parameters.Scope, parameters.ScopeDepth, parameters.IncludeVersions)];
        return new XElement(WebDavXml.Dav + "from", scopes.Select(scope => scope.ToXElement()));
    }
}