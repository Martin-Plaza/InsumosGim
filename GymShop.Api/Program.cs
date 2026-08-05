using System.Security.Claims;
using System.Text;
using GymShop.Api.Middleware;
using GymShop.Api.Services;
using GymShop.Api.Security;
using GymShop.Application;
using GymShop.Application.Abstractions;
using GymShop.Infrastructure;
using GymShop.Infrastructure.Configuration;
using GymShop.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.OpenApi;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Ingresar solo el token JWT, sin el prefijo Bearer. La sesion se invalida si el usuario se desactiva o cambia de rol."
    });

    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecuritySchemeReference("Bearer", document, null),
            []
        }
    });
});
builder.Services.AddHttpContextAccessor();
builder.Services.AddApplication();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<JwtTokenValidationEvents>();
builder.Services.AddSingleton<IValidateOptions<JwtOptions>, JwtOptionsValidator>();
builder.Services.AddSingleton<IValidateOptions<MercadoPagoOptions>>(
    new MercadoPagoOptionsValidator(builder.Environment.EnvironmentName));
builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddOptions<MercadoPagoOptions>()
    .Bind(builder.Configuration.GetSection(MercadoPagoOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddInfrastructure(builder.Configuration);

var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? [];

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((options, jwtOptions) =>
    {
        var jwt = jwtOptions.Value;
        options.EventsType = typeof(JwtTokenValidationEvents);
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Secret!)),
            NameClaimType = ClaimTypes.Name,
            RoleClaimType = ClaimTypes.Role
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

var mercadoPagoOptions = app.Services.GetRequiredService<IOptions<MercadoPagoOptions>>().Value;
if (app.Environment.IsDevelopment() && mercadoPagoOptions.Enabled && string.IsNullOrWhiteSpace(mercadoPagoOptions.WebhookSecret))
{
    app.Logger.LogWarning("Mercado Pago is enabled without webhook HMAC verification in Development. Configure MercadoPago:WebhookSecret before using real notifications.");
}

if (app.Environment.IsDevelopment())
{
    await DatabaseInitializer.InitializeAsync(app.Services);
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseExceptionHandler();
app.UseStaticFiles();
app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
