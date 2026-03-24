using OpenDocEditor.Core.Models.Document;

namespace OpenDocEditor.Core.Services.Documents;

/// <summary>Фасад для работы с документами.</summary>
public interface IDocumentService
{
    Task<DocModel> CreateNewAsync();
    Task<DocModel> OpenAsync(string filePath, CancellationToken ct = default);
    Task SaveAsync(DocModel doc, string? filePath = null, CancellationToken ct = default);
    Task ExportPdfAsync(DocModel doc, string filePath, CancellationToken ct = default);
    bool CanOpen(string filePath);
}

public interface IDocxReaderService
{
    Task<DocModel> ReadAsync(string filePath, CancellationToken ct = default);
    Task<DocModel> ReadAsync(Stream stream, CancellationToken ct = default);
}

public interface IDocxWriterService
{
    Task WriteAsync(DocModel doc, string filePath, CancellationToken ct = default);
    Task WriteAsync(DocModel doc, Stream stream, CancellationToken ct = default);
}

public interface IPdfExportService
{
    Task ExportAsync(DocModel doc, string filePath, CancellationToken ct = default);
    Task ExportAsync(DocModel doc, Stream stream, CancellationToken ct = default);
}
