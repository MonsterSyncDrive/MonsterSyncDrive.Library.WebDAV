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
using System.Net;

namespace MonsterSyncDrive.Library.WebDAV.Request;

/// <summary>
/// 配置由 WebDavClient 自行创建和拥有的 HTTP 客户端。
/// </summary>
/// <remarks>默认关闭自动重定向和系统默认凭据；请求超时延伸到正文阶段，创建客户端后修改配置不会重新配置现有处理器。</remarks>
public class WebDavClientParams
{
    /// <summary>
    /// 初始化 WebDavClientParams 的默认请求设置。
    /// </summary>
    /// <remarks>关闭系统默认凭据和自动重定向，开启代理及预认证；未设置地址或超时时，分别要求绝对请求 URI 和采用 100 秒超时。</remarks>
    public WebDavClientParams()
    {
        UseDefaultCredentials = false;
        DefaultRequestHeaders = new Dictionary<string, string>();
        PreAuthenticate = true;
        UseProxy = true;
    }

    /// <summary>
    /// 获取或设置是否使用当前进程的系统默认身份凭据。
    /// </summary>
    /// <value>默认 false；设置显式 Credentials 时客户端关闭系统默认凭据。</value>
    public bool UseDefaultCredentials
    {
        get; set;
    }

    /// <summary>
    /// 获取或设置相对请求地址使用的 HTTP(S) 基地址。
    /// </summary>
    /// <value>默认 null，只能发送绝对 URI；目录基地址通常应以 / 结尾，不允许内嵌凭据或片段。</value>
    public Uri? BaseAddress
    {
        get; set;
    }

    /// <summary>
    /// 获取或设置 HTTP 身份认证使用的显式凭据。
    /// </summary>
    /// <value>默认 null；支持的认证方式由平台 HTTP 处理器与服务器共同决定。</value>
    public ICredentials? Credentials
    {
        get; set;
    }

    /// <summary>
    /// 获取或设置创建客户端时复制到 HttpClient 的默认请求头。
    /// </summary>
    /// <value>默认空字典；请求级 Headers 可覆盖同名默认头，客户端创建后修改字典不会重新应用。</value>
    public IDictionary<string, string> DefaultRequestHeaders
    {
        get; set;
    }

    /// <summary>
    /// 获取或设置 HTTP 处理器的预身份认证选项。
    /// </summary>
    /// <value>默认 true；首次访问某个认证范围仍可能需要服务器先发起挑战，不保证首个请求就带凭据。</value>
    public bool PreAuthenticate
    {
        get; set;
    }

    /// <summary>
    /// 获取或设置显式网络代理。
    /// </summary>
    /// <value>默认 null 使用 HTTP 处理器的系统代理配置；UseProxy 为 false 时不使用代理。</value>
    public IWebProxy? Proxy
    {
        get; set;
    }

    /// <summary>
    /// 获取或设置覆盖请求头、正文传输与正文读取等待的请求超时。
    /// </summary>
    /// <value>null 使用 100 秒；Timeout.InfiniteTimeSpan 禁用本库的请求计时，不等同于锁租约 Timeout。</value>
    public TimeSpan? Timeout
    {
        get; set;
    }

    /// <summary>
    /// 获取或设置 HTTP 处理器是否使用网络代理。
    /// </summary>
    /// <value>默认 true；本机模拟服务或不经代理的连接可显式关闭。</value>
    public bool UseProxy
    {
        get; set;
    }

    /// <summary>
    /// 获取或设置 HTTP 处理器是否自动跟随重定向。
    /// </summary>
    /// <value>默认 false，避免通用重定向改变 DAV 方法或重发带认证信息的请求。</value>
    public bool AllowAutoRedirect
    {
        get; set;
    }

    /// <summary>
    /// 获取或设置单个响应 XML 文档的字符总数上限。
    /// </summary>
    /// <value>默认 64 × 1024 × 1024 个字符，必须为正数；这是字符计数而非网络字节数。</value>
    public long MaxXmlCharacters { get; set; } = 64 * 1024 * 1024;
}
