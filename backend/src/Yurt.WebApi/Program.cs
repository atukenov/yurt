using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Text.Json.Serialization;
using System.Security.Claims;
using Serilog;
using Yurt.Infrastructure;
using Yurt.Infrastructure.Hubs;
using Yurt.Infrastructure.Persistence;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Diagnostics;
using AspNetCoreRateLimit;
using Asp.Versioning;
using Yurt.WebApi.Middleware;

var builder = WebApplication.CreateBuilder(args);

// ── Serilog ───────────────────────────────────────────────────────────────────
builder.Host.UseSerilog((ctx, lc) =>
    lc.ReadFrom.Configuration(ctx.Configuration)
      .Enrich.FromLogContext());

// ── Infrastructure (DB, Auth, SignalR, Services) ──────────────────────────────
builder.Services.AddInfrastructure(builder.Configuration);

// ── API Versioning ────────────────────────────────────────────────────────────
builder.Services.AddApiVersioning(o =>
{
    o.DefaultApiVersion = new ApiVersion(1, 0);
    o.AssumeDefaultVersionWhenUnspecified = true;
    o.ReportApiVersions = true;
}).AddApiExplorer(o =>
{
    o.GroupNameFormat = "'v'VVV";
    o.SubstituteApiVersionInUrl = true;
});

// ── Controllers ───────────────────────────────────────────────────────────────
builder.Services.AddControllers()
    .AddJsonOptions(opts =>
    {
        opts.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        opts.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

// ── Swagger ───────────────────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Yurt API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new()
    {
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Enter your JWT token (without 'Bearer ' prefix)"
    });
    c.AddSecurityRequirement(new()
    {
        {
            new() { Reference = new() { Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme, Id = "Bearer" } },
            Array.Empty<string>()
        }
    });
});

// ── CORS ──────────────────────────────────────────────────────────────────────
builder.Services.AddCors(opts =>
    opts.AddDefaultPolicy(p =>
        p.WithOrigins(
            builder.Configuration.GetSection("AllowedOrigins").Get<string[]>()
                ?? ["http://localhost:4200", "http://localhost:4300"])
         .AllowAnyHeader()
         .AllowAnyMethod()
         .AllowCredentials()));

builder.Services.AddMemoryCache();
builder.Services.AddProblemDetails();

// ── Rate Limiting ─────────────────────────────────────────────────────────────
builder.Services.Configure<IpRateLimitOptions>(builder.Configuration.GetSection("IpRateLimiting"));
builder.Services.AddSingleton<IIpPolicyStore, MemoryCacheIpPolicyStore>();
builder.Services.AddSingleton<IRateLimitCounterStore, MemoryCacheRateLimitCounterStore>();
builder.Services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();
builder.Services.AddSingleton<IProcessingStrategy, AsyncKeyLockProcessingStrategy>();
builder.Services.AddInMemoryRateLimiting();

// ─────────────────────────────────────────────────────────────────────────────
var app = builder.Build();

// ── Liveness probe — raw middleware, absolute first position ──────────────────
// Must be before UseForwardedHeaders, UseHsts, Serilog, rate limiting, and auth.
// Nothing in the pipeline can interfere with it.
app.Use(async (ctx, next) =>
{
    if (ctx.Request.Path.StartsWithSegments("/health"))
    {
        ctx.Response.StatusCode = 200;
        ctx.Response.ContentType = "text/plain";
        await ctx.Response.WriteAsync("Healthy");
        return;
    }
    await next(ctx);
});

// HTTPS / Forwarded Headers
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders =
        ForwardedHeaders.XForwardedFor |
        ForwardedHeaders.XForwardedProto
});

app.UseHttpsRedirection();

if (!app.Environment.IsDevelopment())
    app.UseHsts();

// Configure the HTTP request pipeline.
app.UseExceptionHandler(opts =>
    opts.Run(async ctx =>
    {
        var ex = ctx.Features.Get<IExceptionHandlerFeature>()?.Error;
        var logger = ctx.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Unhandled exception for {Method} {Path}", ctx.Request.Method, ctx.Request.Path);

        ctx.Response.StatusCode = 500;
        ctx.Response.ContentType = "application/problem+json";
        await ctx.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = 500,
            Title = "An unexpected error occurred.",
            Instance = ctx.Request.Path
        });
    }));

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Yurt API v1");
        c.RoutePrefix = "swagger";
    });
}


app.UseMiddleware<CorrelationIdMiddleware>();
app.UseSerilogRequestLogging(opts =>
    opts.EnrichDiagnosticContext = (diag, ctx) =>
    {
        var userId = ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is not null) diag.Set("UserId", userId);
        if (ctx.Items["CorrelationId"] is string cid) diag.Set("CorrelationId", cid);
    });

// Security headers
var isDev = app.Environment.IsDevelopment();
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
    ctx.Response.Headers["X-Frame-Options"] = "DENY";
    ctx.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    ctx.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
    // Skip in Development so it doesn't interfere with the self-hosted Swagger UI.
    // This is a pure JSON API in production with no first-party HTML/JS to allow-list.
    if (!isDev)
        ctx.Response.Headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";
    await next();
});

app.UseIpRateLimiting();
app.UseCors();
app.UseAuthentication();

// Block admin routes for accounts that still have the must_change_password claim set
app.Use(async (ctx, next) =>
{
    var path = ctx.Request.Path.Value ?? "";
    if (path.Contains("/admin/", StringComparison.OrdinalIgnoreCase) &&
        !path.Contains("/auth/login", StringComparison.OrdinalIgnoreCase) &&
        !path.Contains("/auth/logout", StringComparison.OrdinalIgnoreCase) &&
        !path.Contains("/auth/refresh", StringComparison.OrdinalIgnoreCase) &&
        !path.Contains("/auth/change-password", StringComparison.OrdinalIgnoreCase))
    {
        var mustChange = ctx.User.FindFirstValue("must_change_password");
        if (mustChange == "true")
        {
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            ctx.Response.ContentType = "application/problem+json";
            await ctx.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = 403,
                Title = "Password change required.",
                Detail = "You must change your password before accessing this resource.",
                Instance = ctx.Request.Path
            });
            return;
        }
    }
    await next();
});

app.UseAuthorization();
app.MapControllers();
app.MapHub<OrdersHub>("/hubs/orders");

// ── Seed ──────────────────────────────────────────────────────────────────────
await DbSeeder.SeedAsync(app.Services);

app.Run();

public partial class Program { } // for integration tests
