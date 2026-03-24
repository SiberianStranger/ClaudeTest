using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using OpenDocEditor.Core.Models.Document;

namespace OpenDocEditor.App.Controls;

/// <summary>
/// Основной контрол редактора — отображает документ постранично.
/// Editing layer: обрабатывает текстовый ввод и управляет DocModel напрямую.
/// </summary>
public sealed class DocumentCanvas : Panel
{
    private DocModel? _document;
    private readonly List<PageControl> _pages = [];

    // Caret / editing state
    private List<DocParagraph> _allParas = [];
    private int _caretParaIdx;
    private int _caretCharOffset;
    private bool _caretVisible;
    private bool _isFocused;
    private readonly DispatcherTimer _caretTimer;

    // Константы разметки
    private const double PageMarginPx  = 20;
    private const double ScreenDpi     = 96.0;
    private const double TwipsPerInch  = 1440.0;

    public static readonly StyledProperty<DocModel?> DocumentProperty =
        AvaloniaProperty.Register<DocumentCanvas, DocModel?>(nameof(Document));

    public DocModel? Document
    {
        get => GetValue(DocumentProperty);
        set => SetValue(DocumentProperty, value);
    }

    /// <summary>Fired when the user edits the document content.</summary>
    public event EventHandler? DocumentModified;

    static DocumentCanvas()
    {
        DocumentProperty.Changed.AddClassHandler<DocumentCanvas>((ctrl, e) =>
            ctrl.OnDocumentChanged(e.NewValue as DocModel));
    }

