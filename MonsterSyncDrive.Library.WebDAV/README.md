<!--
MIT License

Copyright (c) 2025 - 2026 MonsterSyncDrive.Com

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
-->

# MonsterSyncDrive.WebDAV

无第三方运行时依赖的异步 WebDAV 客户端库，目标框架 `net10.0`。网络使用 `HttpClient`，XML 使用显式的 `XmlReader` / LINQ to XML，支持 Windows、Linux、macOS 和 NativeAOT/裁剪，不依赖反射序列化、动态代码或平台专属 API。

## 功能

- RFC 4918 的 PROPFIND、PROPPATCH、MKCOL、GET、HEAD、PUT、DELETE、COPY、MOVE、LOCK、UNLOCK，以及 OPTIONS 能力查询。
- PROPFIND 的 allprop、allprop + include、指定属性、propname、空正文，以及 Depth 0 / 1 / infinity。
- 标量及完整 XML 属性、属性删除、按命名空间解析、资源状态和逐项属性状态。`WebDavProperty.Value` 保留内层 XML，`TextValue` 返回解码文本，`ToXElement()` 保留属性元素的属性与混合内容。
- 流式上传下载、上传当前位置、不可定位的上传流、上传与下载进度、单段/开放/后缀 Range、一次请求多个不连续范围、multipart/byteranges 和 If-Range。
- HTTP ETag、If-Match / If-None-Match（含 `*`）、日期条件、响应头快照、完整 DAV If 表达式，以及 Prefer / Preference-Applied。
- 共享/独占写锁、锁续期、有限/无限超时、活动锁与支持锁类型发现、完整 XML 所有者，以及源和目标分别绑定的 If 条件。
- 完整 DAV:basicsearch 条件组合、比较、全文与语言匹配、多范围、排序、数量限制、评分、查询模式与语法发现，以及已用/可用配额查询。
- 保留完整 XML 的通用 REPORT、集合增量同步、分页续取与无效令牌识别。
- 当前认证主体、用户/组属性与主体报告、ACL 查询、权限授予/拒绝/撤销及受保护和继承条目处理。
- 指定资源类型及属性的集合创建、服务器分配成员 URI、通用 PATCH 和 SabreDAV 局部写入/追加。
- DeltaV 版本控制、版本历史、检出/检入/撤销、标签、工作区、活动、基线、更新、合并及标准报告；BIND/REBIND/UNBIND 与重定向引用资源。
- 可复用并发客户端、代理、系统支持的 HTTP 认证、注入 HttpClient、自定义请求头、覆盖到响应体的取消与超时。
- 逐条读取目录项的 `EnumerateAsync`，DTD/外部实体禁用，默认 67,108,864 个 XML 字符上限，严格校验状态行、命名空间与完整文档结尾。该上限按字符计数，不是网络字节数。

基础文件、属性和锁遵循 [RFC 4918]；下表列出的搜索、配额、同步、ACL、DeltaV、绑定、重定向引用等扩展已经提供专用客户端 API。库实现了协议请求和响应处理，不会让服务器自动具备这些能力。实际可用的功能、可写属性、锁模式、查询操作符和版本策略均取决于服务器；应结合 OPTIONS、相应属性/报告发现以及实际响应判断。CalDAV/CardDAV 的专用日历、联系人业务模型未单独封装，可在服务器支持时通过完整 XML REPORT 和属性 API 接入。

## 项目结构

| 位置 | 职责 |
| --- | --- |
| `IWebDavClient.cs` | 定义异步客户端接口，方便宿主注入和替换实现 |
| `WebDavClient.cs`、`WebDavClient.*.cs` | partial 客户端与接口，分别组织基础操作和各标准扩展 |
| `WebDavClient.Protocol.cs` | 共享扩展传输、完整 XML/返回表示读取、错误和头部快照 |
| `WebDavXml.cs` | 安全流式解析多状态文档，转换资源、属性和活动锁 |
| `WebDavUri.cs` | 按 URI 路径段编码普通文件名，统一跨平台路径语义 |
| `Core/` | 资源、锁、If、错误、同步、版本、绑定和引用模型，网络调度及流包装 |
| `Core/AccessControl/` | 主体、ACE、权限树和 ACL 限制模型 |
| `Request/` | 每个操作的请求参数及 DAV XML 正文构建器 |
| `Response/` | HTTP 头快照、逐资源与逐属性状态、流式响应 |
| `Helpers/` | 参数检查、深度转换、锁条件头及内部 XML 辅助方法 |

`MonsterSyncDrive.Remote.WebDAV` 在此库上实现同步根目录约束、版本检查、限速、临时文件及框架元数据策略。通用客户端只处理 DAV 与 HTTP，不承担同步引擎的文件系统策略。

## API 与默认行为

现有标准操作的方法名沿用 `Propfind`、`PutFile` 等命名，但都返回 `Task`，调用时需要 `await`。客户端同时接受 `string` URI 引用和 `Uri` 的标准操作重载；需要配置时使用对应的 `*Parameters`。

| 操作 | 方法 | 返回结果与关键约定 |
| --- | --- | --- |
| 属性查询 | `Propfind` | 完整读取后返回 `PropfindResponse`，默认 allprop、Depth: 1 |
| 流式枚举 | `EnumerateAsync` | 逐条返回 `WebDavResource`；完整枚举后才能确认文档结尾有效 |
| 属性修改 | `Proppatch` | `ProppatchResponse`；检查每个属性是否得到确认及修改成功 |
| 集合创建 | `Mkcol` | 只创建指定集合，不递归创建父集合 |
| 原始文件读取 | `GetRawFile` | 只读 `WebDavStreamResponse`，支持 Range，结果需要释放 |
| 处理后文件读取 | `GetProcessedFile` | 使用 Translate: t 提示服务器允许处理正文 |
| 原始 GET 响应 | `GetFileResponse` | 返回由调用方释放的 `HttpResponseMessage` |
| 文件复制到流 | `DownloadFileAsync` | 复制正文并核对已知长度，保留目标流的所有权 |
| 文件上传 | `PutFile` | 从源流当前位置开始，默认保留源流 |
| 资源删除 | `Delete` | 保留集合删除中的逐资源失败，不把 404 自动当作成功 |
| 服务器内复制 | `Copy` | 默认 Depth: infinity、允许覆盖目标；目标锁绑定目标 URI |
| 服务器内移动 | `Move` | 默认允许覆盖目标，源锁与目标锁分别绑定各自 URI |
| 锁及续期 | `Lock` | 默认 Shared；新锁默认 Depth: infinity，提供 LockToken 则发送空正文续期 |
| 解锁 | `Unlock` | 通过 Lock-Token 头提交锁令牌 |
| 响应头查询 | `HeadAsync` | 只查询状态和头部，不下载文件正文 |
| 能力查询 | `OptionsAsync` | 从 DAV、Allow 头解析能力标识和允许方法 |
| 存在性查询 | `ExistsAsync` | 2xx 为 true，404 为 false，其余失败状态抛异常 |
| 属性检索 | `Search` | 支持旧式单属性 LIKE 和新类型化表达式；返回 `PropfindResponse` 资源投影，默认范围深度 infinity |
| 完整 XML 报告 | `ReportAsync` / `ReportDocumentAsync` | `WebDavReportResponse` 保留 `Document`、`RootExtensions`、可选 `Resources` 和原始返回表示 |
| 原始扩展请求 | `SendAsync` | 只管理发送与生命周期，请求和响应都由调用方释放 |

