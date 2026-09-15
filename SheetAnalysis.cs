using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;

namespace MyWorkQuickLauncher;

/// <summary>시트(견적) 한 행. 2번째 열(<see cref="Item"/>)이 작업항목 비교 기준이다.</summary>
public sealed record SheetRow(
    string No,
    string Item,
    string Work,
    string TimeClaim,
    string PartAmount,
    string Labor);

/// <summary>
/// gomail 팝업 URL을 받아 견적 테이블을 파싱한다.
/// popup_detail1 / popup_detail2 / api/popup_detail 세 형식 모두 &lt;tr class='tableRow'&gt; 행에
/// [no, 작업항목, 작업, 시간/청구, 부품금액, 공임] 순으로 셀이 들어 있다.
/// </summary>
public static class SheetSource
{
    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var handler = new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All };
        var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(40) };
        client.DefaultRequestHeaders.Add("User-Agent", "MyWorkQuickLauncher/2.0");
        return client;
    }

    public static bool LooksLikeUrl(string value)
        => Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var uri)
           && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    public static async Task<List<SheetRow>> FetchAsync(string url, CancellationToken token = default)
    {
        byte[] bytes = await Http.GetByteArrayAsync(url.Trim(), token);
        // gomail 팝업은 UTF-8로 서빙된다.
        return Parse(Encoding.UTF8.GetString(bytes));
    }

    public static List<SheetRow> Parse(string html)
    {
        var rows = new List<SheetRow>();
        if (string.IsNullOrEmpty(html)) return rows;

        int tableStart = html.IndexOf("<table", StringComparison.OrdinalIgnoreCase);
        int tableEnd = html.IndexOf("</table>", StringComparison.OrdinalIgnoreCase);
        string scope = tableStart >= 0 && tableEnd > tableStart
            ? html.Substring(tableStart, tableEnd - tableStart)
            : html;

        foreach (Match rowMatch in Regex.Matches(
                     scope,
                     @"<tr\b[^>]*>(.*?)(?=<tr\b|</table>|$)",
                     RegexOptions.Singleline | RegexOptions.IgnoreCase))
        {
            string rowHtml = rowMatch.Value;
            if (!rowHtml.Contains("tableRow", StringComparison.OrdinalIgnoreCase))
                continue;

            var cells = Regex.Matches(rowHtml, @"<td\b[^>]*>(.*?)</td>", RegexOptions.Singleline | RegexOptions.IgnoreCase)
                .Select(m => CleanCell(m.Groups[1].Value))
                .ToList();

            if (cells.Count < 2) continue;

            string item = cells[1];
            if (string.IsNullOrWhiteSpace(item) || item is "작업항목" or "작업내용")
                continue;

            rows.Add(new SheetRow(
                Cell(cells, 0),
                item,
                Cell(cells, 2),
                Cell(cells, 3),
                Cell(cells, 4),
                Cell(cells, 5)));
        }

        return rows;
    }

    private static string Cell(List<string> cells, int index)
        => index >= 0 && index < cells.Count ? cells[index] : "";

    private static string CleanCell(string raw)
    {
        string noTags = Regex.Replace(raw, "<[^>]+>", " ");
        string decoded = WebUtility.HtmlDecode(noTags);
        return Regex.Replace(decoded, @"\s+", " ").Trim();
    }

    /// <summary>작업항목 비교 키: 공백을 모두 제거해 표기 차이를 흡수한다.</summary>
    public static string ItemKey(string item)
        => Regex.Replace(item ?? "", @"\s+", "").Trim();
}
