using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using OpenDocEditor.Core.Models.Document;

namespace OpenDocEditor.App.Controls;

public sealed class DocumentCanvas : Panel
{
    private DocModel? _document;
    private readonly List<PageControl> _pages = [];
    private List<DocParagraph> _allParas = [];
    private int  _caretParaIdx;
    private int  _caretCharOffset;
    private bool _caretVisible;
    private bool _isFocused;
    private readonly DispatcherTimer _caretTimer;

    private const double PageMarginPx = 20;
    private const double ScreenDpi    = 96.0;
    private const double TwipsPerInch = 1440.0;

    public static readonly StyledProperty<DocModel?> DocumentProperty =
        AvaloniaProperty.Register<DocumentCanvas, DocModel?>(nameof(Document));

    public DocModel? Document
    {
        get => GetValue(DocumentProperty);
        set => SetValue(DocumentProperty, value);
    }

    public event EventHandler? DocumentModified;

    static DocumentCanvas()
    {
        DocumentProperty.Changed.AddClassHandler<DocumentCanvas>((c, e) =>
            c.OnDocumentChanged(e.NewValue as DocModel));
    }

    public DocumentCanvas()
    {
        Background = Brushes.Transparent;
        Focusable  = true;
        _caretTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(530) };
        _caretTimer.Tick += (_, _) => { _caretVisible = !_caretVisible; PushCaretToPages(); };
    }

    protected override void OnGotFocus(GotFocusEventArgs e)
    {
        base.OnGotFocus(e);
        _isFocused = true; _caretVisible = true;
        _caretTimer.Start(); PushCaretToPages();
    }

    protected override void OnLostFocus(RoutedEventArgs e)
    {
        base.OnLostFocus(e);
        _isFocused = false; _caretTimer.Stop(); _caretVisible = false; PushCaretToPages();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();
        var clickPos = e.GetPosition(this);
        foreach (var page in _pages)
        {
            if (!page.Bounds.Contains(clickPos)) continue;
            var local = clickPos - page.Bounds.TopLeft;
            var (para, ch) = page.HitTest(local);
            if (para != null)
            {
                int idx = _allParas.IndexOf(para);
                if (idx >= 0) { _caretParaIdx = idx; _caretCharOffset = ch; }
            }
            break;
        }
        ResetCaretBlink(); e.Handled = true;
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);
        if (string.IsNullOrEmpty(e.Text) || _allParas.Count == 0) return;
        EnsureCaretInBounds();
        InsertTextAt(_allParas[_caretParaIdx], _caretCharOffset, e.Text);
        _caretCharOffset += e.Text.Length;
        FastRefresh(); e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control)) return;
        if (_allParas.Count == 0) return;
        EnsureCaretInBounds();
        switch (e.Key)
        {
            case Key.Back:   HandleBackspace(); e.Handled = true; break;
            case Key.Delete: HandleDelete();    e.Handled = true; break;
            case Key.Enter:  HandleEnter();     e.Handled = true; break;
            case Key.Left:   MoveCaret(-1);     e.Handled = true; break;
            case Key.Right:  MoveCaret(+1);     e.Handled = true; break;
            case Key.Up:
                if (_caretParaIdx > 0)
                { _caretParaIdx--; _caretCharOffset = Math.Min(_caretCharOffset, _allParas[_caretParaIdx].PlainText.Length); ResetCaretBlink(); }
                e.Handled = true; break;
            case Key.Down:
                if (_caretParaIdx < _allParas.Count - 1)
                { _caretParaIdx++; _caretCharOffset = Math.Min(_caretCharOffset, _allParas[_caretParaIdx].PlainText.Length); ResetCaretBlink(); }
                e.Handled = true; break;
            case Key.Home: _caretCharOffset = 0; ResetCaretBlink(); e.Handled = true; break;
            case Key.End:  _caretCharOffset = _allParas[_caretParaIdx].PlainText.Length; ResetCaretBlink(); e.Handled = true; break;
        }
    }

    private void HandleBackspace()
    {
        var para = _allParas[_caretParaIdx];
        if (_caretCharOffset > 0)
        { DeleteTextAt(para, _caretCharOffset - 1, 1); _caretCharOffset--; FastRefresh(); }
        else if (_caretParaIdx > 0)
        {
            var prev = _allParas[_caretParaIdx - 1]; int prevLen = prev.PlainText.Length;
            foreach (var il in para.Inlines.ToList()) { para.Inlines.Remove(il); prev.Inlines.Add(il); }
            RemoveParagraphFromDoc(para); _caretParaIdx--; _caretCharOffset = prevLen; FullRebuild();
        }
    }

    private void HandleDelete()
    {
        var para = _allParas[_caretParaIdx];
        if (_caretCharOffset < para.PlainText.Length)
        { DeleteTextAt(para, _caretCharOffset, 1); FastRefresh(); }
        else if (_caretParaIdx < _allParas.Count - 1)
        {
            var next = _allParas[_caretParaIdx + 1];
            foreach (var il in next.Inlines.ToList()) { next.Inlines.Remove(il); para.Inlines.Add(il); }
            RemoveParagraphFromDoc(next); FullRebuild();
        }
    }

    private void HandleEnter()
    {
        var para = _allParas[_caretParaIdx];
        var newPara = SplitParagraphAt(para, _caretCharOffset);
        InsertParagraphAfter(para, newPara); _caretParaIdx++; _caretCharOffset = 0; FullRebuild();
    }

    private void MoveCaret(int delta)
    {
        if (delta < 0)
        { if (_caretCharOffset > 0) _caretCharOffset--; else if (_caretParaIdx > 0) { _caretParaIdx--; _caretCharOffset = _allParas[_caretParaIdx].PlainText.Length; } }
        else
        { int len = _allParas[_caretParaIdx].PlainText.Length; if (_caretCharOffset < len) _caretCharOffset++; else if (_caretParaIdx < _allParas.Count - 1) { _caretParaIdx++; _caretCharOffset = 0; } }
        ResetCaretBlink();
    }

    private static void InsertTextAt(DocParagraph para, int offset, string text)
    {
        int pos = 0;
        foreach (var run in para.Inlines.OfType<DocRun>())
        {
            int end = pos + run.Text.Length;
            if (offset <= end) { int ci = offset - pos; run.Text = run.Text[..ci] + text + run.Text[ci..]; return; }
            pos = end;
        }
        var last = para.Inlines.OfType<DocRun>().LastOrDefault();
        if (last != null) last.Text += text; else para.Inlines.Add(new DocRun { Text = text });
    }

    private static void DeleteTextAt(DocParagraph para, int offset, int count)
    {
        int pos = 0;
        foreach (var run in para.Inlines.OfType<DocRun>())
        {
            int end = pos + run.Text.Length;
            if (offset >= pos && offset < end)
            { int ci = offset - pos; int del = Math.Min(count, run.Text.Length - ci); run.Text = run.Text[..ci] + run.Text[(ci + del)..]; return; }
            pos = end;
        }
    }

    private static DocParagraph SplitParagraphAt(DocParagraph para, int offset)
    {
        var newPara = new DocParagraph { Format = para.Format.Clone() };
        var runs = para.Inlines.OfType<DocRun>().ToList(); int pos = 0;
        for (int i = 0; i < runs.Count; i++)
        {
            int end = pos + runs[i].Text.Length;
            if (offset <= end)
            {
                int ci = offset - pos; var ta = runs[i].Text[ci..]; runs[i].Text = runs[i].Text[..ci];
                if (ta.Length > 0) newPara.Inlines.Add(new DocRun { Text = ta, Format = runs[i].Format.Clone() });
                for (int j = i + 1; j < runs.Count; j++) { para.Inlines.Remove(runs[j]); newPara.Inlines.Add(runs[j]); }
                break;
            }
            pos = end;
        }
        return newPara;
    }

    private void InsertParagraphAfter(DocParagraph after, DocParagraph newPara)
    {
        if (_document == null) return;
        foreach (var s in _document.Sections) { int i = s.Blocks.IndexOf(after); if (i >= 0) { s.Blocks.Insert(i + 1, newPara); return; } }
    }

    private void RemoveParagraphFromDoc(DocParagraph para)
    {
        if (_document == null) return;
        foreach (var s in _document.Sections) if (s.Blocks.Remove(para)) return;
    }

    private void FastRefresh()
    {
        PushCaretToPages();
        if (_document != null) _document.IsModified = true;
        DocumentModified?.Invoke(this, EventArgs.Empty);
    }

    private void FullRebuild()
    {
        RebuildPages();
        if (_document != null) _document.IsModified = true;
        DocumentModified?.Invoke(this, EventArgs.Empty);
    }

    private void OnDocumentChanged(DocModel? doc)
    {
        _document = doc; _pages.Clear(); Children.Clear(); _allParas.Clear();
        _caretParaIdx = 0; _caretCharOffset = 0;
        if (doc == null) return;
        Dispatcher.UIThread.Post(RebuildPages);
    }

    private void RebuildPages()
    {
        if (_document == null) return;
        _pages.Clear(); Children.Clear();
        _allParas = _document.Sections.SelectMany(s => s.Blocks.OfType<DocParagraph>()).ToList();
        EnsureCaretInBounds();
        double y = PageMarginPx;
        foreach (var section in _document.Sections)
        {
            var pw = TwipsToPx(section.Layout.Width); var ph = TwipsToPx(section.Layout.Height);
            var page = new PageControl { Section = section, DocStyles = _document.Styles, Width = pw, Height = ph, Margin = new Thickness(PageMarginPx, y, PageMarginPx, 0) };
            _pages.Add(page); Children.Add(page); y += ph + PageMarginPx;
        }
        PushCaretToPages(); InvalidateMeasure();
    }

    private void PushCaretToPages()
    {
        var cp = (_caretParaIdx >= 0 && _caretParaIdx < _allParas.Count) ? _allParas[_caretParaIdx] : null;
        foreach (var page in _pages) { page.CaretParaRef = cp; page.CaretCharOffset = _caretCharOffset; page.CaretVisible = _caretVisible && _isFocused; page.InvalidateVisual(); }
    }

    private void ResetCaretBlink() { _caretVisible = true; _caretTimer.Stop(); _caretTimer.Start(); PushCaretToPages(); }

    private void EnsureCaretInBounds()
    {
        if (_allParas.Count == 0) { _caretParaIdx = 0; _caretCharOffset = 0; return; }
        _caretParaIdx    = Math.Clamp(_caretParaIdx, 0, _allParas.Count - 1);
        _caretCharOffset = Math.Clamp(_caretCharOffset, 0, _allParas[_caretParaIdx].PlainText.Length);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (!_pages.Any()) return new Size(800, 600);
        var maxW = _pages.Max(p => p.Width) + PageMarginPx * 2;
        var totH = _pages.Sum(p => p.Height + PageMarginPx) + PageMarginPx;
        foreach (var page in _pages) page.Measure(new Size(page.Width, page.Height));
        return new Size(maxW, totH);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        double y = PageMarginPx; double cx = finalSize.Width / 2;
        foreach (var page in _pages) { var x = cx - page.Width / 2; page.Arrange(new Rect(x, y, page.Width, page.Height)); y += page.Height + PageMarginPx; }
        return finalSize;
    }

    private static double TwipsToPx(double twips) => twips / TwipsPerInch * ScreenDpi;
}

