namespace OpenDocEditor.Core.Models.Document;

/// <summary>
/// Корневая модель документа — полное внутреннее представление DOCX.
/// Не зависит от формата файла: является чистой доменной моделью.
/// </summary>
public sealed class DocModel
{
    /// <summary>Уникальный идентификатор (генерируется при создании).</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>Путь к исходному файлу (null для нового документа).</summary>
    public string? FilePath { get; set; }

    /// <summary>Имя файла для отображения.</summary>
    public string DisplayName => FilePath != null
        ? Path.GetFileName(FilePath)
        : "Новый документ";

    /// <summary>Секции документа (минимум одна).</summary>
    public List<DocSection> Sections { get; set; } = [new DocSection()];

    /// <summary>Реестр стилей.</summary>
    public StyleRegistry Styles { get; set; } = new();

    /// <summary>Метаданные (Core Properties).</summary>
    public DocProperties Properties { get; set; } = new();

    /// <summary>Метаданные СЭД/ЭДО.</summary>
    public EDM.EdmMetadata Edm { get; set; } = new();

    /// <summary>Флаг несохранённых изменений.</summary>
    public bool IsModified { get; set; }

    /// <summary>Все блоки первой (главной) секции — удобный accessor.</summary>
    public List<IDocBlock> Blocks => Sections[0].Blocks;

    /// <summary>Создаёт пустой документ с одним абзацем.</summary>
    public static DocModel CreateEmpty()
    {
        var doc = new DocModel();
        doc.Blocks.Add(new DocParagraph());
        doc.Styles.Add(new DocStyle
        {
            Id = "Normal",
            Name = "Normal",
            Type = StyleType.Paragraph,
            IsDefault = true,
            IsBuiltIn = true,
            RunFormat = new RunFormat { FontName = "Times New Roman", FontSize = 12 },
            ParaFormat = new ParaFormat { SpaceAfter = 0, LineSpacing = 240 }
        });
        return doc;
    }

    /// <summary>Все абзацы всех секций (для поиска/замены).</summary>
    public IEnumerable<DocParagraph> AllParagraphs =>
        Sections.SelectMany(s => s.Blocks).OfType<DocParagraph>()
        .Concat(Sections.SelectMany(s => s.Blocks).OfType<DocTable>()
            .SelectMany(t => t.Rows).SelectMany(r => r.Cells)
            .SelectMany(c => c.Blocks).OfType<DocParagraph>());
}

/// <summary>Свойства документа (Dublin Core / OPC Core Properties).</summary>
public sealed class DocProperties
{
    public string? Title { get; set; }
    public string? Subject { get; set; }
    public string? Description { get; set; }
    public string? Author { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? Category { get; set; }
    public string? Keywords { get; set; }
    public DateTime? Created { get; set; }
    public DateTime? Modified { get; set; }
    public string? Company { get; set; }
    public int Revision { get; set; } = 1;
}
