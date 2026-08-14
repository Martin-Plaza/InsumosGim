using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace GymShop.Tests.Integration;

[Trait("Category", "Integration")]
public sealed class PasswordResetHttpTests
{
    [Fact]
    public async Task Reset_password_uses_generic_response_and_invalidates_previous_jwt()
    {
        await using var factory = new GymShopWebApplicationFactory();
        await factory.InitializeAsync();
        using var client = factory.CreateHttpsClient();
        const string email = "password-reset@test.com";
        await factory.SeedUserAsync(email, "User", "clave123");
        var previousToken = await factory.LoginAsync(client, email, "clave123");

        var existingResponse = await client.PostAsJsonAsync("/api/auth/forgot-password", new { email });
        var unknownResponse = await client.PostAsJsonAsync("/api/auth/forgot-password", new { email = "unknown@test.com" });
        Assert.Equal(HttpStatusCode.OK, existingResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, unknownResponse.StatusCode);
        using var existingJson = JsonDocument.Parse(await existingResponse.Content.ReadAsStringAsync());
        using var unknownJson = JsonDocument.Parse(await unknownResponse.Content.ReadAsStringAsync());
        Assert.Equal(existingJson.RootElement.GetProperty("message").GetString(), unknownJson.RootElement.GetProperty("message").GetString());
        Assert.Equal(600, existingJson.RootElement.GetProperty("expiresInSeconds").GetInt32());
        var code = existingJson.RootElement.GetProperty("developmentCode").GetString();
        Assert.Matches("^[0-9]{6}$", code!);

        var reset = await client.PostAsJsonAsync("/api/auth/reset-password", new { email, code, newPassword = "nuevaClave456" });
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", previousToken);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
        client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login", new { email, password = "clave123" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/login", new { email, password = "nuevaClave456" })).StatusCode);
    }
}