public sealed class PageControl : Control
{
    public DocSection?    Section   { get; set; }
    public StyleRegistry? DocStyles { get; set; }
    public DocParagraph?  CaretParaRef    { get; set; }
    public int            CaretCharOffset { get; set; }
    public bool           CaretVisible    { get; set; }

    private static readonly IPen CaretPen = new Pen(Brushes.Black, 1.5);

    private const double ScreenDpi    = 96.0;
    private const double TwipsPerInch = 1440.0;
    private const double PtPerTwip    = 1.0 / 20.0;
    private const double PxPerPt      = ScreenDpi / 72.0;

    public override void Render(DrawingContext ctx)
    {
        ctx.DrawRectangle(new SolidColorBrush(Color.FromArgb(20, 0, 0, 0)), null, new Rect(4, 4, Bounds.Width, Bounds.Height), 3, 3);
        ctx.DrawRectangle(new SolidColorBrush(Color.FromArgb(10, 0, 0, 0)), null, new Rect(7, 7, Bounds.Width, Bounds.Height), 3, 3);
        ctx.DrawRectangle(Brushes.White, new Pen(new SolidColorBrush(Color.FromArgb(50, 0, 0, 0)), 1), new Rect(0, 0, Bounds.Width, Bounds.Height));
        if (Section == null) return;
        var layout = Section.Layout;
        double ml = TwipsToPx(layout.MarginLeft); double mt = TwipsToPx(layout.MarginTop);
        double cw = Bounds.Width - ml - TwipsToPx(layout.MarginRight);
        double y = mt;
        using var clip = ctx.PushClip(new Rect(0, 0, Bounds.Width, Bounds.Height));
        foreach (var block in Section.Blocks) y = RenderBlock(ctx, block, ml, y, cw);
    }

