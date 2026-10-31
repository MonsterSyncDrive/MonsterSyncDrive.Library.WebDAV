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
/// 表示 DAV:response 中的资源信息及属性快照。
/// </summary>
/// <remarks>Uri 保留服务器 href 的原始形式；成功属性与全部属性状态分开保存，未知长度和日期保持 null，不自动当作零字节或默认日期。</remarks>
public class WebDavResource
{
    /// <summary>
    /// 创建供资源构建器使用的空快照，并初始化属性与属性状态为空集合。
    /// </summary>
    private WebDavResource()
    {
        Properties = [];
        PropertyStatuses = [];
    }

    /// <summary>
    /// 获取从 DAV:lockdiscovery 解析的活动锁集合。
    /// </summary>
    /// <value>解析器未发现活动锁时返回空集合；手动构建且未设置集合时可为 null。</value>
    public IReadOnlyCollection<ActiveLock>? ActiveLocks
    {
        get; private set;
    }

    /// <summary>
    /// 获取 DAV:getcontentlanguage 指定的内容语言。
    /// </summary>
    /// <value>缺失时为 null。</value>
    public string? ContentLanguage
    {
        get; private set;
    }

    /// <summary>
    /// 获取 DAV:getcontentlength 指定的非负文件字节数。
    /// </summary>
    /// <value>无法取得有效长度时为 null，不能将其当作已知的零字节文件。</value>
    public long? ContentLength
    {
        get; private set;
    }

    /// <summary>
    /// 获取 DAV:getcontenttype 提供的内容媒体类型。
    /// </summary>
    /// <value>缺失时为 null。</value>
    public string? ContentType
    {
        get; private set;
    }

    /// <summary>
    /// 获取 DAV:creationdate 指定的创建时间。
    /// </summary>
    /// <value>协议解析时转换为 UTC；缺失或无效时为 null，手动构建则保留调用方传入值。</value>
    public DateTime? CreationDate
    {
        get; private set;
    }

    /// <summary>
    /// 获取 DAV:displayname 指定的显示名称。
    /// </summary>
    /// <value>显示标题不一定是实际文件名，不能据此替代 href 定位资源。</value>
    public string? DisplayName
    {
        get; private set;
    }

    /// <summary>
    /// 获取 DAV:getetag 提供的服务器实体标签，保留弱标记和引号。
    /// </summary>
    /// <value>缺失时为 null；条件请求应使用服务器标签，不应自行移除引号或转换为本地文件标识。</value>
    public string? ETag
    {
        get; private set;
    }

    /// <summary>
    /// 获取服务器 DAV:href 的原始 URI 引用。
    /// </summary>
    /// <value>可能是绝对地址、服务器根路径或相对引用；调用方按原请求 URI 解析，不使用本地 Path 规则。</value>
    public string? Uri
    {
        get; private set;
    }

    /// <summary>
    /// 获取当前资源是否被识别为 DAV 集合。
    /// </summary>
    /// <value>DAV:resourcetype 中的 collection 或兼容属性 iscollection 表示集合；默认 false。</value>
    public bool IsCollection
    {
        get; private set;
    }

    /// <summary>
    /// 获取兼容扩展 DAV:ishidden 是否将资源标为隐藏。
    /// </summary>
    /// <value>支持文本 1 或 true；缺失或其他值时为 false。</value>
    public bool IsHidden
    {
        get; private set;
    }

    /// <summary>
    /// 获取 DAV:getlastmodified 指定的最后修改时间。
    /// </summary>
    /// <value>协议解析时按固定区域转换为 UTC；缺失或无效时为 null。</value>
    public DateTime? LastModifiedDate
    {
        get; private set;
    }

    /// <summary>
    /// 获取状态为 HTTP 2xx 的完整属性值集合。
    /// </summary>
    /// <value>未设置时为空集合；失败属性只保留在 PropertyStatuses 中。</value>
    public IReadOnlyCollection<WebDavProperty> Properties
    {
        get; private set;
    }

