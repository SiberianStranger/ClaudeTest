using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using OpenDocEditor.Core.Models.Document;

namespace OpenDocEditor.App.Controls;

/// <summary>
/// Основной контрол редактора — отображает документ постранично.
/// Реализует минимальный editing через TextBox-слой.
///
/// Архитектурно разделён на:
///  - Render layer (отрисовка страниц через Avalonia Drawing API)
///  - Editing layer (TextBox оверлей для ввода)
///  - Hit-testing (определение позиции курсора по координатам)
/// </summary>
public sealed class DocumentCanvas : Panel
{
    private DocModel? _document;
    private readonly List<PageControl> _pages = [];

    // Константы разметки
    private const double PageMarginPx = 20;
    private const double ScreenDpi = 96.0;
    private const double TwipsPerInch = 1440.0;

    public static readonly StyledProperty<DocModel?> DocumentProperty =
        AvaloniaProperty.Register<DocumentCanvas, DocModel?>(nameof(Document));

    public DocModel? Document
    {
        get => GetValue(DocumentProperty);
        set => SetValue(DocumentProperty, value);
    }

    static DocumentCanvas()
    {
        DocumentProperty.Changed.AddClassHandler<DocumentCanvas>((ctrl, e) =>
            ctrl.OnDocumentChanged(e.NewValue as DocModel));
    }

    public DocumentCanvas()
    {
        Background = Brushes.Transparent;
        Focusable = true;
    }

    private void OnDocumentChanged(DocModel? doc)
    {
        _document = doc;
        _pages.Clear();
        Children.Clear();

        if (doc == null) return;

        Dispatcher.UIThread.Post(RebuildPages);
    }

    private void RebuildPages()
    {
        if (_document == null) return;

        double y = PageMarginPx;

        foreach (var section in _document.Sections)
        {
            var pageWidth  = TwipsToPx(section.Layout.Width);
            var pageHeight = TwipsToPx(section.Layout.Height);

            var page = new PageControl
            {
                Section = section,
                Styles = _document.Styles,
                Width = pageWidth,
                Height = pageHeight,
                Margin = new Thickness(PageMarginPx, y, PageMarginPx, 0),
            };

            _pages.Add(page);
            Children.Add(page);
            y += pageHeight + PageMarginPx;
        }

        InvalidateMeasure();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (!_pages.Any()) return new Size(800, 600);

        var maxWidth = _pages.Max(p => p.Width) + PageMarginPx * 2;
        var totalHeight = _pages.Sum(p => p.Height + PageMarginPx) + PageMarginPx;

        foreach (var page in _pages)
            page.Measure(new Size(page.Width, page.Height));

        return new Size(maxWidth, totalHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        double y = PageMarginPx;
        double cx = finalSize.Width / 2;

        foreach (var page in _pages)
        {
            var x = cx - page.Width / 2;
            page.Arrange(new Rect(x, y, page.Width, page.Height));
            y += page.Height + PageMarginPx;
        }

        return finalSize;
    }

    private static double TwipsToPx(double twips) =>
        twips / TwipsPerInch * ScreenDpi;
}

/// <summary>Визуальное представление одной страницы документа.</summary>
public sealed class PageControl : Control
{
    public DocSection? Section { get; set; }
    public StyleRegistry? Styles { get; set; }

    private static readonly IBrush PageBackground = Brushes.White;
    private static readonly IPen PageBorder = new Pen(Brushes.LightGray, 1);
    private static readonly IBrush ShadowBrush = new SolidColorBrush(Color.FromArgb(60, 0, 0, 0));

    private const double ScreenDpi = 96.0;
    private const double TwipsPerInch = 1440.0;
    private const double PtPerTwip = 1.0 / 20.0;
    private const double PxPerPt = ScreenDpi / 72.0;

    public override void Render(DrawingContext context)
    {
        // Тень
        context.DrawRectangle(ShadowBrush, null,
            new Rect(3, 3, Bounds.Width, Bounds.Height),
            2, 2);

        // Страница
        context.DrawRectangle(PageBackground, PageBorder,
            new Rect(0, 0, Bounds.Width, Bounds.Height));

        if (Section == null) return;

        var layout = Section.Layout;
        var marginL = TwipsToPx(layout.MarginLeft);
        var marginT = TwipsToPx(layout.MarginTop);
        var contentW = Bounds.Width - marginL - TwipsToPx(layout.MarginRight);

        double y = marginT;
        using var clip = context.PushClip(new Rect(0, 0, Bounds.Width, Bounds.Height));

        foreach (var block in Section.Blocks)
            y = RenderBlock(context, block, marginL, y, contentW);
    }

    private double RenderBlock(DrawingContext ctx, IDocBlock block, double x, double y, double width) =>
        block switch
        {
            DocParagraph p => RenderParagraph(ctx, p, x, y, width),
            DocTable t     => RenderTable(ctx, t, x, y, width),
            _              => y,
        };

    private double RenderParagraph(DrawingContext ctx, DocParagraph para, double x, double y, double width)
    {
        var pFmt = para.Format;
        var spaceBefore = pFmt.SpaceBefore * PtPerTwip * PxPerPt;
        var spaceAfter  = pFmt.SpaceAfter  * PtPerTwip * PxPerPt;
        var indentL     = pFmt.IndentLeft  * PtPerTwip * PxPerPt;
        var indentFirst = pFmt.IndentFirstLine * PtPerTwip * PxPerPt;

        y += spaceBefore;

        if (para.IsEmpty)
        {
            // Пустой абзац — используем высоту шрифта по умолчанию
            return y + GetDefaultLineHeight(pFmt) + spaceAfter;
        }

        // Строим runs в строки
        var lines = LayoutLines(para, x + indentL + indentFirst, x + indentL, x + width, pFmt);

        foreach (var line in lines)
        {
            var lineX = GetLineX(line, x + indentL, x + width, pFmt.Alignment);
            double curX = lineX;

            foreach (var seg in line.Segments)
            {
                var ft = BuildFormattedText(seg.Text, seg.Format, Styles);
                if (ft == null) { curX += seg.Width; continue; }
                ctx.DrawText(ft, new Point(curX, y));
                curX += seg.Width;
            }

            y += line.Height;
        }

        return y + spaceAfter;
    }