    private double RenderBlock(DrawingContext ctx, IDocBlock block, double x, double y, double w) =>
        block switch { DocParagraph p => RenderParagraph(ctx, p, x, y, w), DocTable t => RenderTable(ctx, t, x, y, w), _ => y };

    private double RenderParagraph(DrawingContext ctx, DocParagraph para, double x, double y, double width)
    {
        var f = para.Format;
        double sb = f.SpaceBefore * PtPerTwip * PxPerPt, sa = f.SpaceAfter * PtPerTwip * PxPerPt;
        double il = f.IndentLeft * PtPerTwip * PxPerPt, fi = f.IndentFirstLine * PtPerTwip * PxPerPt;
        y += sb;
        bool isCaret = CaretVisible && CaretParaRef == para;

        if (para.IsEmpty)
        {
            double lh = GetDefaultLineHeight(f);
            if (isCaret) ctx.DrawLine(CaretPen, new Point(x + il, y), new Point(x + il, y + lh));
            return y + lh + sa;
        }

        var lines = LayoutLines(para, x + il + fi, x + il, x + width, f);
        bool drawn = false; double cy = y;

        foreach (var line in lines)
        {
            double lx = GetLineX(line, x + il, x + width, f.Alignment); double cx = lx;
            foreach (var seg in line.Segments)
            {
                var ft = MakeFT(seg.Text.TrimEnd(), seg.Format, DocStyles);
                if (ft != null) ctx.DrawText(ft, new Point(cx, cy));
                DrawDecorations(ctx, seg, cx, cy, line.Height);
                if (isCaret && !drawn && CaretCharOffset >= seg.CharStart && CaretCharOffset < seg.CharStart + seg.Text.Length)
                {
                    int ci = CaretCharOffset - seg.CharStart;
                    double kx = cx + AdvanceWidth(seg.Text[..ci], seg.Format, DocStyles);
                    ctx.DrawLine(CaretPen, new Point(kx, cy), new Point(kx, cy + line.Height));
                    drawn = true;
                }
                cx += seg.Width;
            }
            cy += line.Height;
        }

        if (isCaret && !drawn && lines.Count > 0)
        {
            var last = lines[^1]; double ex = GetLineX(last, x + il, x + width, f.Alignment) + last.TotalWidth;
            ctx.DrawLine(CaretPen, new Point(ex, cy - last.Height), new Point(ex, cy));
        }
        return cy + sa;
    }