`ApplyTo.*.ResourceAndDescendants` 表示当前资源及全部后代，对应 infinity。历史名称 `ResourceAndAncestors` 与它数值相同，仅用于兼容，实际不会查询或操作祖先目录。

## 标准扩展与 API 对照

以下 24 项均已提供请求构造、响应模型和协议测试。表中的方法都在 `WebDavClient` / `IWebDavClient` 上；参数类型位于 `MonsterSyncDrive.WebDAV.Request`，响应类型位于 `MonsterSyncDrive.WebDAV.Response`。标准操作保留既有命名和重载；各新增扩展是否提供 `Uri` 重载，以接口声明和生成的 XML 文档为准。

| 序号 | 功能与协议 | API / 参数 | 关键结果与约定 |
| --- | --- | --- | --- |
| 1 | 一次下载多个不连续区间，[RFC 9110] | `GetRangesAsync`、`GetFileParameters.Range` | `WebDavRangeResponse.ReadPartsAsync` 顺序读取实际分段；完整枚举后检查 `MissingRanges` / `IsRangeRequestSatisfied` |
| 2 | 活动锁与支持锁类型发现，[RFC 4918] | `GetLockDiscoveryAsync` | `LockDiscoveryResponse.ActiveLocks` / `SupportedLocks`；属性 403/404 单独保留 |
| 3 | 锁所有者信息，[RFC 4918] | `LockParameters.Owner`、`ActiveLock.Owner`；`PrincipalLockOwner`、`UriLockOwner`、`XmlLockOwner` | 文本、href 和混合 XML 可往返；所有者说明不等同认证身份 |
| 4 | 多状态中的资源重定向地址，[RFC 4918] | `Propfind` / REPORT 等的 `WebDavResource.RedirectLocation` | 保留 `DAV:location/DAV:href`；不会自动重新发送原操作 |
| 5 | DAV 结构化错误，[RFC 4918] | `WebDavResponse.Error`、`WebDavResource.Error`、`WebDavPropertyStatus.Error` | `WebDavError.Conditions` / `Contains` / `ToXElement` 保留完整名称、href 和未知嵌套详情 |
| 6 | 完整 DAV If 条件，[RFC 4918] | `WebDavRequestOptions.IfConditions`、`WebDavIfHeader`、`WebDavIfList`、`WebDavIfCondition` | 带资源标记/无标记、多个 OR 列表、列表内 AND、Not、状态令牌和强/弱 ETag；支持 `Parse` |
| 7 | 复合搜索、比较、全文及语言匹配，[RFC 5323] | `Search` / `SearchDocumentAsync`、`SearchParameters.Filter`、`WebDavSearchExpression` / `WebDavSearchLiteral` | AND/OR/NOT、eq/lt/lte/gt/gte、LIKE/caseless、contains、is-defined、is-collection、语言谓词与 typed-literal |
| 8 | 搜索排序、数量限制、模式与语法发现，[RFC 5323] | `OrderBy` / `WebDavSearchOrder`、`MaxResults`、`Scopes` / `WebDavSearchScope`；`DiscoverSearchSchemaAsync`、`GetSearchGrammarsAsync` | 多属性或 score 排序、不同深度和历史版本范围；`WebDavSearchResponse.Hits` / `IsTruncated`；QSD 返回数据类型、属性能力及操作符签名 |
| 9 | 已用空间与剩余配额，[RFC 4331] | `GetQuotaAsync` | `WebDavQuotaResponse.UsedBytes` / `AvailableBytes` 使用 `BigInteger?`；零与未知分开，对应属性状态独立保留 |
| 10 | 通用 XML REPORT，[RFC 3253] | `ReportAsync` / `ReportDocumentAsync` | 支持 multistatus 和其他 XML 根；`Document` 不丢弃根级扩展，`Resources` 只投影标准多状态条目 |
| 11 | 集合增量同步，[RFC 6578] | `SyncCollectionAsync`、`SyncCollectionParameters` | `WebDavSyncResponse` 保留新增/修改、删除、无法遍历集合、失败、同步令牌及初始同步标志 |
| 12 | 增量同步分页与续取，[RFC 6578] | `EnumerateSyncPagesAsync` / `SynchronizeCollectionAsync`、`Limit` / `MaxPages` | 根据集合自身 507 与返回令牌续取；`WebDavSyncResult` 聚合最后状态；错误、取消、重复令牌和页数上限都不能作为提交新检查点的成功 |
| 13 | 当前认证用户身份，[RFC 5397] | `GetCurrentPrincipalAsync` | `WebDavCurrentPrincipalResponse.PrincipalUri` / `IsAuthenticated` / `IsUnauthenticated` 区分身份、未认证与查询失败 |
| 14 | ACL 查询、授权、拒绝和撤销，[RFC 3744] | `GetAccessControlAsync`、`SetAclAsync`、`GrantPrivilegesAsync`、`DenyPrivilegesAsync`、`RevokePrivilegesAsync` | 权限树、有效权限、owner/group、继承项、限制；便捷读改写默认自动获取独占锁，可通过 `WebDavAclUpdateParameters` 配置 |
| 15 | 用户/组主体发现与报告，[RFC 3744] | `GetPrincipalAsync`、`PrincipalPropertySearchAsync`、`PrincipalSearchPropertySetAsync`、`PrincipalMatchAsync`、`AclPrincipalPropSetAsync` | `WebDavPrincipalInfo` 保留主体 URL、替代 URI、直接组成员及所属组；报告按 Depth:0 请求 |
| 16 | 指定资源类型并设置属性创建集合，[RFC 5689] | `ExtendedMkcolAsync`、`ExtendedMkColParameters` | `ResourceTypes`、文本和完整 XML 属性；`ExtendedMkColResponse` 检查属性状态，不递归创建父集合 |
| 17 | 服务器分配新资源 URL，[RFC 5995] | `DiscoverAddMemberAsync`、`AddMemberAsync`、`AddMemberParameters` | 发现 `DAV:add-member` 后 POST；`SuggestedName` 仅为 Slug 建议，实际 URL 由 `WebDavWriteResponse.Location` 提供 |
| 18 | Prefer 最小响应与返回表示，[RFC 7240] / [RFC 8144] | `WebDavRequestOptions.Preferences`、`WebDavPreferences`；`PutFileWithResponseAsync`、`PatchAsync`、`AddMemberAsync` | `WebDavWriteResponse.Stream` / `AppliedPreferences` / `ReadXmlRepresentationAsync`；完整文档响应可保留 `Representation` 字节，偏好不保证被采用 |
| 19 | 局部修改与追加，[RFC 5789] 及 SabreDAV 方言 | `DiscoverPatchAsync`、`PatchAsync` / `PatchParameters`；`SabreDavPartialUpdateAsync` / `SabreDavPartialUpdateParameters` | 通用 PATCH 由补丁媒体类型定义语义；SabreDAV 使用 `application/x-sabredav-partialupdate` 和 `X-Update-Range`，需要服务器明确支持 |
| 20 | 版本控制与版本历史，[RFC 3253] | `VersionControlAsync`、`GetVersioningInfoAsync`、`GetVersionHistoryAsync`、`GetVersioningCapabilitiesAsync` | `WebDavVersioningResponse.Items` / `WebDavVersioningInfo`、version-tree 报告及能力/创建位置发现 |
| 21 | 检出、检入、撤销检出，[RFC 3253] | `CheckoutAsync`、`CheckinAsync`、`UncheckoutAsync` | 支持分支、保持检出、活动和未保留检出选项；工作资源和新版本地址从响应 Location 读取 |
| 22 | 标签、工作区、基线、活动、更新、合并，[RFC 3253] | `LabelAsync`、`MakeWorkspaceAsync`、`BaselineControlAsync`、`MakeActivityAsync`、`UpdateVersionAsync`、`MergeAsync`；`ExpandVersionPropertiesAsync`、`LocateVersionsByHistoryAsync`、`PreviewMergeAsync`、`CompareBaselinesAsync`、`GetLatestActivityVersionAsync` | 强类型请求和标准报告，完整 XML 与内部状态保留；合并冲突、工作流和服务器权限由宿主处理 |
| 23 | 同一资源绑定多个 URI，[RFC 5842] | `BindAsync`、`RebindAsync`、`UnbindAsync`、`GetBindingInfoAsync`、`BindingParameters` | `WebDavBindingResponse.Bindings` 保留 `resource-id` / `parent-set`；明确暴露 208 / 508，不在客户端递归追踪绑定 |
| 24 | 创建、修改重定向引用，[RFC 4437] | `CreateRedirectReferenceAsync`、`UpdateRedirectReferenceAsync`、`GetRedirectReferenceAsync`、`RedirectReferenceParameters` | 目标和 temporary/permanent 生命周期；发现时操作引用本身，保留相对目标并提供 `ResolveTarget` |

