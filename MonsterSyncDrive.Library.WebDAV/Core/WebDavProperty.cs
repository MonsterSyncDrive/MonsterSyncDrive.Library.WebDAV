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
using System.Xml.Linq;

namespace MonsterSyncDrive.Library.WebDAV.Core;

/// <summary>
/// 保存以完整 XML 名称标识的 WebDAV 属性及其内容。
/// </summary>
/// <remarks>Value 为内层 XML，TextValue 为解码文本；由完整元素构造时保留元素属性和混合内容，ToXElement 返回独立副本。</remarks>
public class WebDavProperty
{
    /// <summary>
    /// 使用完整属性名称和内层 XML 创建属性值。
    /// </summary>
    /// <remarks>此构造函数保存传入内容，不主动解析 XML；TextValue 或 ToXElement 在需要时解析，因此不合法片段会在访问阶段报告。</remarks>
    /// <param name="name">包含命名空间和本地名称的属性 XName。</param>
    /// <param name="value">属性的内层 XML；普通文本中的 XML 特殊字符应先转义，也可使用完整元素构造函数。</param>
    /// <exception cref="ArgumentNullException">属性名称为 null。</exception>
    public WebDavProperty(XName name, string value)
    {
        ArgumentNullException.ThrowIfNull(name);
        Name = name;
        Value = value;
    }

    /// <summary>
    /// 获取属性的完整 XML 名称，包含命名空间和本地名称。
    /// </summary>
    public XName Name
    {
        get;
    }

    /// <summary>
    /// 获取属性的内层 XML，保留嵌套元素、转义字符和混合内容。
    /// </summary>
    /// <value>普通文本可能包含 XML 转义；需要解码文本时使用 TextValue。</value>
    public string Value
    {
        get;
    }

    /// <summary>
    /// 获取属性的文本内容，复用已有 XML 快照或解析内层 XML，合并后代文本并解码实体。
    /// </summary>
    /// <value>若通过字符串构造时传入了无效的内层 XML，访问该属性会抛出 XML 解析异常。</value>
    public string TextValue => _element?.Value ?? XElement.Parse("<value>" + Value + "</value>").Value;

    /// <summary>
    /// 使用完整属性元素构造时保存的独立副本，用于保留元素属性、命名空间和混合内容。
    /// </summary>
    private readonly XElement? _element;

    /// <summary>
    /// 从完整 XML 属性元素创建独立快照，保留元素属性与混合内容。
    /// </summary>
    /// <param name="element">完整属性元素，不应为 null；构造时复制元素，避免外部修改影响快照。</param>
    /// <exception cref="ArgumentNullException">element 为 null。</exception>
    public WebDavProperty(XElement element) : this(element?.Name ?? throw new ArgumentNullException(nameof(element)), element.GetInnerXml())
    {
        _element = new XElement(element);
    }

    /// <summary>
    /// 返回完整属性元素的独立副本，或根据属性名称和内层 XML 重新构造元素。
    /// </summary>
    /// <remarks>使用完整元素构造时保留元素属性；仅由名称和字符串构造时，片段必须是有效且命名空间声明完整的 XML。</remarks>
    /// <returns>调用方可独立修改的 XElement，不与保存的元素快照共享实例。</returns>
    public XElement ToXElement()
    {
        // 从完整元素构造的属性保留了外层元素属性；返回副本，避免调用方修改保存的响应快照。
        if (_element is not null)
            return new XElement(_element);
        XElement result = new(Name);
        result.SetInnerXml(Value);
        return result;
    }

    /// <summary>
    /// 返回当前WebDavProperty的状态或属性概要，便于日志记录和调试。
    /// </summary>
    /// <returns>包含当前对象关键字段的描述字符串，不触发网络请求。</returns>
    public override string ToString()
    {
        return $"{{ Name: {Name}, Value: {Value} }}";
    }
}
