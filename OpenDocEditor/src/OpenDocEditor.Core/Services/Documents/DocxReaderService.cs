using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using OpenDocEditor.Core.Models.Document;
using Microsoft.Extensions.Logging;
using OxmlPara = DocumentFormat.OpenXml.Wordprocessing.Paragraph;
using OxmlTable = DocumentFormat.OpenXml.Wordprocessing.Table;
using OxmlRun = DocumentFormat.OpenXml.Wordprocessing.Run;

namespace OpenDocEditor.Core.Services.Documents;

/// <summary>
/// Читает DOCX-файл и преобразует его в DocModel.
/// </summary>
public sealed class DocxReaderService : IDocxReaderService
{
    private readonly ILogger<DocxReaderService> _logger;

    public DocxReaderService(ILogger<DocxReaderService> logger) => _logger = logger;

    public async Task<DocModel> ReadAsync(string filePath, CancellationToken ct = default)
    {
        _logger.LogInformation("Opening DOCX: {Path}", filePath);
        await using var stream = File.OpenRead(filePath);
        var doc = await ReadAsync(stream, ct);
        doc.FilePath = filePath;
        doc.Properties.Modified = File.GetLastWriteTimeUtc(filePath);
        return doc;
    }

    public Task<DocModel> ReadAsync(Stream stream, CancellationToken ct = default)
    {
        using var wordDoc = WordprocessingDocument.Open(stream, false);
        var doc = new DocModel();
        ReadStyles(wordDoc, doc.Styles);
        ReadProperties(wordDoc, doc.Properties);
        ReadBody(wordDoc, doc);
        return Task.FromResult(doc);
    }

    private void ReadStyles(WordprocessingDocument wordDoc, StyleRegistry registry)
    {
        var stylesPart = wordDoc.MainDocumentPart?.StyleDefinitionsPart;
        if (stylesPart?.Styles == null) return;

        foreach (var oxStyle in stylesPart.Styles.Elements<Style>())
        {
            var styleType = StyleType.Paragraph;
            if (oxStyle.Type?.Value == StyleValues.Character) styleType = StyleType.Character;
            else if (oxStyle.Type?.Value == StyleValues.Table) styleType = StyleType.Table;
            else if (oxStyle.Type?.Value == StyleValues.Numbering) styleType = StyleType.Numbering;

            var style = new DocStyle
            {
                Id = oxStyle.StyleId?.Value ?? "",
                Name = oxStyle.StyleName?.Val?.Value ?? "",
                Type = styleType,
                IsDefault = oxStyle.Default?.Value == true,
                IsBuiltIn = oxStyle.CustomStyle?.Value != true,
                BasedOn = oxStyle.BasedOn?.Val?.Value,
                NextStyle = oxStyle.NextParagraphStyle?.Val?.Value,
            };

            if (oxStyle.StyleParagraphProperties != null)
                style.ParaFormat = ReadParaFormatFromStyle(oxStyle.StyleParagraphProperties);
            if (oxStyle.StyleRunProperties != null)
                style.RunFormat = ReadRunFormatFromStyle(oxStyle.StyleRunProperties);

            registry.Add(style);
        }
    }

    private void ReadBody(WordprocessingDocument wordDoc, DocModel doc)
    {
        var body = wordDoc.MainDocumentPart?.Document?.Body;
        if (body == null) return;

        var section = doc.Sections[0];
        var sectPr = body.Elements<SectionProperties>().LastOrDefault();
        if (sectPr != null) section.Layout = ReadPageLayout(sectPr);

        foreach (var child in body.ChildElements)
        {
            if (child is SectionProperties) continue;
            var block = ReadBlock(child, wordDoc);
            if (block != null) section.Blocks.Add(block);
        }
    }

    private IDocBlock? ReadBlock(OpenXmlElement element, WordprocessingDocument wordDoc) =>
        element is OxmlPara p ? ReadParagraph(p, wordDoc) :
        element is OxmlTable t ? ReadTable(t, wordDoc) :
        null;

    private DocParagraph ReadParagraph(OxmlPara oxPara, WordprocessingDocument wordDoc)
    {
        var para = new DocParagraph();
        if (oxPara.ParagraphProperties != null)
            para.Format = ReadParaFormat(oxPara.ParagraphProperties);

        foreach (var child in oxPara.ChildElements)
        {
            if (child is OxmlRun run) ReadRun(run, para);
            else if (child is Hyperlink hl) ReadHyperlink(hl, para, wordDoc);
            else if (child is SimpleField sf)
                para.Inlines.Add(new DocField { FieldCode = sf.Instruction?.Value ?? "", CachedValue = sf.InnerText });
        }
        return para;
    }

