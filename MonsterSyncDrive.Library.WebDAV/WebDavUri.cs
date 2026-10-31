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
namespace MonsterSyncDrive.Library.WebDAV;

/// <summary>
/// 提供与操作系统路径规则无关的 WebDAV URI 路径编码方法。
/// </summary>
public static class WebDavUri
{
    /// <summary>
    /// 逐个编码未转义路径段，保留斜杠分隔符及开头和结尾斜杠。
    /// </summary>
    /// <remarks>输入必须是普通路径，不是已编码 URL；百分号、空格、问号和井号按文件名内容编码。反斜杠当作文件名字符，不使用操作系统目录分隔规则。</remarks>
    /// <param name="path">使用 / 分隔的未转义路径，空字符串表示空 URI 引用。</param>
    /// <returns>可安全作为 URI 引用路径使用的已转义字符串。</returns>
    /// <exception cref="ArgumentNullException">path 为 null。</exception>
    public static string EncodePath(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return string.Join('/', path.Split('/').Select(Uri.EscapeDataString));
    }
}
