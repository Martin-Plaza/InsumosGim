using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using GymShop.Application.Abstractions;
using GymShop.Domain.Enums;
using GymShop.Infrastructure.Configuration;
using GymShop.Infrastructure.Services;

namespace GymShop.Tests.Services;

public sealed class ArcaElectronicInvoiceGatewayTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 3, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Check_connection_authenticates_and_returns_wsfe_points_of_sale()
    {
        var options = CreateOptions();
        var handler = new RecordingHandler((request, call) =>
        {
            var body = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            if (request.RequestUri == options.WsaaAddress)
            {
                Assert.Contains("loginCms", body);
                return Xml(WsaaResponse(Now.AddHours(12)));
            }

            if (body.Contains("FEDummy", StringComparison.Ordinal))
                return Xml(DummyResponse());

            Assert.Contains("FEParamGetPtosVenta", body);
            Assert.Contains("30536259194", body);
            Assert.Contains("token-de-prueba", body);
            return Xml(PointsOfSaleResponse(4, 12));
        });
        var gateway = CreateGateway(options, handler);

        var first = await gateway.CheckConnectionAsync();
        var second = await gateway.CheckConnectionAsync();

        Assert.True(first.ConfigurationReady);
        Assert.True(first.WsfeReachable);
        Assert.True(first.WsaaAuthenticated);
        Assert.Equal([4, 12], first.PointsOfSale);
        Assert.Null(first.ErrorCode);
        Assert.Equal(5, handler.Calls);
        Assert.Equal(first.PointsOfSale, second.PointsOfSale);
    }

    [Fact]
    public async Task Check_connection_reports_incomplete_configuration_without_http_requests()
    {
        var handler = new RecordingHandler((_, _) => throw new InvalidOperationException("HTTP should not be called"));
        var gateway = CreateGateway(new ArcaOptions(), handler);

        var result = await gateway.CheckConnectionAsync();

        Assert.False(result.ConfigurationReady);
        Assert.False(result.WsaaAuthenticated);
        Assert.False(result.WsfeReachable);
        Assert.Equal("arca_configuration_incomplete", result.ErrorCode);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task Check_connection_does_not_expose_wsaa_fault_details_beyond_a_bounded_message()
    {
        var options = CreateOptions();
        var handler = new RecordingHandler((request, _) =>
        {
            var body = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return request.RequestUri == options.WsaaAddress
                ? Xml("""
                    <soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
                      <soap:Body><soap:Fault><faultstring>certificado no autorizado</faultstring></soap:Fault></soap:Body>
                    </soap:Envelope>
                    """)
                : Xml(DummyResponse());
        });
        var gateway = CreateGateway(options, handler);

        var result = await gateway.CheckConnectionAsync();

        Assert.Equal("arca_wsaa_rejected", result.ErrorCode);
        Assert.False(result.WsaaAuthenticated);
        Assert.True(result.WsfeReachable);
        Assert.Contains("certificado no autorizado", result.Message);
    }

    private static ArcaElectronicInvoiceGateway CreateGateway(ArcaOptions options, HttpMessageHandler handler) =>
        new(
            new HttpClient(handler),
            options,
            new TestBillingProfile(),
            new ArcaAccessTicketCache(),
            new FixedTimeProvider(Now));

    private static ArcaOptions CreateOptions()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=GymShop ARCA Homologation", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(Now.AddDays(-1), Now.AddYears(1));
        return new ArcaOptions
        {
            CertificatePemBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(certificate.ExportCertificatePem())),
            PrivateKeyPemBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(rsa.ExportPkcs8PrivateKeyPem()))
        };
    }

    private static HttpResponseMessage Xml(string value) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(value, Encoding.UTF8, "text/xml")
    };

    private static string DummyResponse() => """
        <soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
          <soap:Body><FEDummyResponse><FEDummyResult><AppServer>OK</AppServer><DbServer>OK</DbServer><AuthServer>OK</AuthServer></FEDummyResult></FEDummyResponse></soap:Body>
        </soap:Envelope>
        """;

    private static string WsaaResponse(DateTimeOffset expiration) => $"""
        <soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
          <soap:Body><loginCmsResponse><loginCmsReturn>&lt;loginTicketResponse&gt;&lt;header&gt;&lt;expirationTime&gt;{expiration:yyyy-MM-dd'T'HH:mm:sszzz}&lt;/expirationTime&gt;&lt;/header&gt;&lt;credentials&gt;&lt;token&gt;token-de-prueba&lt;/token&gt;&lt;sign&gt;firma-de-prueba&lt;/sign&gt;&lt;/credentials&gt;&lt;/loginTicketResponse&gt;</loginCmsReturn></loginCmsResponse></soap:Body>
        </soap:Envelope>
        """;

    private static string PointsOfSaleResponse(params int[] points) => $"""
        <soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
          <soap:Body><FEParamGetPtosVentaResponse><FEParamGetPtosVentaResult><ResultGet>{string.Join(string.Empty, points.Select(x => $"<PtoVenta><Nro>{x}</Nro><EmisionTipo>CAE</EmisionTipo></PtoVenta>"))}</ResultGet></FEParamGetPtosVentaResult></FEParamGetPtosVentaResponse></soap:Body>
        </soap:Envelope>
        """;

    private sealed class RecordingHandler(Func<HttpRequestMessage, int, HttpResponseMessage> response) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(response(request, Calls));
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class TestBillingProfile : IBillingProfile
    {
        public BillingMode Mode => BillingMode.ElectronicInvoice;
        public SellerTaxCondition TaxCondition => SellerTaxCondition.Monotributo;
        public string BusinessName => "Comercio de prueba";
        public string Cuit => "30-53625919-4";
        public string FiscalAddress => "Catamarca 2730, Rosario";
        public string GrossIncomeNumber => "Exento";
        public DateOnly? ActivityStartDate => new(2020, 1, 1);
        public int? PointOfSale => 4;
        public bool ArcaEnabled => true;
        public bool ElectronicInvoicingReady => true;
    }
}