    private static void ReadRun(OxmlRun oxRun, DocParagraph para)
    {
        var fmt = oxRun.RunProperties != null ? ReadRunFormat(oxRun.RunProperties) : RunFormat.Default;

        foreach (var child in oxRun.ChildElements)
        {
            if (child is Text text)
                para.Inlines.Add(new DocRun(text.InnerText ?? "", fmt));
            else if (child is Break brk)
            {
                var breakType = BreakType.Line;
                if (brk.Type?.Value == BreakValues.Page) breakType = BreakType.Page;
                else if (brk.Type?.Value == BreakValues.Column) breakType = BreakType.Column;
                para.Inlines.Add(new DocBreak { BreakType = breakType });
            }
        }
    }

    private static void ReadHyperlink(Hyperlink hl, DocParagraph para, WordprocessingDocument wordDoc)
    {
        var link = new DocHyperlink { Tooltip = hl.Tooltip?.Value };
        if (hl.Id != null)
        {
            var rel = wordDoc.MainDocumentPart?.HyperlinkRelationships.FirstOrDefault(r => r.Id == hl.Id);
            link.Url = rel?.Uri.ToString() ?? "";
        }
        foreach (var run in hl.Elements<OxmlRun>())
        {
            var fmt = run.RunProperties != null ? ReadRunFormat(run.RunProperties) : RunFormat.Default;
            foreach (var text in run.Elements<Text>())
                link.Runs.Add(new DocRun(text.InnerText ?? "", fmt));
        }
        para.Inlines.Add(link);
    }

    private DocTable ReadTable(OxmlTable oxTable, WordprocessingDocument wordDoc)
    {
        var table = new DocTable();
        foreach (var oxRow in oxTable.Elements<TableRow>())
        {
            var row = new DocTableRow
            {
                IsHeader = oxRow.TableRowProperties?.GetFirstChild<TableHeader>() != null
            };
            foreach (var oxCell in oxRow.Elements<TableCell>())
            {
                var cell = new DocTableCell();
                var gridSpan = oxCell.TableCellProperties?.GridSpan?.Val?.Value;
                if (gridSpan != null) cell.GridSpan = (int)gridSpan;

                foreach (var child in oxCell.ChildElements)
                {
                    var block = ReadBlock(child, wordDoc);
                    if (block != null) cell.Blocks.Add(block);
                }
                row.Cells.Add(cell);
            }
            table.Rows.Add(row);
        }
        return table;
    }

    private static RunFormat ReadRunFormat(RunProperties rpr)
    {
        var underlineVal = rpr.Underline?.Val?.Value;
        var isUnderline = underlineVal != null && underlineVal != UnderlineValues.None;

        var vertAlign = VerticalAlignment.Baseline;
        if (rpr.VerticalTextAlignment?.Val?.Value == VerticalPositionValues.Superscript)
            vertAlign = VerticalAlignment.Superscript;
        else if (rpr.VerticalTextAlignment?.Val?.Value == VerticalPositionValues.Subscript)
            vertAlign = VerticalAlignment.Subscript;

        return new RunFormat
        {
            FontName = rpr.RunFonts?.Ascii?.Value ?? rpr.RunFonts?.HighAnsi?.Value,
            FontSize = rpr.FontSize?.Val?.Value is string fs && float.TryParse(fs, out var fsv) ? fsv / 2 : null,
            Bold = rpr.Bold?.Val?.Value != false && rpr.Bold != null,
            Italic = rpr.Italic?.Val?.Value != false && rpr.Italic != null,
            Underline = isUnderline,
            Strikethrough = rpr.Strike?.Val?.Value != false && rpr.Strike != null,
            SmallCaps = rpr.SmallCaps?.Val?.Value != false && rpr.SmallCaps != null,
            AllCaps = rpr.Caps?.Val?.Value != false && rpr.Caps != null,
            Color = DocColor.FromHex(rpr.Color?.Val?.Value),
            Lang = rpr.Languages?.Val?.Value,
            StyleId = rpr.RunStyle?.Val?.Value,
            VerticalAlignment = vertAlign,
        };
    }

