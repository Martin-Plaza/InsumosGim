using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace GymShop.Infrastructure.Configuration;

public sealed class ArcaOptions
{
    public const string SectionName = "Arca";
    public const string HomologationEnvironment = "Homologation";

    public string Environment { get; set; } = HomologationEnvironment;
    public string CertificatePemBase64 { get; set; } = string.Empty;
    public string PrivateKeyPemBase64 { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 20;

    public Uri WsaaAddress => new("https://wsaahomo.afip.gov.ar/ws/services/LoginCms");
    public Uri WsfeAddress => new("https://wswhomo.afip.gov.ar/wsfev1/service.asmx");

    public bool IsConfigured =>
        string.Equals(Environment, HomologationEnvironment, StringComparison.OrdinalIgnoreCase) &&
        !string.IsNullOrWhiteSpace(CertificatePemBase64) &&
        !string.IsNullOrWhiteSpace(PrivateKeyPemBase64);

    public IReadOnlyList<string> Validate(bool enabled)
    {
        var failures = new List<string>();
        if (TimeoutSeconds is < 1 or > 120)
            failures.Add("Arca:TimeoutSeconds must be between 1 and 120.");

        if (!enabled) return failures;

        if (!string.Equals(Environment, HomologationEnvironment, StringComparison.OrdinalIgnoreCase))
            failures.Add("Only Arca:Environment=Homologation is supported. Production is intentionally blocked.");
        if (string.IsNullOrWhiteSpace(CertificatePemBase64))
            failures.Add("Arca:CertificatePemBase64 is required when ARCA is enabled.");
        if (string.IsNullOrWhiteSpace(PrivateKeyPemBase64))
            failures.Add("Arca:PrivateKeyPemBase64 is required when ARCA is enabled.");

        if (failures.Count == 0)
        {
            try
            {
                using var certificate = LoadCertificate();
                if (!certificate.HasPrivateKey)
                    failures.Add("ARCA testing certificate does not have its matching private key.");
                if (certificate.NotBefore.ToUniversalTime() > DateTime.UtcNow || certificate.NotAfter.ToUniversalTime() <= DateTime.UtcNow)
                    failures.Add("ARCA testing certificate is not currently valid.");
            }
            catch (Exception exception) when (exception is FormatException or CryptographicException or ArgumentException)
            {
                failures.Add("ARCA testing certificate or private key is invalid or they do not match.");
            }
        }

        return failures;
    }

    public X509Certificate2 LoadCertificate()
    {
        var certificatePem = DecodePem(CertificatePemBase64);
        var privateKeyPem = DecodePem(PrivateKeyPemBase64);
        using var pemCertificate = X509Certificate2.CreateFromPem(certificatePem, privateKeyPem);

        // Re-import the pair as PKCS#12 so the private key is backed by the
        // platform certificate implementation. SignedCms cannot reliably use
        // the transient PEM key directly in Linux containers.
        var pkcs12 = pemCertificate.Export(X509ContentType.Pkcs12, string.Empty);
        return X509CertificateLoader.LoadPkcs12(
            pkcs12,
            string.Empty,
            X509KeyStorageFlags.EphemeralKeySet | X509KeyStorageFlags.Exportable);
    }

    private static string DecodePem(string value) =>
        Encoding.UTF8.GetString(Convert.FromBase64String(value.Trim()));
}