### 命名空间

| 命名空间 | 常用公开类型 |
| --- | --- |
| `MonsterSyncDrive.WebDAV` | `WebDavClient`、`IWebDavClient`、`WebDavXml`、`WebDavUri` |
| `MonsterSyncDrive.WebDAV.Request` | 基础 `*Parameters`、`SearchParameters`、`WebDavSearchExpression`、`SyncCollectionParameters`、`WebDavAclUpdateParameters`、`ExtendedMkColParameters`、`PatchParameters`、所有 DeltaV/绑定/引用参数和 `WebDavPreferences` |
| `MonsterSyncDrive.WebDAV.Response` | 基础响应、`WebDavRangeResponse`、`WebDavReportResponse`、`WebDavSyncResponse` / `WebDavSyncResult`、搜索/QSD/配额/ACL/主体/版本/绑定/引用响应 |
| `MonsterSyncDrive.WebDAV.Core` | `WebDavResource`、`ActiveLock`、`XmlLockOwner`、`WebDavIfHeader`、`WebDavError`、`WebDavSyncChange`、`WebDavVersioningInfo`、`WebDavBindingInfo`、`WebDavRedirectReference` |
| `MonsterSyncDrive.WebDAV.Core.AccessControl` | `WebDavPrincipal`、`WebDavAce`、`WebDavPrivileges`、`WebDavPrincipalInfo`、`WebDavSupportedPrivilege`、`WebDavAclRestrictions` |

`Request/Versioning`、`Response/Bindings` 等是源文件分组，公开类型仍使用上表的 `Request` / `Response` / `Core` 命名空间。

## 连接配置

| 配置 | 默认值 | 说明 |
| --- | --- | --- |
| `BaseAddress` | null | 未设置时只能使用绝对 URI；目录地址通常应以 `/` 结尾 |
| `Credentials` | null | 显式 HTTP 认证凭据；提供后优先于系统默认凭据 |
| `UseDefaultCredentials` | false | 是否使用进程的系统身份认证 |
| `PreAuthenticate` | true | 首次访问新的认证范围仍可能需要服务器挑战 |
| `UseProxy` | true | 是否启用代理 |
| `Proxy` | null | null 使用处理器的系统代理设置 |
| `AllowAutoRedirect` | false | 自动重定向由宿主明确选择，防止通用重定向改变 DAV 方法 |
| `Timeout` | null，客户端采用 100 秒 | 覆盖响应头、正文及等待继续读取；可指定无限时长 |
| `MaxXmlCharacters` | 67,108,864 | 整个响应 XML 文档的字符上限，必须为正数 |
| `DefaultRequestHeaders` | 空字典 | 创建客户端时复制；单次请求头只覆盖本次请求 |

也可传入预配置的 `HttpClient`，复用宿主的处理器、连接池、代理与证书配置。该构造方式默认借用客户端。内部生成的 XML 正文采用 UTF-8；若自定义 XML 的 ContentType，字符集应保持与实际 UTF-8 编码一致。PUT 的 ContentType 仅标记源字节类型，不负责重新编码。

## 使用

```csharp
using System.Net;
using MonsterSyncDrive.WebDAV;
using MonsterSyncDrive.WebDAV.Core;
using MonsterSyncDrive.WebDAV.Request;

using var client = new WebDavClient(new WebDavClientParams
{
    BaseAddress = new Uri("https://example.com/dav/user/"),
    Credentials = new NetworkCredential("user", "password"),
    Timeout = TimeSpan.FromMinutes(5)
});

(await client.Mkcol("documents/")).EnsureSuccessStatusCode();
var remotePath = WebDavUri.EncodePath("documents/中文 #%.txt");
await using var source = File.OpenRead("local.txt");
(await client.PutFile(remotePath, source,
    new PutFileParameters { IfNoneMatch = "*" })).EnsureSuccessStatusCode();

var listing = await client.Propfind("documents/", new PropfindParameters
{
    ApplyTo = ApplyTo.Propfind.ResourceAndChildren
});
listing.EnsureSuccessStatusCode();

await using var destination = File.Create("download.txt");
await client.DownloadFileAsync(remotePath, destination);
```

方法接受标准 URI 引用，已经编码的 URL 不会重复编码；普通文件路径可先使用 `WebDavUri.EncodePath`。BaseAddress 通常应以 `/` 结尾，相对地址按标准 URI 规则解析；根路径 `/file` 相对于服务器根目录。路径使用 `/`，不使用操作系统路径拼接。

```csharp
using var response = await client.GetRawFile(remotePath, new GetFileParameters
{
    Range = new System.Net.Http.Headers.RangeHeaderValue(1024, 2047),
    IfMatch = "\"known-etag\"",
    CancellationToken = cancellationToken
});
response.EnsureSuccessStatusCode();
// 检查 StatusCode：服务器可忽略 Range 返回 200；206 时库校验 Content-Range。
await response.Stream.CopyToAsync(destination, cancellationToken);
```

上述分段片段使用前文的 `client`、`remotePath` 和 `destination`，`cancellationToken` 由宿主提供。若服务器忽略 Range 返回 200，流中是整个文件，不能从请求偏移直接当作分段数据拼接；总文件长度应从 Content-Range 的总长度或资源属性读取，而不是把 206 的 Content-Length 当成总长度。

## 标量属性与完整 XML 属性

