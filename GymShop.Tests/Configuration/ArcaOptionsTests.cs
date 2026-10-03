using GymShop.Infrastructure.Configuration;

namespace GymShop.Tests.Configuration;

public sealed class ArcaOptionsTests
{
    [Fact]
    public void Disabled_connector_does_not_require_secrets()
    {
        Assert.Empty(new ArcaOptions().Validate(enabled: false));
    }

    [Fact]
    public void Enabled_connector_requires_homologation_secrets()
    {
        var failures = new ArcaOptions().Validate(enabled: true);

        Assert.Contains(failures, value => value.Contains("CertificatePemBase64", StringComparison.Ordinal));
        Assert.Contains(failures, value => value.Contains("PrivateKeyPemBase64", StringComparison.Ordinal));
    }

    [Fact]
    public void Production_is_explicitly_blocked()
    {
        var options = new ArcaOptions
        {
            Environment = "Production",
            CertificatePemBase64 = "not-a-certificate",
            PrivateKeyPemBase64 = "not-a-key"
        };

        Assert.Contains(options.Validate(enabled: true), value => value.Contains("Production is intentionally blocked", StringComparison.Ordinal));
    }
}