    private static void DrawDecorations(DrawingContext ctx, LineSeg seg, double x, double y, double lh)
    {
        if (!seg.Format.Underline && !seg.Format.Strikethrough) return;
        var pen = new Pen(Brushes.Black, 0.75);
        if (seg.Format.Underline)     ctx.DrawLine(pen, new Point(x, y + lh * 0.90), new Point(x + seg.Width, y + lh * 0.90));
        if (seg.Format.Strikethrough) ctx.DrawLine(pen, new Point(x, y + lh * 0.55), new Point(x + seg.Width, y + lh * 0.55));
    }

    private double RenderTable(DrawingContext ctx, DocTable table, double x, double y, double width)
    {
        if (table.Rows.Count == 0) return y;
        double colW = width / Math.Max(table.ColumnCount, 1);
        var border = new Pen(new SolidColorBrush(Color.FromRgb(180, 180, 180)), 0.5);
        foreach (var row in table.Rows)
        {
            double rowH = 20; double cx = x;
            foreach (var cell in row.Cells) { double cw = colW * cell.GridSpan; double h = 4; foreach (var b in cell.Blocks) h = RenderBlock(ctx, b, cx + 4, h, cw - 8); rowH = Math.Max(rowH, h + 4); cx += cw; }
            cx = x;
            foreach (var cell in row.Cells) { double cw = colW * cell.GridSpan; ctx.DrawRectangle(null, border, new Rect(cx, y, cw, rowH)); double fy = y + 4; foreach (var b in cell.Blocks) fy = RenderBlock(ctx, b, cx + 4, fy, cw - 8); cx += cw; }
            y += rowH;
        }
        return y;
    }

