namespace OpenDocEditor.Core.Models.Document;

/// <summary>
/// Таблица. Соответствует &lt;w:tbl&gt; в OpenXML.
/// </summary>
public sealed class DocTable : IDocBlock
{
    public TableFormat Format { get; set; } = new();
    public List<DocTableRow> Rows { get; set; } = [];

    public int RowCount => Rows.Count;
    public int ColumnCount => Rows.FirstOrDefault()?.Cells.Count ?? 0;
}

public sealed class DocTableRow
{
    public List<DocTableCell> Cells { get; set; } = [];
    public int? Height { get; set; }   // twips, null = auto
    public bool IsHeader { get; set; }
}

public sealed class DocTableCell
{
    public List<IDocBlock> Blocks { get; set; } = [];
    public CellFormat Format { get; set; } = new();
    public int? Width { get; set; }    // twips
    public int GridSpan { get; set; } = 1;
    public int RowSpan { get; set; } = 1;
}

public sealed class TableFormat
{
    public int? Width { get; set; }
    public TableAlignment Alignment { get; set; }
    public int CellMarginTop { get; set; } = 115;
    public int CellMarginBottom { get; set; } = 115;
    public int CellMarginLeft { get; set; } = 108;
    public int CellMarginRight { get; set; } = 108;
    public BorderSet? Borders { get; set; }
}

public sealed class CellFormat
{
    public DocColor? BackgroundColor { get; set; }
    public VerticalCellAlignment VerticalAlignment { get; set; }
    public BorderSet? Borders { get; set; }
}

public sealed class BorderSet
{
    public BorderLine? Top { get; set; }
    public BorderLine? Bottom { get; set; }
    public BorderLine? Left { get; set; }
    public BorderLine? Right { get; set; }
    public BorderLine? InsideH { get; set; }
    public BorderLine? InsideV { get; set; }
}

public sealed class BorderLine
{
    public BorderStyle Style { get; set; } = BorderStyle.Single;
    public int Width { get; set; } = 4;  // eighths of a point
    public DocColor? Color { get; set; }
}

public enum TableAlignment { Left, Center, Right }
public enum VerticalCellAlignment { Top, Center, Bottom }
public enum BorderStyle { None, Single, Double, Dotted, Dashed, Triple }