    public DocumentCanvas()
    {
        Background = Brushes.Transparent;
        Focusable  = true;

        _caretTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(530) };
        _caretTimer.Tick += (_, _) =>
        {
            _caretVisible = !_caretVisible;
            PushCaretToPages();
        };
    }

    // ── Focus ─────────────────────────────────────────────────────────────────

    protected override void OnGotFocus(GotFocusEventArgs e)
    {
        base.OnGotFocus(e);
        _isFocused    = true;
        _caretVisible = true;
        _caretTimer.Start();
        PushCaretToPages();
    }

    protected override void OnLostFocus(RoutedEventArgs e)
    {
        base.OnLostFocus(e);
        _isFocused = false;
        _caretTimer.Stop();
        _caretVisible = false;
        PushCaretToPages();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();
        ResetCaretBlink();
        e.Handled = true;
    }

    // ── Text input ────────────────────────────────────────────────────────────

    protected override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);
        if (string.IsNullOrEmpty(e.Text) || _allParas.Count == 0) return;

        EnsureCaretInBounds();
        InsertTextAt(_allParas[_caretParaIdx], _caretCharOffset, e.Text);
        _caretCharOffset += e.Text.Length;

        RefreshAfterEdit();
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        // Let Ctrl+key shortcuts pass to the window (Save, Open, etc.)
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control)) return;
        if (_allParas.Count == 0) return;

        EnsureCaretInBounds();

        switch (e.Key)
        {
            case Key.Back:   HandleBackspace(); e.Handled = true; break;
            case Key.Delete: HandleDelete();    e.Handled = true; break;
            case Key.Enter:  HandleEnter();     e.Handled = true; break;

            case Key.Left:
                MoveCaret(-1);
                e.Handled = true;
                break;
            case Key.Right:
                MoveCaret(+1);
                e.Handled = true;
                break;
            case Key.Up:
                if (_caretParaIdx > 0)
                {
                    _caretParaIdx--;
                    _caretCharOffset = Math.Min(_caretCharOffset, _allParas[_caretParaIdx].PlainText.Length);
                    ResetCaretBlink();
                }
                e.Handled = true;
                break;
            case Key.Down:
                if (_caretParaIdx < _allParas.Count - 1)
                {
                    _caretParaIdx++;
                    _caretCharOffset = Math.Min(_caretCharOffset, _allParas[_caretParaIdx].PlainText.Length);
                    ResetCaretBlink();
                }
                e.Handled = true;
                break;
            case Key.Home:
                _caretCharOffset = 0;
                ResetCaretBlink();
                e.Handled = true;
                break;
            case Key.End:
                _caretCharOffset = _allParas[_caretParaIdx].PlainText.Length;
                ResetCaretBlink();
                e.Handled = true;
                break;
        }
    }

    // ── Edit operations ───────────────────────────────────────────────────────

    private void HandleBackspace()
    {
        var para = _allParas[_caretParaIdx];
        if (_caretCharOffset > 0)
        {
            DeleteTextAt(para, _caretCharOffset - 1, 1);
            _caretCharOffset--;
            RefreshAfterEdit();
        }
        else if (_caretParaIdx > 0)
        {
            var prev    = _allParas[_caretParaIdx - 1];
            int prevLen = prev.PlainText.Length;

            foreach (var inline in para.Inlines.ToList())
            {
                para.Inlines.Remove(inline);
                prev.Inlines.Add(inline);
            }
            RemoveParagraphFromDoc(para);

            _caretParaIdx--;
            _caretCharOffset = prevLen;
            RefreshAfterEdit();
        }
    }

    private void HandleDelete()
    {
        var para = _allParas[_caretParaIdx];
        if (_caretCharOffset < para.PlainText.Length)
        {
            DeleteTextAt(para, _caretCharOffset, 1);
            RefreshAfterEdit();
        }
        else if (_caretParaIdx < _allParas.Count - 1)
        {
            var next = _allParas[_caretParaIdx + 1];
            foreach (var inline in next.Inlines.ToList())
            {
                next.Inlines.Remove(inline);
                para.Inlines.Add(inline);
            }
            RemoveParagraphFromDoc(next);
            RefreshAfterEdit();
        }
    }

    private void HandleEnter()
    {
        var para    = _allParas[_caretParaIdx];
        var newPara = SplitParagraphAt(para, _caretCharOffset);
        InsertParagraphAfter(para, newPara);

        _caretParaIdx++;
        _caretCharOffset = 0;
        RefreshAfterEdit();
    }

    private void MoveCaret(int delta)
    {
        if (delta < 0)
        {
            if (_caretCharOffset > 0)
                _caretCharOffset--;
            else if (_caretParaIdx > 0)
            {
                _caretParaIdx--;
                _caretCharOffset = _allParas[_caretParaIdx].PlainText.Length;
            }
        }
        else
        {
            int len = _allParas[_caretParaIdx].PlainText.Length;
            if (_caretCharOffset < len)
                _caretCharOffset++;
            else if (_caretParaIdx < _allParas.Count - 1)
            {
                _caretParaIdx++;
                _caretCharOffset = 0;
            }
        }
        ResetCaretBlink();
    }

    // ── Text/paragraph helpers ────────────────────────────────────────────────

    private static void InsertTextAt(DocParagraph para, int offset, string text)
    {
        int pos = 0;
        foreach (var run in para.Inlines.OfType<DocRun>())
        {
            int end = pos + run.Text.Length;
            if (offset <= end)
            {
                int ci = offset - pos;
                run.Text = run.Text[..ci] + text + run.Text[ci..];
                return;
            }
            pos = end;
        }
        // Append at end of last run (or create one)
        var last = para.Inlines.OfType<DocRun>().LastOrDefault();
        if (last != null) last.Text += text;
        else para.Inlines.Add(new DocRun { Text = text });
    }

    private static void DeleteTextAt(DocParagraph para, int offset, int count)
    {
        int pos = 0;
        foreach (var run in para.Inlines.OfType<DocRun>())
        {
            int end = pos + run.Text.Length;
            if (offset >= pos && offset < end)
            {
                int ci  = offset - pos;
                int del = Math.Min(count, run.Text.Length - ci);
                run.Text = run.Text[..ci] + run.Text[(ci + del)..];
                return;
            }
            pos = end;
        }
    }

    private static DocParagraph SplitParagraphAt(DocParagraph para, int offset)
    {
        var newPara = new DocParagraph { Format = para.Format.Clone() };
        var runs    = para.Inlines.OfType<DocRun>().ToList();
        int pos     = 0;

        for (int i = 0; i < runs.Count; i++)
        {
            int end = pos + runs[i].Text.Length;
            if (offset <= end)
            {
                int ci        = offset - pos;
                var textAfter = runs[i].Text[ci..];
                runs[i].Text  = runs[i].Text[..ci];

                if (textAfter.Length > 0)
                    newPara.Inlines.Add(new DocRun { Text = textAfter, Format = runs[i].Format.Clone() });

                for (int j = i + 1; j < runs.Count; j++)
                {
                    para.Inlines.Remove(runs[j]);
                    newPara.Inlines.Add(runs[j]);
                }
                break;
            }
            pos = end;
        }
        return newPara;
    }

    private void InsertParagraphAfter(DocParagraph after, DocParagraph newPara)
    {
        if (_document == null) return;
        foreach (var section in _document.Sections)
        {
            int idx = section.Blocks.IndexOf(after);
            if (idx >= 0) { section.Blocks.Insert(idx + 1, newPara); return; }
        }
    }

    private void RemoveParagraphFromDoc(DocParagraph para)
    {
        if (_document == null) return;
        foreach (var section in _document.Sections)
            if (section.Blocks.Remove(para)) return;
    }

    // ── Layout / render lifecycle ─────────────────────────────────────────────

    private void OnDocumentChanged(DocModel? doc)
    {
        _document = doc;
        _pages.Clear();
        Children.Clear();
        _allParas.Clear();
        _caretParaIdx    = 0;
        _caretCharOffset = 0;

        if (doc == null) return;
        Dispatcher.UIThread.Post(RebuildPages);
    }

    private void RebuildPages()
    {
        if (_document == null) return;

        _pages.Clear();
        Children.Clear();

        _allParas = _document.Sections
            .SelectMany(s => s.Blocks.OfType<DocParagraph>())
            .ToList();

        EnsureCaretInBounds();

        double y = PageMarginPx;
        foreach (var section in _document.Sections)
        {
            var pageWidth  = TwipsToPx(section.Layout.Width);
            var pageHeight = TwipsToPx(section.Layout.Height);

            var page = new PageControl
            {
                Section = section,
                Styles  = _document.Styles,
                Width   = pageWidth,
                Height  = pageHeight,
                Margin  = new Thickness(PageMarginPx, y, PageMarginPx, 0),
            };

            _pages.Add(page);
            Children.Add(page);
            y += pageHeight + PageMarginPx;
        }

        PushCaretToPages();
        InvalidateMeasure();
    }

    private void RefreshAfterEdit()
    {
        RebuildPages();
        if (_document != null) _document.IsModified = true;
        DocumentModified?.Invoke(this, EventArgs.Empty);
    }

    private void PushCaretToPages()
    {
        var caretPara = (_caretParaIdx >= 0 && _caretParaIdx < _allParas.Count)
            ? _allParas[_caretParaIdx]
            : null;

        foreach (var page in _pages)
        {
            page.CaretParaRef    = caretPara;
            page.CaretCharOffset = _caretCharOffset;
            page.CaretVisible    = _caretVisible && _isFocused;
            page.InvalidateVisual();
        }
    }

    private void ResetCaretBlink()
    {
        _caretVisible = true;
        _caretTimer.Stop();
        _caretTimer.Start();
        PushCaretToPages();
    }

    private void EnsureCaretInBounds()
    {
        if (_allParas.Count == 0) { _caretParaIdx = 0; _caretCharOffset = 0; return; }
        _caretParaIdx    = Math.Clamp(_caretParaIdx,    0, _allParas.Count - 1);
        _caretCharOffset = Math.Clamp(_caretCharOffset, 0, _allParas[_caretParaIdx].PlainText.Length);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (!_pages.Any()) return new Size(800, 600);

        var maxWidth    = _pages.Max(p => p.Width) + PageMarginPx * 2;
        var totalHeight = _pages.Sum(p => p.Height + PageMarginPx) + PageMarginPx;

        foreach (var page in _pages)
            page.Measure(new Size(page.Width, page.Height));

        return new Size(maxWidth, totalHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        double y  = PageMarginPx;
        double cx = finalSize.Width / 2;

        foreach (var page in _pages)
        {
            var x = cx - page.Width / 2;
            page.Arrange(new Rect(x, y, page.Width, page.Height));
            y += page.Height + PageMarginPx;
        }
        return finalSize;
    }

    private static double TwipsToPx(double twips) => twips / TwipsPerInch * ScreenDpi;
}

// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Визуальное представление одной страницы документа.</summary>
public sealed class PageControl : Control
{
    public DocSection?     Section      { get; set; }
    public new StyleRegistry?  Styles       { get; set; }

    // Caret info pushed from DocumentCanvas
    public DocParagraph?   CaretParaRef    { get; set; }
    public int             CaretCharOffset { get; set; }
    public bool            CaretVisible    { get; set; }

    private static readonly IBrush PageBackground = Brushes.White;
    private static readonly IPen   PageBorder     = new Pen(Brushes.LightGray, 1);
    private static readonly IBrush ShadowBrush    = new SolidColorBrush(Color.FromArgb(60, 0, 0, 0));
    private static readonly IPen   CaretPen        = new Pen(Brushes.Black, 1.5);

    private const double ScreenDpi     = 96.0;
    private const double TwipsPerInch  = 1440.0;
    private const double PtPerTwip     = 1.0 / 20.0;
    private const double PxPerPt       = ScreenDpi / 72.0;

    public override void Render(DrawingContext context)
    {
        // Shadow
        context.DrawRectangle(ShadowBrush, null,
            new Rect(3, 3, Bounds.Width, Bounds.Height), 2, 2);

        // Page background
        context.DrawRectangle(PageBackground, PageBorder,
            new Rect(0, 0, Bounds.Width, Bounds.Height));

        if (Section == null) return;

        var layout   = Section.Layout;
        var marginL  = TwipsToPx(layout.MarginLeft);
        var marginT  = TwipsToPx(layout.MarginTop);
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
            DocTable     t => RenderTable(ctx, t, x, y, width),
            _              => y,
        };