    private static RunFormat ReadRunFormatFromStyle(StyleRunProperties srp) => new RunFormat
    {
        FontName = srp.RunFonts?.Ascii?.Value,
        FontSize = srp.FontSize?.Val?.Value is string fs && float.TryParse(fs, out var fsv) ? fsv / 2 : null,
        Bold = srp.Bold != null,
        Italic = srp.Italic != null,
        Underline = srp.Underline != null,
        Color = DocColor.FromHex(srp.Color?.Val?.Value),
    };

    private static ParaFormat ReadParaFormat(ParagraphProperties ppr)
    {
        var alignment = ParagraphAlignment.Left;
        if (ppr.Justification?.Val?.Value == JustificationValues.Center) alignment = ParagraphAlignment.Center;
        else if (ppr.Justification?.Val?.Value == JustificationValues.Right) alignment = ParagraphAlignment.Right;
        else if (ppr.Justification?.Val?.Value == JustificationValues.Both) alignment = ParagraphAlignment.Justify;

        return new ParaFormat
        {
            StyleId = ppr.ParagraphStyleId?.Val?.Value,
            Alignment = alignment,
            IndentLeft = ParseTwips(ppr.Indentation?.Left?.Value),
            IndentRight = ParseTwips(ppr.Indentation?.Right?.Value),
            IndentFirstLine = ParseTwips(ppr.Indentation?.FirstLine?.Value),
            IndentHanging = ParseTwips(ppr.Indentation?.Hanging?.Value),
            SpaceBefore = ParseTwips(ppr.SpacingBetweenLines?.Before?.Value),
            SpaceAfter = ParseTwips(ppr.SpacingBetweenLines?.After?.Value),
            LineSpacing = ppr.SpacingBetweenLines?.Line?.Value is string ls && int.TryParse(ls, out var lsv) ? lsv : 240,
            NumberingId = ppr.NumberingProperties?.NumberingId?.Val?.Value is int nid ? nid : (int?)null,
            NumberingLevel = (int?)ppr.NumberingProperties?.NumberingLevelReference?.Val?.Value ?? 0,
            KeepTogether = ppr.KeepLines != null,
            KeepWithNext = ppr.KeepNext != null,
            PageBreakBefore = ppr.PageBreakBefore != null,
        };
    }

    private static ParaFormat ReadParaFormatFromStyle(StyleParagraphProperties spp)
    {
        var alignment = ParagraphAlignment.Left;
        if (spp.Justification?.Val?.Value == JustificationValues.Center) alignment = ParagraphAlignment.Center;
        else if (spp.Justification?.Val?.Value == JustificationValues.Right) alignment = ParagraphAlignment.Right;
        else if (spp.Justification?.Val?.Value == JustificationValues.Both) alignment = ParagraphAlignment.Justify;

        return new ParaFormat
        {
            Alignment = alignment,
            SpaceBefore = ParseTwips(spp.SpacingBetweenLines?.Before?.Value),
            SpaceAfter = ParseTwips(spp.SpacingBetweenLines?.After?.Value),
        };
    }

    private static PageLayout ReadPageLayout(SectionProperties sectPr)
    {
        var layout = new PageLayout();
        if (sectPr.GetFirstChild<PageSize>() is { } pageSize)
        {
            layout.Width = (int)(pageSize.Width?.Value ?? 12240);
            layout.Height = (int)(pageSize.Height?.Value ?? 15840);
            layout.Orientation = pageSize.Orient?.Value == PageOrientationValues.Landscape
                ? PageOrientation.Landscape : PageOrientation.Portrait;
        }
        if (sectPr.GetFirstChild<PageMargin>() is { } margins)
        {
            layout.MarginTop = margins.Top?.Value ?? 1440;
            layout.MarginBottom = (int)(margins.Bottom?.Value ?? 1440);
            layout.MarginLeft = (int)(margins.Left?.Value ?? 1800);
            layout.MarginRight = (int)(margins.Right?.Value ?? 1800);
        }
        return layout;
    }

    private void ReadProperties(WordprocessingDocument wordDoc, DocProperties props)
    {
        var corePart = wordDoc.CoreFilePropertiesPart;
        if (corePart == null) return;
        try
        {
            using var reader = System.Xml.XmlReader.Create(corePart.GetStream());
            while (reader.Read())
            {
                if (reader.NodeType != System.Xml.XmlNodeType.Element) continue;
                if (reader.LocalName == "title") props.Title = reader.ReadElementContentAsString();
                else if (reader.LocalName == "creator") props.Author = reader.ReadElementContentAsString();
            }
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Could not read document properties"); }
    }

    private static int ParseTwips(string? value) => int.TryParse(value, out var v) ? v : 0;
}
