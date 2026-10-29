using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;

namespace Yurt.Application.Features.Menu.Import;

/// <summary>
/// Reads the first sheet of an Excel file (.xlsx) or a CSV file into rows of cell texts, without any library: an
/// .xlsx is a zip of XML parts. The old binary .xls format is not read; Excel and Google Sheets can save as .xlsx.
/// </summary>
public static class SpreadsheetReader
{
    /// <summary>Largest unpacked XML part we read, so a small zip that unpacks to gigabytes is refused.</summary>
    private const long MaxPartBytes = 30 * 1024 * 1024;

    public static List<string[]> Read(Stream file, string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".xlsx" => ReadXlsx(file),
            ".csv" or ".txt" => ReadCsv(file),
            ".xls" => throw new SpreadsheetException("Old .xls files are not supported. In Excel choose File → Save As → Excel Workbook (.xlsx)."),
            _ => throw new SpreadsheetException("Upload an Excel (.xlsx) or CSV file."),
        };
    }

    // ── xlsx ─────────────────────────────────────────────────────────────────

    private static List<string[]> ReadXlsx(Stream file)
    {
        ZipArchive zip;
        try { zip = new ZipArchive(file, ZipArchiveMode.Read, leaveOpen: true); }
        catch (InvalidDataException) { throw new SpreadsheetException("The file is not a valid .xlsx file."); }

        using (zip)
        {
            var shared = ReadSharedStrings(zip);
            var sheetPath = FirstSheetPath(zip);
            var sheet = zip.GetEntry(sheetPath) ?? throw new SpreadsheetException("The file has no sheet.");
            return ReadSheet(sheet, shared);
        }
    }

    private static XmlReader OpenPart(ZipArchiveEntry entry)
    {
        if (entry.Length > MaxPartBytes) throw new SpreadsheetException("The file is too large.");
        return XmlReader.Create(entry.Open(), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
    }

    /// <summary>Path of the first sheet in the workbook's order (the one Excel opens on), via the workbook's relations.</summary>
    private static string FirstSheetPath(ZipArchive zip)
    {
        string? relId = null;
        var wb = zip.GetEntry("xl/workbook.xml");
        if (wb is not null)
        {
            using var r = OpenPart(wb);
            while (r.Read())
                if (r.NodeType == XmlNodeType.Element && r.LocalName == "sheet")
                {
                    relId = r.GetAttribute("id", "http://schemas.openxmlformats.org/officeDocument/2006/relationships");
                    break;
                }
        }
        var rels = zip.GetEntry("xl/_rels/workbook.xml.rels");
        if (relId is not null && rels is not null)
        {
            using var r = OpenPart(rels);
            while (r.Read())
                if (r.NodeType == XmlNodeType.Element && r.LocalName == "Relationship" && r.GetAttribute("Id") == relId)
                {
                    var target = r.GetAttribute("Target") ?? "";
                    return target.StartsWith('/') ? target.TrimStart('/') : "xl/" + target;
                }
        }
        return "xl/worksheets/sheet1.xml";
    }

    private static List<string> ReadSharedStrings(ZipArchive zip)
    {
        var list = new List<string>();
        var entry = zip.GetEntry("xl/sharedStrings.xml");
        if (entry is null) return list;
        using var r = OpenPart(entry);
        StringBuilder? current = null;
        while (r.Read())
        {
            if (r.NodeType == XmlNodeType.Element && r.LocalName == "si")
            {
                current = new StringBuilder();
                if (r.IsEmptyElement) { list.Add(""); current = null; }
            }
            // Phonetic hints (rPh) repeat the text in another script; only the main runs count.
            else if (r.NodeType == XmlNodeType.Element && r.LocalName == "rPh") r.Skip();
            else if (r.NodeType == XmlNodeType.Element && r.LocalName == "t" && current is not null) current.Append(r.ReadElementContentAsString());
            else if (r.NodeType == XmlNodeType.EndElement && r.LocalName == "si" && current is not null)
            {
                list.Add(current.ToString());
                current = null;
            }
        }
        return list;
    }

    private static List<string[]> ReadSheet(ZipArchiveEntry sheet, List<string> shared)
    {
        var rows = new List<string[]>();
        using var r = OpenPart(sheet);
        List<string>? row = null;
        int rowNumber = 0;
        while (r.Read())
        {
            if (r.NodeType == XmlNodeType.Element && r.LocalName == "row")
            {
                // Keep the sheet's row numbers, so "row 7" in an error is row 7 in Excel.
                var n = int.TryParse(r.GetAttribute("r"), out var parsed) ? parsed : rowNumber + 1;
                while (rows.Count < n - 1) rows.Add([]);
                rowNumber = n;
                row = [];
                if (r.IsEmptyElement) { rows.Add([]); row = null; }
            }
            else if (r.NodeType == XmlNodeType.EndElement && r.LocalName == "row" && row is not null)
            {
                rows.Add([.. row]);
                row = null;
            }
            else if (r.NodeType == XmlNodeType.Element && r.LocalName == "c" && row is not null)
            {
                var col = ColumnIndex(r.GetAttribute("r")) ?? row.Count;
                var type = r.GetAttribute("t");
                var text = ReadCell(r, type, shared);
                while (row.Count < col) row.Add("");
                if (row.Count == col) row.Add(text); else row[col] = text;
            }
        }
        return rows;
    }

    private static string ReadCell(XmlReader r, string? type, List<string> shared)
    {
        if (r.IsEmptyElement) return "";
        var depth = r.Depth;
        string? value = null;
        var inline = new StringBuilder();
        while (r.Read() && !(r.NodeType == XmlNodeType.EndElement && r.Depth == depth))
        {
            if (r.NodeType != XmlNodeType.Element) continue;
            if (r.LocalName == "v") value = r.ReadElementContentAsString();
            else if (r.LocalName == "rPh") r.Skip();
            else if (r.LocalName == "t") inline.Append(r.ReadElementContentAsString());
            // A formula's text is not what the cell shows; its cached value (v) is.
            else if (r.LocalName == "f") r.Skip();
            if (r.NodeType == XmlNodeType.EndElement && r.Depth == depth) break;
        }
        return type switch
        {
            "s" => int.TryParse(value, out var i) && i >= 0 && i < shared.Count ? shared[i] : "",
            "inlineStr" => inline.ToString(),
            "b" => value == "1" ? "TRUE" : "FALSE",
            _ => value ?? inline.ToString(),
        };
    }

    /// <summary>"C7" → 2 (zero-based column).</summary>
    private static int? ColumnIndex(string? cellRef)
    {
        if (string.IsNullOrEmpty(cellRef)) return null;
        var col = 0;
        var any = false;
        foreach (var ch in cellRef)
        {
            if (ch is >= 'A' and <= 'Z') { col = col * 26 + (ch - 'A' + 1); any = true; }
            else break;
        }
        return any ? col - 1 : null;
    }

    // ── csv ──────────────────────────────────────────────────────────────────

    private static List<string[]> ReadCsv(Stream file)
    {
        string text;
        using (var reader = new StreamReader(file, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true))
            text = reader.ReadToEnd();

        // Excel in Russian and Kazakh locales saves CSV with semicolons; pick whichever the header line uses more.
        var firstLine = text.Split('\n', 2)[0];
        var sep = firstLine.Count(c => c == ';') > firstLine.Count(c => c == ',') ? ';'
            : firstLine.Contains('\t') && !firstLine.Contains(',') ? '\t' : ',';

        var rows = new List<string[]>();
        var row = new List<string>();
        var cell = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; }
                else if (c == '"') quoted = false;
                else cell.Append(c);
            }
            else if (c == '"' && cell.Length == 0) quoted = true;
            else if (c == sep) { row.Add(cell.ToString()); cell.Clear(); }
            else if (c == '\r') { }
            else if (c == '\n') { row.Add(cell.ToString()); cell.Clear(); rows.Add([.. row]); row = []; }
            else cell.Append(c);
        }
        if (cell.Length > 0 || row.Count > 0) { row.Add(cell.ToString()); rows.Add([.. row]); }
        return rows;
    }

    /// <summary>
    /// A price as typed by a person: "1 200", "1200,50", "1 200 ₸", "1200.5". Null when it is not a number.
    /// </summary>
    public static decimal? ParsePrice(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var cleaned = new StringBuilder();
        foreach (var ch in text.Trim())
        {
            if (char.IsDigit(ch) || ch == '-') cleaned.Append(ch);
            else if (ch is ',' or '.') cleaned.Append('.');
            else if (char.IsWhiteSpace(ch) || ch is ' ' or ' ' or '₸' or '\'') continue;
            else if (char.IsLetter(ch)) continue; // "тг", "tg", "KZT"
            else return null;
        }
        var s = cleaned.ToString();
        // "1.200.50" is ambiguous; a single separator is the decimal point.
        if (s.Count(c => c == '.') > 1) return null;
        return decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : null;
    }
}

public class SpreadsheetException(string message) : Exception(message);