    /// <summary>
    /// 获取全部 DAV:propstat 中逐属性的状态，包括失败属性。
    /// </summary>
    /// <value>未设置时为空集合；可选属性 404 应由使用者根据业务需求判断。</value>
    public IReadOnlyCollection<WebDavPropertyStatus> PropertyStatuses
    {
        get; private set;
    }

    /// <summary>
    /// 获取资源级 DAV:status，与逐属性状态区分。
    /// </summary>
    /// <value>使用 propstat 表达属性结果且未返回资源级状态时为 null。</value>
    public int? StatusCode
    {
        get; internal set;
    }
    /// <summary>
    /// 获取资源级 DAV:responsedescription 的说明文本。
    /// </summary>
    /// <value>未提供时为 null。</value>
    public string? ResponseDescription
    {
        get; internal set;
    }

    /// <summary>获取 DAV:location/DAV:href 指示的新地址，保留服务器提供的绝对或相对 URI 形式。</summary>
    public string? RedirectLocation
    {
        get; internal set;
    }

    /// <summary>获取资源级 DAV:error 的完整前置或后置条件，未提供时为 null。</summary>
    public WebDavError? Error
    {
        get; internal set;
    }

    /// <summary>获取服务器通过 DAV:supportedlock 声明的锁能力条目。</summary>
    public IReadOnlyCollection<SupportedLock> SupportedLocks { get; internal set; } = [];

    /// <summary>
    /// 分步组装资源快照，保留未知属性与空集合的区别。
    /// </summary>
    /// <remarks>Build 创建新对象，但不深复制传入集合；构建后不要继续修改作为快照数据使用的集合。</remarks>
    public class Builder
    {
        /// <summary>
        /// 构建期间保存的从 DAV:lockdiscovery 解析的活动锁集合。
        /// </summary>
        /// <remarks>Build 将此值写入新快照；字段本身不触发 HTTP 操作。</remarks>
        private IReadOnlyCollection<ActiveLock>? _activeLocks;
        /// <summary>
        /// 构建期间保存的 DAV:getcontentlanguage 指定的内容语言。
        /// </summary>
        /// <remarks>Build 将此值写入新快照；字段本身不触发 HTTP 操作。</remarks>
        private string? _contentLanguage;
        /// <summary>
        /// 构建期间保存的 DAV:getcontentlength 指定的非负文件字节数。
        /// </summary>
        /// <remarks>Build 将此值写入新快照；字段本身不触发 HTTP 操作。</remarks>
        private long? _contentLength;
        /// <summary>
        /// 构建期间保存的 DAV:getcontenttype 提供的内容媒体类型。
        /// </summary>
        /// <remarks>Build 将此值写入新快照；字段本身不触发 HTTP 操作。</remarks>
        private string? _contentType;
        /// <summary>
        /// 构建期间保存的 DAV:creationdate 指定的创建时间。
        /// </summary>
        /// <remarks>Build 将此值写入新快照；字段本身不触发 HTTP 操作。</remarks>
        private DateTime? _creationDate;
        /// <summary>
        /// 构建期间保存的 DAV:displayname 指定的显示名称。
        /// </summary>
        /// <remarks>Build 将此值写入新快照；字段本身不触发 HTTP 操作。</remarks>
        private string? _displayName;
        /// <summary>
        /// 构建期间保存的 DAV:getetag 提供的服务器实体标签，保留弱标记和引号。
        /// </summary>
        /// <remarks>Build 将此值写入新快照；字段本身不触发 HTTP 操作。</remarks>
        private string? _eTag;
        /// <summary>
        /// 构建期间保存的服务器 DAV:href 的原始 URI 引用。
        /// </summary>
        /// <remarks>Build 将此值写入新快照；字段本身不触发 HTTP 操作。</remarks>
        private string? _uri;
        /// <summary>
        /// 构建期间保存的当前资源是否被识别为 DAV 集合。
        /// </summary>
        /// <remarks>Build 将此值写入新快照；字段本身不触发 HTTP 操作。</remarks>
        private bool _isCollection;
        /// <summary>
        /// 构建期间保存的兼容扩展 DAV:ishidden 是否将资源标为隐藏。
        /// </summary>
        /// <remarks>Build 将此值写入新快照；字段本身不触发 HTTP 操作。</remarks>
        private bool _isHidden;
        /// <summary>
        /// 构建期间保存的 DAV:getlastmodified 指定的最后修改时间。
        /// </summary>
        /// <remarks>Build 将此值写入新快照；字段本身不触发 HTTP 操作。</remarks>
        private DateTime? _lastModifiedDate;
        /// <summary>
        /// 构建期间保存的状态为 HTTP 2xx 的完整属性值集合。
        /// </summary>
        /// <remarks>Build 将此值写入新快照；字段本身不触发 HTTP 操作。</remarks>
        private IReadOnlyCollection<WebDavProperty>? _properties;
        /// <summary>
        /// 构建期间保存的全部 DAV:propstat 中逐属性的状态，包括失败属性。
        /// </summary>
        /// <remarks>Build 将此值写入新快照；字段本身不触发 HTTP 操作。</remarks>
        private IReadOnlyCollection<WebDavPropertyStatus>? _propertyStatuses;

