namespace OpenDocEditor.Core.Models.EDM;

/// <summary>
/// Метаданные системы электронного документооборота (СЭД).
/// Поддерживает форматы Directum RX, 1С-ДО, ТЕЗИС и совместимые.
/// Хранится в custom XML-части DOCX (docProps/custom.xml).
/// </summary>
public sealed class EdmMetadata
{
    /// <summary>Уникальный идентификатор документа в СЭД.</summary>
    public string? DocumentId { get; set; }

    /// <summary>Регистрационный номер (например: № 1234/2025).</summary>
    public string? RegistrationNumber { get; set; }

    /// <summary>Дата регистрации.</summary>
    public DateTime? RegistrationDate { get; set; }

    /// <summary>Вид документа (Приказ, Протокол, Договор, …).</summary>
    public string? DocumentType { get; set; }

    /// <summary>Автор/составитель.</summary>
    public string? Author { get; set; }

    /// <summary>Организация-отправитель.</summary>
    public string? Organization { get; set; }

    /// <summary>ИНН организации (для интеграции с ФНС/госсистемами).</summary>
    public string? Inn { get; set; }

    /// <summary>Степень конфиденциальности.</summary>
    public SecurityLevel SecurityLevel { get; set; } = SecurityLevel.Public;

    /// <summary>Состояние маршрута согласования.</summary>
    public WorkflowState WorkflowState { get; set; } = WorkflowState.Draft;

    /// <summary>Подписи ЭЦП, прикреплённые к документу.</summary>
    public List<SignatureInfo> Signatures { get; set; } = [];

    /// <summary>Журнал аудита (история действий).</summary>
    public List<AuditEntry> AuditLog { get; set; } = [];

    /// <summary>Ссылки на связанные документы (прикреплённые, родительский).</summary>
    public List<DocumentLink> Links { get; set; } = [];

    /// <summary>Расширяемые пользовательские атрибуты (ключ-значение).</summary>
    public Dictionary<string, string> CustomAttributes { get; set; } = new();

    public bool HasRegistration => !string.IsNullOrEmpty(RegistrationNumber);
    public bool IsSigned => Signatures.Any(s => s.IsValid == true);
}

/// <summary>Информация об электронной подписи.</summary>
public sealed class SignatureInfo
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string? SignerName { get; set; }
    public string? SignerPosition { get; set; }
    public string? SignerOrganization { get; set; }
    public string? CertificateThumbprint { get; set; }
    public string? CertificateSerial { get; set; }
    public string? CertificateIssuer { get; set; }
    public DateTime? SignedAt { get; set; }
    public SignatureAlgorithm Algorithm { get; set; } = SignatureAlgorithm.GostR34102012_256;
    public byte[]? SignatureValue { get; set; }
    public bool? IsValid { get; set; }   // null = не проверялась
    public string? ValidationError { get; set; }

    /// <summary>Тип подписи.</summary>
    public SignatureRole Role { get; set; } = SignatureRole.Author;
}

/// <summary>Запись журнала аудита.</summary>
public sealed class AuditEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? UserId { get; set; }
    public string? UserName { get; set; }
    public AuditAction Action { get; set; }
    public string? Comment { get; set; }
    public string? SystemName { get; set; }   // СМЭВ, Госключ, ручной ввод
}

/// <summary>Ссылка на связанный документ.</summary>
public sealed class DocumentLink
{
    public string? DocumentId { get; set; }
    public string? RegistrationNumber { get; set; }
    public DocumentLinkType LinkType { get; set; }
    public string? SystemName { get; set; }
}

public enum SecurityLevel { Public, Confidential, Secret, TopSecret }

public enum WorkflowState
{
    Draft,           // Черновик
    InApproval,      // На согласовании
    Approved,        // Согласован
    Rejected,        // Отклонён
    Registered,      // Зарегистрирован
    Sent,            // Отправлен
    Executed,        // Исполнен
    Archived         // В архиве
}

public enum SignatureAlgorithm
{
    GostR34102012_256,  // ГОСТ Р 34.10-2012 (256 бит) — основной в РФ
    GostR34102012_512,  // ГОСТ Р 34.10-2012 (512 бит)
    GostR34102001,      // Устаревший ГОСТ (до 2019)
    RsaSha256,          // RSA + SHA-256 (для совместимости)
}

public enum SignatureRole { Author, Approver, Signer, Witness }
public enum AuditAction { Created, Opened, Edited, Saved, Sent, Received, Approved, Rejected, Signed, Registered, Archived, Printed }
public enum DocumentLinkType { Attachment, Parent, Child, Reply, Supersedes }
