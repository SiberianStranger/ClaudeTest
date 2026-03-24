using OpenDocEditor.Core.Models.Document;
using OpenDocEditor.Core.Models.EDM;

namespace OpenDocEditor.Core.Services.EDM;

/// <summary>
/// Абстракция интеграции с системой электронного документооборота.
/// Реализации: Directum RX (REST), 1С-ДО (COM/REST), ТЕЗИС (REST), заглушка.
/// </summary>
public interface IEdmService
{
    string SystemName { get; }
    bool IsConnected { get; }

    /// <summary>Регистрирует документ в СЭД, присваивает регномер.</summary>
    Task<EdmRegistrationResult> RegisterDocumentAsync(DocModel doc, CancellationToken ct = default);

    /// <summary>Получает текущее состояние маршрута согласования.</summary>
    Task<WorkflowState> GetWorkflowStateAsync(string documentId, CancellationToken ct = default);

    /// <summary>Отправляет документ на согласование по указанному маршруту.</summary>
    Task SendForApprovalAsync(DocModel doc, ApprovalRouteOptions options, CancellationToken ct = default);

    /// <summary>Поиск документов по атрибутам.</summary>
    Task<IReadOnlyList<DocumentSearchResult>> SearchAsync(DocumentSearchQuery query, CancellationToken ct = default);

    /// <summary>Загружает документ из СЭД по ID.</summary>
    Task<DocModel?> FetchDocumentAsync(string documentId, CancellationToken ct = default);
}

public sealed class EdmRegistrationResult
{
    public bool Success { get; set; }
    public string? DocumentId { get; set; }
    public string? RegistrationNumber { get; set; }
    public DateTime? RegistrationDate { get; set; }
    public string? Error { get; set; }
}

public sealed class ApprovalRouteOptions
{
    public string? RouteId { get; set; }
    public string[] Approvers { get; set; } = [];
    public string? Comment { get; set; }
    public DateTime? Deadline { get; set; }
    public bool Parallel { get; set; } = false;
}

public sealed class DocumentSearchQuery
{
    public string? TextQuery { get; set; }
    public string? DocumentType { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    public string? Author { get; set; }
    public WorkflowState? State { get; set; }
    public int PageSize { get; set; } = 50;
    public int Page { get; set; } = 1;
}

public sealed class DocumentSearchResult
{
    public string DocumentId { get; set; } = string.Empty;
    public string? RegistrationNumber { get; set; }
    public string? Title { get; set; }
    public string? DocumentType { get; set; }
    public string? Author { get; set; }
    public DateTime? Date { get; set; }
    public WorkflowState State { get; set; }
}
