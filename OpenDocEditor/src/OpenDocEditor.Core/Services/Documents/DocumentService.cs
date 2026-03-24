using OpenDocEditor.Core.Models.Document;
using OpenDocEditor.Core.Models.EDM;
using Microsoft.Extensions.Logging;

namespace OpenDocEditor.Core.Services.Documents;

/// <summary>
/// Фасад: основная точка входа для операций с документами.
/// Оркестрирует чтение, запись, экспорт и аудит.
/// </summary>
public sealed class DocumentService : IDocumentService
{
    private readonly IDocxReaderService _reader;
    private readonly IDocxWriterService _writer;
    private readonly IPdfExportService _pdfExport;
    private readonly ILogger<DocumentService> _logger;

    public DocumentService(
        IDocxReaderService reader,
        IDocxWriterService writer,
        IPdfExportService pdfExport,
        ILogger<DocumentService> logger)
    {
        _reader = reader;
        _writer = writer;
        _pdfExport = pdfExport;
        _logger = logger;
    }

    public Task<DocModel> CreateNewAsync()
    {
        _logger.LogInformation("Creating new document");
        var doc = DocModel.CreateEmpty();
        doc.Properties.Created = DateTime.UtcNow;
        doc.Properties.Modified = DateTime.UtcNow;
        doc.Edm.AuditLog.Add(new AuditEntry { Action = AuditAction.Created });
        return Task.FromResult(doc);
    }

    public async Task<DocModel> OpenAsync(string filePath, CancellationToken ct = default)
    {
        if (!CanOpen(filePath))
            throw new NotSupportedException($"Формат не поддерживается: {Path.GetExtension(filePath)}");

        var doc = await _reader.ReadAsync(filePath, ct);
        doc.Edm.AuditLog.Add(new AuditEntry { Action = AuditAction.Opened });
        return doc;
    }

    public async Task SaveAsync(DocModel doc, string? filePath = null, CancellationToken ct = default)
    {
        var targetPath = filePath ?? doc.FilePath
            ?? throw new InvalidOperationException("Не указан путь для сохранения.");

        doc.Properties.Modified = DateTime.UtcNow;
        doc.Properties.Revision++;

        await _writer.WriteAsync(doc, targetPath, ct);

        doc.FilePath = targetPath;
        doc.IsModified = false;
        doc.Edm.AuditLog.Add(new AuditEntry { Action = AuditAction.Saved });

        _logger.LogInformation("Document saved: {Path}", targetPath);
    }

    public async Task ExportPdfAsync(DocModel doc, string filePath, CancellationToken ct = default)
    {
        await _pdfExport.ExportAsync(doc, filePath, ct);
        doc.Edm.AuditLog.Add(new AuditEntry { Action = AuditAction.Saved, Comment = $"PDF: {filePath}" });
    }

    public bool CanOpen(string filePath) =>
        Path.GetExtension(filePath).Equals(".docx", StringComparison.OrdinalIgnoreCase);
}
