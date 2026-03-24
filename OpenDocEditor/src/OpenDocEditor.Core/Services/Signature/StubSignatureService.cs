using OpenDocEditor.Core.Models.Document;
using OpenDocEditor.Core.Models.EDM;

namespace OpenDocEditor.Core.Services.Signature;

/// <summary>
/// Заглушка службы ЭЦП для разработки и тестирования.
/// Замените на CryptoproSignatureService (КриптоПро) в production.
///
/// Интеграция с КриптоПро:
///   1. Установите КриптоПро CSP 5.0
///   2. Добавьте NuGet: CryptoPro.Sharpei (или используйте P/Invoke через cpcsp)
///   3. Реализуйте ISignatureService на основе System.Security.Cryptography.Pkcs
///      с провайдером GOST R 34.10-2012
///
/// Интеграция с Госключом:
///   1. Используйте REST API Госключ (oauth2 + /sign endpoint)
///   2. Документация: https://goskey.ru/api
/// </summary>
public sealed class StubSignatureService : ISignatureService
{
    public bool IsAvailable => true;
    public string ProviderName => "Stub (Тестовый)";

    public Task<SignatureInfo> SignDocumentAsync(DocModel doc, SigningOptions options, CancellationToken ct = default)
    {
        var sig = new SignatureInfo
        {
            SignerName = "Тестовый подписант",
            SignerOrganization = "ООО Тест",
            CertificateThumbprint = "AABBCCDD1122334455667788AABB1122334455",
            CertificateSerial = "1234567890",
            CertificateIssuer = "CN=Test CA, O=TestOrg, C=RU",
            SignedAt = DateTime.UtcNow,
            Algorithm = SignatureAlgorithm.GostR34102012_256,
            SignatureValue = GenerateStubSignature(doc),
            IsValid = true,
            Role = options.Role,
        };

        doc.Edm.Signatures.Add(sig);
        doc.Edm.AuditLog.Add(new AuditEntry
        {
            Action = AuditAction.Signed,
            UserName = sig.SignerName,
            Comment = $"Подписано ({ProviderName})",
        });

        return Task.FromResult(sig);
    }

    public Task<IReadOnlyList<SignatureVerificationResult>> VerifySignaturesAsync(DocModel doc, CancellationToken ct = default)
    {
        var results = doc.Edm.Signatures.Select(sig => new SignatureVerificationResult
        {
            Signature = sig,
            IsValid = sig.SignatureValue?.Length > 0,
            VerifiedAt = DateTime.UtcNow,
        }).ToList();

        return Task.FromResult<IReadOnlyList<SignatureVerificationResult>>(results);
    }

    public Task<IReadOnlyList<CertificateInfo>> GetAvailableCertificatesAsync(CancellationToken ct = default)
    {
        var certs = new List<CertificateInfo>
        {
            new()
            {
                Thumbprint = "AABBCCDD1122334455667788AABB1122334455",
                SubjectName = "CN=Тестов Тест Тестович, O=ООО Тест, C=RU",
                IssuerName = "CN=Test CA",
                NotBefore = DateTime.Today.AddYears(-1),
                NotAfter = DateTime.Today.AddYears(1),
                Inn = "7700000001",
                Organization = "ООО Тест",
                Algorithm = SignatureAlgorithm.GostR34102012_256,
            }
        };

        return Task.FromResult<IReadOnlyList<CertificateInfo>>(certs);
    }

    private static byte[] GenerateStubSignature(DocModel doc)
    {
        // В реальной реализации: хэш SHA-256/ГОСТ + подпись ГОСТ Р 34.10-2012
        var text = doc.AllParagraphs.SelectMany(p => p.Inlines.OfType<Models.Document.DocRun>())
            .Aggregate("", (acc, r) => acc + r.Text);
        return System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(text));
    }
}