        /// <summary>
        /// 设置资源的活动锁集合。
        /// </summary>
        /// <param name="activeLocks">已解析的活动锁集合；无活动锁时传入空集合。</param>
        /// <returns>当前构建器，可继续链式设置其他属性。</returns>
        public Builder WithActiveLocks(IReadOnlyCollection<ActiveLock> activeLocks)
        {
            _activeLocks = activeLocks;
            return this;
        }

        /// <summary>
        /// 设置资源内容的语言标识。
        /// </summary>
        /// <param name="contentLanguage">DAV:getcontentlanguage 的值；未提供时可为 null。</param>
        /// <returns>当前构建器，可继续链式设置其他属性。</returns>
        public Builder WithContentLanguage(string? contentLanguage)
        {
            _contentLanguage = contentLanguage;
            return this;
        }

        /// <summary>
        /// 设置资源的已知字节数，保留未知长度。
        /// </summary>
        /// <param name="contentLength">已知的内容长度；null 表示未知，构建器自身不校验数值范围。</param>
        /// <returns>当前构建器，可继续链式设置其他属性。</returns>
        public Builder WithContentLength(long? contentLength)
        {
            _contentLength = contentLength;
            return this;
        }

        /// <summary>
        /// 设置资源的内容媒体类型。
        /// </summary>
        /// <param name="contentType">DAV:getcontenttype 的值；未提供时可为 null。</param>
        /// <returns>当前构建器，可继续链式设置其他属性。</returns>
        public Builder WithContentType(string? contentType)
        {
            _contentType = contentType;
            return this;
        }

        /// <summary>
        /// 设置资源的创建时间。
        /// </summary>
        /// <param name="creationDate">解析器提供的 UTC 创建时间，或手动构建时希望保留的时间；未知时为 null。</param>
        /// <returns>当前构建器，可继续链式设置其他属性。</returns>
        public Builder WithCreationDate(DateTime? creationDate)
        {
            _creationDate = creationDate;
            return this;
        }

        /// <summary>
        /// 设置资源的服务器显示名称。
        /// </summary>
        /// <param name="displayName">显示标题；null 表示未提供，不应以此替代真实 href 定位文件。</param>
        /// <returns>当前构建器，可继续链式设置其他属性。</returns>
        public Builder WithDisplayName(string? displayName)
        {
            _displayName = displayName;
            return this;
        }