    public (DocParagraph? para, int charOffset) HitTest(Point local)
    {
        if (Section == null) return (null, 0);
        var layout = Section.Layout;
        double x = TwipsToPx(layout.MarginLeft), y = TwipsToPx(layout.MarginTop);
        double width = Bounds.Width - x - TwipsToPx(layout.MarginRight);
        DocParagraph? lastPara = null;
        foreach (var block in Section.Blocks)
        {
            if (block is not DocParagraph para) continue;
            lastPara = para;
            var (nextY, hit) = HitTestPara(para, x, y, width, local);
            if (hit >= 0) return (para, hit);
            y = nextY;
        }
        return (lastPara, lastPara?.PlainText.Length ?? 0);
    }

    private (double nextY, int hit) HitTestPara(DocParagraph para, double x, double y, double width, Point p)
    {
        var f = para.Format;
        double sb = f.SpaceBefore * PtPerTwip * PxPerPt, sa = f.SpaceAfter * PtPerTwip * PxPerPt;
        double il = f.IndentLeft * PtPerTwip * PxPerPt, fi = f.IndentFirstLine * PtPerTwip * PxPerPt;
        y += sb;
        if (para.IsEmpty) { double lh = GetDefaultLineHeight(f); return (y + lh + sa, (p.Y >= y - sb && p.Y < y + lh) ? 0 : -1); }
        var lines = LayoutLines(para, x + il + fi, x + il, x + width, f); double cy = y;
        foreach (var line in lines)
        {
            if (p.Y >= cy && p.Y < cy + line.Height)
            {
                double lx = GetLineX(line, x + il, x + width, f.Alignment); double cx = lx;
                foreach (var seg in line.Segments) { if (p.X < cx + seg.Width) return (cy, seg.CharStart + HitSeg(seg, p.X - cx)); cx += seg.Width; }
                var ls = line.Segments[^1]; return (cy, ls.CharStart + ls.Text.TrimEnd().Length);
            }
            cy += line.Height;
        }
        return (cy + sa, -1);
    }

    private int HitSeg(LineSeg seg, double relX)
    {
        int lo = 0, hi = seg.Text.Length;
        while (lo < hi) { int mid = (lo + hi + 1) / 2; if (AdvanceWidth(seg.Text[..mid], seg.Format, DocStyles) <= relX) lo = mid; else hi = mid - 1; }
        if (lo < seg.Text.Length)
        { double wL = AdvanceWidth(seg.Text[..lo], seg.Format, DocStyles), wH = AdvanceWidth(seg.Text[..(lo + 1)], seg.Format, DocStyles); if (relX - wL > wH - relX) return lo + 1; }
        return lo;
    }

