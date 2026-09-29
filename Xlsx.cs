using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace MyWorkQuickLauncher;

/// <summary>
/// 아주 작은 .xlsx(OOXML) 라이터. 외부 패키지(ClosedXML/EPPlus 등) 없이 BCL의
/// System.IO.Compression만으로 표 하나짜리 엑셀 파일을 만든다 - 이 프로젝트는
/// 외부 NuGet 의존성이 0개라는 원칙을 유지하기 위함이다.
/// 지원 범위는 이 앱이 실제로 쓰는 만큼만: 볼드, 글자색, 셀 배경색, 가로 정렬, 숫자 서식.
/// </summary>
internal static class Xlsx
{
    /// <summary>
    /// 한 셀에 적용할 서식(값 동등성으로 자동으로 중복 제거되어 스타일 테이블을 이룬다).
    /// <paramref name="Numeric"/>은 "숫자로 쓸지"(정렬 계산이 되는 실제 숫자 셀) 여부이고,
    /// <paramref name="NumberFormat"/>은 그중 천단위 구분처럼 커스텀 서식이 필요할 때만 채운다
    /// (null이면 숫자여도 Excel 기본 "General" 서식 - 소수점 있는 값을 그대로 보여준다).
    /// </summary>
    internal readonly record struct CellStyle(bool Bold, string? FillArgb, string? FontArgb, string Align, bool Numeric, string? NumberFormat);

    // ---- 이 앱의 "시트 분석 결과" 화면과 같은 볼드/배경색을 그대로 쓰는 사전 정의 스타일 ----
    private const string HeaderFill = "FF0F172A"; // Theme.HeaderBg
    private const string HeaderFont = "FFFFFFFF"; // 흰 글자
    private const string MissingFill = "FFFFF48F"; // 노란색 음영(누락 작업항목)
    private const string MoneyFormat = "#,##0";

    internal static readonly CellStyle HeaderLeft = new(true, HeaderFill, HeaderFont, "left", false, null);
    internal static readonly CellStyle HeaderCenter = HeaderLeft with { Align = "center" };
    internal static readonly CellStyle HeaderRight = HeaderLeft with { Align = "right" };

    internal static readonly CellStyle MissingLeft = new(true, MissingFill, null, "left", false, null);
    internal static readonly CellStyle MissingCenter = MissingLeft with { Align = "center" };
    internal static readonly CellStyle MissingRightGeneral = MissingLeft with { Align = "right", Numeric = true };
    internal static readonly CellStyle MissingRightMoney = MissingLeft with { Align = "right", Numeric = true, NumberFormat = MoneyFormat };

    internal static readonly CellStyle NormalLeft = new(false, null, null, "left", false, null);
    internal static readonly CellStyle NormalCenter = NormalLeft with { Align = "center" };
    internal static readonly CellStyle NormalRightGeneral = NormalLeft with { Align = "right", Numeric = true };
    internal static readonly CellStyle NormalRightMoney = NormalLeft with { Align = "right", Numeric = true, NumberFormat = MoneyFormat };

    // ---- 호출자가 지정한 배경색을 쓰는 스타일(예: 작업항목 병합 결과의 출처 A~D 색상 구분) ----
    internal static CellStyle TintedLeft(string fillArgb) => new(false, fillArgb, null, "left", false, null);
    internal static CellStyle TintedCenter(string fillArgb) => TintedLeft(fillArgb) with { Align = "center" };
    internal static CellStyle TintedRightGeneral(string fillArgb) => TintedLeft(fillArgb) with { Align = "right", Numeric = true };
    internal static CellStyle TintedRightMoney(string fillArgb) => TintedLeft(fillArgb) with { Align = "right", Numeric = true, NumberFormat = MoneyFormat };

    internal sealed class Cell
    {
        public string Text { get; }
        public CellStyle Style { get; }
        public Cell(string text, CellStyle style) { Text = text ?? ""; Style = style; }
    }

    /// <summary>표 하나를 <paramref name="path"/>에 .xlsx로 저장한다. rows[0]을 헤더 행으로 간주하지 않고 그대로 쓴다(호출자가 헤더도 rows에 포함).</summary>
    internal static void Save(string path, string sheetName, int[] columnWidthChars, IReadOnlyList<IReadOnlyList<Cell>> rows)
    {
        var (fontsXml, fillsXml, cellXfsXml, styleIndex) = BuildStyleSheet(rows);
        string sheetXml = BuildSheetXml(columnWidthChars, rows, styleIndex);

        if (File.Exists(path)) File.Delete(path);
        using var fileStream = new FileStream(path, FileMode.CreateNew);
        using var zip = new ZipArchive(fileStream, ZipArchiveMode.Create);

        WriteEntry(zip, "[Content_Types].xml", ContentTypesXml());
        WriteEntry(zip, "_rels/.rels", RootRelsXml());
        WriteEntry(zip, "xl/workbook.xml", WorkbookXml(Escape(sheetName)));
        WriteEntry(zip, "xl/_rels/workbook.xml.rels", WorkbookRelsXml());
        WriteEntry(zip, "xl/styles.xml", StylesXml(fontsXml, fillsXml, cellXfsXml));
        WriteEntry(zip, "xl/worksheets/sheet1.xml", sheetXml);
    }