```csharp
using System.Xml.Linq;

XNamespace metadata = "urn:example:metadata";
var patch = await client.Proppatch(remotePath, new ProppatchParameters
{
    // 普通文本由序列化器转义；这里的 & 和 < 不需要手工编码。
    PropertiesToSet = new Dictionary<XName, string>
    {
        [metadata + "title"] = "中文标题 & value < 10"
    },
    // 需要元素属性或混合内容时传入完整元素，不把字符串作为 XML 片段。
    XmlPropertiesToSet =
    [
        new XElement(metadata + "details", new XAttribute("format", "custom"),
            new XElement(metadata + "label", "value"))
    ],
    PropertiesToRemove = [metadata + "obsolete"]
});
patch.EnsureSuccessStatusCode();

var query = await client.Propfind(remotePath, new PropfindParameters
{
    ApplyTo = ApplyTo.Propfind.ResourceOnly,
    RequestType = PropfindRequestType.NamedProperties,
    CustomProperties = [metadata + "title", metadata + "details"]
});
query.EnsureSuccessStatusCode();
var resource = query.Resources.Single();
foreach (var property in resource.Properties)
{
    Console.WriteLine(property.TextValue); // 解码后的文本，嵌套文本会合并。
    XElement preservedElement = property.ToXElement(); // 含原元素属性的独立副本。
}
```

属性名称的身份由命名空间和本地名称共同决定。`{DAV:}getcontentlength` 与 `{urn:example}getcontentlength` 是不同属性，扩展属性不能覆盖标准长度。`Properties` 只包含成功返回的属性，`PropertyStatuses` 同时保留成功和失败结果；对业务必需的属性，应检查其是否实际返回成功。

`WebDavProperty.Value` 是内层 XML，可能保留 `&amp;` 等转义，适合 XML 往返；读取普通文本使用 `TextValue`。通过完整元素构造时保存元素自身的属性；仅使用名称和内层字符串构造时，字符串需要是有效的 XML 内容。

## 锁创建、续期与释放

```csharp
var acquired = await client.Lock(remotePath, new LockParameters
{
    LockScope = LockScope.Exclusive,
    ApplyTo = ApplyTo.Lock.ResourceOnly,
    Owner = new UriLockOwner("mailto:owner@example.com"),
    Timeout = TimeSpan.FromMinutes(5)
});
acquired.EnsureSuccessStatusCode();
var lockToken = acquired.ActiveLocks.Single().LockToken
    ?? throw new InvalidDataException("服务器没有提供锁令牌。");
try
{
    var refreshed = await client.Lock(remotePath, new LockParameters
    {
        LockToken = lockToken, // 非空令牌选择续期路径：空正文，不重设 Depth 或所有者。
        Timeout = TimeSpan.FromMinutes(5)
    });
    refreshed.EnsureSuccessStatusCode();
    // 修改受保护资源时，在 PutFileParameters / ProppatchParameters 等参数中设置 LockToken。
}
finally
{
    (await client.Unlock(remotePath, lockToken)).EnsureSuccessStatusCode();
}
```

实际授予的租约以 `ActiveLock.Timeout` 为准；`Timeout.InfiniteTimeSpan` 表示服务器报告 Infinite，null 表示未提供或无法识别，二者含义不同。客户端不会自动续期，宿主应根据自己的操作周期安排续期并保留锁令牌。

COPY 的目标锁与 MOVE 的源锁、目标锁分别绑定对应的绝对资源 URI。MOVE 同时提交两种锁时，在一个 If 字段中构造两组带标记条件；UNLOCK 则使用独立的 Lock-Token 头。

## 扩展用法

以下示例沿用前文的 `client`；`cancellationToken`、已保存的 ETag 和同步检查点由宿主提供。示例展示调用方式，目标服务器需实际支持相应扩展。

### 多个不连续下载区间

```csharp
using System.Net.Http.Headers;

var byteRanges = new RangeHeaderValue();
byteRanges.Ranges.Add(new RangeItemHeaderValue(0, 1023));
byteRanges.Ranges.Add(new RangeItemHeaderValue(4096, 5119));

using var ranged = await client.GetRangesAsync(remotePath, new GetFileParameters
{
    Range = byteRanges,
    IfMatch = "\"known-etag\"",
    CancellationToken = cancellationToken
});
ranged.EnsureSuccessStatusCode();

await foreach (var part in ranged.ReadPartsAsync(cancellationToken))
{
    Console.WriteLine(part.ContentRange?.ToString() ?? "服务器返回整个文件");
    // 示例仅消费数据。实际保存时按 ContentRange 的 From 定位，不按分段顺序推算偏移。
    await part.Stream.CopyToAsync(Stream.Null, cancellationToken);
}
if (ranged.MissingRanges is { Count: > 0 } missing)
    Console.WriteLine("需要续取：" + string.Join(", ", missing));
```

服务器可以合并相邻区间、改变返回顺序，或只返回请求中可满足部分的子集；合法的部分覆盖不会被误判为损坏。必须完整枚举后检查 `MissingRanges` / `IsRangeRequestSatisfied`，并对缺失范围另发请求；库不自动续取。若总长度未知，或提前结束枚举，完整覆盖情况可能仍为 null。每段 `Stream` 仅在当前枚举位置有效，进入下一段后不可继续使用；未读部分会被排空。

服务器忽略 Range 返回 200 时，`IsRangeIgnored` 为 true，只有一个 `ContentRange == null` 的完整文件段；应按完整文件处理，不能把正文直接写入第一个请求偏移。多段结果本身需要释放，只能枚举一次。MIME 边界、实际字节长度及范围结构的最终校验在完整枚举结束时完成。

### 完整 If 条件、锁发现与错误详情

```csharp
var locks = await client.GetLockDiscoveryAsync(remotePath);
locks.EnsureSuccessStatusCode();
foreach (var active in locks.ActiveLocks)
{
    Console.WriteLine(active.LockType);
    Console.WriteLine(active.Owner?.ToXElement()); // 包括结构化 XML 所有者。
}

var condition = WebDavIfHeader.ForRequest(
    new WebDavIfList(
        WebDavIfCondition.StateToken("urn:example:lock-token"),
        WebDavIfCondition.EntityTag("\"v1\"")),
    new WebDavIfList(
        WebDavIfCondition.StateToken("urn:example:obsolete-token", isNegated: true)));
var conditional = await client.HeadAsync(remotePath, new WebDavRequestOptions
{
    IfConditions = condition
});
if (conditional.Error is { } error)
    foreach (var detail in error.Conditions)
        Console.WriteLine($"{detail.Name}: {detail.ToXElement()}");
```

一个 `WebDavIfList` 的条件按 AND 组合，同一资源的多个列表按 OR 组合。对 MOVE 等涉及多个资源的操作，使用 `WebDavIfHeader.ForResources` 与 `WebDavIfResourceConditions` 分别指定源、目标资源。`IfConditions` 替代该操作自动生成的简化锁条件；最后应用的原始 `Headers` 仍可覆盖它。`WebDavIfHeader.Parse` 可解析和重新输出规范表达式，不允许把带标记与无标记模式混在同一个头中。

锁发现的空列表可能表示没有活动锁，也可能表示属性未提供；应检查 `PropertyStatuses`。`XmlLockOwner` 保留完整 `DAV:owner` XML；该数据只是说明，不证明所有者当前已认证或持有令牌。HTTP、资源和属性三个层级的 `Error` 分别检查；`RedirectLocation` 也属于单个资源结果，读取后由宿主决定是否以及如何处理重定向。

