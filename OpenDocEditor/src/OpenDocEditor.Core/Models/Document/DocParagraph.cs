namespace OpenDocEditor.Core.Models.Document;

/// <summary>
/// Базовый интерфейс блочных элементов документа.
/// </summary>
public interface IDocBlock { }

/// <summary>
/// Абзац — основная единица текстового контента.
/// Соответствует &lt;w:p&gt; в OpenXML.
/// </summary>
public sealed class DocParagraph : IDocBlock
{
    public ParaFormat Format { get; set; } = ParaFormat.Default;

    /// <summary>Inline-элементы абзаца (runs, images, fields, breaks).</summary>
    public List<IInlineElement> Inlines { get; set; } = [];

    /// <summary>Возвращает весь текст абзаца без форматирования.</summary>
    public string PlainText => string.Concat(Inlines.OfType<DocRun>().Select(r => r.Text));

    /// <summary>Пустой абзац (только параграф-маркер).</summary>
    public bool IsEmpty => !Inlines.Any() || Inlines.All(i => i is DocRun r && string.IsNullOrEmpty(r.Text));

    public DocParagraph() { }

    public DocParagraph(string text, RunFormat? runFmt = null, ParaFormat? paraFmt = null)
    {
        Format = paraFmt ?? ParaFormat.Default;
        if (!string.IsNullOrEmpty(text))
            Inlines.Add(new DocRun(text, runFmt));
    }

    /// <summary>Добавить текстовый фрагмент.</summary>
    public DocParagraph AddRun(string text, RunFormat? fmt = null)
    {
        Inlines.Add(new DocRun(text, fmt));
        return this;
    }

    public DocParagraph Clone()
    {
        var clone = new DocParagraph { Format = Format.Clone() };
        foreach (var inline in Inlines)
        {
            clone.Inlines.Add(inline switch
            {
                DocRun r => r.Clone(),
                DocBreak b => new DocBreak { BreakType = b.BreakType },
                DocField f => new DocField { FieldCode = f.FieldCode, CachedValue = f.CachedValue, Format = f.Format.Clone() },
                DocImage img => new DocImage { Data = img.Data, ContentType = img.ContentType, WidthEmu = img.WidthEmu, HeightEmu = img.HeightEmu },
                _ => inline
            });
        }
        return clone;
    }
}