    private static void WriteEntry(ZipArchive zip, string entryName, string content)
    {
        var entry = zip.CreateEntry(entryName, CompressionLevel.Optimal);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        writer.Write(content);
    }

    // ------------------------------------------------------------------ styles.xml

    /// <summary>커스텀 배경색 목록(중복 없이). 실제 OOXML fillId는 이 목록의 인덱스 + 2(0,1은 예약).</summary>
    private static (List<(bool Bold, string? FontArgb)> Fonts, List<string> CustomFills, Dictionary<CellStyle, int> Index)
        CollectStyleParts(IReadOnlyList<IReadOnlyList<Cell>> rows)
    {
        var fonts = new List<(bool, string?)> { (false, null) }; // fontId 0 = 기본
        var fontIndex = new Dictionary<(bool, string?), int> { [(false, null)] = 0 };
        var customFills = new List<string>();
        var fillIndex = new Dictionary<string, int>();
        var styleIndex = new Dictionary<CellStyle, int>();

        foreach (var row in rows)
        {
            foreach (var cell in row)
            {
                if (styleIndex.ContainsKey(cell.Style)) continue;

                var fontKey = (cell.Style.Bold, cell.Style.FontArgb);
                if (!fontIndex.ContainsKey(fontKey))
                {
                    fontIndex[fontKey] = fonts.Count;
                    fonts.Add(fontKey);
                }

                string? fillArgb = cell.Style.FillArgb;
                if (fillArgb != null && !fillIndex.ContainsKey(fillArgb))
                {
                    fillIndex[fillArgb] = customFills.Count;
                    customFills.Add(fillArgb);
                }

                styleIndex[cell.Style] = -1; // 나중 단계에서 실제 xf 인덱스로 채운다
            }
        }

        return (fonts, customFills, styleIndex);
    }

    private static (string FontsXml, string FillsXml, string CellXfsXml, Dictionary<CellStyle, int> StyleIndex) BuildStyleSheet(
        IReadOnlyList<IReadOnlyList<Cell>> rows)
    {
        var (fonts, customFills, styleIndex) = CollectStyleParts(rows);

        var fontIndex = new Dictionary<(bool, string?), int>();
        for (int i = 0; i < fonts.Count; i++) fontIndex[fonts[i]] = i;

        var fillIndex = new Dictionary<string, int>(); // 색상 -> 실제 OOXML fillId(0,1 예약 포함)
        for (int i = 0; i < customFills.Count; i++) fillIndex[customFills[i]] = i + 2;

        var fontsXml = new StringBuilder();
        foreach (var (bold, colorArgb) in fonts)
        {
            fontsXml.Append("<font>");
            if (bold) fontsXml.Append("<b/>");
            fontsXml.Append("<sz val=\"10\"/>");
            if (colorArgb != null) fontsXml.Append($"<color rgb=\"{colorArgb}\"/>");
            fontsXml.Append("<name val=\"맑은 고딕\"/>");
            fontsXml.Append("</font>");
        }

        var fillsXml = new StringBuilder();
        fillsXml.Append("<fill><patternFill patternType=\"none\"/></fill>");
        fillsXml.Append("<fill><patternFill patternType=\"gray125\"/></fill>");
        foreach (var color in customFills)
        {
            fillsXml.Append(
                $"<fill><patternFill patternType=\"solid\"><fgColor rgb=\"{color}\"/><bgColor indexed=\"64\"/></patternFill></fill>");
        }

        // numFmtId 164 = 이 앱에서 쓰는 유일한 커스텀 서식(천단위 구분, 소수점 없음)
        var cellXfsXml = new StringBuilder();
        cellXfsXml.Append("<xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/>"); // 인덱스 0 = 기본값
        int nextXf = 1;
        var finalIndex = new Dictionary<CellStyle, int> { };

        foreach (var style in styleIndex.Keys.ToList())
        {
            int fontId = fontIndex[(style.Bold, style.FontArgb)];
            int fillId = style.FillArgb != null ? fillIndex[style.FillArgb] : 0;
            int numFmtId = style.NumberFormat != null ? 164 : 0;

            cellXfsXml.Append(
                $"<xf numFmtId=\"{numFmtId}\" fontId=\"{fontId}\" fillId=\"{fillId}\" borderId=\"0\" xfId=\"0\" " +
                $"applyFont=\"1\" applyFill=\"{(fillId > 0 ? 1 : 0)}\" applyAlignment=\"1\" applyNumberFormat=\"{(numFmtId != 0 ? 1 : 0)}\">" +
                $"<alignment horizontal=\"{style.Align}\" vertical=\"center\"/></xf>");
            finalIndex[style] = nextXf;
            nextXf++;
        }

        return (fontsXml.ToString(), fillsXml.ToString(), cellXfsXml.ToString(), finalIndex);
    }