    private double RenderTable(DrawingContext ctx, DocTable table, double x, double y, double width)
    {
        if (!table.Rows.Any()) return y;
        var colWidth = width / Math.Max(table.ColumnCount, 1);
        var borderPen = new Pen(Brushes.Gray, 0.5);

        foreach (var row in table.Rows)
        {
            double rowMaxH = 20;
            double cx = x;

            // Первый проход — вычислить высоту строки
            foreach (var cell in row.Cells)
            {
                var cellW = colWidth * cell.GridSpan;
                double cellH = 4;
                foreach (var block in cell.Blocks)
                    cellH = RenderBlock(ctx, block, cx + 4, cellH, cellW - 8);
                rowMaxH = Math.Max(rowMaxH, cellH + 4);
                cx += cellW;
            }

            // Второй проход — нарисовать
            cx = x;
            foreach (var cell in row.Cells)
            {
                var cellW = colWidth * cell.GridSpan;
                ctx.DrawRectangle(null, borderPen, new Rect(cx, y, cellW, rowMaxH));

                double cellY = y + 4;
                foreach (var block in cell.Blocks)
                    cellY = RenderBlock(ctx, block, cx + 4, cellY, cellW - 8);
                cx += cellW;
            }

            y += rowMaxH;
        }

        return y;
    }

    // ── Layout engine (простейший word-wrap) ─────────────────────────────────

    private sealed record LineSeg(string Text, RunFormat Format, double Width);
    private sealed record Line(List<LineSeg> Segments, double Height, double TotalWidth);

    private List<Line> LayoutLines(DocParagraph para, double firstLineX, double lineX, double maxX, ParaFormat pFmt)
    {
        var lines = new List<Line>();
        var currentLine = new List<LineSeg>();
        double curX = firstLineX;
        double lineHeight = GetDefaultLineHeight(pFmt);
        bool isFirst = true;

        foreach (var inline in para.Inlines.OfType<DocRun>())
        {
            var words = SplitIntoWords(inline.Text);
            foreach (var word in words)
            {
                var ft = BuildFormattedText(word, inline.Format, Styles);
                if (ft == null) continue;

                var wordW = ft.Width;
                var wordH = ft.Height;
                lineHeight = Math.Max(lineHeight, wordH * 1.2);

                var rightBound = maxX;
                if (curX + wordW > rightBound && currentLine.Any())
                {
                    // Перенос строки
                    lines.Add(new Line([.. currentLine], lineHeight, curX - (isFirst ? firstLineX : lineX)));
                    currentLine.Clear();
                    curX = lineX;
                    lineHeight = wordH * 1.2;
                    isFirst = false;
                }

                currentLine.Add(new LineSeg(word, inline.Format, wordW));
                curX += wordW;
            }
        }

        if (currentLine.Any())
            lines.Add(new Line([.. currentLine], lineHeight, curX - (isFirst ? firstLineX : lineX)));

        return lines;
    }

    private static IEnumerable<string> SplitIntoWords(string text)
    {
        if (string.IsNullOrEmpty(text)) yield break;

        var word = new System.Text.StringBuilder();
        foreach (var ch in text)
        {
            if (ch == ' ' || ch == '\t')
            {
                if (word.Length > 0) { yield return word.ToString(); word.Clear(); }
                yield return ch.ToString();
            }
            else
            {
                word.Append(ch);
            }
        }
        if (word.Length > 0) yield return word.ToString();
    }

    private static double GetLineX(Line line, double marginX, double maxX, ParagraphAlignment align) =>
        align switch
        {
            ParagraphAlignment.Center  => marginX + (maxX - marginX - line.TotalWidth) / 2,
            ParagraphAlignment.Right   => maxX - line.TotalWidth,
            _                          => marginX,
        };

    private static FormattedText? BuildFormattedText(string text, RunFormat fmt, StyleRegistry? styles)
    {
        if (string.IsNullOrEmpty(text)) return null;

        var resolved = styles?.ResolveRunFormat(fmt.StyleId) ?? RunFormat.Default;
        var fontName = fmt.FontName ?? resolved.FontName ?? "Arial";
        var fontSize = (fmt.FontSize ?? resolved.FontSize ?? 12) * PxPerPt;

        var weight = (fmt.Bold || resolved.Bold) ? FontWeight.Bold : FontWeight.Normal;
        var style  = (fmt.Italic || resolved.Italic) ? FontStyle.Italic : FontStyle.Normal;

        IBrush color = fmt.Color != null
            ? new SolidColorBrush(Color.FromRgb(fmt.Color.R, fmt.Color.G, fmt.Color.B))
            : Brushes.Black;

        var typeface = new Typeface(fontName, style, weight);

        return new FormattedText(
            text,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            typeface,
            fontSize,
            color);
    }

    private double GetDefaultLineHeight(ParaFormat pFmt)
    {
        var lineSpacingPx = pFmt.LineSpacing * PtPerTwip * PxPerPt;
        return Math.Max(lineSpacingPx, 14); // минимум 14px
    }

    private static double TwipsToPx(double twips) =>
        twips / TwipsPerInch * ScreenDpi;
}
