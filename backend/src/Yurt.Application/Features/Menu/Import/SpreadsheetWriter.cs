using System.Globalization;
using System.IO.Compression;
using System.Security;
using System.Text;

namespace Yurt.Application.Features.Menu.Import;

/// <summary>Writes sheets of text and number cells as a minimal .xlsx file (menu template, profit and accounting exports).</summary>
public static class SpreadsheetWriter
{
    public static byte[] Write(string sheetName, IReadOnlyList<IReadOnlyList<object?>> rows)
        => Write([(sheetName, rows)]);

    /// <summary>One sheet per entry, in this order. Sheet names are cut to Excel's 31 characters and must differ.</summary>
    public static byte[] Write(IReadOnlyList<(string Name, IReadOnlyList<IReadOnlyList<object?>> Rows)> sheets)
    {
        if (sheets.Count == 0) throw new ArgumentException("At least one sheet is needed.", nameof(sheets));
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            var overrides = new StringBuilder();
            var entries = new StringBuilder();
            var rels = new StringBuilder();
            for (var i = 1; i <= sheets.Count; i++)
            {
                overrides.Append($"""<Override PartName="/xl/worksheets/sheet{i}.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>""");
                entries.Append($"""<sheet name="{SecurityElement.Escape(SheetName(sheets[i - 1].Name))}" sheetId="{i}" r:id="rId{i}"/>""");
                rels.Append($"""<Relationship Id="rId{i}" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet{i}.xml"/>""");
            }
            Add(zip, "[Content_Types].xml",
                $"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>{overrides}<Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/></Types>""");
            Add(zip, "_rels/.rels",
                """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>""");
            Add(zip, "xl/workbook.xml",
                $"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets>{entries}</sheets></workbook>""");
            var stylesId = sheets.Count + 1;
            Add(zip, "xl/_rels/workbook.xml.rels",
                $"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">{rels}<Relationship Id="rId{stylesId}" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/></Relationships>""");
            // Style 1 = bold, for the header row.
            Add(zip, "xl/styles.xml",
                """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><fonts count="2"><font><sz val="11"/><name val="Calibri"/></font><font><b/><sz val="11"/><name val="Calibri"/></font></fonts><fills count="2"><fill><patternFill patternType="none"/></fill><fill><patternFill patternType="gray125"/></fill></fills><borders count="1"><border/></borders><cellStyleXfs count="1"><xf/></cellStyleXfs><cellXfs count="2"><xf/><xf fontId="1" applyFont="1"/></cellXfs></styleSheet>""");

            for (var i = 0; i < sheets.Count; i++)
                Add(zip, $"xl/worksheets/sheet{i + 1}.xml", SheetXml(sheets[i].Rows));
        }
        return ms.ToArray();
    }

    private static string SheetXml(IReadOnlyList<IReadOnlyList<object?>> rows)
    {
        var sheet = new StringBuilder();
        sheet.Append("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetFormatPr defaultRowHeight="15"/><cols><col min="1" max="20" width="22" customWidth="1"/></cols><sheetData>""");
        for (var r = 0; r < rows.Count; r++)
        {
            sheet.Append($"<row r=\"{r + 1}\">");
            for (var c = 0; c < rows[r].Count; c++)
            {
                var cellRef = ColumnName(c) + (r + 1);
                var style = r == 0 ? " s=\"1\"" : "";
                switch (rows[r][c])
                {
                    case null: break;
                    case decimal or int or double:
                        sheet.Append($"<c r=\"{cellRef}\"{style}><v>{Convert.ToString(rows[r][c], CultureInfo.InvariantCulture)}</v></c>");
                        break;
                    default:
                        sheet.Append($"<c r=\"{cellRef}\" t=\"inlineStr\"{style}><is><t xml:space=\"preserve\">{SecurityElement.Escape(rows[r][c]!.ToString())}</t></is></c>");
                        break;
                }
            }
            sheet.Append("</row>");
        }
        sheet.Append("</sheetData></worksheet>");
        return sheet.ToString();
    }

    /// <summary>Excel refuses sheet names over 31 characters or with any of : \ / ? * [ ].</summary>
    private static string SheetName(string name)
    {
        var clean = new string(name.Select(ch => ":\\/?*[]".Contains(ch) ? ' ' : ch).ToArray()).Trim();
        if (clean.Length == 0) clean = "Sheet";
        return clean.Length > 31 ? clean[..31] : clean;
    }

    private static void Add(ZipArchive zip, string path, string content)
    {
        using var w = new StreamWriter(zip.CreateEntry(path).Open(), new UTF8Encoding(false));
        w.Write(content);
    }

    private static string ColumnName(int index)
    {
        var name = "";
        for (index++; index > 0; index = (index - 1) / 26) name = (char)('A' + (index - 1) % 26) + name;
        return name;
    }
}