    // ------------------------------------------------------------------ sheet1.xml

    private static string BuildSheetXml(int[] columnWidthChars, IReadOnlyList<IReadOnlyList<Cell>> rows, Dictionary<CellStyle, int> styleIndex)
    {
        var sb = new StringBuilder();
        sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        sb.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">");

        if (columnWidthChars.Length > 0)
        {
            sb.Append("<cols>");
            for (int i = 0; i < columnWidthChars.Length; i++)
                sb.Append($"<col min=\"{i + 1}\" max=\"{i + 1}\" width=\"{columnWidthChars[i]}\" customWidth=\"1\"/>");
            sb.Append("</cols>");
        }

        sb.Append("<sheetData>");
        for (int r = 0; r < rows.Count; r++)
        {
            sb.Append($"<row r=\"{r + 1}\">");
            var row = rows[r];
            for (int c = 0; c < row.Count; c++)
            {
                var cell = row[c];
                string cellRef = ColumnLetter(c + 1) + (r + 1);
                int xf = styleIndex[cell.Style];

                if (cell.Style.Numeric && double.TryParse(
                        cell.Text.Replace(",", "").Trim(),
                        NumberStyles.Float, CultureInfo.InvariantCulture, out double number))
                {
                    sb.Append($"<c r=\"{cellRef}\" s=\"{xf}\"><v>{number.ToString(CultureInfo.InvariantCulture)}</v></c>");
                }
                else
                {
                    sb.Append($"<c r=\"{cellRef}\" s=\"{xf}\" t=\"inlineStr\"><is><t xml:space=\"preserve\">{Escape(cell.Text)}</t></is></c>");
                }
            }
            sb.Append("</row>");
        }
        sb.Append("</sheetData>");
        sb.Append("</worksheet>");
        return sb.ToString();
    }

    private static string ColumnLetter(int oneBasedIndex)
    {
        var s = new StringBuilder();
        int n = oneBasedIndex;
        while (n > 0)
        {
            int rem = (n - 1) % 26;
            s.Insert(0, (char)('A' + rem));
            n = (n - 1) / 26;
        }
        return s.ToString();
    }

    private static string Escape(string s) => s
        .Replace("&", "&amp;")
        .Replace("<", "&lt;")
        .Replace(">", "&gt;");

    // ------------------------------------------------------------------ 고정 파트

    private static string ContentTypesXml() =>
        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
        "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
        "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
        "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
        "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>" +
        "<Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>" +
        "<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>" +
        "</Types>";

    private static string RootRelsXml() =>
        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
        "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
        "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>" +
        "</Relationships>";

    private static string WorkbookXml(string sheetNameEscaped) =>
        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
        "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" " +
        "xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">" +
        $"<sheets><sheet name=\"{sheetNameEscaped}\" sheetId=\"1\" r:id=\"rId1\"/></sheets>" +
        "</workbook>";

    private static string WorkbookRelsXml() =>
        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
        "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
        "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/>" +
        "<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>" +
        "</Relationships>";

    private static string StylesXml(string fontsXml, string fillsXml, string cellXfsXml)
    {
        int fontCount = Regex.Matches(fontsXml, "<font>").Count;
        int fillCount = 2 + Regex.Matches(fillsXml, "<fill><patternFill patternType=\"solid\"").Count;
        int xfCount = Regex.Matches(cellXfsXml, "<xf ").Count; // 기본(인덱스 0) 포함해서 센다

        return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">" +
            "<numFmts count=\"1\"><numFmt numFmtId=\"164\" formatCode=\"#,##0\"/></numFmts>" +
            $"<fonts count=\"{fontCount}\">{fontsXml}</fonts>" +
            $"<fills count=\"{fillCount}\">{fillsXml}</fills>" +
            "<borders count=\"1\"><border><left/><right/><top/><bottom/><diagonal/></border></borders>" +
            "<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>" +
            $"<cellXfs count=\"{xfCount}\">{cellXfsXml}</cellXfs>" +
            "<cellStyles count=\"1\"><cellStyle name=\"Normal\" xfId=\"0\" builtinId=\"0\"/></cellStyles>" +
            "</styleSheet>";
    }
}