### 复合搜索、排序、查询模式与配额

```csharp
XNamespace dav = "DAV:";
var search = await client.SearchDocumentAsync("", new SearchParameters
{
    Scopes = [new WebDavSearchScope("/dav/user/documents/",
        ApplyTo.Propfind.ResourceAndDescendants)],
    SelectProperties = [dav + "displayname", dav + "getetag", dav + "getcontentlength"],
    Filter = WebDavSearchExpression.And(
        WebDavSearchExpression.Compare(dav + "getcontentlength",
            WebDavSearchComparison.GreaterThanOrEqual, WebDavSearchLiteral.Integer(1024)),
        WebDavSearchExpression.Or(
            WebDavSearchExpression.Like(dav + "displayname", "%.txt", caseless: true),
            WebDavSearchExpression.Contains("季度报告")),
        WebDavSearchExpression.Not(WebDavSearchExpression.IsCollection())),
    OrderBy = [new WebDavSearchOrder(descending: true),
        new WebDavSearchOrder(dav + "displayname", caseless: true)],
    MaxResults = 100
});
search.EnsureSuccessStatusCode();
foreach (var hit in search.Hits)
    Console.WriteLine($"{hit.Resource.Uri}: score={hit.Score}");

var schema = await client.DiscoverSearchSchemaAsync("", new SearchParameters
{
    Scope = "/dav/user/documents/"
});
schema.EnsureSuccessStatusCode();
foreach (var property in schema.Schemas.SelectMany(item => item.Properties))
    Console.WriteLine($"{property.DataType}: searchable={property.Searchable}");

var quota = await client.GetQuotaAsync("documents/");
quota.EnsureSuccessStatusCode();
Console.WriteLine($"已用：{quota.UsedBytes}，可用：{quota.AvailableBytes}");
Console.WriteLine($"额度属性状态：{quota.AvailableStatusCode}");
```

旧代码的 `SearchProperty` + `SearchKeyword` 仍生成 LIKE；不能与 `Filter` 或 `MatchAll` 混用。无过滤查询需要显式设置 `MatchAll = true`，避免条件遗漏变成全范围搜索。普通字面值、XML Schema typed-literal、xml:lang 和语言谓词均有类型化入口；表达式导出返回独立 XML 副本。

`Scopes` 支持多个范围、Depth 0/1/infinity 和 `IncludeVersions`。`OrderBy` 按集合顺序确定排序优先级，属性为 null 表示 score。`MaxResults` 是服务器可以忽略的数量建议，并非标准 SEARCH 分页游标；服务器用仲裁资源的 507 表示截断时，`IsTruncated` 为 true，资源错误也会影响 `IsSuccessful`。评分仅在对应查询上下文中有意义。

`GetSearchGrammarsAsync` 返回 `supported-query-grammar-set` 中的完整语法名称；QSD 返回每个范围的模式、属性数据类型、searchable/selectable/sortable/caseless 声明及操作符签名。缺少能力标记表示未声明，不能据此推断服务器一定拒绝该属性；未声明数据类型按规范取 xs:string。全文分词、相关性、大小写和语言匹配的实际处理仍由服务器决定。

配额使用 `BigInteger?`，不会因大于 Int64 而截断。`AvailableBytes == 0` 是合法的配额耗尽；null 配合 403/404 等状态表示未知、未提供或无权读取，不能推断为零或无限额度。

### 通用 REPORT 与集合增量同步

```csharp
// 请求根和响应结构由目标服务器支持的报告协议规定。
XNamespace reportNamespace = "urn:example:reports";
var report = await client.ReportDocumentAsync("documents/",
    new XElement(reportNamespace + "summary"));
report.EnsureSuccessStatusCode();
Console.WriteLine(report.Document);       // 完整根，允许不是 multistatus。
Console.WriteLine(report.RootExtensions.Count);

string? savedSyncToken = null; // 实际应用从自己的持久化检查点读取；null 表示首次列举。
var sync = await client.SynchronizeCollectionAsync("documents/",
    new SyncCollectionParameters
    {
        SyncToken = savedSyncToken,
        Level = WebDavSyncLevel.Children,
        Properties = [WebDavXml.Dav + "getetag", WebDavXml.Dav + "resourcetype"],
        Limit = 500,
        MaxPages = 1000
    }, cancellationToken);
sync.EnsureSuccessStatusCode();
foreach (var change in sync.Changes)
    Console.WriteLine($"{change.Kind}: {change.ResolvedUri}");

// 宿主应先成功、完整地应用 Changes，再与本地快照一起提交 sync.SyncToken。
// 在这里仍不能仅因“拿到了新 token”就立即覆盖已保存的检查点。
```

`SyncCollectionAsync` 读取单页；`EnumerateSyncPagesAsync` 按返回 token 续取；`SynchronizeCollectionAsync` 收集全部页面并按解析后的成员 URI 保留最后一次状态。HTTP Depth 固定为 0，实际范围由 `Level` 的 Children / Descendants 决定；后者在线上使用 `infinite`，不是 PROPFIND 的 `infinity`。

增量中的成员 404 表示正常删除，不导致整体失败；集合删除还需要宿主移除对应本地后代。无法遍历的子集合在 `TraversalFailures` 中保留，不会被当作空目录。失效 token 的单页响应设置 `IsTokenInvalid`，自动分页抛出 `WebDavSyncTokenInvalidException`；库不会擅自用空 token 重试并覆盖已有快照。

分页并非本地事务。成员失败、属性缺失、取消、后续页面 HTTP/XML 错误、重复续取 token、超出 `MaxPages` 或本地应用失败时，都应保留原检查点；只有完整读取、检查所需属性并成功应用全部变更后，才能提交最终 token。单页的 token 和已产出的页面不能被误当成整批已完成。业务必需属性还应检查各资源的 `PropertyStatuses`。

### 认证主体、用户/组及 ACL

```csharp
using MonsterSyncDrive.WebDAV.Core.AccessControl;

var identity = await client.GetCurrentPrincipalAsync("");
identity.EnsureSuccessStatusCode();
Console.WriteLine(identity.PrincipalUri);
Console.WriteLine(identity.IsUnauthenticated);

var acl = await client.GetAccessControlAsync(remotePath);
acl.EnsureSuccessStatusCode();
Console.WriteLine(acl.HasAcl);
Console.WriteLine(string.Join(", ", acl.CurrentUserPrivileges));

var alice = WebDavPrincipal.ForHref("/principals/users/alice/");
var granted = await client.GrantPrivilegesAsync(remotePath, alice,
    [WebDavPrivileges.Read], new WebDavAclUpdateParameters
    {
        AcquireLock = true // 默认值：独占 Depth:0 LOCK -> 读取 -> ACL -> UNLOCK。
    });
granted.EnsureSuccessStatusCode();

// 撤销此主体可编辑 ACE 中的显式 read 授予。
var revoked = await client.RevokePrivilegesAsync(remotePath, alice,
    [WebDavPrivileges.Read]);
revoked.EnsureSuccessStatusCode();
```

