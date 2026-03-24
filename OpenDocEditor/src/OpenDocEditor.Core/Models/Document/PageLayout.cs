namespace OpenDocEditor.Core.Models.Document;

/// <summary>Параметры страницы секции.</summary>
public sealed class PageLayout : ICloneable
{
    /// <summary>Ширина страницы (twips).</summary>
    public int Width { get; set; } = 12240;   // A4 ~ 210mm
    /// <summary>Высота страницы (twips).</summary>
    public int Height { get; set; } = 15840;  // A4 ~ 297mm

    public int MarginTop { get; set; } = 1440;
    public int MarginBottom { get; set; } = 1440;
    public int MarginLeft { get; set; } = 1800;
    public int MarginRight { get; set; } = 1800;

    public PageOrientation Orientation { get; set; } = PageOrientation.Portrait;

    public int HeaderDistance { get; set; } = 720;
    public int FooterDistance { get; set; } = 720;

    /// <summary>Количество колонок (1 = нет колонок).</summary>
    public int Columns { get; set; } = 1;

    public static PageLayout A4Portrait => new();
    public static PageLayout A4Landscape => new()
    {
        Width = 15840, Height = 12240, Orientation = PageOrientation.Landscape
    };

    public double WidthMm => Width / 56.69;
    public double HeightMm => Height / 56.69;

    object ICloneable.Clone() => new PageLayout
    {
        Width = Width, Height = Height,
        MarginTop = MarginTop, MarginBottom = MarginBottom,
        MarginLeft = MarginLeft, MarginRight = MarginRight,
        Orientation = Orientation,
        HeaderDistance = HeaderDistance, FooterDistance = FooterDistance,
        Columns = Columns,
    };
}

public enum PageOrientation { Portrait, Landscape }
