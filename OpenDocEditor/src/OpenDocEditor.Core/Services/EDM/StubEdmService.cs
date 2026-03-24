using OpenDocEditor.Core.Models.Document;
using OpenDocEditor.Core.Models.EDM;

namespace OpenDocEditor.Core.Services.EDM;

/// <summary>
/// Заглушка СЭД-сервиса.
///
/// Интеграция с Directum RX:
///   - REST API: /integration/odata/SessionService
///   - Документация: https://help.directum.ru/api
///   - Авторизация: OAuth2 / Basic
///
/// Интеграция с 1С-ДО (Документооборот):
///   - HTTP-сервис 1С или COM-объект "V83.COMConnector"
///   - Метод: ДокументооборотHTTPКлиент.ЗарегистрироватьДокумент()
///
/// СМЭВ 3.0 (межведомственный электронный обмен):///   - WSDL: https://mc.gov.ru/services/...
///   - Используйте TransportClient (NuGet: Smev3Client или собственная реализация WCF)
/// </summary>
public sealed class StubEdmService : IEdmService
{
    private int _counter = 1000;

    public string SystemName => "Stub (Тестовая СЭД)";
    public bool IsConnected => true;

    public Task<EdmRegistrationResult> RegisterDocumentAsync(DocModel doc, CancellationToken ct = default)
    {
        var regNumber = $"ОБ-{++_counter}/{DateTime.Now.Year}";
        doc.Edm.RegistrationNumber = regNumber;
        doc.Edm.RegistrationDate = DateTime.Now;
        doc.Edm.DocumentId ??= Guid.NewGuid().ToString();
        doc.Edm.WorkflowState = WorkflowState.Registered;

        doc.Edm.AuditLog.Add(new AuditEntry
        {
            Action = AuditAction.Registered,
            Comment = $"Зарегистрирован: {regNumber}",
            SystemName = SystemName,
        });

        return Task.FromResult(new EdmRegistrationResult
        {
            Success = true,
            DocumentId = doc.Edm.DocumentId,
            RegistrationNumber = regNumber,
            RegistrationDate = doc.Edm.RegistrationDate,
        });
    }

    public Task<WorkflowState> GetWorkflowStateAsync(string documentId, CancellationToken ct = default)
        => Task.FromResult(WorkflowState.Registered);

    public Task SendForApprovalAsync(DocModel doc, ApprovalRouteOptions options, CancellationToken ct = default)
    {
        doc.Edm.WorkflowState = WorkflowState.InApproval;
        doc.Edm.AuditLog.Add(new AuditEntry
        {
            Action = AuditAction.Sent,
            Comment = $"Отправлено на согласование. Участники: {string.Join(", ", options.Approvers)}",
            SystemName = SystemName,
        });
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<DocumentSearchResult>> SearchAsync(DocumentSearchQuery query, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<DocumentSearchResult>>([]);

    public Task<DocModel?> FetchDocumentAsync(string documentId, CancellationToken ct = default)
        => Task.FromResult<DocModel?>(null);
}
