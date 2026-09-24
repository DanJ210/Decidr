using System.IO.Compression;
using System.Threading.RateLimiting;
using System.Text.Json.Serialization;
using Azure.Core;
using Azure.Identity;
using Azure.Storage.Blobs;
using backend.Data;
using backend.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddJsonFile("appsettings.Development.local.json", optional: true, reloadOnChange: true);
}

// Add services to the container.

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
    options.MimeTypes = [.. ResponseCompressionDefaults.MimeTypes, "application/json", "image/svg+xml"];
});
builder.Services.Configure<BrotliCompressionProviderOptions>(options =>
{
    options.Level = CompressionLevel.Fastest;
});
builder.Services.Configure<GzipCompressionProviderOptions>(options =>
{
    options.Level = CompressionLevel.Fastest;
});
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("api", context =>
    {
        var partitionKey = context.User.FindFirst("oid")?.Value
            ?? context.Connection.RemoteIpAddress?.ToString()
            ?? "unknown";

        return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 120,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true,
        });
    });
});

var entraAuthority = builder.Configuration["Entra:Authority"];
var entraAudience = builder.Configuration["Entra:Audience"];
var authenticationMode = AuthenticationModeResolver.Resolve(builder.Configuration, builder.Environment);
var entraEnabled = authenticationMode == AuthenticationMode.Entra;
if (entraEnabled && (string.IsNullOrWhiteSpace(entraAuthority) || string.IsNullOrWhiteSpace(entraAudience)))
{
    throw new InvalidOperationException(
        "Entra:Authority and Entra:Audience are required when Authentication:Mode is 'Entra'.");
}

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AuthorizationPolicies.AccessAsUser, policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireAssertion(context => AuthorizationPolicies.HasAccessAsUserScope(context.User));
    });
});
if (entraEnabled)
{
    var audience = entraAudience!.TrimEnd('/');

    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.Authority = entraAuthority;
            options.MapInboundClaims = false;
            options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidAudience = audience,
            };
        });
}

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (!string.IsNullOrWhiteSpace(connectionString))
{
    builder.Services.AddDbContext<DecidirDbContext>(options =>
        options.UseSqlServer(connectionString));
    builder.Services.AddScoped<ICommunityCourtService, EfCoreCourtService>();
    builder.Services.AddScoped<IAuthenticatedUserService, EfAuthenticatedUserService>();
}
else
{
    if (entraEnabled)
    {
        throw new InvalidOperationException("A database connection is required when Entra authentication is configured.");
    }

    builder.Services.AddSingleton<ICommunityCourtService, InMemoryCommunityCourtService>();
    builder.Services.AddSingleton<IAuthenticatedUserService, UnavailableAuthenticatedUserService>();
}
builder.Services.AddScoped<IActorResolver, ActorResolver>();
builder.Services.AddSingleton<MediaUploadProcessingQueue>();
builder.Services.AddHostedService<MediaUploadWorker>();

var evidenceBlobServiceUri = builder.Configuration["EvidenceStorage:BlobServiceUri"];
var evidenceContainerName = builder.Configuration["EvidenceStorage:ContainerName"];
if (!string.IsNullOrWhiteSpace(evidenceBlobServiceUri) && !string.IsNullOrWhiteSpace(evidenceContainerName))
{
    builder.Services.AddSingleton<TokenCredential>(_ => new DefaultAzureCredential());
    builder.Services.AddSingleton(serviceProvider =>
    {
        var options = new BlobClientOptions
        {
            Retry =
            {
                Mode = Azure.Core.RetryMode.Exponential,
                MaxRetries = 5,
                Delay = TimeSpan.FromSeconds(0.8),
                MaxDelay = TimeSpan.FromSeconds(8),
                NetworkTimeout = TimeSpan.FromSeconds(100),
            },
        };
        var serviceClient = new BlobServiceClient(
            new Uri(evidenceBlobServiceUri),
            serviceProvider.GetRequiredService<TokenCredential>(),
            options);
        return serviceClient.GetBlobContainerClient(evidenceContainerName);
    });
    builder.Services.AddSingleton<ICaseEvidenceStorage, AzureBlobCaseEvidenceStorage>();
    builder.Services.AddSingleton<ICaseMediaUploadSessionStore, BlobCaseMediaUploadSessionStore>();
}
else if (builder.Environment.IsDevelopment())
{
    builder.Services.AddSingleton<ICaseEvidenceStorage, LocalCaseEvidenceStorage>();
    builder.Services.AddSingleton<ICaseMediaUploadSessionStore, LocalCaseMediaUploadSessionStore>();
}
else
{
    throw new InvalidOperationException(
        "EvidenceStorage:BlobServiceUri and EvidenceStorage:ContainerName must be configured outside Development.");
}

var app = builder.Build();

if (authenticationMode == AuthenticationMode.SeededTesting)
{
    app.Logger.LogWarning(
        "SeededTesting authentication is active. X-Dev-User-Id can impersonate any seeded account.");
}

// Apply EF Core migrations automatically only in Development.
if (app.Environment.IsDevelopment() && !string.IsNullOrWhiteSpace(connectionString))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<DecidirDbContext>();
    db.Database.Migrate();
}

if (authenticationMode == AuthenticationMode.SeededTesting && !string.IsNullOrWhiteSpace(connectionString))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<DecidirDbContext>();
    DataSeeder.Seed(db);
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseResponseCompression();
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    await next();
});

if (entraEnabled)
{
    app.UseAuthentication();
    app.UseAuthorization();
}

app.UseRateLimiter();
var controllerEndpoints = app.MapControllers().RequireRateLimiting("api");
if (entraEnabled)
{
    controllerEndpoints.RequireAuthorization(AuthorizationPolicies.AccessAsUser);
}

app.UseDefaultFiles();
app.UseStaticFiles();
app.MapFallbackToFile("index.html");

app.Run();
