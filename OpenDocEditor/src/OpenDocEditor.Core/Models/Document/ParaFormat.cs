namespace OpenDocEditor.Core.Models.Document;

/// <summary>
/// Форматирование абзаца — соответствует pPr в OpenXML.
/// </summary>
public sealed class ParaFormat : ICloneable
{
    public ParagraphAlignment Alignment { get; set; } = ParagraphAlignment.Left;

    /// <summary>Отступ слева (twips). 1440 twips = 1 дюйм.</summary>
    public int IndentLeft { get; set; }
    public int IndentRight { get; set; }
    public int IndentFirstLine { get; set; }
    public int IndentHanging { get; set; }

    /// <summary>Интервал перед/после абзаца (twips).</summary>
    public int SpaceBefore { get; set; }
    public int SpaceAfter { get; set; }

    public LineSpacingRule LineSpacingRule { get; set; } = LineSpacingRule.Auto;
    /// <summary>Значение межстрочного интервала (twips или %).</summary>
    public int LineSpacing { get; set; } = 240; // 240 twips = single

    public bool KeepTogether { get; set; }
    public bool KeepWithNext { get; set; }
    public bool PageBreakBefore { get; set; }
    public bool WidowControl { get; set; } = true;

    /// <summary>ID стиля абзаца (Normal, Heading1, …).</summary>
    public string? StyleId { get; set; }

    /// <summary>Нумерация: ID списка и уровень.</summary>
    public int? NumberingId { get; set; }
    public int NumberingLevel { get; set; }

    public DocColor? Shading { get; set; }

    public static ParaFormat Default => new();

    public ParaFormat Clone() => (ParaFormat)((ICloneable)this).Clone();

    object ICloneable.Clone() => new ParaFormat
    {
        Alignment = Alignment,
        IndentLeft = IndentLeft,
        IndentRight = IndentRight,
        IndentFirstLine = IndentFirstLine,
        IndentHanging = IndentHanging,
        SpaceBefore = SpaceBefore,
        SpaceAfter = SpaceAfter,
        LineSpacingRule = LineSpacingRule,
        LineSpacing = LineSpacing,
        KeepTogether = KeepTogether,
        KeepWithNext = KeepWithNext,
        PageBreakBefore = PageBreakBefore,
        WidowControl = WidowControl,
        StyleId = StyleId,
        NumberingId = NumberingId,
        NumberingLevel = NumberingLevel,
        Shading = Shading,
    };
}

public enum ParagraphAlignment { Left, Center, Right, Justify }
public enum LineSpacingRule { Auto, Exact, AtLeast }
