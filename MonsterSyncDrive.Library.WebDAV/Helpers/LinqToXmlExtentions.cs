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

namespace MonsterSyncDrive.WebDAV.Helpers;

/// <summary>
/// 提供 XML 声明序列化及内层 XML 读写辅助方法。
/// </summary>
internal static class LinqToXmlExtentions
{
    /// <summary>
    /// 序列化 XML 文档，并在正文前保留 XML 声明与换行。
    /// </summary>
    /// <param name="doc">待序列化的 XML 文档，不应为 null。</param>
    /// <returns>文档声明和正文拼接得到的 XML 字符串。</returns>
    public static string ToStringWithDeclaration(this XDocument doc)
    {
        ArgumentNullException.ThrowIfNull(doc, nameof(doc));

        return doc.Declaration + Environment.NewLine + doc;
    }

    /// <summary>
    /// 读取元素内部的 XML 片段，保留嵌套元素、文本与必要命名空间。
    /// </summary>
    /// <param name="element">要读取内容的完整 XML 元素。</param>
    /// <returns>不包含当前元素外层起止标签的内层 XML。</returns>
    public static string GetInnerXml(this XElement element)
    {
        using (var reader = element.CreateReader())
        {
            reader.MoveToContent();
            return reader.ReadInnerXml();
        }
    }

    /// <summary>
    /// 解析 XML 片段并替换指定元素的全部子节点。
    /// </summary>
    /// <remarks>片段放入临时根元素后解析，支持多个相邻节点和混合内容；所需前缀必须在片段中有有效命名空间声明。</remarks>
    /// <param name="element">接收解析后子节点的目标元素。</param>
    /// <param name="innerXml">有效的内层 XML 片段；纯文本的特殊字符应先转义。</param>
    /// <exception cref="System.Xml.XmlException">片段不是有效 XML，或使用未声明的命名空间前缀。</exception>
    public static void SetInnerXml(this XElement element, string innerXml) => element.ReplaceNodes(XElement.Parse("<dummy>" + innerXml + "</dummy>").Nodes());
}
