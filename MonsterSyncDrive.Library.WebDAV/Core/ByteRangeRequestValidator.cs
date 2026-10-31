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
using System.Net.Http.Headers;

namespace MonsterSyncDrive.Library.WebDAV.Core;

/// <summary>校验单次范围请求的实际返回区间，并计算服务器合法部分满足请求时仍需续取的范围。</summary>
internal sealed class ByteRangeRequestValidator
{
    /// <summary>创建结果时复制的请求区间，不随调用方修改而变化。</summary>
    private readonly RangeItemHeaderValue[] _requested;
    /// <summary>已经返回的区间，只保存位置元数据，不保存文件正文。</summary>
    private readonly List<(long From, long To)> _received = [];
    /// <summary>服务器已经报告的完整表示长度；尚未知晓时为 null。</summary>
    public long? TotalLength
    {
        get; private set;
    }
    /// <summary>完整验证后尚未返回的可满足范围；总长度未知或尚未完整验证时为 null。</summary>
    public IReadOnlyList<RangeItemHeaderValue>? MissingRanges
    {
        get; private set;
    }

    /// <summary>为 bytes Range 请求建立独立的验证状态。</summary>
    /// <param name="request">已经校验为非空 bytes 单位的请求范围。</param>
    public ByteRangeRequestValidator(RangeHeaderValue request)
    {
        _requested = request.Ranges.ToArray();
    }

    /// <summary>验证一个完整的 Content-Range 和可选分段长度，并登记收到的区间。</summary>
    /// <param name="range">当前部分的字节范围。</param>
    /// <param name="contentLength">可选的本段 Content-Length。</param>
    /// <returns>可以用来限制分段读取的精确字节数。</returns>
    /// <exception cref="InvalidDataException">范围、长度、总长度或与原请求的关系不合法。</exception>
    public long Add(ContentRangeHeaderValue? range, long? contentLength)
    {
        (long from, long to) = GetInterval(range, contentLength);
        long length = to - from + 1;
        if (range!.Length is { } total)
        {
            if (TotalLength.HasValue && TotalLength != total)
                throw new InvalidDataException("Byte-range parts disagree about the representation length.");
            TotalLength = total;
        }
        if (TotalLength is { } known)
        {
            ValidateKnownInterval((from, to), Normalize(known));
        }
        else
        {
            // 总长度未知时无法反推后缀起点或被文件末尾截短的终点。没有后缀请求时，
            // 仍要求实际端点落在请求区间内；允许区间合并时覆盖两段之间的间隙。
            RangeItemHeaderValue[] absolute = _requested.Where(item => item.From.HasValue).ToArray();
            bool suffix = _requested.Any(item => !item.From.HasValue);
            if (!suffix && (!absolute.Any(item => from >= item.From && (!item.To.HasValue || from <= item.To))
                || !absolute.Any(item => to >= item.From && (!item.To.HasValue || to <= item.To))))
                throw new InvalidDataException("The returned interval lies outside the requested byte ranges.");
        }
        _received.Add((from, to));
        return length;
    }

    /// <summary>统一校验单段和多段的 bytes 区间、长度及整数边界。</summary>
    /// <param name="range">服务器返回的 Content-Range。</param>
    /// <param name="contentLength">可选的本段 Content-Length。</param>
    /// <returns>已验证的包含式起止位置。</returns>
    public static (long From, long To) GetInterval(ContentRangeHeaderValue? range, long? contentLength)
    {
        return range is null || !range.Unit.Equals("bytes", StringComparison.OrdinalIgnoreCase) || !range.HasRange
            || range.From is not { } from || range.To is not { } to || from < 0 || to < from
            || to - from == long.MaxValue || (range.HasLength && to >= range.Length)
            ? throw new InvalidDataException("Invalid byte Content-Range.")
            : contentLength is { } declared && declared != to - from + 1
            ? throw new InvalidDataException("Content-Length disagrees with Content-Range.")
            : ((long From, long To))(from, to);
    }

