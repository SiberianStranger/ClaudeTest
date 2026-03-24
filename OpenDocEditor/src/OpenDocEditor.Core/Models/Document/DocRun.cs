namespace OpenDocEditor.Core.Models.Document;

/// <summary>
/// Базовый интерфейс для содержимого абзаца (inline elements).
/// </summary>
public interface IInlineElement { }

/// <summary>
/// Текстовый фрагмент с единым форматированием.
/// Соответствует элементу &lt;w:r&gt; в OpenXML.
/// </summary>
public sealed class DocRun : IInlineElement
{
    public string Text { get; set; } = string.Empty;
    public RunFormat Format { get; set; } = RunFormat.Default;

    public DocRun() { }
    public DocRun(string text, RunFormat? format = null)
    {
        Text = text;
        Format = format ?? RunFormat.Default;
    }

    public DocRun Clone() => new(Text, Format.Clone());
}

/// <summary>
/// Разрыв: строки, страницы, или столбца.
/// </summary>
public sealed class DocBreak : IInlineElement
{
    public BreakType BreakType { get; set; } = BreakType.Line;
}

/// <summary>
/// Встроенное изображение.
/// </summary>
public sealed class DocImage : IInlineElement
{
    public byte[] Data { get; set; } = [];
    public string ContentType { get; set; } = "image/png";
    public int WidthEmu { get; set; }   // English Metric Units (914400 EMU = 1 inch)
    public int HeightEmu { get; set; }
    public string? AltText { get; set; }

    public double WidthPt => WidthEmu / 12700.0;
    public double HeightPt => HeightEmu / 12700.0;
}

/// <summary>
/// Поле (PAGE, DATE, AUTHOR, DOCPROPERTY и др.).
/// </summary>
public sealed class DocField : IInlineElement
{
    public string FieldCode { get; set; } = string.Empty;
    public string? CachedValue { get; set; }
    public RunFormat Format { get; set; } = RunFormat.Default;
}

/// <summary>
/// Гиперссылка.
/// </summary>
public sealed class DocHyperlink : IInlineElement
{
    public string Url { get; set; } = string.Empty;
    public string? Tooltip { get; set; }
    public List<DocRun> Runs { get; set; } = [];
}

public enum BreakType { Line, Page, Column }
