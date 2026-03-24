namespace OpenDocEditor.Core.Models.Document;

/// <summary>
/// Форматирование символов (character formatting) — соответствует rPr в OpenXML.
/// </summary>
public sealed class RunFormat : ICloneable
{
    public string? FontName { get; set; }
    public float? FontSize { get; set; }       // pt
    public bool Bold { get; set; }
    public bool Italic { get; set; }
    public bool Underline { get; set; }
    public string? UnderlineStyle { get; set; } // single, double, dotted, etc.
    public bool Strikethrough { get; set; }
    public bool DoubleStrikethrough { get; set; }
    public bool SmallCaps { get; set; }
    public bool AllCaps { get; set; }
    public DocColor? Color { get; set; }
    public DocColor? Highlight { get; set; }
    public VerticalAlignment VerticalAlignment { get; set; } = VerticalAlignment.Baseline;
    public string? Lang { get; set; }           // ru-RU, en-US …
    public string? StyleId { get; set; }        // ссылка на символьный стиль
    public int? Spacing { get; set; }           // межсимвольный интервал (twips)
    public int? Kern { get; set; }              // кернинг (pt * 2)

    public static RunFormat Default => new();

    public RunFormat Clone() => (RunFormat)((ICloneable)this).Clone();

    object ICloneable.Clone() => new RunFormat
    {
        FontName = FontName,
        FontSize = FontSize,
        Bold = Bold,
        Italic = Italic,
        Underline = Underline,
        UnderlineStyle = UnderlineStyle,
        Strikethrough = Strikethrough,
        DoubleStrikethrough = DoubleStrikethrough,
        SmallCaps = SmallCaps,
        AllCaps = AllCaps,
        Color = Color,
        Highlight = Highlight,
        VerticalAlignment = VerticalAlignment,
        Lang = Lang,
        StyleId = StyleId,
        Spacing = Spacing,
        Kern = Kern,
    };

    /// <summary>Возвращает новый формат, применяющий изменения поверх базового.</summary>
    public RunFormat ApplyOverride(RunFormat over)
    {
        var result = Clone();
        if (over.FontName != null) result.FontName = over.FontName;
        if (over.FontSize != null) result.FontSize = over.FontSize;
        if (over.Bold) result.Bold = true;
        if (over.Italic) result.Italic = true;
        if (over.Underline) result.Underline = true;
        if (over.Strikethrough) result.Strikethrough = true;
        if (over.Color != null) result.Color = over.Color;
        if (over.Highlight != null) result.Highlight = over.Highlight;
        if (over.Lang != null) result.Lang = over.Lang;
        return result;
    }
}

public sealed class DocColor
{
    public byte R { get; init; }
    public byte G { get; init; }
    public byte B { get; init; }

    public DocColor(byte r, byte g, byte b) { R = r; G = g; B = b; }

    public static DocColor Black => new(0, 0, 0);
    public static DocColor White => new(255, 255, 255);
    public static DocColor Auto => new(0, 0, 0);  // автоцвет

    public static DocColor? FromHex(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex) || hex.Equals("auto", StringComparison.OrdinalIgnoreCase))
            return null;
        hex = hex.TrimStart('#');
        if (hex.Length == 6)
            return new DocColor(
                Convert.ToByte(hex[..2], 16),
                Convert.ToByte(hex[2..4], 16),
                Convert.ToByte(hex[4..6], 16));
        return null;
    }

    public string ToHex() => $"#{R:X2}{G:X2}{B:X2}";
    public override string ToString() => ToHex();
}

public enum VerticalAlignment { Baseline, Superscript, Subscript }
