using OpenDocEditor.Core.Models.Document;
using Microsoft.Extensions.Logging;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;

namespace OpenDocEditor.Core.Services.Documents;

/// <summary>
/// Экспорт DocModel в PDF через PdfSharpCore (MIT).
/// </summary>
public sealed class PdfExportService : IPdfExportService
{
    private const double PtPerTwip = 1.0 / 20.0;
    private const double PxPerPt = 96.0 / 72.0;

    private readonly ILogger<PdfExportService> _logger;

    public PdfExportService(ILogger<PdfExportService> logger) => _logger = logger;

    public async Task ExportAsync(DocModel doc, string filePath, CancellationToken ct = default)
    {
        _logger.LogInformation("Exporting PDF: {Path}", filePath);
        await using var stream = File.Open(filePath, FileMode.Create, FileAccess.Write);
        await ExportAsync(doc, stream, ct);
    }

    public Task ExportAsync(DocModel doc, Stream stream, CancellationToken ct = default)
    {
        using var pdf = new PdfDocument();
        pdf.Info.Title = doc.Properties.Title ?? doc.DisplayName;
        pdf.Info.Author = doc.Properties.Author ?? "";
        pdf.Info.Creator = "OpenDocEditor 1.0";

        foreach (var section in doc.Sections)
        {
            ct.ThrowIfCancellationRequested();

            var layout = section.Layout;
            var pageW = layout.Width * PtPerTwip;
            var pageH = layout.Height * PtPerTwip;
            var mL    = layout.MarginLeft   * PtPerTwip;
            var mR    = layout.MarginRight  * PtPerTwip;
            var mT    = layout.MarginTop    * PtPerTwip;
            var mB    = layout.MarginBottom * PtPerTwip;
            var cW    = pageW - mL - mR;

            var page = pdf.AddPage();
            page.Width  = XUnit.FromPoint(pageW);
            page.Height = XUnit.FromPoint(pageH);

            using var gfx = XGraphics.FromPdfPage(page);
            double y = mT;

            foreach (var block in section.Blocks)
            {
                ct.ThrowIfCancellationRequested();
                double nextY = RenderBlock(gfx, block, mL, y, cW, doc.Styles);

                // Перенос на новую страницу при переполнении
                if (nextY > pageH - mB && block != section.Blocks.Last())
                {
                    page = pdf.AddPage();
                    page.Width  = XUnit.FromPoint(pageW);
                    page.Height = XUnit.FromPoint(pageH);
                    y = mT;
                    // Рендерим блок заново на новой странице
                    using var gfx2 = XGraphics.FromPdfPage(page);
                    nextY = RenderBlock(gfx2, block, mL, mT, cW, doc.Styles);
                }

                y = nextY;
            }
        }

        pdf.Save(stream);
        return Task.CompletedTask;
    }

    private double RenderBlock(XGraphics gfx, IDocBlock block, double x, double y, double width, StyleRegistry styles) =>
        block switch
        {
            DocParagraph p => RenderParagraph(gfx, p, x, y, width, styles),
            DocTable t     => RenderTable(gfx, t, x, y, width, styles),
            _              => y,
        };

    private double RenderParagraph(XGraphics gfx, DocParagraph para, double x, double y, double width, StyleRegistry styles)
    {
        if (para.IsEmpty) return y + GetDefaultLineHeight(para.Format);

        var pFmt = para.Format;
        y += pFmt.SpaceBefore * PtPerTwip;
        double indentL = pFmt.IndentLeft * PtPerTwip;
        double indentF = pFmt.IndentFirstLine * PtPerTwip;

        var segments = BuildSegments(para, styles);
        if (!segments.Any()) return y + GetDefaultLineHeight(pFmt);

        // Простейший word-wrap layout
        var lines = WordWrap(segments, x + indentL + indentF, x + indentL, x + width, gfx);
        foreach (var line in lines)
        {
            double lineX = GetLineX(line, x + indentL, x + width, pFmt.Alignment);
            double curX = lineX;
            foreach (var (text, font, color, w) in line.Items)
            {
                gfx.DrawString(text, font, new XSolidBrush(color), curX, y + line.Ascent);
                curX += w;
            }
            y += line.Height;
        }

        return y + pFmt.SpaceAfter * PtPerTwip;
    }