`GrantPrivilegesAsync`、`DenyPrivilegesAsync` 和 `RevokePrivilegesAsync` 默认自动申请五分钟独占写锁，操作结束后释放自己申请的锁。服务器不支持 LOCK，或宿主已有并发控制时，可设置 `AcquireLock = false`；提供 `LockToken` 则借用该锁，方法不会获取或释放它。自动获取锁时不能同时用完整 `IfConditions` 或原始 If 头覆盖尚未知的令牌；可改为显式提供已有锁令牌或关闭自动加锁。

便捷更新先读取完整 ACL，只变更匹配主体的可编辑项，保留其他主体和扩展数据，并遵守服务器声明的限制；ACL 未成功返回时不会把它当作空 ACL 覆盖。`SetAclAsync` 直接发送可编辑 ACL 的完整替换集合，不执行自动读改写或自动获取锁，调用方负责准备完整集合与并发条件；保护/继承条目由服务器保留，不重新发送为可编辑项。

撤销显式授予不等于创建 deny，也不会消除保护条目、继承条目或组带来的有效权限。权限聚合不在客户端擅自展开；最终有效权限应读取 `CurrentUserPrivileges`。主体 URI 应来自 `PrincipalUrl` / 身份发现结果，不要把显示名称当成 ACL 身份。`GetPrincipalAsync` 和主体报告提供直接组关系，不自动递归展开嵌套组，也不创建或删除服务器账户。

### 扩展集合创建、服务器分配 URL 和 Prefer

```csharp
// 仅在支持对应资源类型的服务器上创建集合；示例使用标准 collection 类型。
var collection = await client.ExtendedMkcolAsync("inbox/", new ExtendedMkColParameters
{
    ResourceTypes = [WebDavXml.Dav + "collection"],
    PropertiesToSet = new Dictionary<XName, string> { [WebDavXml.Dav + "displayname"] = "收件箱" }
});
collection.EnsureSuccessStatusCode();

await using var memberSource = File.OpenRead("local.txt");
using var created = await client.AddMemberAsync("inbox/", memberSource, new AddMemberParameters
{
    SuggestedName = "upload.txt",
    Preferences = new WebDavPreferences { Return = WebDavReturnPreference.Representation }
});
created.EnsureSuccessStatusCode();
Console.WriteLine(created.Location);           // 使用服务器实际分配的 URL。
Console.WriteLine(created.AppliedPreferences.Return);
await created.Stream.CopyToAsync(Stream.Null, cancellationToken);
```

`ResourceTypes` 至少包含 `DAV:collection`，可按服务器支持情况加入其他完整资源类型名称；创建时可同时写入普通文本属性和 `XmlPropertiesToSet`。`ExtendedMkColResponse.PropertyStatuses` 保留 mkcol-response 中的逐属性结果。

`AddMemberAsync` 在没有指定 `AddMemberUri` 时先查询 `DAV:add-member`，不支持则明确失败；库校验发现的端点与集合属于同一来源。`SuggestedName` 只是服务器可忽略的 Slug，不能依它推算新 URL。同步完成的成功创建需要 Location；202 表示服务器接受后异步处理，不能视为内容已经创建完成。

`Preferences.Return` 支持 Minimal / Representation，另有 handling、respond-async、wait 和 depth-noroot 等标准选项。服务器可以忽略偏好，应结合实际状态和 `Preference-Applied` 判断；最小响应也不等同全部内部操作都成功。`PutFileWithResponseAsync`、`PatchAsync` 和 `AddMemberAsync` 返回需要释放的 `WebDavWriteResponse`，可以流式消费任意表示；XML 正文可显式调用 `ReadXmlRepresentationAsync`，解析后获得 `Document` 和 DAV `Error`。完整文档类响应通过 `Representation` 保留需要的原始字节，仍受协议正文大小限制。

### PATCH、局部覆盖和追加

```csharp
var patchCapabilities = await client.DiscoverPatchAsync(remotePath);
patchCapabilities.EnsureSuccessStatusCode();
Console.WriteLine(string.Join(", ", patchCapabilities.AcceptedMediaTypes));

// 该调用仅适用于明确支持 SabreDAV partialupdate 的服务器。
await using var appendSource = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("追加一行\n"));
using var appended = await client.SabreDavPartialUpdateAsync(remotePath, appendSource,
    new SabreDavPartialUpdateParameters
    {
        Append = true,
        IfMatch = "\"known-etag\""
    });
appended.EnsureSuccessStatusCode();
```

`PatchAsync` 实现 [RFC 5789] 的方法和媒体类型传输，不自动生成 JSON Patch、XML Patch 或二进制差分。补丁格式必须与服务器 `Accept-Patch` 声明及其具体语义一致；没有声明不应被当作已支持任意格式。

