namespace OpenDocEditor.Core.Models.Document;

/// <summary>
/// Стиль документа (Normal, Heading1, …).
/// Соответствует &lt;w:style&gt; в styles.xml.
/// </summary>
public sealed class DocStyle
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public StyleType Type { get; set; }
    public string? BasedOn { get; set; }
    public string? NextStyle { get; set; }
    public bool IsDefault { get; set; }
    public bool IsBuiltIn { get; set; }
    public ParaFormat? ParaFormat { get; set; }
    public RunFormat? RunFormat { get; set; }
}

/// <summary>
/// Реестр стилей документа.
/// </summary>
public sealed class StyleRegistry
{
    private readonly Dictionary<string, DocStyle> _styles = new(StringComparer.OrdinalIgnoreCase);

    public void Add(DocStyle style) => _styles[style.Id] = style;

    public DocStyle? Get(string? id) =>
        id != null && _styles.TryGetValue(id, out var s) ? s : null;

    public DocStyle? Default =>
        _styles.Values.FirstOrDefault(s => s.IsDefault && s.Type == StyleType.Paragraph);

    /// <summary>Резолвит полное форматирование с учётом наследования стилей.</summary>
    public RunFormat ResolveRunFormat(string? styleId)
    {
        var result = RunFormat.Default;
        var chain = GetStyleChain(styleId);
        foreach (var style in chain)
            if (style.RunFormat != null)
                result = result.ApplyOverride(style.RunFormat);
        return result;
    }

    public IReadOnlyCollection<DocStyle> All => _styles.Values;

    private IEnumerable<DocStyle> GetStyleChain(string? id)
    {
        var chain = new List<DocStyle>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var current = Get(id);
        while (current != null && visited.Add(current.Id))
        {
            chain.Insert(0, current);
            current = current.BasedOn != null ? Get(current.BasedOn) : null;
        }
        return chain;
    }
}

public enum StyleType { Paragraph, Character, Table, Numbering }
