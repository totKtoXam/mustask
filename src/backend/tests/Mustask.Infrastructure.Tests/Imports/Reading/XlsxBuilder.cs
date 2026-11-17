using System.IO.Compression;
using System.Security;
using System.Text;

namespace Mustask.Infrastructure.Tests.Imports.Reading;

/// <summary>
/// Builds made-up .xlsx files for the reader's tests as raw zip parts, so a test controls every byte — the hostile files (a macro,
/// an external link, a bomb) are the same builder with a tweak. No real workbook is ever used.
/// </summary>
internal sealed class XlsxBuilder
{
    public const int GeneralStyle = 0;

    public const int DateStyle = 1;

    public const int DateTimeStyle = 2;

    public const int TimeStyle = 3;

    private readonly List<(string Name, SheetBuilder Sheet)> _sheets = [];

    private readonly List<string> _shared = [];

    public bool Date1904 { get; set; }

    public List<(string Path, byte[] Content)> ExtraParts { get; } = [];

    public string? ExtraContentTypes { get; set; }

    public string? ExtraWorkbookRelationships { get; set; }

    public XlsxBuilder Sheet(string name, Action<SheetBuilder> fill)
    {
        var sheet = new SheetBuilder(_shared);
        fill(sheet);
        _sheets.Add((name, sheet));
        return this;
    }

    public byte[] Build()
    {
        using var stream = new MemoryStream();

        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var types = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">");
            types.Append("<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/>");
            types.Append("<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>");
            types.Append("<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>");
            types.Append("<Override PartName=\"/xl/sharedStrings.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sharedStrings+xml\"/>");

            for (var i = 0; i < _sheets.Count; i++)
            {
                types.Append(System.Globalization.CultureInfo.InvariantCulture, $"<Override PartName=\"/xl/worksheets/sheet{i + 1}.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>");
            }

            types.Append(ExtraContentTypes).Append("</Types>");
            Add(zip, "[Content_Types].xml", types.ToString());

            Add(zip, "_rels/.rels", "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>");

