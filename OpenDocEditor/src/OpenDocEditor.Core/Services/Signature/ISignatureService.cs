using OpenDocEditor.Core.Models.Document;
using OpenDocEditor.Core.Models.EDM;

namespace OpenDocEditor.Core.Services.Signature;

/// <summary>
/// Абстракция службы электронной подписи.
/// Конкретные реализации: КриптоПро CSP, ViPNet CSP, Госключ (мобильный), тестовая заглушка.
/// </summary>
public interface ISignatureService
{
    /// <summary>Проверяет, доступен ли провайдер ЭЦП (установлен СКЗИ).</summary>
    bool IsAvailable { get; }

    /// <summary>Название провайдера (КриптоПро, ViPNet, Stub).</summary>
    string ProviderName { get; }

    /// <summary>Подписывает документ квалифицированной ЭП.</summary>
    Task<SignatureInfo> SignDocumentAsync(DocModel doc, SigningOptions options, CancellationToken ct = default);

    /// <summary>Проверяет все подписи документа.</summary>
    Task<IReadOnlyList<SignatureVerificationResult>> VerifySignaturesAsync(DocModel doc, CancellationToken ct = default);

    /// <summary>Возвращает список доступных сертификатов текущего пользователя.</summary>
    Task<IReadOnlyList<CertificateInfo>> GetAvailableCertificatesAsync(CancellationToken ct = default);
}

public sealed class SigningOptions
{
    public string? CertificateThumbprint { get; set; }
    public SignatureRole Role { get; set; } = SignatureRole.Signer;
    public string? Comment { get; set; }
    /// <summary>Встроить подпись в файл (true) или создать отдельный .sig файл.</summary>
    public bool Embedded { get; set; } = true;
    /// <summary>Метка времени через TSP (требует интернет).</summary>
    public bool UseTimestamp { get; set; } = true;
}

public sealed class SignatureVerificationResult
{
    public SignatureInfo Signature { get; set; } = new();
    public bool IsValid { get; set; }
    public string? Error { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public bool CertificateExpired { get; set; }
    public bool CertificateRevoked { get; set; }
}

public sealed class CertificateInfo
{
    public string Thumbprint { get; set; } = string.Empty;
    public string SubjectName { get; set; } = string.Empty;
    public string IssuerName { get; set; } = string.Empty;
    public DateTime NotBefore { get; set; }
    public DateTime NotAfter { get; set; }
    public bool IsExpired => DateTime.UtcNow > NotAfter;
    public string? Inn { get; set; }
    public string? Organization { get; set; }
    public SignatureAlgorithm Algorithm { get; set; }
    public string DisplayName => $"{SubjectName} ({NotAfter:dd.MM.yyyy})";
}