局部文件覆盖和追加采用 [SabreDAV partialupdate 规范](https://sabre.io/dav/http-patch/)，`X-Update-Range` 是方言扩展，并非所有 WebDAV 服务器的标准能力。可从 DAV 能力中的 `sabredav-partialupdate`、Accept-Patch 及服务器文档确认支持。`Append = true` 时省略 `Offset` / `End`；覆盖模式设置 `Offset`，可选包含式 `End`，闭合区间大小必须等于正文长度。该方言要求已知 Content-Length，不可定位源流需提供可验证的声明长度；一次补丁只写一个区间。应使用 ETag 或锁防止并发修改，库不会自动下载整个文件再重新上传。

### DeltaV 版本、工作区、基线和活动

```csharp
(await client.VersionControlAsync(remotePath)).EnsureSuccessStatusCode();
(await client.CheckoutAsync(remotePath)).EnsureSuccessStatusCode();
await using (var versionSource = File.OpenRead("local.txt"))
    (await client.PutFile(remotePath, versionSource)).EnsureSuccessStatusCode();
var checkedIn = await client.CheckinAsync(remotePath);
checkedIn.EnsureSuccessStatusCode();

var history = await client.GetVersionHistoryAsync(remotePath);
history.EnsureSuccessStatusCode();
foreach (var version in history.Items)
    Console.WriteLine($"{version.VersionName}: {version.Resource.Uri}");

(await client.LabelAsync(remotePath, new LabelParameters
{
    LabelName = "release-1",
    Action = VersionLabelAction.Set
})).EnsureSuccessStatusCode();
```

示例为支持原地检出的服务器流程；若服务器创建工作资源，应检查 CHECKOUT 的 Location，并对那个工作资源继续写入与检入。`UncheckoutAsync` 撤销检出，不自动保存当前修改。`VersioningRequestOptions.LabelSelector` 用于按标签选择版本，库会按协议把该请求头编码为 UTF-8 URI 转义形式；XML 中的标签名称仍使用原文。CHECKOUT 的 Label 头与 `ApplyToVersion` 不能同时使用。

REPORT 携带任何 Depth（包括 0）时，服务器使用 207 多状态封装每个目标的原报告。`WebDavVersioningResponse.Reports` 保留目标、报告 XML 和各自的错误；`Items`、`BaselineChanges`、`MergePreviews`、`LatestActivityVersions` 提供对应的内容投影。未发送 Depth 时按该报告自身规定的响应根解析。`Depth` 只用于支持对应深度的操作。

`return=minimal` 还可能使 Depth 报告的内容被省略，仅返回资源级 200 或空属性结果。此时保留原始 HTTP 状态，`HasOmittedReports` / `WasOmittedByMinimalPreference` 明确标示省略，`IsReportAvailable` 和 `IsSuccessful` 为 false；不能把缺少报告数据误解成完整的空版本历史。

`GetVersioningCapabilitiesAsync` 从 OPTIONS 读取 DeltaV 能力及版本历史、工作区、活动的可创建集合位置。`MakeWorkspaceAsync` / `MakeActivityAsync` 应使用这些服务器允许的位置；`BaselineControlAsync` 可捕获配置或使用已有基线；`UpdateVersionAsync` 通过版本或标签更新资源；`MergeAsync` 支持多个源、活动与自动检出/合并控制。服务端仍决定是否接受分支、并行开发和合并，库不自动解决内容冲突。

版本报告包括 version-tree、expand-property、locate-by-history、merge-preview、compare-baseline、latest-activity-version；对应表中的专用方法保留完整 XML，`WebDavVersioningResponse` 还投影 `Items`、`BaselineChanges`、`MergePreviews` 和 `LatestActivityVersion`。版本历史、工作资源、工作区、活动和基线是不同资源，不能把单次方法成功等同于整套服务器工作流完成。

### 多 URI 绑定和重定向引用

```csharp
(await client.BindAsync("aliases/", new BindingParameters
{
    Segment = "另一个名字.txt",
    Source = new Uri(client.BaseAddress!, remotePath),
    Overwrite = false
})).EnsureSuccessStatusCode();

var bindings = await client.GetBindingInfoAsync("aliases/");
bindings.EnsureSuccessStatusCode();
foreach (var item in bindings.Bindings)
    Console.WriteLine(item.ResourceId);

(await client.CreateRedirectReferenceAsync("shortcuts/latest",
    new RedirectReferenceParameters
    {
        Target = new Uri("../documents/file.txt", UriKind.Relative),
        Lifetime = RedirectReferenceLifetime.Temporary
    })).EnsureSuccessStatusCode();

var reference = await client.GetRedirectReferenceAsync("shortcuts/latest");
reference.EnsureSuccessStatusCode();
foreach (var item in reference.References)
    Console.WriteLine(item.Target);
```

BIND 为同一资源增加 URI，REBIND 移动源绑定，UNBIND 删除指定绑定；这些操作不复制文件正文。`Segment` 接收未转义的单个名字，由库按路径段编码；不得传入多级路径。`resource-id` 用于资源身份识别，`parent-set` 描述父绑定。库保留多个别名、208 Already Reported 和 508 Loop Detected，不根据绑定自动递归发请求。

重定向引用是由服务器保存的引用资源。`UpdateRedirectReferenceAsync` 修改目标或生命周期，省略字段保留原值；`GetRedirectReferenceAsync` 通过 `Apply-To-Redirect-Ref: T` 查询引用本身。相对目标按引用资源 URI 解析，可使用 `WebDavRedirectReference.ResolveTarget`；不会在客户端把引用变成内容复制，也不会自动跟随普通 3xx 重发 DAV 修改请求。

## 错误与生命周期

标准操作通常把 HTTP 失败保留在响应中，不自动重试。使用 `IsSuccessful` / `EnsureSuccessStatusCode()` 判断结果。COPY/MOVE/DELETE 的 HTTP 207 会返回 `WebDavMultiStatusResponse`，其 `IsSuccessful` 同时检查内部资源/属性错误。PROPPATCH 也检查逐项结果和属性确认。PROPFIND 的可选属性 404 保留在 `PropertyStatuses` 中；资源级错误会使整体结果失败。

便捷方法会按自身契约提前检查状态：`DownloadFileAsync` 和 `EnumerateAsync` 对非成功 HTTP 状态抛出 `HttpRequestException`；`ExistsAsync` 仅把 404 转成 false，其他非成功状态也抛异常。使用 `GetRawFile` 取得流时，应先检查返回状态再消费正文。

网络、取消、无效 URI、超限/错误/截断 XML 会抛异常。成功的属性请求必须返回合法 HTTP 207；HTML 登录页不会变成成功的空目录。通用目录查询允许有效的空 multistatus；同步应用应额外核对所需资源和关键属性。

非流式操作在返回前释放网络响应。`GetRawFile` / `GetProcessedFile`、`GetRangesAsync` 以及返回 `WebDavWriteResponse` 的写操作结果支持 `using` 和 `await using`，关闭结果或其正文流会释放响应。多范围分段流仅在当前枚举位置有效，完整枚举或提前结束枚举都会释放多范围响应。`GetFileResponse` / `SendAsync` 返回的原始响应由调用方释放，`SendAsync` 的请求也由调用方释放。上传默认借用 Stream，`LeaveOpen = false` 可以转交所有权；PUT、POST 和 PATCH 所接收的 HttpContent 由对应操作释放。下载目标流始终由调用方管理。

| 资源 | 所有权及释放时机 |
| --- | --- |
| 库自行创建的 `HttpClient` | 客户端拥有，释放 WebDavClient 时关闭 |
| 宿主传入的 `HttpClient` | 默认借用；指定 `disposeHttpClient: true` 后转交所有权 |
| 非流式操作的 HTTP 响应 | 方法返回前关闭，状态和头部已复制为快照 |
| `WebDavStreamResponse` / `WebDavWriteResponse` 及其 Stream | 调用方关闭结果或流，连同原响应一起释放 |
| `WebDavRangeResponse` | 调用方关闭，或在完整/提前结束分段枚举时释放 |
| `WebDavRangePart.Stream` | 只借用当前分段；进入下一段后不可再读取，不独立拥有整个响应 |
| `GetFileResponse` 的原始响应 | 调用方负责关闭 |
| `SendAsync` 的原始请求和响应 | 均由调用方关闭 |
| PUT / POST / PATCH 的源 Stream | 默认保留，`LeaveOpen = false` 时随上传正文释放 |
| PUT / POST / PATCH 的 HttpContent | 由操作释放，其内部资源按该正文自己的规则处理 |
| 下载目标 Stream | 由调用方创建、定位和关闭 |

`new WebDavClient(httpClient)` 默认借用 HttpClient；传入 `disposeHttpClient: true` 可转交所有权。释放 WebDavClient 会取消自身的在途请求和响应体；借用的 HttpClient 仍可使用。一个客户端可以并发使用，各请求的参数对象应独立创建，传输中不应修改。

客户端默认请求超时 100 秒，计时包含响应体读取及等待调用方继续读取，可设 `Timeout.InfiniteTimeSpan`。使用外部 HttpClient 时，其超时也会生效。锁超时是服务器锁租约，与请求超时不同。默认不自动跟随重定向，也不使用系统默认凭据；代理及认证可配置，自定义证书策略可通过注入的 HttpClientHandler 配置。

`EnumerateAsync` 流式返回条目，必须完整枚举才能确认文档结尾有效；需要原子目录快照的调用方应使用 `Propfind` 或先收集完全部条目。HTTP Range、扩展方法、可写属性和锁的实际支持均由服务器决定。

流式枚举先检查外层 HTTP 状态，但仍可能产出带资源级失败状态的有效 DAV 条目。消费每个 `WebDavResource` 时，应同时检查它的 `StatusCode` 和业务所需的 `PropertyStatuses`，不能把协议结构有效等同于该资源操作成功。

## 复杂处理逻辑

### 请求与正文共享同一个生命周期

调度器以 `ResponseHeadersRead` 获取响应头。成功后，关联取消令牌源交给 `LifetimeContent`；不能在发送方法返回时就释放它，否则随后的正文读取会失去请求超时控制。包装器复制正文头，取消时关闭原始正文，正常释放时先注销取消回调，再关闭正文和令牌源。

`WebDavReadStream` 组合请求级令牌和单次读取令牌，并把取消关闭正文造成的底层异常恢复为取消语义。流和响应可能相互触发关闭，所以底层包装器使用原子释放标志保证回调与资源清理至多执行一次。

### 完整 XML 校验与逐项结果

解析器同时匹配 `DAV:` 和元素名称，禁用 DTD、外部解析器并对整个文档应用字符限制。每个 response 使用子读取器单独物化，关闭子读取器后主读取器停在当前条目末尾，从而不会跳过紧邻的下一条资源。

读到 multistatus 的结束标签后继续消费到文档末尾，保证额外根节点及非法尾随内容也会报错。异步 XML 读取必须等待底层任务真正结束；取消通过关闭正文打断，不提前遗弃仍在执行的读取，避免释放 XmlReader 时掩盖取消异常。

HTTP 207 仅表示存在多个结果。PROPPATCH 额外确认全部请求属性均有对应结果；COPY、MOVE、DELETE、集合 LOCK 和各扩展修改保留内部失败。资源值字典只接收成功 propstat，失败属性仍保留状态，未知长度和无效日期保持 null。通用 REPORT 同时保留完整根与可选资源投影，不能把非 multistatus XML 当作成功的空目录；同步、QSD、DeltaV 等专用 API 还会验证各自要求的成功状态与文档形态。

### 原始字节、范围与重发

下载关闭自动解压并请求 identity 编码，使 Content-Length、Content-Range 与实际传输字节保持同一坐标系。对 206 校验 bytes 单位、起止位置、总长度边界和本段长度；单段请求还检查起点及上界，后缀范围在总长度已知时反推起点。多范围正文按每段已验证的字节数消费，不在二进制数据中搜索 MIME 边界；完整消费后结合总长度计算未返回的请求范围，允许标准规定的合法子集响应。

上传和下载正文借用固定大小的共享缓冲区，不按文件长度分配内存。可定位源流记录初始位置和剩余长度，认证等场景再次序列化正文时恢复到该位置，进度重新从零累计；起点超过流末尾时按空正文处理。不可定位流再次序列化时会抛出异常，防止发送已消费后的空正文或残余内容。上传正文重复释放时至多关闭一次源流。库不为写操作增加自动重试，普通 HTTP 认证挑战由底层处理器处理。

### NativeAOT 与文档产物

库启用 `IsAotCompatible`、`IsTrimmable` 和对应分析器，使用显式 XML 读取和模型构造，不依赖运行时反射生成序列化器。发布原生应用时仍需目标平台对应的编译工具链。

所有源码文件具有完整 MIT 文件头，类型、构造函数、枚举值、字段、属性和方法都提供中文 XML 文档；参数、返回值、默认值、异常及资源所有权在对应声明处说明。复杂分支另有行内注释解释协议与生命周期约束。

构建时自动生成 `MonsterSyncDrive.WebDAV.xml`，与程序集位于同一输出目录，例如 `bin/Release/net10.0/`。引用或分发库时保留该 XML 文件，即可在支持的 IDE 中显示 API 文档。

## 验证

```powershell
dotnet test Test.MonsterSyncDrive.WebDAV/Test.MonsterSyncDrive.WebDAV.csproj -c Release
dotnet build MonsterSyncDrive.Remote.WebDAV/MonsterSyncDrive.Remote.WebDAV.csproj -c Release
dotnet publish Test.MonsterSyncDrive.WebDAV/Test.MonsterSyncDrive.WebDAV.csproj -c Release -r win-x64 -p:AotSmoke=true -o artifacts/webdav-native
./artifacts/webdav-native/Test.MonsterSyncDrive.WebDAV.exe
```

本次代码清理验证（2026-10-26，Windows x64，.NET SDK 10.0.403）：仅执行 `MonsterSyncDrive.WebDAV` 和 `MonsterSyncDrive.Remote.WebDAV` 的 Release 编译，均为零警告、零错误；未运行测试或 NativeAOT 发布。

清理前的验证记录：Release 测试 489 项全部通过，无跳过；Windows x64 NativeAOT 发布并运行成功；远程适配工程编译为零警告、零错误。测试工程位于解决方案的 `200.测试工程` 文件夹。

测试不依赖外部 WebDAV 账号。包含真实 TCP/HTTP 模拟服务器和可控的 HttpMessageHandler：覆盖完整文件生命周期、Basic 挑战认证、并发、分块上传、属性、锁、If 条件、错误 XML、单/多范围响应、搜索/QSD/配额、REPORT/增量分页、ACL/主体、扩展集合、POST/PATCH/Prefer、DeltaV、绑定、重定向引用以及取消/释放。`AotSmoke=true` 在同一测试工程中启用无测试框架的原生验证入口，运行基础的真实 TCP/HTTP 流程和扩展的进程内请求/响应场景，并将整个库作为裁剪分析根；运行时还检查动态代码不可用。NativeAOT 发布需要平台对应的原生工具链；Linux/macOS 请替换为本机 RID 和可执行文件名。

## 协议参考

- 基础 DAV、HTTP 范围与条件：[RFC 4918]、[RFC 9110]。
- 搜索、配额、增量同步：[RFC 5323]、[RFC 4331]、[RFC 6578]。
- 当前认证主体、ACL 与用户/组报告：[RFC 5397]、[RFC 3744]。
- 集合类型、服务器分配 URL、PATCH 与偏好：[RFC 5689]、[RFC 5995]、[RFC 5789]、[RFC 7240]、[RFC 8144]。
- DeltaV 版本、资源绑定和重定向引用：[RFC 3253]、[RFC 5842]、[RFC 4437]。

[RFC 4918]: https://www.rfc-editor.org/rfc/rfc4918.html
[RFC 9110]: https://www.rfc-editor.org/rfc/rfc9110.html
[RFC 5323]: https://www.rfc-editor.org/rfc/rfc5323.html
[RFC 4331]: https://www.rfc-editor.org/rfc/rfc4331.html
[RFC 6578]: https://www.rfc-editor.org/rfc/rfc6578.html
[RFC 5397]: https://www.rfc-editor.org/rfc/rfc5397.html
[RFC 3744]: https://www.rfc-editor.org/rfc/rfc3744.html
[RFC 5689]: https://www.rfc-editor.org/rfc/rfc5689.html
[RFC 5995]: https://www.rfc-editor.org/rfc/rfc5995.html
[RFC 5789]: https://www.rfc-editor.org/rfc/rfc5789.html
[RFC 7240]: https://www.rfc-editor.org/rfc/rfc7240.html
[RFC 8144]: https://www.rfc-editor.org/rfc/rfc8144.html
[RFC 3253]: https://www.rfc-editor.org/rfc/rfc3253.html
[RFC 5842]: https://www.rfc-editor.org/rfc/rfc5842.html
[RFC 4437]: https://www.rfc-editor.org/rfc/rfc4437.html

## 许可证

本项目使用 [MIT License](../LICENSE)。源码文件头、项目文件头和本文档头部包含完整许可文本；分发时应保留版权及许可声明。
