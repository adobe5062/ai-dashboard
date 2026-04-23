using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;

namespace Dashboard.Frontend.Helpers;

public static class MarkdownHelper
{
    private static readonly Regex FenceSplit   = new(@"(```[\w]*\n[\s\S]*?```)", RegexOptions.Compiled);
    private static readonly Regex FenceExtract = new(@"^```[\w]*\n([\s\S]*?)```$", RegexOptions.Compiled);
    private static readonly Regex Bold         = new(@"\*\*(.+?)\*\*", RegexOptions.Compiled);
    private static readonly Regex InlineCode   = new(@"`([^`]+)`", RegexOptions.Compiled);

    public static MarkupString Render(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return new MarkupString("");

        var sb = new StringBuilder();
        foreach (var part in FenceSplit.Split(text))
        {
            var fence = FenceExtract.Match(part);
            if (fence.Success)
            {
                var code = WebUtility.HtmlEncode(fence.Groups[1].Value);
                sb.Append($"""<pre style="background:var(--base);border:1px solid var(--bord);padding:12px 14px;margin:8px 0;font-family:'JetBrains Mono',monospace;font-size:11px;color:var(--amber);overflow-x:auto;white-space:pre-wrap;line-height:1.6;">{code}</pre>""");
                continue;
            }

            foreach (var para in part.Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var lines = para.Split('\n').Select(line =>
                {
                    line = Bold.Replace(line, """<span style="color:var(--amber);font-weight:600;">$1</span>""");
                    line = InlineCode.Replace(line, """<code style="background:var(--surf2);color:var(--amber);border-radius:2px;padding:1px 6px;font-family:'JetBrains Mono',monospace;font-size:10px;">$1</code>""");
                    return line;
                });
                sb.Append($"<p style=\"margin:0 0 6px;\">{string.Join("<br />", lines)}</p>");
            }
        }
        return new MarkupString(sb.ToString());
    }
}