        /// <summary>
        /// 设置服务器提供的资源实体标签。
        /// </summary>
        /// <param name="eTag">保留引号和弱标记的 ETag；未提供时为 null。</param>
        /// <returns>当前构建器，可继续链式设置其他属性。</returns>
        public Builder WithETag(string? eTag)
        {
            _eTag = eTag;
            return this;
        }

        /// <summary>
        /// 设置资源在服务器响应中的 URI 引用。
        /// </summary>
        /// <param name="uri">原始 DAV:href，可为绝对或相对形式；null 表示手动构建时尚未指定。</param>
        /// <returns>当前构建器，可继续链式设置其他属性。</returns>
        public Builder WithUri(string? uri)
        {
            _uri = uri;
            return this;
        }

        /// <summary>
        /// 标记资源为集合。
        /// </summary>
        /// <returns>当前构建器，可继续链式设置其他属性。</returns>
        public Builder IsCollection()
        {
            _isCollection = true;
            return this;
        }

        /// <summary>
        /// 标记资源为非集合。
        /// </summary>
        /// <returns>当前构建器，可继续链式设置其他属性。</returns>
        public Builder IsNotCollection()
        {
            _isCollection = false;
            return this;
        }

        /// <summary>
        /// 标记资源为隐藏。
        /// </summary>
        /// <returns>当前构建器，可继续链式设置其他属性。</returns>
        public Builder IsHidden()
        {
            _isHidden = true;
            return this;
        }

        /// <summary>
        /// 标记资源为非隐藏。
        /// </summary>
        /// <returns>当前构建器，可继续链式设置其他属性。</returns>
        public Builder IsNotHidden()
        {
            _isHidden = false;
            return this;
        }

        /// <summary>
        /// 设置资源的最后修改时间。
        /// </summary>
        /// <param name="lastModifiedDate">协议解析时为 UTC 时间；手动构建时保留传入值，未知时为 null。</param>
        /// <returns>当前构建器，可继续链式设置其他属性。</returns>
        public Builder WithLastModifiedDate(DateTime? lastModifiedDate)
        {
            _lastModifiedDate = lastModifiedDate;
            return this;
        }

        /// <summary>
        /// 设置资源成功返回的完整属性集合。
        /// </summary>
        /// <param name="properties">包含状态为 2xx 的属性值集合，不应为 null。</param>
        /// <returns>当前构建器，可继续链式设置其他属性。</returns>
        public Builder WithProperties(IReadOnlyCollection<WebDavProperty> properties)
        {
            ArgumentNullException.ThrowIfNull(properties);
            _properties = properties;
            return this;
        }

        /// <summary>
        /// 设置资源全部属性的成功或失败状态。
        /// </summary>
        /// <param name="propertyStatuses">所有 propstat 的逐属性结果集合，不应为 null。</param>
        /// <returns>当前构建器，可继续链式设置其他属性。</returns>
        public Builder WithPropertyStatuses(IReadOnlyCollection<WebDavPropertyStatus> propertyStatuses)
        {
            ArgumentNullException.ThrowIfNull(propertyStatuses);
            _propertyStatuses = propertyStatuses;
            return this;
        }

        /// <summary>
        /// 按当前构建状态创建新的WebDavResource数据快照。
        /// </summary>
        /// <remarks>未设置的属性与属性状态使用空集合；其他可选值保持未知，不深复制调用方提供的集合。</remarks>
        /// <returns>包含当前构建值的新快照实例。</returns>
        public WebDavResource Build()
        {
            return new()
            {
                ActiveLocks = _activeLocks,
                ContentLanguage = _contentLanguage,
                ContentLength = _contentLength,
                ContentType = _contentType,
                CreationDate = _creationDate,
                DisplayName = _displayName,
                ETag = _eTag,
                Uri = _uri,
                IsCollection = _isCollection,
                IsHidden = _isHidden,
                LastModifiedDate = _lastModifiedDate,
                Properties = _properties ?? [],
                PropertyStatuses = _propertyStatuses ?? []
            };
        }
    }
}
