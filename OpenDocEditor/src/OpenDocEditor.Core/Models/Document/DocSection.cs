namespace OpenDocEditor.Core.Models.Document;

/// <summary>
/// Секция документа — группа блоков с общей разметкой страницы.
/// Соответствует &lt;w:sectPr&gt; в OpenXML.
/// </summary>
public sealed class DocSection
{
    public PageLayout Layout { get; set; } = PageLayout.A4Portrait;
    public List<IDocBlock> Blocks { get; set; } = [];
    public DocHeaderFooter? Header { get; set; }
    public DocHeaderFooter? Footer { get; set; }
    public DocHeaderFooter? FirstPageHeader { get; set; }
    public DocHeaderFooter? FirstPageFooter { get; set; }
    public bool DifferentFirstPage { get; set; }
    public SectionBreakType BreakType { get; set; } = SectionBreakType.NextPage;
}

/// <summary>Колонтитул секции.</summary>
public sealed class DocHeaderFooter
{
    public List<IDocBlock> Blocks { get; set; } = [];
}

public enum SectionBreakType { Continuous, NextPage, EvenPage, OddPage }
