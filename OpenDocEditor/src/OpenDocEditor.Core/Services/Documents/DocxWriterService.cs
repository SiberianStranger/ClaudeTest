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
/// Сериализует DocModel в DOCX-файл (OpenXML SDK).
/// </summary>
public sealed class DocxWriterService : IDocxWriterService
{
    private readonly ILogger<DocxWriterService> _logger;

    public DocxWriterService(ILogger<DocxWriterService> logger) => _logger = logger;

    public async Task WriteAsync(DocModel doc, string filePath, CancellationToken ct = default)
    {
        _logger.LogInformation("Saving DOCX: {Path}", filePath);
        await using var stream = File.Open(filePath, FileMode.Create, FileAccess.Write);
        await WriteAsync(doc, stream, ct);
    }

    public Task WriteAsync(DocModel doc, Stream stream, CancellationToken ct = default)
    {
        using var wordDoc = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document);
        var mainPart = wordDoc.AddMainDocumentPart();
        mainPart.Document = new Document(new Body());

        WriteStyles(mainPart, doc.Styles);
        WriteBody(mainPart, doc);
        WriteProperties(wordDoc, doc.Properties);

        mainPart.Document.Save();
        return Task.CompletedTask;
    }

    private static void WriteStyles(MainDocumentPart mainPart, StyleRegistry registry)
    {
        var stylesPart = mainPart.AddNewPart<StyleDefinitionsPart>();
        var styles = new Styles();

        foreach (var style in registry.All)
        {
            var styleType = style.Type switch
            {
                StyleType.Character => StyleValues.Character,
                StyleType.Table     => StyleValues.Table,
                StyleType.Numbering => StyleValues.Numbering,
                _                   => StyleValues.Paragraph,
            };

            var oxStyle = new Style { StyleId = style.Id, Type = styleType };
            if (style.IsDefault) oxStyle.Default = OnOffValue.FromBoolean(true);
            if (!style.IsBuiltIn) oxStyle.CustomStyle = OnOffValue.FromBoolean(true);

            oxStyle.AppendChild(new StyleName { Val = style.Name });
            if (style.BasedOn != null) oxStyle.AppendChild(new BasedOn { Val = style.BasedOn });
            if (style.RunFormat != null) oxStyle.AppendChild(BuildStyleRunProps(style.RunFormat));
            if (style.ParaFormat != null) oxStyle.AppendChild(BuildStyleParaProps(style.ParaFormat));

            styles.AppendChild(oxStyle);
        }
        stylesPart.Styles = styles;
    }

    private static void WriteBody(MainDocumentPart mainPart, DocModel doc)
    {
        var body = mainPart.Document.Body!;
        foreach (var section in doc.Sections)
        {
            foreach (var block in section.Blocks)
            {
                var el = WriteBlock(block);
                if (el != null) body.AppendChild(el);
            }
            body.AppendChild(BuildSectPr(section.Layout));
        }
    }

    private static OpenXmlElement? WriteBlock(IDocBlock block) =>
        block is DocParagraph p ? WriteParagraph(p) :
        block is DocTable t ? WriteTable(t) :
        null;

    private static OxmlPara WriteParagraph(DocParagraph para)
    {
        var oxPara = new OxmlPara();
        oxPara.AppendChild(BuildParaProps(para.Format));

        foreach (var inline in para.Inlines)
        {
            if (inline is DocRun run)
            {
                oxPara.AppendChild(BuildRun(run));
            }
            else if (inline is DocBreak brk)
            {
                var breakType = brk.BreakType switch
                {
                    BreakType.Page   => BreakValues.Page,
                    BreakType.Column => BreakValues.Column,
                    _                => BreakValues.TextWrapping,
                };
                oxPara.AppendChild(new OxmlRun(new Break { Type = breakType }));
            }
            else if (inline is DocField field)
            {
                oxPara.AppendChild(new SimpleField { Instruction = field.FieldCode });
            }
        }
        return oxPara;
    }

    private static OxmlRun BuildRun(DocRun run)
    {
        var oxRun = new OxmlRun();
        if (!IsDefaultFormat(run.Format))
            oxRun.AppendChild(BuildRunProps(run.Format));

        var text = new Text { Text = run.Text };
        if (run.Text.Length > 0 && (run.Text[0] == ' ' || run.Text[^1] == ' '))
            text.Space = SpaceProcessingModeValues.Preserve;
        oxRun.AppendChild(text);
        return oxRun;
    }

    private static OxmlTable WriteTable(DocTable table)
    {
        var oxTable = new OxmlTable();
        foreach (var row in table.Rows)
        {
            var oxRow = new TableRow();
            foreach (var cell in row.Cells)
            {
                var oxCell = new TableCell();
                if (cell.GridSpan > 1)
                    oxCell.AppendChild(new TableCellProperties(new GridSpan { Val = cell.GridSpan }));
                foreach (var block in cell.Blocks)
                {
                    var el = WriteBlock(block);
                    if (el != null) oxCell.AppendChild(el);
                }
                if (!oxCell.HasChildren) oxCell.AppendChild(new OxmlPara());
                oxRow.AppendChild(oxCell);
            }
            oxTable.AppendChild(oxRow);
        }
        return oxTable;
    }

    private static ParagraphProperties BuildParaProps(ParaFormat fmt)
    {
        var ppr = new ParagraphProperties();

        if (fmt.StyleId != null)
            ppr.AppendChild(new ParagraphStyleId { Val = fmt.StyleId });

        if (fmt.Alignment != ParagraphAlignment.Left)
        {
            var jVal = fmt.Alignment switch
            {
                ParagraphAlignment.Center  => JustificationValues.Center,
                ParagraphAlignment.Right   => JustificationValues.Right,
                ParagraphAlignment.Justify => JustificationValues.Both,
                _                          => JustificationValues.Left,
            };
            ppr.AppendChild(new Justification { Val = jVal });
        }

        if (fmt.IndentLeft != 0 || fmt.IndentRight != 0 || fmt.IndentFirstLine != 0 || fmt.IndentHanging != 0)
            ppr.AppendChild(new Indentation
            {
                Left      = fmt.IndentLeft > 0 ? fmt.IndentLeft.ToString() : null,
                Right     = fmt.IndentRight > 0 ? fmt.IndentRight.ToString() : null,
                FirstLine = fmt.IndentFirstLine > 0 ? fmt.IndentFirstLine.ToString() : null,
                Hanging   = fmt.IndentHanging > 0 ? fmt.IndentHanging.ToString() : null,
            });

        if (fmt.SpaceBefore != 0 || fmt.SpaceAfter != 0 || fmt.LineSpacing != 240)
        {
            var lineRule = fmt.LineSpacingRule switch
            {
                LineSpacingRule.Exact   => LineSpacingRuleValues.Exact,
                LineSpacingRule.AtLeast => LineSpacingRuleValues.AtLeast,
                _                       => LineSpacingRuleValues.Auto,
            };
            var sbl = new SpacingBetweenLines { Line = fmt.LineSpacing.ToString(), LineRule = lineRule };
            if (fmt.SpaceBefore > 0) sbl.Before = fmt.SpaceBefore.ToString();
            if (fmt.SpaceAfter > 0)  sbl.After = fmt.SpaceAfter.ToString();
            ppr.AppendChild(sbl);
        }

        if (fmt.KeepTogether) ppr.AppendChild(new KeepLines());
        if (fmt.KeepWithNext) ppr.AppendChild(new KeepNext());
        if (fmt.PageBreakBefore) ppr.AppendChild(new PageBreakBefore());

        if (fmt.NumberingId != null)
            ppr.AppendChild(new NumberingProperties(
                new NumberingId { Val = fmt.NumberingId.Value },
                new NumberingLevelReference { Val = fmt.NumberingLevel }));

        return ppr;
    }

    private static RunProperties BuildRunProps(RunFormat fmt)
    {
        var rpr = new RunProperties();
        if (fmt.StyleId != null) rpr.AppendChild(new RunStyle { Val = fmt.StyleId });

        if (fmt.FontName != null)
            rpr.AppendChild(new RunFonts { Ascii = fmt.FontName, HighAnsi = fmt.FontName, ComplexScript = fmt.FontName });

        if (fmt.FontSize != null)
        {
            var halfPt = ((int)(fmt.FontSize.Value * 2)).ToString();
            rpr.AppendChild(new FontSize { Val = halfPt });
            rpr.AppendChild(new FontSizeComplexScript { Val = halfPt });
        }

        if (fmt.Bold) rpr.AppendChild(new Bold());
        if (fmt.Italic) rpr.AppendChild(new Italic());
        if (fmt.Underline) rpr.AppendChild(new Underline { Val = UnderlineValues.Single });
        if (fmt.Strikethrough) rpr.AppendChild(new Strike());
        if (fmt.SmallCaps) rpr.AppendChild(new SmallCaps());
        if (fmt.AllCaps) rpr.AppendChild(new Caps());

        if (fmt.Color != null)
            rpr.AppendChild(new Color { Val = fmt.Color.ToHex().TrimStart('#') });

        if (fmt.Lang != null)
            rpr.AppendChild(new Languages { Val = fmt.Lang, Bidi = fmt.Lang });

        if (fmt.VerticalAlignment != VerticalAlignment.Baseline)
        {
            var vVal = fmt.VerticalAlignment == VerticalAlignment.Superscript
                ? VerticalPositionValues.Superscript
                : VerticalPositionValues.Subscript;
            rpr.AppendChild(new VerticalTextAlignment { Val = vVal });
        }

        return rpr;
    }

    private static StyleRunProperties BuildStyleRunProps(RunFormat fmt)
    {
        var srp = new StyleRunProperties();
        if (fmt.FontName != null) srp.AppendChild(new RunFonts { Ascii = fmt.FontName });
        if (fmt.FontSize != null) srp.AppendChild(new FontSize { Val = ((int)(fmt.FontSize.Value * 2)).ToString() });
        if (fmt.Bold) srp.AppendChild(new Bold());
        if (fmt.Italic) srp.AppendChild(new Italic());
        if (fmt.Color != null) srp.AppendChild(new Color { Val = fmt.Color.ToHex().TrimStart('#') });
        return srp;
    }

    private static StyleParagraphProperties BuildStyleParaProps(ParaFormat fmt)
    {
        var spp = new StyleParagraphProperties();
        if (fmt.SpaceBefore > 0 || fmt.SpaceAfter > 0)
        {
            var sbl = new SpacingBetweenLines();
            if (fmt.SpaceBefore > 0) sbl.Before = fmt.SpaceBefore.ToString();
            if (fmt.SpaceAfter > 0) sbl.After = fmt.SpaceAfter.ToString();
            spp.AppendChild(sbl);
        }
        return spp;
    }

    private static SectionProperties BuildSectPr(PageLayout layout)
    {
        var orient = layout.Orientation == PageOrientation.Landscape
            ? PageOrientationValues.Landscape
            : PageOrientationValues.Portrait;

        return new SectionProperties(
            new PageSize
            {
                Width  = (UInt32Value)(uint)layout.Width,
                Height = (UInt32Value)(uint)layout.Height,
                Orient = orient,
            },
            new PageMargin
            {
                Top    = layout.MarginTop,
                Bottom = layout.MarginBottom,
                Left   = (UInt32Value)(uint)layout.MarginLeft,
                Right  = (UInt32Value)(uint)layout.MarginRight,
            });
    }

    private static void WriteProperties(WordprocessingDocument wordDoc, DocProperties props)
    {
        try
        {
            var corePart = wordDoc.AddCoreFilePropertiesPart();
            using var writer = System.Xml.XmlWriter.Create(corePart.GetStream(FileMode.Create));
            writer.WriteStartDocument();
            writer.WriteStartElement("cp", "coreProperties",
                "http://schemas.openxmlformats.org/package/2006/metadata/core-properties");
            writer.WriteAttributeString("xmlns", "dc", null, "http://purl.org/dc/elements/1.1/");
            if (props.Title != null) writer.WriteElementString("title", "http://purl.org/dc/elements/1.1/", props.Title);
            if (props.Author != null) writer.WriteElementString("creator", "http://purl.org/dc/elements/1.1/", props.Author);
            writer.WriteElementString("revision", "http://schemas.openxmlformats.org/package/2006/metadata/core-properties", props.Revision.ToString());
            writer.WriteEndElement();
            writer.WriteEndDocument();
        }
        catch { /* non-critical */ }
    }

    private static bool IsDefaultFormat(RunFormat fmt) =>
        fmt.FontName == null && fmt.FontSize == null &&
        !fmt.Bold && !fmt.Italic && !fmt.Underline && !fmt.Strikethrough &&
        fmt.Color == null && fmt.StyleId == null;
}