            var workbook = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">");
            workbook.Append(Date1904 ? "<workbookPr date1904=\"1\"/>" : "<workbookPr/>").Append("<sheets>");
            var rels = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">");

            for (var i = 0; i < _sheets.Count; i++)
            {
                workbook.Append(System.Globalization.CultureInfo.InvariantCulture, $"<sheet name=\"{SecurityElement.Escape(_sheets[i].Name)}\" sheetId=\"{i + 1}\" r:id=\"rId{i + 1}\"/>");
                rels.Append(System.Globalization.CultureInfo.InvariantCulture, $"<Relationship Id=\"rId{i + 1}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet{i + 1}.xml\"/>");
                Add(zip, $"xl/worksheets/sheet{i + 1}.xml", _sheets[i].Sheet.ToXml());

                if (_sheets[i].Sheet.Links.Count > 0)
                {
                    var sheetRels = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">");

                    for (var link = 0; link < _sheets[i].Sheet.Links.Count; link++)
                    {
                        sheetRels.Append(System.Globalization.CultureInfo.InvariantCulture, $"<Relationship Id=\"rIdL{link}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/hyperlink\" Target=\"{SecurityElement.Escape(_sheets[i].Sheet.Links[link].Target)}\" TargetMode=\"External\"/>");
                    }

                    sheetRels.Append("</Relationships>");
                    Add(zip, $"xl/worksheets/_rels/sheet{i + 1}.xml.rels", sheetRels.ToString());
                }
            }

            workbook.Append("</sheets></workbook>");
            rels.Append("<Relationship Id=\"rIdS\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>");
            rels.Append("<Relationship Id=\"rIdSS\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/sharedStrings\" Target=\"sharedStrings.xml\"/>");
            rels.Append(ExtraWorkbookRelationships).Append("</Relationships>");
            Add(zip, "xl/workbook.xml", workbook.ToString());
            Add(zip, "xl/_rels/workbook.xml.rels", rels.ToString());

            Add(zip, "xl/styles.xml", "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><numFmts count=\"2\"><numFmt numFmtId=\"164\" formatCode=\"dd\\.mm\\.yyyy\\ hh:mm\"/><numFmt numFmtId=\"165\" formatCode=\"hh:mm\"/></numFmts><cellStyleXfs count=\"1\"><xf numFmtId=\"14\"/></cellStyleXfs><cellXfs count=\"4\"><xf numFmtId=\"0\"/><xf numFmtId=\"14\"/><xf numFmtId=\"164\"/><xf numFmtId=\"165\"/></cellXfs></styleSheet>");

            var shared = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><sst xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">");

            foreach (var text in _shared)
            {
                shared.Append("<si><t xml:space=\"preserve\">").Append(SecurityElement.Escape(text)).Append("</t></si>");
            }

            shared.Append("</sst>");
            Add(zip, "xl/sharedStrings.xml", shared.ToString());

            foreach (var (path, content) in ExtraParts)
            {
                var entry = zip.CreateEntry(path, CompressionLevel.Optimal);
                using var entryStream = entry.Open();
                entryStream.Write(content);
            }
        }

        return stream.ToArray();
    }

    private static void Add(ZipArchive zip, string path, string xml)
    {
        var entry = zip.CreateEntry(path, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(xml);
    }

    internal sealed class SheetBuilder(List<string> shared)
    {
        private readonly SortedDictionary<int, SortedDictionary<int, string>> _rows = [];

        public List<(string Reference, string Target)> Links { get; } = [];

        public List<string> Merges { get; } = [];

        public string? RawSheetXmlPrefix { get; set; }

        public SheetBuilder Text(string reference, string text)
        {
            shared.Add(text);
            return Put(reference, $"<c r=\"{reference}\" t=\"s\"><v>{shared.Count - 1}</v></c>");
        }

        public SheetBuilder Inline(string reference, string text) =>
            Put(reference, $"<c r=\"{reference}\" t=\"inlineStr\"><is><t xml:space=\"preserve\">{SecurityElement.Escape(text)}</t></is></c>");

        public SheetBuilder Number(string reference, double value, int style = GeneralStyle) =>
            Put(reference, string.Create(System.Globalization.CultureInfo.InvariantCulture, $"<c r=\"{reference}\" s=\"{style}\"><v>{value:R}</v></c>"));

        public SheetBuilder Bool(string reference, bool value) =>
            Put(reference, $"<c r=\"{reference}\" t=\"b\"><v>{(value ? 1 : 0)}</v></c>");

        public SheetBuilder FormulaText(string reference, string formula, string cached) =>
            Put(reference, $"<c r=\"{reference}\" t=\"str\"><f>{SecurityElement.Escape(formula)}</f><v>{SecurityElement.Escape(cached)}</v></c>");

        public SheetBuilder FormulaNumber(string reference, string formula, double cached) =>
            Put(reference, string.Create(System.Globalization.CultureInfo.InvariantCulture, $"<c r=\"{reference}\"><f>{SecurityElement.Escape(formula)}</f><v>{cached:R}</v></c>"));

        public SheetBuilder FormulaWithoutValue(string reference, string formula) =>
            Put(reference, $"<c r=\"{reference}\"><f>{SecurityElement.Escape(formula)}</f></c>");

        public SheetBuilder Error(string reference, string code) =>
            Put(reference, $"<c r=\"{reference}\" t=\"e\"><v>{SecurityElement.Escape(code)}</v></c>");

        public SheetBuilder Link(string reference, string target)
        {
            Links.Add((reference, target));
            return this;
        }

        public SheetBuilder Merge(string region)
        {
            Merges.Add(region);
            return this;
        }

        public string ToXml()
        {
            var xml = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            xml.Append(RawSheetXmlPrefix);
            xml.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheetData>");

            foreach (var (row, cells) in _rows)
            {
                xml.Append(System.Globalization.CultureInfo.InvariantCulture, $"<row r=\"{row}\">");

                foreach (var cell in cells.Values)
                {
                    xml.Append(cell);
                }

                xml.Append("</row>");
            }

            xml.Append("</sheetData>");

            if (Merges.Count > 0)
            {
                xml.Append(System.Globalization.CultureInfo.InvariantCulture, $"<mergeCells count=\"{Merges.Count}\">");
                Merges.ForEach(merge => xml.Append($"<mergeCell ref=\"{merge}\"/>"));
                xml.Append("</mergeCells>");
            }

            if (Links.Count > 0)
            {
                xml.Append("<hyperlinks>");

                for (var i = 0; i < Links.Count; i++)
                {
                    xml.Append(System.Globalization.CultureInfo.InvariantCulture, $"<hyperlink ref=\"{Links[i].Reference}\" r:id=\"rIdL{i}\"/>");
                }

                xml.Append("</hyperlinks>");
            }

            return xml.Append("</worksheet>").ToString();
        }

        private SheetBuilder Put(string reference, string cellXml)
        {
            var letters = new string(reference.TakeWhile(char.IsLetter).ToArray());
            var row = int.Parse(reference[letters.Length..], System.Globalization.CultureInfo.InvariantCulture);
            var column = letters.Aggregate(0, (acc, ch) => (acc * 26) + (ch - 'A' + 1));

            if (!_rows.TryGetValue(row, out var cells))
            {
                cells = [];
                _rows[row] = cells;
            }

            cells[column] = cellXml;
            return this;
        }
    }
}