    private double RenderParagraph(DrawingContext ctx, DocParagraph para, double x, double y, double width)
    {
        var pFmt        = para.Format;
        var spaceBefore = pFmt.SpaceBefore * PtPerTwip * PxPerPt;
        var spaceAfter  = pFmt.SpaceAfter  * PtPerTwip * PxPerPt;
        var indentL     = pFmt.IndentLeft  * PtPerTwip * PxPerPt;
        var indentFirst = pFmt.IndentFirstLine * PtPerTwip * PxPerPt;

        y += spaceBefore;

        bool isCaret = CaretVisible && CaretParaRef == para;

        if (para.IsEmpty)
        {
            var lh = GetDefaultLineHeight(pFmt);
            if (isCaret)
                ctx.DrawLine(CaretPen, new Point(x + indentL, y), new Point(x + indentL, y + lh));
            return y + lh + spaceAfter;
        }

        var lines     = LayoutLines(para, x + indentL + indentFirst, x + indentL, x + width, pFmt);
        bool caretDrawn = false;
        double curY   = y;

        foreach (var line in lines)
        {
            var lineX = GetLineX(line, x + indentL, x + width, pFmt.Alignment);
            double curX = lineX;

            foreach (var seg in line.Segments)
            {
                var ft = BuildFormattedText(seg.Text, seg.Format, Styles);
                if (ft == null) { curX += seg.Width; continue; }

                ctx.DrawText(ft, new Point(curX, curY));

                // Draw caret if it falls inside this segment
                if (isCaret && !caretDrawn)
                {
                    int segEnd = seg.CharStart + seg.Text.Length;
                    if (CaretCharOffset >= seg.CharStart && CaretCharOffset < segEnd)
                    {
                        int ci      = CaretCharOffset - seg.CharStart;
                        double cx   = curX;
                        if (ci > 0)
                        {
                            var ftPart = BuildFormattedText(seg.Text[..ci], seg.Format, Styles);
                            cx += ftPart?.Width ?? 0;
                        }
                        ctx.DrawLine(CaretPen, new Point(cx, curY), new Point(cx, curY + line.Height));
                        caretDrawn = true;
                    }
                }

                curX += seg.Width;
            }

            curY += line.Height;
        }

        // Caret at end of paragraph (after last character)
        if (isCaret && !caretDrawn && lines.Count > 0)
        {
            var last      = lines[^1];
            var lastLineX = GetLineX(last, x + indentL, x + width, pFmt.Alignment);
            double endX   = lastLineX + last.TotalWidth;
            ctx.DrawLine(CaretPen,
                new Point(endX, curY - last.Height),
                new Point(endX, curY));
        }

        return curY + spaceAfter;
    }

