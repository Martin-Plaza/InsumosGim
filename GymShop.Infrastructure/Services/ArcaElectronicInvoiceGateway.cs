using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml.Linq;
using GymShop.Application.Abstractions;
using GymShop.Infrastructure.Configuration;

namespace GymShop.Infrastructure.Services;

public sealed class ArcaElectronicInvoiceGateway(
    HttpClient httpClient,
    ArcaOptions options,
    IBillingProfile billingProfile,
    ArcaAccessTicketCache ticketCache,
    TimeProvider timeProvider) : IArcaElectronicInvoiceGateway
{
    private const string WsfeNamespace = "http://ar.gov.afip.dif.FEV1/";

    public async Task<ArcaConnectionStatus> CheckConnectionAsync(CancellationToken cancellationToken = default)
    {
        var checkedAt = timeProvider.GetUtcNow().UtcDateTime;
        if (!options.IsConfigured)
            return Failure(false, false, "arca_configuration_incomplete", "Faltan el certificado o la clave privada de homologacion.", checkedAt);

        var wsfeReachable = false;
        try
        {
            wsfeReachable = await CheckWsfeHealthAsync(cancellationToken);
            if (!wsfeReachable)
                return Failure(false, false, "arca_wsfe_unavailable", "ARCA respondio, pero alguno de los servicios de WSFE no esta disponible.", checkedAt);

            var ticket = await ticketCache.GetOrCreateAsync(CreateAccessTicketAsync, timeProvider, cancellationToken);
            var pointsOfSale = await GetPointsOfSaleAsync(ticket, cancellationToken);
            return new ArcaConnectionStatus(
                ArcaOptions.HomologationEnvironment,
                true,
                true,
                true,
                pointsOfSale,
                null,
                pointsOfSale.Count == 0
                    ? "La autenticacion funciono, pero ARCA no devolvio puntos de venta habilitados para WSFE."
                    : "Conexion de homologacion validada correctamente.",
                checkedAt);
        }
        catch (ArcaGatewayException exception)
        {
            return Failure(exception.WsaaAuthenticated, wsfeReachable, exception.Code, exception.Message, checkedAt);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Failure(false, wsfeReachable, "arca_timeout", "ARCA no respondio dentro del tiempo configurado.", checkedAt);
        }
        catch (HttpRequestException)
        {
            return Failure(false, wsfeReachable, "arca_network_error", "No se pudo establecer comunicacion con ARCA.", checkedAt);
        }
        catch (Exception exception) when (exception is FormatException or CryptographicException or ArgumentException)
        {
            return Failure(false, wsfeReachable, "arca_certificate_invalid", "El certificado de homologacion o su clave privada no son validos.", checkedAt);
        }
    }

    private ArcaConnectionStatus Failure(bool authenticated, bool reachable, string code, string message, DateTime checkedAt) =>
        new(ArcaOptions.HomologationEnvironment, options.IsConfigured, authenticated, reachable, [], code, message, checkedAt);

    private async Task<bool> CheckWsfeHealthAsync(CancellationToken cancellationToken)
    {
        var body = new XElement(XName.Get("FEDummy", WsfeNamespace));
        var response = await SendSoapAsync(options.WsfeAddress, body, "FEDummy", cancellationToken);
        var values = response.Descendants()
            .Where(x => x.Name.LocalName is "AppServer" or "DbServer" or "AuthServer")
            .Select(x => x.Value.Trim())
            .ToList();
        return values.Count == 3 && values.All(x => string.Equals(x, "OK", StringComparison.OrdinalIgnoreCase));
    }

    private async Task<ArcaAccessTicket> CreateAccessTicketAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var uniqueId = now.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var loginTicketRequest = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement("loginTicketRequest",
                new XAttribute("version", "1.0"),
                new XElement("header",
                    new XElement("uniqueId", uniqueId),
                    new XElement("generationTime", now.AddMinutes(-5).ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture)),
                    new XElement("expirationTime", now.AddMinutes(10).ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture))),
                new XElement("service", "wsfe")));

        using var certificate = options.LoadCertificate();
        var content = new ContentInfo(Encoding.UTF8.GetBytes(loginTicketRequest.ToString(SaveOptions.DisableFormatting)));
        var signedCms = new SignedCms(content, detached: false);
        var signer = new CmsSigner(SubjectIdentifierType.IssuerAndSerialNumber, certificate)
        {
            IncludeOption = X509IncludeOption.EndCertOnly
        };
        signedCms.ComputeSignature(signer, silent: true);
        var cms = Convert.ToBase64String(signedCms.Encode());

        XNamespace soap = "http://schemas.xmlsoap.org/soap/envelope/";
        XNamespace wsaa = "http://wsaa.view.sua.dvadac.desein.afip.gov";
        var envelope = new XDocument(
            new XElement(soap + "Envelope",
                new XAttribute(XNamespace.Xmlns + "soapenv", soap.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "wsaa", wsaa.NamespaceName),
                new XElement(soap + "Header"),
                new XElement(soap + "Body",
                    new XElement(wsaa + "loginCms", new XElement(wsaa + "in0", cms)))));

        var response = await SendXmlAsync(options.WsaaAddress, envelope, "loginCms", cancellationToken);
        ThrowIfSoapFault(response, false);
        var returnValue = response.Descendants().FirstOrDefault(x => x.Name.LocalName == "loginCmsReturn")?.Value;
        if (string.IsNullOrWhiteSpace(returnValue))
            throw new ArcaGatewayException("arca_wsaa_invalid_response", "WSAA devolvio una respuesta sin credenciales.", false);

        XDocument ticketDocument;
        try { ticketDocument = XDocument.Parse(returnValue); }
        catch (Exception exception) when (exception is System.Xml.XmlException or ArgumentException)
        {
            throw new ArcaGatewayException("arca_wsaa_invalid_response", "WSAA devolvio credenciales con un formato inesperado.", false);
        }

        var token = ticketDocument.Descendants().FirstOrDefault(x => x.Name.LocalName == "token")?.Value;
        var sign = ticketDocument.Descendants().FirstOrDefault(x => x.Name.LocalName == "sign")?.Value;
        var expiration = ticketDocument.Descendants().FirstOrDefault(x => x.Name.LocalName == "expirationTime")?.Value;
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(sign) ||
            !DateTimeOffset.TryParse(expiration, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var expiresAt))
            throw new ArcaGatewayException("arca_wsaa_invalid_response", "WSAA no devolvio un ticket de acceso completo.", false);

        return new ArcaAccessTicket(token, sign, expiresAt);
    }

    private async Task<IReadOnlyList<int>> GetPointsOfSaleAsync(ArcaAccessTicket ticket, CancellationToken cancellationToken)
    {
        var auth = new XElement(XName.Get("Auth", WsfeNamespace),
            new XElement(XName.Get("Token", WsfeNamespace), ticket.Token),
            new XElement(XName.Get("Sign", WsfeNamespace), ticket.Sign),
            new XElement(XName.Get("Cuit", WsfeNamespace), DigitsOnly(billingProfile.Cuit)));
        var body = new XElement(XName.Get("FEParamGetPtosVenta", WsfeNamespace), auth);
        var response = await SendSoapAsync(options.WsfeAddress, body, "FEParamGetPtosVenta", cancellationToken);
        ThrowIfBusinessErrors(response);
        return response.Descendants()
            .Where(x => x.Name.LocalName == "Nro")
            .Select(x => int.TryParse(x.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var value) ? value : 0)
            .Where(x => x > 0)
            .Distinct()
            .Order()
            .ToList();
    }

    private async Task<XDocument> SendSoapAsync(Uri address, XElement body, string action, CancellationToken cancellationToken)
    {
        XNamespace soap = "http://schemas.xmlsoap.org/soap/envelope/";
        var envelope = new XDocument(
            new XElement(soap + "Envelope",
                new XAttribute(XNamespace.Xmlns + "soapenv", soap.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "ar", WsfeNamespace),
                new XElement(soap + "Header"),
                new XElement(soap + "Body", body)));
        var response = await SendXmlAsync(address, envelope, $"{WsfeNamespace}{action}", cancellationToken);
        ThrowIfSoapFault(response, true);
        return response;
    }

    private async Task<XDocument> SendXmlAsync(Uri address, XDocument document, string soapAction, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, address);
        request.Headers.TryAddWithoutValidation("SOAPAction", $"\"{soapAction}\"");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/xml"));
        request.Content = new StringContent(document.ToString(SaveOptions.DisableFormatting), Encoding.UTF8, "text/xml");
        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        var xml = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode && string.IsNullOrWhiteSpace(xml))
            throw new ArcaGatewayException("arca_http_error", "ARCA rechazo la solicitud HTTP.", false);
        try { return XDocument.Parse(xml); }
        catch (Exception exception) when (exception is System.Xml.XmlException or ArgumentException)
        {
            throw new ArcaGatewayException("arca_invalid_response", "ARCA devolvio una respuesta que no es XML valido.", false);
        }
    }

    private static void ThrowIfSoapFault(XDocument response, bool authenticated)
    {
        var fault = response.Descendants().FirstOrDefault(x => x.Name.LocalName == "Fault");
        if (fault is null) return;
        var detail = fault.Descendants().FirstOrDefault(x => x.Name.LocalName is "faultstring" or "message")?.Value;
        throw new ArcaGatewayException(
            authenticated ? "arca_wsfe_fault" : "arca_wsaa_rejected",
            SanitizeProviderMessage(detail, authenticated ? "WSFE rechazo la solicitud." : "WSAA rechazo el certificado o la autorizacion."),
            authenticated);
    }

    private static void ThrowIfBusinessErrors(XDocument response)
    {
        var errors = response.Descendants()
            .Where(x => x.Name.LocalName == "Err")
            .Select(x => new
            {
                Code = x.Elements().FirstOrDefault(y => y.Name.LocalName == "Code")?.Value,
                Message = x.Elements().FirstOrDefault(y => y.Name.LocalName == "Msg")?.Value
            })
            .ToList();
        if (errors.Count == 0) return;
        var first = errors[0];
        var code = string.IsNullOrWhiteSpace(first.Code) ? "unknown" : first.Code;
        throw new ArcaGatewayException(
            $"arca_wsfe_{code}",
            SanitizeProviderMessage(first.Message, "WSFE rechazo la consulta de puntos de venta."),
            true);
    }

    private static string SanitizeProviderMessage(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value)) return fallback;
        var normalized = string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return normalized.Length <= 300 ? normalized : normalized[..300];
    }

    private static string DigitsOnly(string value) => new(value.Where(char.IsAsciiDigit).ToArray());

    private sealed class ArcaGatewayException(string code, string message, bool wsaaAuthenticated) : Exception(message)
    {
        public string Code { get; } = code;
        public bool WsaaAuthenticated { get; } = wsaaAuthenticated;
    }
}