    private double RenderTable(XGraphics gfx, DocTable table, double x, double y, double width, StyleRegistry styles)
    {
        if (!table.Rows.Any()) return y;
        double colW = width / Math.Max(table.ColumnCount, 1);
        var borderPen = new XPen(XColors.Gray, 0.5);

        foreach (var row in table.Rows)
        {
            double cx = x;
            double rowH = 18;

            foreach (var cell in row.Cells)
            {
                double cellW = colW * cell.GridSpan;
                double cellY = y + 3;
                foreach (var block in cell.Blocks)
                    cellY = RenderBlock(gfx, block, cx + 3, cellY, cellW - 6, styles);
                rowH = Math.Max(rowH, cellY - y + 3);
                cx += cellW;
            }

            cx = x;
            foreach (var cell in row.Cells)
            {
                double cellW = colW * cell.GridSpan;
                gfx.DrawRectangle(borderPen, cx, y, cellW, rowH);
                cx += cellW;
            }
            y += rowH;
        }
        return y;
    }

    // ── Layout helpers ────────────────────────────────────────────────────────

    private sealed record SegmentInfo(string Text, XFont Font, XColor Color);
    private sealed record LineInfo(List<(string Text, XFont Font, XColor Color, double W)> Items, double Height, double Ascent, double TotalWidth);

    private List<SegmentInfo> BuildSegments(DocParagraph para, StyleRegistry styles)
    {
        var result = new List<SegmentInfo>();
        foreach (var run in para.Inlines.OfType<DocRun>())
        {
            if (string.IsNullOrEmpty(run.Text)) continue;
            var fmt = run.Format;
            var resolved = styles.ResolveRunFormat(fmt.StyleId ?? para.Format.StyleId);
            var fontName = fmt.FontName ?? resolved.FontName ?? "Arial";
            var fontSize = (double)(fmt.FontSize ?? resolved.FontSize ?? 12);
            var bold = fmt.Bold || resolved.Bold;
            var italic = fmt.Italic || resolved.Italic;

            var xStyle = XFontStyle.Regular;
            if (bold && italic) xStyle = XFontStyle.BoldItalic;
            else if (bold) xStyle = XFontStyle.Bold;
            else if (italic) xStyle = XFontStyle.Italic;

            XFont font;
            try { font = new XFont(fontName, fontSize, xStyle); }
            catch { font = new XFont("Arial", fontSize, xStyle); }

            var color = fmt.Color != null
                ? XColor.FromArgb(255, fmt.Color.R, fmt.Color.G, fmt.Color.B)
                : XColors.Black;

            result.Add(new SegmentInfo(run.Text, font, color));
        }
        return result;
    }

    private static List<LineInfo> WordWrap(List<SegmentInfo> segs, double firstX, double lineX, double maxX, XGraphics gfx)
    {
        var lines = new List<LineInfo>();
        var currentItems = new List<(string, XFont, XColor, double)>();
        double curX = firstX;
        double lineH = 14;
        double ascent = 12;
        bool isFirst = true;

        foreach (var seg in segs)
        {
            var words = SplitWords(seg.Text);
            foreach (var word in words)
            {
                var size = gfx.MeasureString(word, seg.Font);
                lineH = Math.Max(lineH, size.Height * 1.2);
                ascent = Math.Max(ascent, size.Height);

                if (curX + size.Width > maxX && currentItems.Any())
                {
                    lines.Add(new LineInfo([.. currentItems], lineH, ascent, curX - (isFirst ? firstX : lineX)));
                    currentItems.Clear();
                    curX = lineX;
                    lineH = size.Height * 1.2;
                    ascent = size.Height;
                    isFirst = false;
                }

                currentItems.Add((word, seg.Font, seg.Color, size.Width));
                curX += size.Width;
            }
        }

        if (currentItems.Any())
            lines.Add(new LineInfo([.. currentItems], lineH, ascent, curX - (isFirst ? firstX : lineX)));

        return lines;
    }

    private static IEnumerable<string> SplitWords(string text)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var c in text)
        {
            if (c == ' ' || c == '\t')
            {
                if (sb.Length > 0) { yield return sb.ToString(); sb.Clear(); }
                yield return c.ToString();
            }
            else sb.Append(c);
        }
        if (sb.Length > 0) yield return sb.ToString();
    }

    private static double GetLineX(LineInfo line, double marginX, double maxX, ParagraphAlignment align) =>
        align switch
        {
            ParagraphAlignment.Center => marginX + (maxX - marginX - line.TotalWidth) / 2,
            ParagraphAlignment.Right  => maxX - line.TotalWidth,
            _                         => marginX,
        };

    private static double GetDefaultLineHeight(ParaFormat fmt) =>
        Math.Max(fmt.LineSpacing * PtPerTwip, 14);
}