    private double RenderTable(DrawingContext ctx, DocTable table, double x, double y, double width)
    {
        if (!table.Rows.Any()) return y;
        var colWidth  = width / Math.Max(table.ColumnCount, 1);
        var borderPen = new Pen(Brushes.Gray, 0.5);

        foreach (var row in table.Rows)
        {
            double rowMaxH = 20;
            double cx      = x;

            foreach (var cell in row.Cells)
            {
                var cellW = colWidth * cell.GridSpan;
                double cellH = 4;
                foreach (var block in cell.Blocks)
                    cellH = RenderBlock(ctx, block, cx + 4, cellH, cellW - 8);
                rowMaxH = Math.Max(rowMaxH, cellH + 4);
                cx += cellW;
            }

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

    // ── Layout engine (word-wrap) ─────────────────────────────────────────────

    // CharStart = byte offset into paragraph's PlainText at which this segment begins
    private sealed record LineSeg(string Text, RunFormat Format, double Width, int CharStart);
    private sealed record Line(List<LineSeg> Segments, double Height, double TotalWidth);

    private List<Line> LayoutLines(DocParagraph para, double firstLineX, double lineX, double maxX, ParaFormat pFmt)
    {
        var lines       = new List<Line>();
        var currentLine = new List<LineSeg>();
        double curX     = firstLineX;
        double lineH    = GetDefaultLineHeight(pFmt);
        bool isFirst    = true;
        int charOffset  = 0;  // tracks position in PlainText

        foreach (var inline in para.Inlines.OfType<DocRun>())
        {
            foreach (var word in SplitIntoWords(inline.Text))
            {
                var ft = BuildFormattedText(word, inline.Format, Styles);
                if (ft == null) { charOffset += word.Length; continue; }

                var wordW = ft.Width;
                var wordH = ft.Height;
                lineH = Math.Max(lineH, wordH * 1.2);

                if (curX + wordW > maxX && currentLine.Count > 0)
                {
                    lines.Add(new Line([.. currentLine], lineH, curX - (isFirst ? firstLineX : lineX)));
                    currentLine.Clear();
                    curX    = lineX;
                    lineH   = wordH * 1.2;
                    isFirst = false;
                }

                currentLine.Add(new LineSeg(word, inline.Format, wordW, charOffset));
                charOffset += word.Length;
                curX       += wordW;
            }
        }

        if (currentLine.Count > 0)
            lines.Add(new Line([.. currentLine], lineH, curX - (isFirst ? firstLineX : lineX)));

        return lines;
    }

    /// <summary>
    /// Splits text into word tokens. Trailing space is ATTACHED to its preceding word
    /// so that FormattedText.Width includes the space advance (single-space tokens
    /// return 0 width in Avalonia because whitespace-only text has no visual bounds).
    /// </summary>
    private static IEnumerable<string> SplitIntoWords(string text)
    {
        if (string.IsNullOrEmpty(text)) yield break;

        var word = new System.Text.StringBuilder();
        foreach (var ch in text)
        {
            word.Append(ch);
            if (ch == ' ' || ch == '\t')
            {
                yield return word.ToString(); // e.g. "hello " — space included in width
                word.Clear();
            }
        }
        if (word.Length > 0) yield return word.ToString();
    }

    private static double GetLineX(Line line, double marginX, double maxX, ParagraphAlignment align) =>
        align switch
        {
            ParagraphAlignment.Center => marginX + (maxX - marginX - line.TotalWidth) / 2,
            ParagraphAlignment.Right  => maxX - line.TotalWidth,
            _                         => marginX,
        };

    private static FormattedText? BuildFormattedText(string text, RunFormat fmt, StyleRegistry? styles)
    {
        if (string.IsNullOrEmpty(text)) return null;

        var resolved = styles?.ResolveRunFormat(fmt.StyleId) ?? RunFormat.Default;
        var fontName = fmt.FontName ?? resolved.FontName ?? "Arial";
        var fontSize = (fmt.FontSize ?? resolved.FontSize ?? 12) * PxPerPt;

        var weight   = (fmt.Bold   || resolved.Bold)   ? FontWeight.Bold   : FontWeight.Normal;
        var style    = (fmt.Italic || resolved.Italic) ? FontStyle.Italic  : FontStyle.Normal;

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
        return Math.Max(lineSpacingPx, 14);
    }

    private static double TwipsToPx(double twips) => twips / TwipsPerInch * ScreenDpi;
}
