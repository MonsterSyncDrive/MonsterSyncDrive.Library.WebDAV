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
using System.Runtime.CompilerServices;
using System.Xml;
using System.Xml.Linq;

namespace MonsterSyncDrive.Library.WebDAV;

/// <summary>
/// 提供按完整命名空间解析的 DAV XML 流式读取和资源转换方法。
/// </summary>
/// <remarks>禁用 DTD 及外部解析器，限制单个文档的字符总数；解析失败不退化为成功的空目录。</remarks>
public static class WebDavXml
{
    /// <summary>
    /// 标准 WebDAV XML 命名空间 DAV:；解析时同时匹配该命名空间与元素本地名称。
    /// </summary>
    public static readonly XNamespace Dav = "DAV:";

    /// <summary>
    /// 逐条读取 DAV:multistatus 的直接 response 子元素，并验证完整文档结束。
    /// </summary>
    /// <remarks>禁用 DTD 与外部实体；取消时关闭正文以打断无令牌的 XmlReader 等待。允许合法空集合，但错误根元素、截断或额外根节点会抛出异常。</remarks>
    /// <param name="response">包含多状态正文的 HTTP 响应；本方法读取并关闭正文流，响应对象仍由拥有者释放。</param>
    /// <param name="maxCharacters">本 XML 文档最多允许的字符数，必须为正数，默认 64 × 1024 × 1024。</param>
    /// <param name="cancellationToken">同时控制解析和取消时关闭正文的令牌。</param>
    /// <returns>按服务器顺序返回的完整 response 元素；完整枚举后才可确认尾部 XML 合法。</returns>
    /// <exception cref="System.IO.InvalidDataException">不是 DAV:multistatus，或文档未完整结束。</exception>
    /// <exception cref="System.Xml.XmlException">XML 格式错误、包含禁止的 DTD，或超过字符上限。</exception>
    /// <exception cref="OperationCanceledException">调用方取消了正文读取。</exception>
    public static async IAsyncEnumerable<XElement> ReadResponsesAsync(HttpResponseMessage response,
        long maxCharacters = 64 * 1024 * 1024,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxCharacters);
        // XmlReader 的部分异步方法没有 CancellationToken 参数。取消时关闭正文，使等待网络数据的读取也能退出。
        // 注册句柄在枚举器结束时注销；请求级超时另由响应正文包装器继续管理。
        using CancellationTokenRegistration abort = cancellationToken.UnsafeRegister(static value => ((HttpContent)value!).Dispose(), response.Content);
        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        // 同时禁止 DTD 和外部解析器，既不展开实体，也不允许协议响应读取其他文件或网络资源。
        // 字符上限作用于整个文档，而非单条 response，避免通过大量小条目绕过限制。
        using XmlReader reader = XmlReader.Create(stream, new XmlReaderSettings
        {
            Async = true,
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            IgnoreComments = true,
            IgnoreProcessingInstructions = true,
            MaxCharactersInDocument = maxCharacters
        });
        if (await ReadAsync(reader.MoveToContentAsync, cancellationToken).ConfigureAwait(false) != XmlNodeType.Element
            || reader.LocalName != "multistatus" || reader.NamespaceURI != Dav.NamespaceName)
            throw new InvalidDataException("Expected a DAV:multistatus document.");
        var depth = reader.Depth;
        // 自闭合的 multistatus 是合法空集合；非空根节点则必须实际读到相应的结束标签。
        var closed = reader.IsEmptyElement;
        while (await ReadAsync(reader.ReadAsync, cancellationToken).ConfigureAwait(false))
        {
            if (reader.NodeType == XmlNodeType.EndElement && reader.Depth == depth)
            {
                closed = true;
                break;
            }
            if (reader.NodeType != XmlNodeType.Element || reader.Depth != depth + 1
                || reader.LocalName != "response" || reader.NamespaceURI != Dav.NamespaceName)
                continue;
            XElement element;
            // ReadSubtree 只消耗当前 response，关闭子读取器后主读取器停在该元素末尾。
            // 下一轮只执行一次 ReadAsync，就不会跳过紧邻的下一条 response；内存中也只物化当前条目。
            using (XmlReader subtree = reader.ReadSubtree())
                element = await ReadAsync(() => XElement.LoadAsync(subtree, LoadOptions.PreserveWhitespace, cancellationToken), cancellationToken).ConfigureAwait(false);
            yield return element;
        }
        if (!closed)
            throw new InvalidDataException("Incomplete DAV:multistatus document.");
        // 读到根结束标签还不够：继续消费文档尾部，额外根节点或非法尾随内容仍必须被解析器报告。
        while (await ReadAsync(reader.ReadAsync, cancellationToken).ConfigureAwait(false))
        {
        }
        cancellationToken.ThrowIfCancellationRequested();
    }

    /// <summary>
    /// 解析 DAV:status 中的 HTTP 状态行，校验状态前缀与三位状态码范围。
    /// </summary>
    /// <param name="element">包含 HTTP 状态行的元素；null 或无效内容均视为协议错误。</param>
    /// <returns>范围为 100 至 599 的整数状态码。</returns>
    /// <exception cref="System.IO.InvalidDataException">缺少状态行、没有 HTTP/ 前缀，或状态码不是有效范围内的整数。</exception>
    public static int ReadStatus(XElement? element)
    {
        var parts = element?.Value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return parts is not { Length: >= 2 } || !parts[0].StartsWith("HTTP/", StringComparison.OrdinalIgnoreCase)
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var code) || code is < 100 or > 599
            ? throw new InvalidDataException("Invalid or missing DAV HTTP status line.")
            : code;
    }

    /// <summary>
    /// 把一个 DAV:response 转换为资源快照，区分成功属性与全部属性状态。
    /// </summary>
    /// <remarks>仅成功 propstat 的属性参与元数据解析，外部命名空间的同名属性不会冒充 DAV 属性；无效长度或日期保留未知，日期按固定区域转换为 UTC。</remarks>
    /// <param name="response">名称必须为 DAV:response 的完整资源元素。</param>
    /// <returns>保留原始 href、资源级状态及成功属性的 WebDavResource。</returns>
    /// <exception cref="ArgumentNullException">response 为 null。</exception>
    /// <exception cref="System.IO.InvalidDataException">元素、href、状态行或 propstat 结构不合法。</exception>
    public static WebDavResource ParseResource(XElement response)
    {
        ArgumentNullException.ThrowIfNull(response);
        if (response.Name != Dav + "response")
            throw new InvalidDataException("Expected DAV:response.");
        var href = response.Element(Dav + "href")?.Value;
        if (string.IsNullOrWhiteSpace(href))
            throw new InvalidDataException("DAV resource has no href.");
        XElement? direct = response.Element(Dav + "status");
        Dictionary<XName, XElement> values = [];
        List<WebDavPropertyStatus> statuses = [];
        XElement[] propstats = response.Elements(Dav + "propstat").ToArray();
        if (direct is null && propstats.Length == 0)
            throw new InvalidDataException("DAV resource has neither status nor propstat.");
        foreach (XElement? propstat in propstats)
        {
            var code = ReadStatus(propstat.Element(Dav + "status"));
            var description = propstat.Element(Dav + "responsedescription")?.Value;
            XElement prop = propstat.Element(Dav + "prop") ?? throw new InvalidDataException("DAV propstat has no prop.");
            // 同一 propstat 中的属性共享不可变错误快照，避免逐属性重复复制 XML。
            // 同时兼容旧版 DeltaV 的 responsedescription/error 与 DAV 的同级 error。
            WebDavError? error = ParseError(propstat.Element(Dav + "error")
                ?? propstat.Element(Dav + "responsedescription")?.Element(Dav + "error"));
            foreach (XElement property in prop.Elements())
            {
                // 所有属性都保留状态，但只有 2xx 属性进入元数据字典；不能把失败的空长度或类型当成有效数据。
                statuses.Add(new WebDavPropertyStatus(property.Name, code, description)
                {
                    Error = error
                });
                if (code is >= 200 and < 300)
                    _ = values.TryAdd(property.Name, property);
            }
        }
        // 查找标准属性时始终使用完整 DAV: 名称，避免扩展命名空间中的同名属性污染长度、类型或 ETag。
        // 构造 WebDavProperty 时复制完整元素，分别保留内层 XML 和元素自身的属性，支持可靠往返。
        WebDavResource.Builder builder = new WebDavResource.Builder().WithUri(href)
            .WithContentLength(Long("getcontentlength")).WithContentLanguage(Text("getcontentlanguage"))
            .WithContentType(Text("getcontenttype")).WithDisplayName(Text("displayname"))
            .WithCreationDate(Date("creationdate")).WithLastModifiedDate(Date("getlastmodified"))
            .WithETag(Text("getetag"))
            .WithActiveLocks(ParseLockDiscovery(Get("lockdiscovery")))
            .WithProperties(values.Values.Select(value => new WebDavProperty(value)).ToArray())
            .WithPropertyStatuses(statuses);
        if (Get("resourcetype")?.Element(Dav + "collection") is not null || Text("iscollection") is "1" or "true")
            _ = builder.IsCollection();
        if (Text("ishidden") is "1" or "true")
            _ = builder.IsHidden();
        WebDavResource resource = builder.Build();
        resource.StatusCode = direct is null ? null : ReadStatus(direct);
        resource.ResponseDescription = response.Element(Dav + "responsedescription")?.Value;
        resource.Error = ParseError(response.Element(Dav + "error")
            ?? response.Element(Dav + "responsedescription")?.Element(Dav + "error"));
        resource.RedirectLocation = response.Element(Dav + "location")?.Element(Dav + "href")?.Value;
        resource.SupportedLocks = ParseSupportedLocks(Get("supportedlock"));
        return resource;

        // 只在成功属性中查找标准 DAV 元素，名称与外部命名空间严格区分。
        XElement? Get(string name)
        {
            return values.GetValueOrDefault(Dav + name);
        }
        // 标量文本使用 XElement.Value 解码，完整 XML 副本则由 Properties 单独保存。
        string? Text(string name)
        {
            return Get(name)?.Value;
        }
        // 长度必须是非负整数；缺失或无效时保留未知，不能悄悄改成零字节。
        long? Long(string name)
        {
            return long.TryParse(Text(name), NumberStyles.None, CultureInfo.InvariantCulture, out var value) && value >= 0 ? value : null;
        }
        // HTTP 日期与 ISO 日期统一按固定区域解析为 UTC，避免当前系统语言或时区改变同步判断。
        DateTime? Date(string name)
        {
            return DateTimeOffset.TryParse(Text(name), CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTimeOffset value) ? value.UtcDateTime : null;
        }
    }

    /// <summary>
    /// 解析 DAV:lockdiscovery 内的活动写锁，包括范围、令牌、所有者、根 URI 和租约。
    /// </summary>
    /// <remarks>简单所有者使用文本或 URI 模型，复杂 owner 保留全部 XML。Infinite 与未知租约分别保存为 Timeout.InfiniteTimeSpan 和 null。</remarks>
    /// <param name="discovery">可选的 DAV:lockdiscovery 元素，不按本地名称接受外部命名空间的 activelock。</param>
    /// <returns>已解析的活动锁集合；缺少 lockdiscovery 时返回空集合。</returns>
    public static IReadOnlyCollection<ActiveLock> ParseLockDiscovery(XElement? discovery)
    {
        return discovery is null
            ? []
            : discovery.Name != Dav + "lockdiscovery"
            ? throw new InvalidDataException("Expected DAV:lockdiscovery.")
            : (IReadOnlyCollection<ActiveLock>)discovery.Elements(Dav + "activelock").Select(element =>
        {
            XElement? owner = element.Element(Dav + "owner");
            XElement? scope = element.Element(Dav + "lockscope");
            var timeout = element.Element(Dav + "timeout")?.Value;
            // “无限租约”和“未提供租约”是不同状态；前者用 -1 毫秒的标准常量，后者保持 null。
            TimeSpan? duration = timeout?.Equals("Infinite", StringComparison.OrdinalIgnoreCase) == true
                ? System.Threading.Timeout.InfiniteTimeSpan : null;
            if (timeout?.StartsWith("Second-", StringComparison.OrdinalIgnoreCase) == true
                && uint.TryParse(timeout.AsSpan(7), NumberStyles.None, CultureInfo.InvariantCulture, out var seconds))
                duration = TimeSpan.FromSeconds(seconds);
            // locktoken 与 lockroot 的值来自各自的 DAV:href，不是简单按本地名称抓取任意后代文本。
            // owner 的 href 只有在合法绝对 URI 时才建立 UriLockOwner，否则保留其普通文本说明。
            return new ActiveLock.Builder()
                .WithApplyTo(element.Element(Dav + "depth")?.Value switch
                {
                    "0" => ApplyTo.Lock.ResourceOnly,
                    "infinity" => ApplyTo.Lock.ResourceAndAncestors,
                    _ => null
                })
                .WithLockScope(scope?.Element(Dav + "exclusive") is not null ? LockScope.Exclusive
                    : scope?.Element(Dav + "shared") is not null ? LockScope.Shared : null)
                .WithLockToken(element.Element(Dav + "locktoken")?.Element(Dav + "href")?.Value)
                .WithLockRoot(element.Element(Dav + "lockroot")?.Element(Dav + "href")?.Value)
                .WithOwner(ParseLockOwner(owner))
                .WithLockType(element.Element(Dav + "locktype")?.Elements().FirstOrDefault()?.Name)
                .WithTimeout(duration).Build();
        }).ToArray();
    }

    /// <summary>解析 supportedlock 中全部锁能力条目，不丢弃未来的锁类型扩展。</summary>
    /// <param name="supportedLock">DAV:supportedlock 元素，缺失时为 null。</param>
    /// <returns>服务器声明的共享、独占及其他锁能力条目。</returns>
    public static IReadOnlyCollection<SupportedLock> ParseSupportedLocks(XElement? supportedLock)
    {
        return supportedLock is null
            ? Array.Empty<SupportedLock>()
            : supportedLock.Name != Dav + "supportedlock"
            ? throw new InvalidDataException("Expected DAV:supportedlock.")
            : (IReadOnlyCollection<SupportedLock>)Array.AsReadOnly(supportedLock.Elements(Dav + "lockentry").Select(element => new SupportedLock(element)).ToArray());
    }

    /// <summary>将 DAV:owner 转换为普通文本、URI 或完整 XML 模型。</summary>
    /// <param name="owner">完整 DAV:owner 元素，缺失时为 null。</param>
    /// <returns>保留原有信息的所有者模型，或者 null。</returns>
    public static LockOwner? ParseLockOwner(XElement? owner)
    {
        if (owner is null)
            return null;
        if (owner.Name != Dav + "owner")
            throw new InvalidDataException("Expected DAV:owner.");
        if (!owner.HasAttributes && !owner.HasElements && owner.Nodes().All(node => node is XText))
            return new PrincipalLockOwner(owner.Value);
        XElement[] children = owner.Elements().ToArray();
        XElement? href = children.Length == 1 && children[0].Name == Dav + "href" ? children[0] : null;
        // 仅简单 href 允许简化为 UriLockOwner；任何属性、混合文本或扩展节点都必须保留完整 owner。
        return !owner.HasAttributes && href is not null && !href.HasAttributes && !href.HasElements
            && owner.Nodes().Where(node => node is not XElement).All(node => node is XText text && string.IsNullOrWhiteSpace(text.Value))
            && Uri.IsWellFormedUriString(href.Value, UriKind.Absolute)
            ? new UriLockOwner(href.Value)
            : new XmlLockOwner(owner);
    }

    /// <summary>读取完整 DAV:error 条件，不按本地名称误收其他命名空间的同名元素。</summary>
    /// <param name="element">DAV:error 元素，未提供时为 null。</param>
    /// <returns>完整错误模型；null 输入返回 null。</returns>
    /// <exception cref="InvalidDataException">非空元素不是 DAV:error。</exception>
    public static WebDavError? ParseError(XElement? element)
    {
        return element is null
            ? null
            : element.Name != Dav + "error" ? throw new InvalidDataException("Expected DAV:error.") : new WebDavError(element);
    }

    /// <summary>安全读取 HTTP 失败正文中可能存在的结构化 DAV 错误。</summary>
    /// <param name="response">包含错误正文的原始 HTTP 响应，响应所有权仍由调用方管理。</param>
    /// <param name="maxCharacters">错误文档字符上限，默认 64 × 1024 × 1024。</param>
    /// <param name="cancellationToken">取消正文读取的令牌。</param>
    /// <returns>DAV:error 模型；无 XML 正文、HTML 错误页或非 DAV 错误时返回 null。</returns>
    /// <remarks>失败响应的格式错误或超限不会掩盖原始 HTTP 状态，此时不提供错误详情；传输及取消仍按异常报告。</remarks>
    public static async Task<WebDavError?> ReadErrorAsync(HttpResponseMessage response,
        long maxCharacters = 64 * 1024 * 1024, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxCharacters);
        var mediaType = response.Content.Headers.ContentType?.MediaType;
        if (response.Content.Headers.ContentLength == 0 || (mediaType is not null
            && !mediaType.Equals("application/xml", StringComparison.OrdinalIgnoreCase)
            && !mediaType.Equals("text/xml", StringComparison.OrdinalIgnoreCase)
            && !mediaType.EndsWith("+xml", StringComparison.OrdinalIgnoreCase)))
            return null;
        try
        {
            XElement root = await ReadDocumentAsync(response, maxCharacters, cancellationToken).ConfigureAwait(false);
            return root.Name == Dav + "error" ? ParseError(root) : ParseError(root.Element(Dav + "error"));
        }
        catch (XmlException) when (!response.IsSuccessStatusCode)
        {
            // 禁用 DTD 的读取器不会执行外部访问；无效错误 XML 可忽略，使原始 4xx/5xx 仍然可检查。
            return null;
        }
    }

    /// <summary>
    /// 在禁用 DTD 和外部解析器的条件下，完整读取单个 XML 响应文档。
    /// </summary>
    /// <param name="response">包含 XML 正文的 HTTP 响应。</param>
    /// <param name="maxCharacters">整个 XML 文档的字符总数上限。</param>
    /// <param name="token">控制正文读取，并在取消时关闭原正文的令牌。</param>
    /// <returns>已完整加载的文档根元素，用于普通 LOCK 的 DAV:prop 解析。</returns>
    internal static async Task<XElement> ReadDocumentAsync(HttpResponseMessage response, long maxCharacters, CancellationToken token)
    {
        using CancellationTokenRegistration abort = token.UnsafeRegister(static value => ((HttpContent)value!).Dispose(), response.Content);
        await using Stream stream = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
        using XmlReader reader = XmlReader.Create(stream, new XmlReaderSettings
        {
            Async = true,
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = maxCharacters
        });
        XDocument document = await ReadAsync(() => XDocument.LoadAsync(reader, LoadOptions.PreserveWhitespace, token), token).ConfigureAwait(false);
        return document.Root ?? throw new InvalidDataException("Missing XML document element.");
    }

    /// <summary>
    /// 等待 XML 异步读取真正结束，统一正文关闭引发的取消异常。
    /// </summary>
    /// <remarks>不使用 WaitAsync 提前放弃仍在进行的 XmlReader 操作，以免随后 Dispose 触发异步操作重入检查；取消靠关闭正文打断底层读取。</remarks>
    /// <param name="read">实际启动且必须被等待完成的 XML 读取委托。</param>
    /// <param name="token">检查及恢复取消语义的令牌。</param>
    /// <typeparam name="T">底层异步 XML 读取操作的结果类型。</typeparam>
    /// <returns>底层 XML 读取完成后的结果。</returns>
    private static async Task<T> ReadAsync<T>(Func<Task<T>> read, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        // 必须等待底层读取真正退出后才能释放 XmlReader。若用 WaitAsync 提前返回，Dispose 时可能仍有读取进行，
        // 从而抛出“异步操作正在进行”的异常并掩盖取消；关闭正文后等待原任务结束，再恢复取消语义。
        try
        {
            return await read().ConfigureAwait(false);
        }
        catch (Exception) when (token.IsCancellationRequested)
        {
            throw new OperationCanceledException(token);
        }
    }
}
