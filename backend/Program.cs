using System.IO.Compression;
using System.Text.Json.Serialization;
using backend.Data;
using backend.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.ResponseCompression;

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

var entraAuthority = builder.Configuration["Entra:Authority"];
var entraAudience = builder.Configuration["Entra:Audience"];
var entraConfigured = !string.IsNullOrWhiteSpace(entraAuthority) && !string.IsNullOrWhiteSpace(entraAudience);
if (!entraConfigured && !builder.Environment.IsDevelopment())
{
    throw new InvalidOperationException("Entra:Authority and Entra:Audience must be configured outside Development.");
}

builder.Services.AddAuthorization();
if (entraConfigured)
{
    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.Authority = entraAuthority;
            options.Audience = entraAudience;
            options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
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
    builder.Services.AddSingleton<ICommunityCourtService, InMemoryCommunityCourtService>();
    builder.Services.AddSingleton<IAuthenticatedUserService, UnavailableAuthenticatedUserService>();
}
builder.Services.AddScoped<IActorResolver, ActorResolver>();

var app = builder.Build();

// Apply EF Core migrations and seed data when a database connection is configured.
if (app.Environment.IsDevelopment() && !string.IsNullOrWhiteSpace(connectionString))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<DecidirDbContext>();
    db.Database.Migrate();
    DataSeeder.Seed(db);
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseResponseCompression();

if (entraConfigured)
{
    app.UseAuthentication();
    app.UseAuthorization();
}

app.MapControllers();

app.UseDefaultFiles();
app.UseStaticFiles();
app.MapFallbackToFile("index.html");

app.Run();