    /// <summary>完整读完响应后复核全部区间，并计算尚未返回的可满足请求区间。</summary>
    /// <remarks>RFC 9110 第 15.3.7 节允许服务器仅满足请求的一部分；缺失区间不构成协议错误。未知总长度时不推测后缀及文件末尾。</remarks>
    public void Complete()
    {
        if (_received.Count == 0)
            throw new InvalidDataException("A partial response contained no range parts.");
        if (TotalLength is not { } total)
            return;
        (long From, long To)[] expectedRanges = Normalize(total);
        // 最早的分段可能使用 */ 未声明总长度，后续分段才给出总长度，因此在此统一复核。
        foreach ((long From, long To) actual in _received)
            ValidateKnownInterval(actual, expectedRanges);
        (long From, long To)[] received = Merge(_received);
        List<RangeItemHeaderValue> missing = [];
        foreach ((long From, long To) in Merge(expectedRanges))
        {
            long next = From;
            bool covered = false;
            foreach ((long From, long To) actual in received)
            {
                if (actual.To < next)
                    continue;
                if (actual.From > To)
                    break;
                if (actual.From > next)
                    missing.Add(new RangeItemHeaderValue(next, actual.From - 1));
                if (actual.To >= To)
                {
                    covered = true;
                    break;
                }
                next = actual.To + 1;
            }
            if (!covered)
                missing.Add(new RangeItemHeaderValue(next, To));
        }
        MissingRanges = Array.AsReadOnly(missing.ToArray());
    }

    /// <summary>按已知总长度检查实际端点，允许请求子集以及跨请求间隙的合并。</summary>
    /// <param name="actual">实际返回的区间。</param>
    /// <param name="expected">已按文件长度归一化的请求范围。</param>
    private static void ValidateKnownInterval((long From, long To) actual, (long From, long To)[] expected)
    {
        // 部分满足不要求端点等于原请求端点，但两个端点必须位于请求范围内，
        // 否则服务器可能把完全无关的文件字节伪装成一个合法的部分结果。
        if (!expected.Any(item => item.From <= actual.From && item.To >= actual.From)
            || !expected.Any(item => item.From <= actual.To && item.To >= actual.To))
            throw new InvalidDataException("The returned interval does not match requested ranges or their coalescing.");
    }

    /// <summary>排序并合并重叠及相邻区间，只保留覆盖信息以便计算差集。</summary>
    /// <param name="ranges">已经验证的非负闭合区间。</param>
    /// <returns>按起点排列且互不相邻的区间。</returns>
    private static (long From, long To)[] Merge(IEnumerable<(long From, long To)> ranges)
    {
        List<(long From, long To)> merged = [];
        foreach ((long From, long To) range in ranges.OrderBy(item => item.From))
        {
            // 已知总长度最多为 Int64.MaxValue，因此有效终点最多是 MaxValue - 1。
            if (merged.Count == 0 || range.From > merged[^1].To + 1)
                merged.Add(range);
            else
                merged[^1] = (merged[^1].From, Math.Max(merged[^1].To, range.To));
        }
        return merged.ToArray();
    }

    /// <summary>按已知总长度解析闭合、开放和后缀区间，并省略不可满足的项。</summary>
    /// <param name="total">完整表示的字节长度。</param>
    /// <returns>已截断到文件末尾的有效区间元数据。</returns>
    private (long From, long To)[] Normalize(long total)
    {
        if (total <= 0)
            return [];
        List<(long From, long To)> result = [];
        foreach (RangeItemHeaderValue item in _requested)
        {
            if (item.From is { } start)
            {
                if (start < total)
                    result.Add((start, Math.Min(item.To ?? (total - 1), total - 1)));
            }
            else if (item.To is > 0)
                result.Add((Math.Max(0, total - item.To.Value), total - 1));
        }
        return result.ToArray();
    }
}