    private sealed record LineSeg(string Text, RunFormat Format, double Width, int CharStart);
    private sealed record Line(List<LineSeg> Segments, double Height, double TotalWidth);

    private List<Line> LayoutLines(DocParagraph para, double firstX, double lineX, double maxX, ParaFormat pFmt)
    {
        var lines = new List<Line>(); var current = new List<LineSeg>();
        double curX = firstX; double lineH = GetDefaultLineHeight(pFmt); bool isFirst = true; int charOff = 0;
        foreach (var inline in para.Inlines.OfType<DocRun>())
        {
            foreach (var word in SplitWords(inline.Text))
            {
                double wordW = AdvanceWidth(word, inline.Format, DocStyles);
                double wordH = MakeFT(word.TrimEnd(), inline.Format, DocStyles)?.Height ?? lineH / 1.2;
                lineH = Math.Max(lineH, wordH * 1.2);
                if (curX + wordW > maxX && current.Count > 0)
                { lines.Add(new Line([..current], lineH, curX - (isFirst ? firstX : lineX))); current.Clear(); curX = lineX; lineH = wordH * 1.2; isFirst = false; }
                current.Add(new LineSeg(word, inline.Format, wordW, charOff));
                charOff += word.Length; curX += wordW;
            }
        }
        if (current.Count > 0) lines.Add(new Line([..current], lineH, curX - (isFirst ? firstX : lineX)));
        return lines;
    }

    private static IEnumerable<string> SplitWords(string text)
    {
        if (string.IsNullOrEmpty(text)) yield break;
        var sb = new System.Text.StringBuilder();
        foreach (var ch in text) { sb.Append(ch); if (ch == ' ' || ch == '\t') { yield return sb.ToString(); sb.Clear(); } }
        if (sb.Length > 0) yield return sb.ToString();
    }

    private static double GetLineX(Line line, double marginX, double maxX, ParagraphAlignment align) =>
        align switch { ParagraphAlignment.Center => marginX + (maxX - marginX - line.TotalWidth) / 2, ParagraphAlignment.Right => maxX - line.TotalWidth, _ => marginX };

    // Sentinel trick: AdvanceWidth("text i") - AdvanceWidth("i") captures trailing-space advance
    private static double AdvanceWidth(string text, RunFormat fmt, StyleRegistry? styles)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        if (text[^1] != ' ' && text[^1] != '\t') return MakeFT(text, fmt, styles)?.Width ?? 0;
        const string s = "i";
        double comb = MakeFT(text + s, fmt, styles)?.Width ?? 0;
        double sent = MakeFT(s,        fmt, styles)?.Width ?? 0;
        return Math.Max(0, comb - sent);
    }

    private static FormattedText? MakeFT(string text, RunFormat fmt, StyleRegistry? styles)
    {
        if (string.IsNullOrEmpty(text)) return null;
        var r = styles?.ResolveRunFormat(fmt.StyleId) ?? RunFormat.Default;
        var fontName = fmt.FontName ?? r.FontName ?? "Arial";
        var fontSize = (fmt.FontSize ?? r.FontSize ?? 12) * PxPerPt;
        var weight   = (fmt.Bold   || r.Bold)   ? FontWeight.Bold   : FontWeight.Normal;
        var fstyle   = (fmt.Italic || r.Italic) ? FontStyle.Italic  : FontStyle.Normal;
        IBrush color = fmt.Color != null ? new SolidColorBrush(Color.FromRgb(fmt.Color.R, fmt.Color.G, fmt.Color.B)) : Brushes.Black;
        return new FormattedText(text, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(fontName, fstyle, weight), fontSize, color);
    }

    private double GetDefaultLineHeight(ParaFormat f) => Math.Max(f.LineSpacing * PtPerTwip * PxPerPt, 14);
    private static double TwipsToPx(double twips) => twips / TwipsPerInch * ScreenDpi;
}
